#!/usr/bin/env python3
"""Write the asset tables of Obstacles/README.md and Forest/README.md from the bake reports
(art_source/environment/work/forest/<set>_report.json, written by forestlib.bake_set). Only the part between the
'<!-- tables -->' markers is replaced; the prose around it is hand-written. Usage: python3 forest_readme.py"""
import json
import os

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
WORK = os.path.join(REPO, 'art_source', 'environment', 'work', 'forest')
ENV = os.path.join(REPO, 'UnityProject', 'Assets', '_Game', 'Art', 'Environment')

# intended gameplay box (Unity local, pivot = front-face centre on the floor unless stated): text per piece
HIT = {
    'OB_RootLow_05': 'Low top 0.5, full 7.0 w x 0.6 d (visual root band ~1.0 m deep)',
    'OB_RootLow_06': 'Low top 0.6, full 7.0 w x 0.6 d',
    'OB_RootLow_Half_05': 'Low top 0.5, 4.0 w x 0.6 d centred on the pivot (also Low 0.4 x +0.5..+4.5: scale Y 0.8)',
    'OB_BranchHigh_10': 'High bottom 1.0, full 7.0 w x 0.8 d, up to 3.0',
    'OB_BranchKnot_05': 'Low top 0.5, Walk, 2.4 w x 0.8 d (on a canopy beam)',
    'OB_LogWalk_09': 'Low top 0.9, Walk, full 7.0 w x 2.0 d (C2 s70; C13 Low 0.8: scale Y 0.89)',
    'OB_LogWalk_Giant_09': 'Low top 0.9, Walk, full 7.0 w x 5.0 d (C12 s15)',
    'OB_Driftwood_05': 'Low top 0.5, Walk, full 7.0 w x 0.6 d (C5 ford, in shallow water)',
    'OB_RootWall_4m': 'Blocker 4.0 w (x -2..+2 about the pivot) x 1.6 d, full height (C2/C12 half-path blockers)',
    'OB_RootWall_Divider_10m': 'Divider / blocker 1.2 w x 10 d per segment (tile along Z, overlap 0.4 m)',
    'OB_VineCurtain_7m': 'High bottom 1.0 (visual bottom 1.06-1.3), full 7.0 w x 0.6 d',
    'OB_VineCurtain_3m': 'High bottom 1.0, 2.4 w x 0.6 d (canopy beam, C8 s160)',
    'OB_VineAnchor': 'no collision; pivot = rope attach point (anchor sA, hA)',
    'OB_VineRope_7m': 'no collision; pivot = top (attach), hangs to -7.0; scale Y to the vine length L; empty _Grip at -6.7',
    'OB_FloatLog_9m': 'FloatingLog -0.4..+0.4 about the water surface, 9.0 w x 0.8 d',
    'OB_DebrisMat_5m': 'FloatingLog (debris mat) 5.0 w (x -2.5..+2.5) x 0.8 d',
    'OB_Snag_9m': 'Snag riverbed..+0.3, 9.0 w x 0.8 d (leap only)',
    'OB_LowBranch_Water_55': 'LowBranch bottom +0.5 above water, 5.5 w centred on the pivot (C6: x -1.0..+4.5 -> pivot x +1.75)',
    'OB_RiverRock_14': 'Rock 1.4 w x 1.4 d, full height (top 1.6 above water, base 1.7 below)',
    'OB_SteppingRock_A': 'walk top 0 (wet stepping rock 2.6 x 2.7, C9 risky)',
    'OB_SteppingRock_B': 'walk top 0 (2.3 x 3.6)',
    'OB_Boulder_12': 'Blocker 1.2 w x 1.2 d, height 2.55', 'OB_Boulder_14': 'Blocker 1.4 w x 1.4 d, height 2.8',
    'OB_Boulder_16': 'Blocker 1.6 w x 1.6 d, height 3.0', 'OB_Boulder_20': 'Blocker 2.0 w x 2.0 d, height 3.3',
    'OB_WetRockLow_05': 'Low top 0.5, full 7.0 w x 0.6 d (C7 wet rock)',
    'OB_GapLip_8m': 'Gap edge: lip at Z = 0, floor (top -0.02) for Z -3..0, drop 6.3 m; far lip = rotate 180 deg',
    'OB_GapLip_4m': 'Gap edge for partial gaps (4.5 w), as above',
    'OB_Thorns_15': 'Thorns 1.5 w x 1.2 d, top 0.6', 'OB_Thorns_25': 'Thorns 2.5 w x 1.2 d, top 0.6',
    'OB_Thorns_35': 'Thorns 3.5 w x 1.2 d, top 0.6 (full width = 2 side by side)',
    'PK_Coin': 'Coin point at the pivot (centre); faces Z; spin about Y', 'PK_Crystal': 'Crystal point at the pivot',
    'PK_Shield': 'Shield pickup at the pivot; faces -Z (toward the runner)',
    'DM_Cairn': 'Discovery marker (off path), pivot on the ground', 'DM_SurveyPost': 'Discovery marker (off path)',
    'TR_CanopyBeam_A_12m': 'walk top 0, 2.4 w, Z 0..12 (branch continues 0.9 m past each end)',
    'TR_CanopyBeam_B_10m': 'walk top 0, 2.0 w, Z 0..10',
    'TR_CanopyBeam_Mid_8m': 'walk top 0, 2.4 w, Z 0..8, constant section (chain beams)',
    'TR_CanopyPlatform_20m': 'walk top 0, 7.4 w at Z 0 tapering to 2.9 w at Z 20 (C8 takeoff funnel), trunk behind',
    'TR_PathEdge_Roots_8m': 'path edge strip, Z 0..8, X -0.4 (path side) .. +2.2 (out); left side = rotate 180',
    'TR_PathEdge_Moss_8m': 'as above, moss cushions + stones',
    'TR_Riverbank_10m': 'pivot at the water line; path top at Y +1.0; bank faces +X (river side)',
    'TR_WaterEdge_10m': 'pivot at the water surface; shallow edge stones + reeds',
}


def table(sets):
    rows = ['| Asset | Texture set | Size W x H x D (m) | Tris LOD0 / 1 / 2 | Intended gameplay box | Measured |',
            '|---|---|---|---|---|---|']
    for st in sets:
        r = json.load(open(os.path.join(WORK, st + '_report.json')))
        for p in r['pieces']:
            w, h, d = p['size']
            rows.append(f"| `{p['name']}` | `{st}` {r['res']} ({r['density']:.0f} px/m) | {w:.1f} x {h:.1f} x {d:.1f} | "
                        f"{' / '.join(f'{t:,}' for t in p['tris'])} | {HIT.get(p['name'], '')} | {p['hit']} |")
    return '\n'.join(rows)


def patch(path, sets):
    s = open(path).read()
    a, b = s.index('<!-- tables -->'), s.rindex('<!-- /tables -->')
    s = s[:a] + '<!-- tables -->\n' + table(sets) + '\n' + s[b:]
    open(path, 'w').write(s)
    print('wrote', os.path.relpath(path, REPO))


if __name__ == '__main__':
    patch(os.path.join(ENV, 'Obstacles', 'README.md'),
          ['OB_Roots', 'OB_Logs', 'OB_Vines', 'OB_Water', 'OB_Stone', 'OB_Hazard', 'PK_Pickups'])
    patch(os.path.join(ENV, 'Forest', 'README.md'),
          ['TR_Canopy', 'TR_CanopyPlatform', 'TR_Edges', 'FT_Stiltwood_A', 'FT_Stiltwood_B', 'FT_Stiltwood_Gate',
           'FT_MidTrees'])
