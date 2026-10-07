# Decision Log

Every human decision, once made, goes here. Agents check this file before asking you anything.
Newest first.

| Date | Gate | Decision | Options considered | Decided by |
|---|---|---|---|---|
| 2026-10-07 | — | Pause lifted. Single focus: deliver a playable version. Reviewers apply fixes | — | Owner |
| 2026-10-07 | — | Tests are deferred: focus on making the code correct now; add the remaining tests later | — | Owner |
| 2026-10-07 | — | **Paused for owner code review.** Let the running agents (B1 collisions, B2 track, FP0 compile review) finish and commit their output; then make no further code changes and launch no new work until the owner has reviewed and says to continue. Review findings are reported, not applied. Resume watchdog disabled | — | Owner |
| 2026-10-07 | — | **Autonomous until the first playable version.** No questions to the owner; agents use their recommended defaults (marked `[ASSUMED]`, reviewed later). Don't stop until the owner can play a first version. Sessions may hand off and resume after usage limits on their own | — | Owner |
| 2026-10-07 | — | Don't install toolchains in the cloud container. Write the code; it runs on the owner's M4 MacBook (24 GB), where installing Unity and dependencies is fine | — | Owner |
| 2026-10-06 | — | **Focus on building and testing the game now.** Release pipeline, store and other shipping work are paused until later | — | Owner |
| 2026-10-06 | — | Add the `JungleBooze.App` assembly (composition root) to the project's assembly list | Add / Don't add | Owner |
| 2026-10-06 | G5 | No tracking for anyone: contextual ads only, no ATT prompt | No tracking / Adults-only ATT / Decide week 4 | Owner |
| 2026-10-06 | G6 | Paid trademark lawyer check stays in week 7 (owner accepts the risk of redoing voice/art if a name fails) | Week 3 / Week 7 | Owner |
| 2026-10-06 | G6 | Public app name candidate for the lawyer check: "Pista & Duko: Jungle Swing", subtitle "Endless vine-swinging runner" | 8 ranked candidates in docs/compliance/ | Owner |
| 2026-10-06 | — | **Quality over schedule:** if a world or feature isn't at full quality, launch waits. Never ship lighter versions to hit a date | Ship lighter and polish later / Delay launch | Owner |
| 2026-10-06 | G1 | Launch language: English only; EU languages in later updates | English / English + FIGS / English + DE + FR | Owner |
| 2026-10-06 | G2 | Hero sash worn as a band around the chest, just below the X on her back | Band below X / Keep diagonal / Move X lower | Owner |
| 2026-10-06 | G6 | Names: hero "Pista", macaw "Duko" (pending a proper trademark check in US/EU/UK registers and the App Store) | Pista/Duko, Fera/Rubo, Selva/Florin, Ilka/Orito, Tamsa/Tinko | Owner |
| 2026-10-06 | G2 | Macaw design: M3 "Dusk" (violet-and-orange coin thief) | M1 Blaze / M2 Goldbelly / M3 Dusk | Owner |
| 2026-10-06 | G2 | Hero design: H2 "Mapcloth" (cat-like wild girl, treasure-map clothes, big X on her back) | H1 Topknot / H2 Mapcloth / H3 Leafcape | Owner |
| 2026-10-06 | G2 | Art style: C "Inkbound Pulp" (bold comic-book look with ink outlines) | A Chunky Totem / B Golden Expedition / C Inkbound Pulp | Owner |
| 2026-10-06 | G1 | Macaw speaks a few words (e.g. "Vine!", "Look out!"), localized | Words / Squawks only / Squawks + catchphrase | Owner |
| 2026-10-06 | G0 | Unity license for CI: owner's free Personal account (move to a CI-only account later) | Personal / CI account / Pro | Owner |
| 2026-10-06 | G0 | Lowest supported device for 60 fps: iPhone 11 / SE 2nd gen (A13) | XR-XS / 11-SE2 / 12 | Owner |
| 2026-10-06 | G0 | iOS builds compiled and signed on the owner's own Mac | Own Mac / GitHub macOS runners / Codemagic-Unity Build | Owner |
| 2026-10-06 | G1 | Launch worlds: all 4 (Jungle, River, Mountains, Ruins) | 2 / 3 / 4 worlds | Owner |
| 2026-10-06 | G1 | Mood: pulpy adventure (warm golden light, drums and brass, treasure-hunt feel) | Sunny cartoon comedy / Pulpy adventure / Mysterious wonder | Owner |
| 2026-10-06 | G1 | Companion: macaw parrot (loud, colorful, flies overhead, squawks warnings) | Macaw / Capuchin / Jaguar cub / Sloth | Owner |
| 2026-10-06 | G1 | Hero: wild jungle kid (grew up among animals, barefoot, fearless, playful) | Wild kid / Young explorer / Treasure hunter / Park ranger | Owner |
| 2026-10-06 | — | Platform: iOS first (Android later) | Android first / iOS first | Owner |
