# Prompt Library

**Owner:** art-director | **Status:** Draft templates, untested (no image key yet). G2 decided: style C, hero H2, macaw M3; G6 names: hero Pista, macaw Duko (trademark check pending) | **Last updated:** 2026-10-06

Templates for the image generator (2D concepts) and hand-off notes for the image-to-3D tool.
Every prompt is written to be pasted as-is. After a template is tested, record the seed, settings, and a
thumbnail path in the "Test log" table of its file and change its status to **Tested**.

| File | Contents |
|---|---|
| `style_boards.md` | Style blocks A/B/C (A and B archive), `{STYLE_C_CHAR}` for character sheets, one board prompt per world |
| `hero_variants.md` | **Chosen H2:** locked SUBJECT, model sheet, expression sheet, pose sheet, 3D hand-off, in-game check. H1/H3 archive |
| `macaw_variants.md` | **Chosen M3:** locked SUBJECT, model sheet, expression sheet, pose sheet, 3D hand-off, placement check. M1/M2 archive |

## How templates are built

Each prompt = **SUBJECT block** + **STYLE block** + **FORMAT block**, plus a **NEGATIVE** prompt.
- `{STYLE_A}`, `{STYLE_B}`, `{STYLE_C}` are defined once in `style_boards.md`. Paste the full text in place
  of the token. For G2, render the hero and macaw concepts in **all three styles only if the owner asks**;
  by default render them in the recommended style (`{STYLE_C}`) so variants are compared on design, not style.
- Keep the SUBJECT block word-for-word identical between the concept sheet and the turnaround of the same
  character. Change only the FORMAT block.

## Global negative prompt (append to every negative)

```
photorealistic, photo, realistic skin pores, 3D render artifacts, text, watermark, signature, logo, letters,
brand names, copyrighted characters, Tarzan, Mowgli, Moana, Crash Bandicoot, Lara Croft, Indiana Jones,
fedora, whip, pith helmet, loincloth, headdress, war paint, tribal tattoo, sacred symbols, weapons, guns, blood,
gore, alcohol, bottles, cigarettes, extra fingers, missing fingers, fused limbs, extra limbs, deformed hands,
cropped head, cropped feet, blurry, low resolution, jpeg artifacts, busy background, multiple characters
(unless the prompt asks), sexualized, adult proportions on a child
```

## Settings

| Setting | Style boards | Concept sheets | Turnarounds | 3D hand-off images |
|---|---|---|---|---|
| Aspect ratio | 9:16 (portrait, matches the game) and 16:9 (store/board) | 3:2 | 21:9 or 3:1 | 1:1 |
| Images per prompt | 4 | 4 | 4 | 4 |
| Seed | Fixed per batch, logged | Fixed, logged | **Reuse the chosen concept's seed** | Reuse the chosen seed |
| Prompt strength / guidance | Medium | Medium-high | High | High |
| Reference image | none | none | chosen concept sheet | chosen front view |

If the generator supports a reference/consistency image, always feed the approved concept sheet into the
turnaround and 3D hand-off prompts.

## Consistency tips

1. **Lock the words.** Hex codes, hair shape, outfit pieces, and signature item stay word-for-word the same in every prompt
   for a character. Synonyms ("teal" vs "turquoise") make the generator drift.
2. **Lock the seed.** Once an image is approved, reuse its seed for every follow-up view and change only the FORMAT block.
3. **One subject per image.** Generate hero and macaw separately, then combine for key art.
4. **Neutral light for sheets.** Concept sheets and turnarounds use flat, even studio light on a plain light-gray
   background (`#D9D9D9`); the golden light belongs to style boards and key art only. Baked-in sunlight breaks 3D texturing.
5. **Name the view explicitly.** "front view, orthographic, A-pose, arms 30 degrees from body" works better than "turnaround".
6. **Check the back view first.** Players see the hero from behind; reject sets where the back view is weak or inconsistent.
7. **Squint test every result.** Shrink to 64 px tall in grayscale: the silhouette must still read.
8. **Log everything.** Seed, settings, date, file path, and chosen image go into the file's Test log, and the tool,
   plan, and commercial-use license go into `docs/LICENSES.md` when an asset is kept.

## 3D hand-off rules (image-to-3D tool)

Images sent to the image-to-3D tool must be: single character, full body, front view, A-pose, neutral expression with
closed mouth, plain light-gray background, even lighting with no cast shadows, no motion blur, no loose hair strands,
no thin straps, no transparent parts. Ask for a quad or clean triangle mesh at the budget in `design/STYLE_GUIDE.md`
and a single texture; the asset pipeline decimates, rigs (auto-rigger for humanoids), and repaints as needed.
