#!/usr/bin/env python3
"""Compile the project's hand-written URP shaders (Assets/_Game/Art/Shaders/*.shader) with DXC.

Unity compiles HLSL for Metal by running DXC (HLSL -> SPIR-V) and then translating to Metal; this check runs the
same first step outside Unity: for each pass it joins the HLSLINCLUDE blocks and the HLSLPROGRAM block, defines
the platform macros Unity sets for iOS/Metal and the pass's keywords, resolves "Packages/..." includes against the
real URP 17.3 / Core ShaderLibrary from the editor, and compiles the vertex and fragment entry points to SPIR-V.
That checks ShaderLibrary function names, signatures, struct fields and macros. It does not run Unity's
ShaderLab parser, SPIR-V -> Metal translation, or the Metal compiler.

Keyword coverage is linear, not the full product: every shader_feature combination with the multi_compile
defaults, plus every multi_compile option on its own with all shader_features on.

Called by tools/ci/compile_check.sh, which downloads DXC from a pinned LunarG Vulkan SDK. Standard library only.
See docs/ci/COMPILE_CHECK.md.
"""

import argparse
import itertools
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path

# Macros Unity's shader compiler defines for an iOS (Metal, DXC) compile, besides SHADER_TARGET and the stage.
PLATFORM_DEFINES = [
    "SHADER_API_METAL",
    "SHADER_API_MOBILE",
    "UNITY_COMPILER_DXC",
    "UNITY_NO_DXT5nm",
    "UNITY_PLATFORM_IOS",
]

# #pragma shortcuts -> keyword groups.
SHORTCUTS = {
    "multi_compile_fog": ["_", "FOG_LINEAR", "FOG_EXP", "FOG_EXP2"],
    "multi_compile_instancing": ["_", "INSTANCING_ON"],
}

DIAG_RE = re.compile(r"^(?P<file>[^:]+):(?P<line>\d+):(?P<col>\d+): (?P<sev>error|warning|fatal error): (?P<msg>.*)$")


def parse_shader(path):
    """Returns (include blocks, passes) as (source, first line) pairs; passes also carry the pass name."""
    text = path.read_text()
    # Blank out // comments with spaces so offsets (and line numbers) stay those of the file.
    code = re.sub(r"//[^\n]*", lambda m: " " * len(m.group(0)), text)

    def line_of(offset):
        return text.count("\n", 0, offset) + 1

    includes = [(m.group(1), line_of(m.start(1))) for m in re.finditer(r"HLSLINCLUDE(.*?)ENDHLSL", code, re.S)]
    passes = []
    for i, m in enumerate(re.finditer(r"HLSLPROGRAM(.*?)ENDHLSL", code, re.S)):
        before = code[:m.start()]
        pass_start = before.rfind("Pass")
        nm = re.search(r'Name\s+"([^"]+)"', before[pass_start:]) if pass_start >= 0 else None
        name = nm.group(1) if nm else "Pass%d" % i
        passes.append((name, m.group(1), line_of(m.start(1))))
    return includes, passes


def parse_pragmas(body):
    entry = {}
    target = 30
    groups = []  # (kind, stage or None, options)
    for m in re.finditer(r"^[ \t]*#pragma[ \t]+(\w+)[ \t]*(.*)$", body, re.M):
        directive, args = m.group(1), m.group(2).split()
        if directive in ("vertex", "fragment"):
            entry[directive] = args[0]
        elif directive == "target":
            target = int(round(float(args[0]) * 10))
        elif directive in SHORTCUTS:
            groups.append(("multi_compile", None, tuple(SHORTCUTS[directive])))
        else:
            km = re.match(r"^(shader_feature|multi_compile)(?:_local)?(?:_(vertex|fragment))?$", directive)
            if km:
                options = args if len(args) > 1 else ["_"] + args
                groups.append((km.group(1), km.group(2), tuple(options)))
    return entry, target, groups


def variants(groups):
    """Linear keyword coverage: all shader_feature combos with multi_compile defaults, then each multi_compile
    option on its own with every shader_feature on."""
    features = [g for g in groups if g[0] == "shader_feature"]
    multis = [g for g in groups if g[0] == "multi_compile"]
    seen, out = set(), []

    def add(choice):
        key = tuple(sorted(k for _, k in choice))
        if key not in seen:
            seen.add(key)
            out.append(choice)

    for combo in itertools.product(*[g[2] for g in features]) if features else [()]:
        add([(g, k) for g, k in zip(features, combo)] + [(g, g[2][0]) for g in multis])
    all_on = [(g, g[2][-1]) for g in features]
    for mi, g in enumerate(multis):
        for opt in g[2]:
            add(all_on + [(g, opt)] + [(o, o[2][0]) for oi, o in enumerate(multis) if oi != mi])
    return out


def keyword_defines(choice, stage):
    defines = []
    for (kind, kstage, _), kw in choice:
        if kw == "_" or (kstage and kstage != stage):
            continue
        defines.append(kw)
    return defines


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--project", required=True)
    ap.add_argument("--unity", required=True)
    ap.add_argument("--dxc", required=True, help="directory with bin/dxc and lib/libdxcompiler.so")
    ap.add_argument("--shaders", default="Assets/_Game/Art/Shaders")
    ap.add_argument("--verbose", action="store_true")
    args = ap.parse_args()

    project = Path(args.project).resolve()
    builtin = Path(args.unity).resolve() / "Editor/Data/Resources/PackageManager/BuiltInPackages"
    dxc_dir = Path(args.dxc).resolve()
    dxc = dxc_dir / "bin" / "dxc"
    env = dict(os.environ, LD_LIBRARY_PATH=str(dxc_dir / "lib"))
    version_text = (project / "ProjectSettings/ProjectVersion.txt").read_text()
    vm = re.search(r"m_EditorVersion:\s*(\d+)\.(\d+)\.(\d+)", version_text)
    unity_version = "%d%d%02d" % (int(vm.group(1)), int(vm.group(2)), int(vm.group(3)))

    shaders = sorted((project / args.shaders).glob("*.shader"))
    errors, warnings, compiled = [], [], 0
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        packages = tmp / "Packages"
        packages.mkdir()
        for name in ("com.unity.render-pipelines.core", "com.unity.render-pipelines.universal",
                     "com.unity.render-pipelines.universal-config", "com.unity.shadergraph"):
            if (builtin / name).is_dir():
                (packages / name).symlink_to(builtin / name)
        (tmp / "Assets").symlink_to(project / "Assets")

        for shader in shaders:
            includes, passes = parse_shader(shader)
            rel = shader.relative_to(project.parent) if project.parent in shader.parents else shader
            for pass_name, body, line in passes:
                entry, target, groups = parse_pragmas(body)
                # Unity compiles HLSLINCLUDE blocks in front of every HLSLPROGRAM of the same shader. A #line
                # directive maps diagnostics in the program body back to the .shader file.
                source = "".join('#line %d "%s"\n%s\n' % (inc_line, shader, inc) for inc, inc_line in includes)
                source += '#line %d "%s"\n%s' % (line, shader, body)
                src_file = tmp / ("%s_%s.hlsl" % (shader.stem, re.sub(r"\W", "_", pass_name)))
                src_file.write_text(source)
                for choice in variants(groups):
                    for stage, profile in (("vertex", "vs_6_0"), ("fragment", "ps_6_0")):
                        if stage not in entry:
                            continue
                        defines = PLATFORM_DEFINES + [
                            "SHADER_TARGET=%d" % target, "UNITY_VERSION=%s" % unity_version,
                            "SHADER_STAGE_VERTEX" if stage == "vertex" else "SHADER_STAGE_FRAGMENT",
                        ] + keyword_defines(choice, stage)
                        cmd = [str(dxc), "-spirv", "-fspv-target-env=vulkan1.1", "-T", profile,
                               "-E", entry[stage], "-HV", "2018", "-Wno-ignored-attributes", "-Wno-conversion",
                               "-I", str(tmp), "-I", str(shader.parent), "-Fo", os.devnull]
                        cmd += ["-D" + d for d in defines]
                        cmd.append(str(src_file))
                        proc = subprocess.run(cmd, capture_output=True, text=True, env=env)
                        compiled += 1
                        kws = " ".join(k for k in keyword_defines(choice, stage)) or "(no keywords)"
                        label = "%s [%s] %s %s" % (rel, pass_name, stage, kws)
                        for out_line in proc.stderr.splitlines():
                            m = DIAG_RE.match(out_line)
                            if not m:
                                continue
                            f = m.group("file")
                            ours = str(project) in f or f == str(src_file)
                            if f.startswith(str(tmp)) and f != str(src_file):
                                f = f.replace(str(tmp) + "/", "")
                            elif str(project.parent) in f and Path(f).is_absolute():
                                f = str(Path(f).resolve().relative_to(project.parent))
                            msg = "%s:%s:%s: %s: %s" % (f, m.group("line"), m.group("col"), m.group("sev"),
                                                        m.group("msg"))
                            if "error" in m.group("sev"):
                                errors.append((label, msg))
                            elif ours:
                                warnings.append((label, msg))
                        if proc.returncode != 0 and not any(lbl == label for lbl, _ in errors):
                            errors.append((label, proc.stderr.strip()[-1500:] or "dxc exited with %d" %
                                           proc.returncode))
                        if args.verbose:
                            print("  %s %s" % ("FAIL" if proc.returncode else "ok  ", label), flush=True)

    print("[shader-check] %d shaders, %d stage compiles" % (len(shaders), compiled))
    unique = []
    for label, msg in errors + [(lbl, m) for lbl, m in warnings]:
        if msg not in [u[1] for u in unique]:
            unique.append((label, msg))
    for label, msg in unique:
        print("  %s\n      (first seen in %s)" % (msg, label))
    print("[shader-check] %s: %d errors, %d warnings in project shaders" % (
        "FAIL" if errors else "PASS", len({m for _, m in errors}), len({m for _, m in warnings})))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
