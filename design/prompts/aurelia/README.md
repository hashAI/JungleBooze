# Prompt Library: realistic Pista, Duko and look-test assets

**Owner:** art-director | **Status:** Ready to run, **untested** (no `OPENAI_API_KEY` / `MESHY_API_KEY` in the
environment where these were written) | **Last updated:** 2026-10-08

Look rules: `design/aurelia/ART_DIRECTION.md`. Scene contents: `design/aurelia/LOOK_TEST_BRIEF.md`.
The older stylized library (`design/prompts/*.md`, Inkbound Pulp) is archived; do not mix its STYLE blocks in.

| File | Contents | Task |
|---|---|---|
| `pista.md` | Locked realistic SUBJECT block (16, board-explorer look), 3 takes × (turnaround + key art), 3D input views after the pick | P0-C |
| ~~`duko.md`~~ | Archived to `archive/pre-aurelia/design/prompts/duko_realistic.md`: Duko was dropped by the owner (2026-10-08) | — |
| `meshy.md` | Meshy image-to-3D / multi-image-to-3D / rigging request bodies for Pista, Duko and look-test props, plus cleanup steps | P0-D, look-test props |
| `environment.md` | Concept images for Meshy props (stiltwood, rootstone, bellcap, sailback, whirlseed, bramble) and 2D cards (matte range, forest wall, veilmoss, reeds) | Look test |

## How prompts are built
Prompt = **SUBJECT** (locked, word-for-word) + **STYLE** (locked) + **FORMAT** (per image type) + **TAKE** (one
interpretation sentence) + **AVOID** (negative sentence; the OpenAI Images API has no negative-prompt parameter).
Paste the blocks in that order, separated by a space. Tokens in `{BRACES}` are replaced by the block of that name.
No character names appear in prompts (names are not needed and can pull the generator toward existing characters).

## Settings (OpenAI Images API)
| Setting | Turnarounds | Key art | 3D input views | Cards (transparent) |
|---|---|---|---|---|
| Model | `gpt-image-2` (as used on 2026-10-07) | same | same | same |
| Endpoint | `/v1/images/generations` | `/v1/images/edits` with the take's turnaround as `image[]` reference | `/v1/images/edits` with the **locked** turnaround as reference | `/v1/images/generations` |
| Size | Pista `2400x1200`; Duko `2800x1200` | `1824x1216` (landscape) `[ASSUMED]`; portrait alt `1216x1824` | `1024x1536` (Pista), `1536x1024` (Duko wings spread) | `2048x1024` or `1536x1024` |
| Quality | `high` | `high` | `high` | `high` |
| Background | opaque | opaque | opaque | `transparent`, `output_format: png` |
| n | 1 | 1 | 1 | 1 |

No seed parameter exists; consistency comes from the locked text and the reference image (rule 10.1 in
ART_DIRECTION). If a model newer than `gpt-image-2` is available when these run, test one turnaround on both and
log which one is used.

### Example calls
```bash
# Generation (turnaround)
curl -sS https://api.openai.com/v1/images/generations \
  -H "Authorization: Bearer $OPENAI_API_KEY" -H "Content-Type: application/json" \
  -d @request.json | jq -r '.data[0].b64_json' | base64 -d > out.png
# request.json: {"model":"gpt-image-2","prompt":"<assembled prompt>","size":"2400x1200","quality":"high","n":1}

# Edit with a reference image (key art, 3D input views)
curl -sS https://api.openai.com/v1/images/edits \
  -H "Authorization: Bearer $OPENAI_API_KEY" \
  -F model=gpt-image-2 -F "image[]=@pista_real_takeA_turnaround.png" \
  -F prompt="<assembled prompt>" -F size=1824x1216 -F quality=high \
  | jq -r '.data[0].b64_json' | base64 -d > out.png
```

## Run order and budget (P0-C)
1. Pista takes A, B, C turnarounds (3) → key art for each from its own turnaround (3).
2. Duko takes A, B, C turnarounds (3) → key art for each (3).
3. Contact sheets for the owner (one per character), owner picks.
4. After the pick: 3D input views (Pista 3, Duko 3), then `meshy.md`.

Budget: 12 images for the owner's choice + at most 1 retry per image for checklist failures = **cap 24 generations**
for step 1–2 `[ASSUMED]`. Each retry is logged with the reason.

**Output folder:** `design/concepts/2026-10-08/` with names `pista_real_take{A|B|C}_{turnaround|keyart}.png`,
`duko_real_take{A|B|C}_{turnaround|keyart}.png`. Large PNGs may be saved as JPEG q92 (as done on 2026-10-07).

## Review before showing the owner
Run the style-lock checklist (ART_DIRECTION 10.3), sections A, B and C, on every image. Typical generator failures
seen on 2026-10-07 and what to check first:
- Pista: the board's crop top coming back (bare midriff); tight leggings instead of technical trousers; holsters or
  weapons appearing; a single long braid instead of a high ponytail; a face that resembles a known actress or game
  heroine; looking older than 16 or glamorized.
- Duko: wrong colors on the tail (teal spreading up the tail), all-violet head, cartoon eyes.
- Realism-specific: plastic skin, uncanny faces, extra toes, hair turning into straight strands or braids,
  Pandora drift in backgrounds (floating rocks, planets, glowing plants).

## Test log
| Date | File / prompt | Model, size, quality | Output | Result |
|---|---|---|---|---|
| — | — | — | — | Not run yet |

When an image is kept, add its tool, plan and license to `docs/LICENSES.md`.
