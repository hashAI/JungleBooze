# assetgen

`gen_env_assets.py` drives concept sheets and 3D generation for the environment kit. Keys come from
`~/.config/junglebooze/secrets.env` (`OPENAI_API_KEY`, `MESHY_API_KEY`); never commit or print them.
Spend caps (owner, 2026-10-07): at most 4 concept images and 200 Meshy credits. The script keeps a ledger in
`tools/assetgen/spend_ledger.json`, refuses to exceed the caps, and prints the Meshy balance first.

    python3 tools/assetgen/gen_env_assets.py balance
    python3 tools/assetgen/gen_env_assets.py concept <group>      # obstacles, pickups, ground, foliage
    python3 tools/assetgen/gen_env_assets.py model <prefab_name> "<prompt>"

Priority: coin, the three common obstacles, ground tile, then power-ups and foliage. Untested (no keys were available
when written). Record each result in docs/LICENSES.md.

## Look pass (2026-10-07)

- `make_ground_textures.py <EnvironmentArt dir>`: procedural, seamlessly tiling `Ground_Trail` and `Ground_JungleFloor`
  textures (no generation service).
- `meshy_env.py` now records the measured Meshy balance before/after each task in `env_spend_log.jsonl`; the cap is
  `MESHY_ENV_CAP` (running total over the whole ledger, default 450). Kit sources: `Foliage_FernClump`,
  `Foliage_BigLeaf`, `Foliage_CanopyTree`, `Prop_RockCluster` (fit with `fit_env.py`, merged by
  `tools/blender/build_jungle_kit.py` into `Jungle_WallA/B/C` + `Jungle_Atlas`).
- Preview without Unity: `tools/blender/run_mock.py` (mirrors the run camera and the look config defaults).
