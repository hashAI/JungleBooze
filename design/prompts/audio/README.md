# AURELIA audio prompts and pipeline

- `aurelia_audio.json`: every ElevenLabs prompt (sound effects, ambience, music composition plans), keyed by the
  spec 103 §11 hook ids. Prompts never name artists, songs, films or brands.
- Generate: `python3 tools/assetgen/elevenlabs_audio.py gen [ids] --ceiling <credits>` (skips existing takes; logs
  credits to `art_source/audio/elevenlabs/calls.jsonl`). Check the balance first: `... credits`.
- Listen-check: `python3 tools/assetgen/audio_analyze.py [--loop] [--detail=4] files` (loudness, true peak, clipping,
  spectral balance, tempo, loop seams).
- Build game files: `python3 tools/assetgen/audio_build.py` → `UnityProject/Assets/_Game/Audio/**.ogg` (curation
  table at the top of the script: chosen takes, filters, loudness targets). Then in Unity:
  `JungleBooze/Audio/Build Audio Catalog` (keeps hand-tuned cue values).
- Demo with sound: `tools/ci/unity.sh method JungleBooze.Editor.Audio.ExpeditionAudioRender.Capture`, then
  `python3 tools/assetgen/audio_offline_mix.py /tmp/junglebooze-video/expedition_audio.jsonl out.wav --video in.mp4 --out demo.mp4`.

Main theme: three candidates were generated (`mus.theme.candA/B/C`, raw takes in `art_source/audio/elevenlabs/raw/`):
A orchestral-folk (D major), B world-percussion with kalimba, marimba and wooden flute (A major), C cinematic pulp brass
(C major). **B was chosen [ASSUMED, owner to confirm]**: it was the only one with a clear explore/danger contrast and
a bright, phone-speaker-friendly mix. The final theme (`mus.theme.main`) uses B's style, and its danger section
(`mus.theme.danger`) was conditioned on the theme itself.
