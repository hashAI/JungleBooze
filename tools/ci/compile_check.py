#!/usr/bin/env python3
"""Compile the Unity project's assemblies outside Unity, the way the Unity editor does.

Reads every .asmdef/.asmref of the project and of the packages in Packages/packages-lock.json, works out which
assemblies exist for a configuration (editor or player, target platform, define constraints, version defines),
and compiles the project assemblies plus the package assemblies they need, in dependency order, with the
editor's bundled Roslyn compiler, the real Unity reference assemblies and Assets/csc.rsp.

Called by tools/ci/compile_check.sh (which downloads the Unity editor parts first). Standard library only.
See docs/ci/COMPILE_CHECK.md.
"""

import argparse
import concurrent.futures
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile
from pathlib import Path

REGISTRY = "https://packages.unity.com"

# Platform names as used in .asmdef includePlatforms/excludePlatforms.
EDITOR_PLATFORM = "Editor"

# Compiler options the Unity editor passes to every assembly (Unity 6 script compilation pipeline):
# C# 9, deterministic, portable PDBs, no implicit mscorlib, the "common warnings" Unity always suppresses
# (Player Settings > Suppress Common Warnings is on in this project: CS0169, CS0649) plus CS0282 and the
# assembly-unification warnings CS1701/CS1702.
BASE_OPTIONS = [
    "-target:library",
    "-nostdlib+",
    "-noconfig",
    "-nologo",
    "-langversion:9.0",
    "-deterministic",
    "-debug:portable",
    "-utf8output",
    "-preferreduilang:en-US",
    "-nowarn:0169",
    "-nowarn:0649",
    "-nowarn:0282",
    "-nowarn:1701",
    "-nowarn:1702",
]

UNITY_VERSION_CHAIN = (
    ["5_3", "5_4", "5_5", "5_6"]
    + [f"{y}_{m}" for y in (2017, 2018, 2019, 2020) for m in (1, 2, 3, 4)]
    + [f"{y}_{m}" for y in (2021, 2022) for m in (1, 2, 3)]
    + ["2023_1", "2023_2", "2023_3"]
)

# Scripting defines the editor sets independent of platform (Unity 6, Mono backend, .NET Standard 2.1).
COMMON_DEFINES = [
    "PLATFORM_ARCH_64", "UNITY_64",
    "ENABLE_AUDIO", "ENABLE_CACHING", "ENABLE_CLOTH", "ENABLE_EVENT_QUEUE", "ENABLE_MICROPHONE",
    "ENABLE_MULTIPLE_DISPLAYS", "ENABLE_PHYSICS", "ENABLE_TEXTURE_STREAMING", "ENABLE_LZMA", "ENABLE_UNITYEVENTS",
    "ENABLE_VR", "ENABLE_WEBCAM", "ENABLE_UNITYWEBREQUEST", "ENABLE_WWW", "ENABLE_CLOUD_SERVICES",
    "ENABLE_CLOUD_SERVICES_ADS", "ENABLE_CLOUD_SERVICES_USE_WEBREQUEST", "ENABLE_CLOUD_SERVICES_CRASH_REPORTING",
    "ENABLE_CLOUD_SERVICES_PURCHASING", "ENABLE_CLOUD_SERVICES_ANALYTICS", "ENABLE_CLOUD_SERVICES_BUILD",
    "ENABLE_EDITOR_GAME_SERVICES", "ENABLE_UNITY_GAME_SERVICES_ANALYTICS_SUPPORT", "ENABLE_CLOUD_LICENSE",
    "ENABLE_EDITOR_HUB_LICENSE", "ENABLE_WEBSOCKET_CLIENT", "ENABLE_GENERATE_NATIVE_PLUGINS_FOR_ASSEMBLIES_API",
    "ENABLE_DIRECTOR_AUDIO", "ENABLE_DIRECTOR_TEXTURE", "ENABLE_MANAGED_JOBS", "ENABLE_MANAGED_TRANSFORM_JOBS",
    "ENABLE_MANAGED_ANIMATION_JOBS", "ENABLE_MANAGED_AUDIO_JOBS", "ENABLE_MANAGED_UNITYTLS", "INCLUDE_DYNAMIC_GI",
    "ENABLE_SCRIPTING_GC_WBARRIERS", "PLATFORM_SUPPORTS_MONO", "RENDER_SOFTWARE_CURSOR", "ENABLE_VIDEO",
    "ENABLE_NAVIGATION_OFFMESHLINK_TO_NAVMESHLINK", "TEXTCORE_1_0_OR_NEWER", "TEXTCORE_FONT_ENGINE_1_5_OR_NEWER",
    "ENABLE_RUNTIME_GI", "ENABLE_CUSTOM_RENDER_TEXTURE", "ENABLE_DIRECTOR", "ENABLE_LOCALIZATION",
    "ENABLE_SPRITES", "ENABLE_TERRAIN", "ENABLE_TILEMAP", "ENABLE_TIMELINE",
    "NET_STANDARD_2_0", "NET_STANDARD", "NET_STANDARD_2_1", "NETSTANDARD", "NETSTANDARD2_1",
    "CSHARP_7_OR_LATER", "CSHARP_7_3_OR_NEWER",
]

EDITOR_DEFINES = [
    "ENABLE_MONO", "ENABLE_PROFILER", "DEBUG", "TRACE", "UNITY_ASSERTIONS",
    "UNITY_EDITOR", "UNITY_EDITOR_64", "UNITY_EDITOR_OSX",
    "ENABLE_UNITY_COLLECTIONS_CHECKS", "ENABLE_BURST_AOT", "UNITY_TEAM_LICENSE",
]

PLATFORM_DEFINES = {
    "iOS": [
        "PLATFORM_IOS", "UNITY_IOS", "UNITY_IPHONE", "UNITY_IPHONE_API", "ENABLE_GAMECENTER", "ENABLE_NETWORK",
        "ENABLE_IOS_ON_DEMAND_RESOURCES", "ENABLE_IOS_APP_SLICING", "PLAYERCONNECTION_LISTENS_FIXED_PORT",
        "DEBUGGER_LISTENS_FIXED_PORT", "PLATFORM_SUPPORTS_ADS_ID", "SUPPORT_ENVIRONMENT_VARIABLES",
        "PLATFORM_SUPPORTS_PROFILER", "ENABLE_UNITYADS_RUNTIME", "UNITY_UNITYADS_API", "ENABLE_ETC_COMPRESSION",
    ],
    "macOSStandalone": [
        "PLATFORM_STANDALONE", "PLATFORM_STANDALONE_OSX", "UNITY_STANDALONE", "UNITY_STANDALONE_OSX",
        "ENABLE_GAMECENTER", "ENABLE_NETWORK", "ENABLE_CLUSTER_SYNC", "ENABLE_CLUSTERINPUT", "ENABLE_SPATIALTRACKING",
        "PLATFORM_SUPPORTS_PROFILER", "PLATFORM_EXTENDS_VULKAN_DEVICE",
    ],
}

# Player builds compile with the IL2CPP backend on iOS.
PLAYER_DEFINES = ["ENABLE_IL2CPP"]

# .asmdef platform name -> PluginImporter platform name in .dll.meta files.
PLUGIN_PLATFORM_NAMES = {"iOS": "iOS", "macOSStandalone": "OSXUniversal"}

# Assembly names the editor resolves to another assembly (the uGUI package's runtime assembly is still
# referenced by its pre-2019.2 name "Unity.ugui", e.g. by the Input System package).
REFERENCE_ALIASES = {"Unity.ugui": "UnityEngine.UI"}

# name -> (is_editor, target platform)
CONFIGS = {
    "editor-ios": (True, "iOS"),
    "editor-osx": (True, "macOSStandalone"),
    "player-ios": (False, "iOS"),
}

DIAG_RE = re.compile(r"^(?P<file>.*?)\((?P<line>\d+),(?P<col>\d+)\): (?P<sev>error|warning) (?P<code>\w+): (?P<msg>.*)$")


def log(msg):
    print(msg, flush=True)


# --------------------------------------------------------------------------------------------------- versions

def parse_version(text):
    """Parses '1.2.3', '1.2.3-pre.4' or Unity's '6000.3.25f1' into a comparable tuple."""
    text = text.strip()
    m = re.match(r"^(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:([abfpx])(\d+))?(?:-(.+))?$", text)
    if not m:
        raise ValueError("cannot parse version '%s'" % text)
    major, minor, patch = (int(m.group(i) or 0) for i in (1, 2, 3))
    letter = m.group(4)
    # Unity release letters: a(lpha) < b(eta) < f(inal) < p(atch). Plain semver counts as final.
    letter_rank = {"a": 0, "b": 1, "x": 1, "f": 2, "p": 3}.get(letter, 2)
    letter_num = int(m.group(5) or 0)
    pre = m.group(6)
    # Semver: a prerelease sorts before the release.
    pre_key = (0, tuple(int(p) if p.isdigit() else p for p in pre.split("."))) if pre else (1, ())
    return (major, minor, patch, letter_rank, letter_num, pre_key)


def version_matches(expression, version):
    """Unity version-define expression: '', 'x' (>= x), '[x]', '[x,y]', '(x,y)', '[x,)', '(,y]' ..."""
    expression = (expression or "").strip()
    if not expression:
        return True
    v = parse_version(version)
    if expression[0] not in "[(":
        return v >= parse_version(expression)
    if "," not in expression:
        return v == parse_version(expression.strip("[]()"))
    low_incl = expression[0] == "["
    high_incl = expression[-1] == "]"
    low, high = (p.strip() for p in expression[1:-1].split(",", 1))
    if low:
        lv = parse_version(low)
        if v < lv or (v == lv and not low_incl):
            return False
    if high:
        hv = parse_version(high)
        if v > hv or (v == hv and not high_incl):
            return False
    return True


# --------------------------------------------------------------------------------------------------- packages

def curl(url, dest=None):
    cmd = ["curl", "-fsSL", "--retry", "5", "--retry-all-errors", url]
    if dest:
        cmd += ["-o", str(dest)]
        subprocess.run(cmd, check=True)
        return None
    return subprocess.run(cmd, check=True, capture_output=True).stdout


def fetch_registry_package(name, version, cache):
    target = cache / "packages" / ("%s@%s" % (name, version))
    if (target / ".complete").exists():
        return target
    meta = json.loads(curl("%s/%s" % (REGISTRY, name)))
    dist = meta["versions"][version]["dist"]
    log("[compile-check] Downloading %s %s" % (name, version))
    with tempfile.TemporaryDirectory(dir=cache) as tmp:
        tgz = Path(tmp) / "package.tgz"
        curl(dist["tarball"], tgz)
        sha1 = hashlib.sha1(tgz.read_bytes()).hexdigest()
        if sha1 != dist["shasum"]:
            raise RuntimeError("%s %s: checksum mismatch (%s != %s)" % (name, version, sha1, dist["shasum"]))
        out = Path(tmp) / "out"
        with tarfile.open(tgz) as tar:
            tar.extractall(out, filter="data")
        if target.exists():
            shutil.rmtree(target)
        shutil.move(str(out / "package"), str(target))
    (target / ".complete").touch()
    return target


def resolve_packages(project, unity, cache, fetch):
    """Returns {name: (version, path or None)} for every package in packages-lock.json."""
    lock = json.loads((project / "Packages" / "packages-lock.json").read_text())["dependencies"]
    builtin_root = unity / "Editor/Data/Resources/PackageManager/BuiltInPackages"
    packages = {}
    for name, info in sorted(lock.items()):
        version = info["version"]
        source = info.get("source")
        if source == "builtin":
            path = builtin_root / name
            packages[name] = (version, path if path.is_dir() else None)
        elif source == "registry":
            path = cache / "packages" / ("%s@%s" % (name, version))
            if fetch:
                path = fetch_registry_package(name, version, cache)
            packages[name] = (version, path)
        elif source == "embedded":
            packages[name] = (version, project / "Packages" / name)
        else:
            raise RuntimeError("package %s: source '%s' is not supported by the compile check" % (name, source))
    return packages


# --------------------------------------------------------------------------------------------------- assemblies

class Assembly:
    def __init__(self, name, asmdef_path, data, package, is_project):
        self.name = name
        self.asmdef_path = asmdef_path
        self.data = data
        self.package = package  # package name or None for Assets
        self.is_project = is_project
        self.sources = []
        self.guid = read_meta_guid(asmdef_path)

    def get(self, key, default):
        value = self.data.get(key)
        return default if value is None else value


def read_meta_guid(path):
    meta = Path(str(path) + ".meta")
    if meta.exists():
        m = re.search(r"^guid:\s*([0-9a-f]+)", meta.read_text(errors="replace"), re.M)
        if m:
            return m.group(1)
    return None


def ignored_dir(name):
    # Unity ignores hidden folders, folders ending in '~' and 'cvs'.
    return name.startswith(".") or name.endswith("~") or name.lower() == "cvs"


def scan_root(root, package, is_project, assemblies, dlls, pending):
    """Finds asmdefs, asmrefs, .cs files and precompiled .dlls under one root. Returns scripts outside any asmdef."""
    owner_of_dir = {}
    asmrefs = {}
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = sorted(d for d in dirnames if not ignored_dir(d))
        for f in sorted(filenames):
            p = Path(dirpath) / f
            if f.endswith(".asmdef"):
                data = json.loads(p.read_text(encoding="utf-8-sig"))
                asm = Assembly(data["name"], p, data, package, is_project)
                if asm.name in assemblies:
                    raise RuntimeError("duplicate assembly name %s (%s, %s)" % (
                        asm.name, assemblies[asm.name].asmdef_path, p))
                assemblies[asm.name] = asm
                owner_of_dir[Path(dirpath)] = ("asmdef", asm.name)
            elif f.endswith(".asmref"):
                data = json.loads(p.read_text(encoding="utf-8-sig"))
                asmrefs[Path(dirpath)] = data["reference"]
            elif f.endswith(".dll"):
                dlls.append(p)
    for d, ref in asmrefs.items():
        owner_of_dir.setdefault(d, ("asmref", ref))

    loose = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = sorted(d for d in dirnames if not ignored_dir(d))
        for f in sorted(filenames):
            if not f.endswith(".cs"):
                continue
            p = Path(dirpath) / f
            d = Path(dirpath)
            owner = None
            while True:
                if d in owner_of_dir:
                    owner = owner_of_dir[d]
                    break
                if d == root or d.parent == d:
                    break
                d = d.parent
            if owner is None:
                loose.append(p)
            else:
                pending.append((owner, p))
    return loose


def attach_sources(assemblies, pending):
    guid_map = {a.guid: a.name for a in assemblies.values() if a.guid}
    for (kind, ref), path in pending:
        name = ref
        if kind == "asmref" and ref.startswith("GUID:"):
            name = guid_map.get(ref[5:], ref)
        asm = assemblies.get(name)
        if asm is None:
            raise RuntimeError("%s belongs to unknown assembly %s" % (path, ref))
        asm.sources.append(path)
    return guid_map


def read_plugin_meta(dll):
    """Returns (explicitly_referenced, editor_enabled, enabled_platforms_or_None, is_analyzer, define_constraints).

    PluginImporter rules: with "Any Platform" on, the plug-in is available everywhere except the platforms listed
    as "Exclude <Platform>: 1" in the Any settings; otherwise only the platforms whose own entry is enabled.
    enabled_platforms_or_None is None for "all platforms except excluded" and then holds the excluded set instead.
    """
    meta = Path(str(dll) + ".meta")
    if not meta.exists():
        return False, True, None, set(), False, []
    text = meta.read_text(errors="replace")
    explicit = bool(re.search(r"isExplicitlyReferenced:\s*1", text))
    analyzer = "RoslynAnalyzer" in text
    constraints = []
    m = re.search(r"defineConstraints:\s*\n((?:\s*-\s*.*\n)*)", text)
    if m:
        constraints = [c.strip()[1:].strip() for c in m.group(1).splitlines() if c.strip().startswith("-")]
    entries = {}
    # platformData entries:  - first:\n      <Group>: <Platform>\n    second:\n      enabled: 0|1\n      settings: ...
    for pm in re.finditer(r"-\s*first:\s*\n\s*([^:\n]+):\s*([^\n]*)\n\s*second:\s*\n\s*enabled:\s*(\d)"
                          r"((?:\n\s{6,}.*)*)", text):
        group, platform, on, settings = pm.group(1).strip(), pm.group(2).strip(), pm.group(3) == "1", pm.group(4)
        entries[(group, platform)] = (on, settings)
    any_entry = entries.get(("Any", ""))
    if not entries or (any_entry and any_entry[0]):
        excluded = set(re.findall(r"Exclude ([^:]+):\s*1", any_entry[1])) if any_entry else set()
        return explicit, "Editor" not in excluded, None, excluded, analyzer, constraints
    enabled = {p for (g, p), (on, _) in entries.items() if on}
    return explicit, ("Editor", "Editor") in entries and entries[("Editor", "Editor")][0], enabled, set(), \
        analyzer, constraints


def constraint_ok(constraint, defines):
    for alt in constraint.split("||"):
        alt = alt.strip()
        if not alt:
            continue
        if alt.startswith("!"):
            if alt[1:].strip() not in defines:
                return True
        elif alt in defines:
            return True
    return False


class Planner:
    def __init__(self, args):
        self.project = Path(args.project).resolve()
        self.unity = Path(args.unity).resolve()
        self.cache = Path(args.cache).resolve()
        self.unity_version = args.unity_version
        self.data = self.unity / "Editor/Data"
        self.packages = resolve_packages(self.project, self.unity, self.cache, fetch=False)
        self.assemblies = {}
        self.dlls = []
        pending = []
        self.loose = scan_root(self.project / "Assets", None, True, self.assemblies, self.dlls, pending)
        for name, (version, path) in self.packages.items():
            if path is not None and path.is_dir():
                scan_root(path, name, False, self.assemblies, self.dlls, pending)
        self.guid_map = attach_sources(self.assemblies, pending)
        self.rsp_options = self.read_project_rsp()

    def read_project_rsp(self):
        rsp = self.project / "Assets" / "csc.rsp"
        if not rsp.exists():
            return []
        options = []
        for line in rsp.read_text().splitlines():
            line = line.strip()
            if line and not line.startswith("#"):
                options.extend(line.split())
        return options

    # ------------------------------------------------------------------ per configuration

    def global_defines(self, is_editor, platform):
        parts = self.unity_version.split(".")
        major, minor = parts[0], parts[1]
        patch = re.match(r"\d+", parts[2]).group(0)
        defines = ["UNITY_%s_%s_%s" % (major, minor, patch), "UNITY_%s_%s" % (major, minor), "UNITY_%s" % major]
        defines += ["UNITY_%s_OR_NEWER" % v for v in UNITY_VERSION_CHAIN]
        defines += ["UNITY_%s_%d_OR_NEWER" % (major, m) for m in range(0, int(minor) + 1)]
        defines += COMMON_DEFINES + PLATFORM_DEFINES[platform]
        defines += EDITOR_DEFINES if is_editor else PLAYER_DEFINES
        # Active Input Handling (ProjectSettings activeInputHandler: 0 old, 1 new, 2 both).
        settings = (self.project / "ProjectSettings" / "ProjectSettings.asset").read_text(errors="replace")
        m = re.search(r"activeInputHandler:\s*(\d)", settings)
        handler = int(m.group(1)) if m else 0
        if handler in (1, 2):
            defines.append("ENABLE_INPUT_SYSTEM")
        if handler in (0, 2):
            defines.append("ENABLE_LEGACY_INPUT_MANAGER")
        # Player Settings > Scripting Define Symbols (per build target group).
        group = {"iOS": "iPhone", "macOSStandalone": "Standalone"}[platform]
        m = re.search(r"scriptingDefineSymbols:\s*\n((?:\s+\w+: .*\n)*)", settings)
        if m:
            for line in m.group(1).splitlines():
                key, _, value = line.strip().partition(":")
                if key == group:
                    defines += [d for d in value.strip().split(";") if d]
        return defines

    def version_defines(self, asm):
        result = []
        for vd in asm.get("versionDefines", []):
            resource = vd.get("name", "")
            if resource == "Unity":
                version = self.unity_version
            elif resource in self.packages:
                version = self.packages[resource][0]
            else:
                continue
            try:
                if version_matches(vd.get("expression", ""), version):
                    result.append(vd["define"])
            except ValueError:
                continue
        return result

    def platform_ok(self, asm, is_editor, platform):
        include = asm.get("includePlatforms", [])
        exclude = asm.get("excludePlatforms", [])
        current = EDITOR_PLATFORM if is_editor else platform
        if include:
            return current in include
        return current not in exclude

    def resolve_ref(self, ref):
        if ref.startswith("GUID:"):
            return self.guid_map.get(ref[5:])
        return REFERENCE_ALIASES.get(ref, ref)

    def plan(self, config):
        is_editor, platform = CONFIGS[config]
        base = self.global_defines(is_editor, platform)
        active = {}
        for name, asm in self.assemblies.items():
            if not self.platform_ok(asm, is_editor, platform):
                continue
            defines = list(base)
            if asm.is_project and is_editor:
                defines.append("UNITY_INCLUDE_TESTS")
            defines += self.version_defines(asm)
            dset = set(defines)
            if not all(constraint_ok(c, dset) for c in asm.get("defineConstraints", [])):
                continue
            if not asm.sources:
                continue
            active[name] = defines

        roots = [n for n in active if self.assemblies[n].is_project]
        needed, order, missing = set(), [], {}

        def visit(n, stack):
            if n in needed:
                return
            if n in stack:
                raise RuntimeError("reference cycle: %s" % " -> ".join(stack + [n]))
            for r in self.assemblies[n].get("references", []):
                rn = self.resolve_ref(r)
                if rn in active:
                    visit(rn, stack + [n])
                else:
                    missing.setdefault(n, []).append(rn or r)
            needed.add(n)
            order.append(n)

        for r in sorted(roots):
            visit(r, [])
        return order, active, missing

    def precompiled_for(self, asm, is_editor, platform, defines):
        refs, analyzers = [], []
        override = asm.get("overrideReferences", False)
        wanted = set(asm.get("precompiledReferences", [])) if override else None
        dset = set(defines)
        for dll in self.dlls:
            explicit, editor_on, enabled, excluded, analyzer, constraints = read_plugin_meta(dll)
            if analyzer:
                continue
            if is_editor and not editor_on:
                continue
            if not is_editor:
                meta_name = PLUGIN_PLATFORM_NAMES[platform]
                if (enabled is None and meta_name in excluded) or (enabled is not None and meta_name not in enabled):
                    continue
            if not all(constraint_ok(c, dset) for c in constraints):
                continue
            if override:
                if dll.name in wanted:
                    refs.append(dll)
            elif not explicit:
                refs.append(dll)
        return refs, analyzers

    def framework_refs(self, is_editor):
        ns = self.data / "NetStandard"
        refs = [ns / "ref/2.1.0/netstandard.dll"]
        refs += sorted((ns / "compat/2.1.0/shims/netfx").glob("*.dll"))
        refs += sorted((ns / "compat/2.1.0/shims/netstandard").glob("*.dll"))
        refs += sorted((ns / "Extensions/2.0.0").glob("*.dll"))
        if is_editor:
            refs += sorted((ns / "EditorExtensions").glob("*.dll"))
        return refs

    def engine_refs(self, is_editor):
        managed = self.data / "Managed" / "UnityEngine"
        builtin = self.unity / "Editor/Data/Resources/PackageManager/BuiltInPackages"
        disabled = set()
        for d in builtin.glob("com.unity.modules.*"):
            mod = d.name[len("com.unity.modules."):]
            if d.name not in self.packages:
                disabled.add(mod.lower() + "module")
        refs = []
        for dll in sorted(managed.glob("*.dll")):
            n = dll.stem
            if n.startswith("Unity.Cecil"):
                continue
            if n.startswith("UnityEditor") and not is_editor:
                continue
            if n.startswith("UnityEngine.") and n[len("UnityEngine."):].lower() in disabled:
                continue
            refs.append(dll)
        if is_editor:
            refs.append(self.data / "Managed" / "UnityEditor.Graphs.dll")
        return refs

    def source_generators(self):
        return sorted((self.data / "Tools/BuildPipeline/Unity.SourceGenerators").glob("*.dll"))


# --------------------------------------------------------------------------------------------------- compile

def csc_command(data):
    dotnet = data / "NetCoreRuntime" / "dotnet"
    csc = data / "DotNetSdkRoslyn" / "csc.dll"
    return [str(dotnet), "exec", str(csc)]


def rel(path, project):
    try:
        return str(Path(path).resolve().relative_to(project.parent))
    except ValueError:
        return str(path)


def run_config(planner, config, out_root, jobs):
    is_editor, platform = CONFIGS[config]
    order, active, missing = planner.plan(config)
    out_dir = out_root / config
    out_dir.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, DOTNET_SYSTEM_GLOBALIZATION_INVARIANT="1", DOTNET_CLI_TELEMETRY_OPTOUT="1",
               DOTNET_NOLOGO="1")
    framework = planner.framework_refs(is_editor)
    engine = planner.engine_refs(is_editor)
    generators = planner.source_generators()

    results = {}
    errors, warnings = [], []
    done = {}

    def build(name):
        asm = planner.assemblies[name]
        defines = active[name]
        refs = list(framework)
        if not asm.get("noEngineReferences", False):
            refs += engine
        pre, _ = planner.precompiled_for(asm, is_editor, platform, defines)
        refs += pre
        for r in asm.get("references", []):
            rn = planner.resolve_ref(r)
            if rn in done:
                if done[rn] is None:
                    return name, "skipped", ["dependency %s failed" % rn], []
                refs.append(done[rn])
        out = out_dir / (name + ".dll")
        opts = list(BASE_OPTIONS)
        opts.append("-optimize-" if is_editor else "-optimize+")
        if asm.get("allowUnsafeCode", False):
            opts.append("-unsafe")
        opts.append("-out:" + str(out))
        opts += ["-define:" + d for d in sorted(set(defines))]
        opts += ['-r:"%s"' % r for r in refs]
        if not asm.get("noEngineReferences", False):
            opts += ['-analyzer:"%s"' % g for g in generators]
        if asm.is_project:
            opts += planner.rsp_options
        else:
            opts += ["-warn:0"]
        opts += ['"%s"' % s for s in asm.sources]
        rsp_text = "\n".join(opts) + "\n"
        key = hashlib.sha256(rsp_text.encode()).hexdigest()
        for s in asm.sources if asm.is_project else []:
            key = hashlib.sha256((key + hashlib.sha256(s.read_bytes()).hexdigest()).encode()).hexdigest()
        for r in refs:
            if str(r).startswith(str(out_dir)):
                key = hashlib.sha256((key + hashlib.sha256(Path(r).read_bytes()).hexdigest()).encode()).hexdigest()
        stamp = out_dir / (name + ".key")
        if not asm.is_project and out.exists() and stamp.exists() and stamp.read_text() == key:
            return name, "cached", [], []
        rsp = out_dir / (name + ".rsp")
        rsp.write_text(rsp_text)
        proc = subprocess.run(csc_command(planner.data) + ["@" + str(rsp)], capture_output=True, text=True, env=env)
        errs, warns = [], []
        for line in (proc.stdout + proc.stderr).splitlines():
            m = DIAG_RE.match(line.strip())
            if m:
                text = "%s(%s,%s): %s %s: %s" % (rel(m.group("file"), planner.project), m.group("line"),
                                                  m.group("col"), m.group("sev"), m.group("code"), m.group("msg"))
                (errs if m.group("sev") == "error" else warns).append(text)
            elif line.strip() and ("error" in line.lower()):
                errs.append(line.strip())
        if proc.returncode != 0:
            if not errs:
                errs.append("csc exited with %d: %s" % (proc.returncode, (proc.stdout + proc.stderr)[-2000:]))
            return name, "failed", errs, warns
        stamp.write_text(key)
        return name, "ok", errs, warns

    # Compile in dependency waves; independent assemblies run in parallel.
    remaining = list(order)
    with concurrent.futures.ThreadPoolExecutor(max_workers=jobs) as pool:
        while remaining:
            ready = [n for n in remaining if all(
                planner.resolve_ref(r) in done or planner.resolve_ref(r) not in remaining
                for r in planner.assemblies[n].get("references", []))]
            if not ready:
                raise RuntimeError("cannot order assemblies: %s" % remaining)
            for name, status, errs, warns in pool.map(build, ready):
                remaining.remove(name)
                done[name] = (out_dir / (name + ".dll")) if status in ("ok", "cached") else None
                results[name] = status
                asm = planner.assemblies[name]
                tag = "project" if asm.is_project else "package"
                log("  %-8s %-7s %s%s" % (status, tag, name,
                                          "" if not (errs or warns) else "  (%d errors, %d warnings)" % (
                                              len(errs), len(warns))))
                if asm.is_project or status == "failed":
                    errors += errs
                    if asm.is_project:
                        warnings += warns
    return order, results, errors, warnings, missing


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--project", required=True)
    ap.add_argument("--unity", required=True)
    ap.add_argument("--cache", required=True)
    ap.add_argument("--unity-version", required=True)
    ap.add_argument("--fetch-only", action="store_true")
    ap.add_argument("--jobs", type=int, default=os.cpu_count() or 2)
    ap.add_argument("configs", nargs="*")
    args = ap.parse_args()

    project, unity, cache = Path(args.project).resolve(), Path(args.unity).resolve(), Path(args.cache).resolve()
    if args.fetch_only:
        resolve_packages(project, unity, cache, fetch=True)
        return 0

    configs = args.configs or list(CONFIGS)
    for c in configs:
        if c not in CONFIGS:
            log("unknown configuration '%s' (known: %s)" % (c, ", ".join(CONFIGS)))
            return 2

    planner = Planner(args)
    if planner.loose:
        log("[compile-check] NOTE: %d script(s) outside any .asmdef (Assembly-CSharp) are not checked:" %
            len(planner.loose))
        for p in planner.loose:
            log("    " + rel(p, planner.project))

    failed = False
    summary = []
    for config in configs:
        log("[compile-check] === %s ===" % config)
        order, results, errors, warnings, missing = run_config(planner, config, cache / "build", args.jobs)
        project_asms = [n for n in order if planner.assemblies[n].is_project]
        for n in project_asms:
            for m in missing.get(n, []):
                log("  note: %s references '%s', which does not exist in this configuration (ignored, as in Unity)"
                    % (n, m))
        for e in errors:
            log("  " + e)
        for w in warnings:
            log("  " + w)
        bad = [n for n, s in results.items() if s not in ("ok", "cached")]
        ok = not bad and not errors
        failed |= not ok
        summary.append("%s: %s (%d project assemblies, %d package assemblies, %d errors, %d warnings)" % (
            config, "PASS" if ok else "FAIL", len(project_asms), len(order) - len(project_asms),
            len(errors), len(warnings)))
    log("[compile-check] Summary")
    for s in summary:
        log("  " + s)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
