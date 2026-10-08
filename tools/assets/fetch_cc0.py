#!/usr/bin/env python3
"""Download the curated CC0 assets for the AURELIA look test from Poly Haven (ADR 0004).

Reads tools/assets/cc0_assets.json, asks the Poly Haven API (https://api.polyhaven.com) for each asset's file list,
downloads the chosen resolution into UnityProject/Assets/_Game/Art/CC0/<type>/<id>/, checks every file's MD5 against
the API, and appends one row per asset (source URL, authors, license) to docs/LICENSES.md.

Python 3.8+ standard library only. Run from anywhere:

    python3 tools/assets/fetch_cc0.py --dry-run      # list what would be downloaded (sizes), download nothing
    python3 tools/assets/fetch_cc0.py                # download everything (about 70 MB), then update LICENSES.md
    python3 tools/assets/fetch_cc0.py --only fern_02 rocky_trail
    python3 tools/assets/fetch_cc0.py --force        # download again even if the files exist

Files already on disk with the right MD5 are skipped, so the script can be re-run safely. The downloaded files are
large binaries tracked by Git LFS (.gitattributes); Unity creates their .meta files on the next editor focus.
Import settings (ASTC sizes, normal maps, cubemap for the HDRI) are applied by the editor script
Assets/_Game/Editor/LookTest/LookTestAssetImportRules.cs, not by this script.
"""

import argparse
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.request

API = "https://api.polyhaven.com"
ASSET_PAGE = "https://polyhaven.com/a/"
USER_AGENT = "PistaDuko-LookTest-Fetch/1.0 (CC0 asset fetch for a mobile game prototype)"

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
LIST_PATH = os.path.join(SCRIPT_DIR, "cc0_assets.json")
ART_ROOT = os.path.join(REPO_ROOT, "UnityProject", "Assets", "_Game", "Art", "CC0")
LICENSES_PATH = os.path.join(REPO_ROOT, "docs", "LICENSES.md")
LICENSE_SECTION = "## CC0 assets (Poly Haven), fetched by tools/assets/fetch_cc0.py"

# Folder per asset type under Art/CC0/.
TYPE_FOLDERS = {"hdris": "HDRI", "textures": "Textures", "models": "Models"}

# Preferred formats per map (smallest file that Unity imports well; Unity re-encodes to ASTC anyway).
MAP_FORMAT_PREFERENCE = {"Alpha": ["png", "jpg"], "leaves_alpha": ["png", "jpg"]}
DEFAULT_MAP_FORMATS = ["jpg", "png"]


def http_json(url, retries=3):
    for attempt in range(retries):
        try:
            request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
            with urllib.request.urlopen(request, timeout=60) as response:
                return json.loads(response.read().decode("utf-8"))
        except (urllib.error.URLError, TimeoutError) as error:
            if attempt == retries - 1:
                raise RuntimeError("Request failed: %s (%s)" % (url, error))
            time.sleep(2 * (attempt + 1))
    return None


def download(url, target, expected_md5, retries=3):
    os.makedirs(os.path.dirname(target), exist_ok=True)
    temp = target + ".part"
    for attempt in range(retries):
        try:
            request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
            digest = hashlib.md5()
            with urllib.request.urlopen(request, timeout=300) as response, open(temp, "wb") as out:
                while True:
                    block = response.read(1 << 16)
                    if not block:
                        break
                    digest.update(block)
                    out.write(block)
            if expected_md5 and digest.hexdigest() != expected_md5:
                raise RuntimeError("MD5 mismatch for %s" % url)
            os.replace(temp, target)  # Atomic: a half-written file never looks finished.
            return
        except (urllib.error.URLError, TimeoutError, RuntimeError) as error:
            if os.path.exists(temp):
                os.remove(temp)
            if attempt == retries - 1:
                raise RuntimeError("Download failed: %s (%s)" % (url, error))
            time.sleep(2 * (attempt + 1))


def file_md5(path):
    digest = hashlib.md5()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1 << 16), b""):
            digest.update(block)
    return digest.hexdigest()


def pick_format(entry, preferences):
    for fmt in preferences:
        if fmt in entry:
            return entry[fmt]
    return None


def plan_asset(asset_type, item, files):
    """Returns a list of (url, size, md5, filename) for one asset at its configured resolution."""
    res = item["res"]
    planned = []
    if asset_type == "hdris":
        entry = files["hdri"].get(res)
        if entry is None or "hdr" not in entry:
            raise RuntimeError("%s: no .hdr at %s (has %s)" % (item["id"], res, sorted(files["hdri"].keys())))
        planned.append(entry["hdr"])
    else:
        if asset_type == "models" and not item.get("maps_only", False):
            fbx = files.get("fbx", {}).get(res, {}).get("fbx")
            if fbx is None:
                raise RuntimeError("%s: no FBX at %s" % (item["id"], res))
            planned.append(fbx)
        for map_key in item["maps"]:
            by_res = files.get(map_key)
            if by_res is None:
                raise RuntimeError("%s: map '%s' not offered (has %s)" % (item["id"], map_key, sorted(files.keys())))
            entry = by_res.get(res)
            if entry is None:
                raise RuntimeError("%s: map '%s' has no %s" % (item["id"], map_key, res))
            chosen = pick_format(entry, MAP_FORMAT_PREFERENCE.get(map_key, DEFAULT_MAP_FORMATS))
            if chosen is None:
                raise RuntimeError("%s: map '%s' %s has no jpg/png" % (item["id"], map_key, res))
            planned.append(chosen)

    result = []
    for entry in planned:
        url = entry["url"]
        result.append((url, entry.get("size", 0), entry.get("md5"), url.rsplit("/", 1)[-1]))
    return result


def authors_text(info):
    authors = info.get("authors") or {}
    return ", ".join("%s (%s)" % (name, role) for name, role in authors.items()) or "Poly Haven"


def license_row(asset_type, item, info, folder_rel, license_text):
    asset_id = item["id"]
    return (
        "| Poly Haven `%s` (%s) in `%s/` | CC0 %s | %s%s, by %s | %s | Yes (public domain, no attribution required; "
        "credit given anyway) | %s |"
        % (
            asset_id,
            info.get("name", asset_id),
            folder_rel,
            asset_type[:-1] if asset_type.endswith("s") else asset_type,
            ASSET_PAGE,
            asset_id,
            authors_text(info),
            license_text,
            time.strftime("%Y-%m-%d"),
        )
    )


def append_licenses(rows):
    with open(LICENSES_PATH, "r", encoding="utf-8") as handle:
        text = handle.read()

    new_rows = []
    for asset_id, row in rows:
        marker = "Poly Haven `%s`" % asset_id
        if marker not in text:
            new_rows.append(row)

    if not new_rows:
        return 0

    if LICENSE_SECTION not in text:
        text = text.rstrip("\n") + "\n\n" + LICENSE_SECTION + "\n\n" + (
            "All Poly Haven assets are CC0 1.0 (https://polyhaven.com/license): free for commercial use, no "
            "attribution required. We credit the authors here anyway.\n\n"
            "| Item | Type | Source | License | Commercial use OK | Added |\n"
            "|---|---|---|---|---|---|\n"
        )
    text = text.rstrip("\n") + "\n" + "\n".join(new_rows) + "\n"

    with open(LICENSES_PATH, "w", encoding="utf-8") as handle:
        handle.write(text)
    return len(new_rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--dry-run", action="store_true", help="list files and sizes; download nothing")
    parser.add_argument("--only", nargs="*", default=None, help="asset ids to fetch (default: all)")
    parser.add_argument("--force", action="store_true", help="download again even if files exist")
    parser.add_argument("--no-licenses", action="store_true", help="do not touch docs/LICENSES.md")
    args = parser.parse_args()

    with open(LIST_PATH, "r", encoding="utf-8") as handle:
        curated = json.load(handle)
    license_text = curated.get("license", "CC0 1.0")

    total_bytes = 0
    failures = []
    license_rows = []
    for asset_type in ("hdris", "textures", "models"):
        for item in curated.get(asset_type, []):
            asset_id = item["id"]
            if args.only and asset_id not in args.only:
                continue
            try:
                info = http_json("%s/info/%s" % (API, asset_id))
                files = http_json("%s/files/%s" % (API, asset_id))
                planned = plan_asset(asset_type, item, files)
            except RuntimeError as error:
                failures.append(str(error))
                print("FAIL  %s: %s" % (asset_id, error))
                continue

            folder = os.path.join(ART_ROOT, TYPE_FOLDERS[asset_type], asset_id)
            folder_rel = os.path.relpath(folder, REPO_ROOT).replace(os.sep, "/")
            size = sum(p[1] for p in planned)
            total_bytes += size
            print("%-6s %-40s %-4s %7.1f MB  -> %s" % (asset_type[:-1], asset_id, item["res"], size / 1e6, folder_rel))
            asset_ok = True
            for url, file_size, md5, name in planned:
                target = os.path.join(folder, name)
                exists = os.path.exists(target) and (md5 is None or file_md5(target) == md5)
                state = "have" if exists and not args.force else ("plan" if args.dry_run else "get ")
                print("         %s %-48s %7.1f MB" % (state, name, file_size / 1e6))
                if args.dry_run or (exists and not args.force):
                    continue
                try:
                    download(url, target, md5)
                except RuntimeError as error:
                    asset_ok = False
                    failures.append(str(error))
                    print("FAIL  %s" % error)

            if asset_ok:
                license_rows.append((asset_id, license_row(asset_type, item, info, folder_rel, license_text)))

    print("\nTotal: %.1f MB in %d assets." % (total_bytes / 1e6, len(license_rows)))
    if not args.dry_run and not args.no_licenses and license_rows:
        added = append_licenses(license_rows)
        print("docs/LICENSES.md: %d new row(s)." % added)
    if failures:
        print("\n%d problem(s):" % len(failures))
        for failure in failures:
            print("  - " + failure)
        return 1
    if not args.dry_run:
        print("Next: switch to Unity (it imports the files), then JungleBooze > Look Test > Build Scene.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
