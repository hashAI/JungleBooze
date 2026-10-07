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
