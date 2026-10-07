# Roadmap (written 2026-10-07, after the first device run worked)

Owner priorities: (1) the jungle-crossing feel (Tarzan/Mowgli), (2) smooth and bug-free, (3) quality over schedule.
Meshy credits are scarce (about 465 left): procedural Blender first; contact sheet before any Meshy spend.

## Phase A: the crossing feels real (now)
- T5 canopy layers (Ascent/High/Descent, bough surface, vines inside the canopy) on top of the route generator.
- T8 scenery: stateless placer for trees, ferns, roots, light shafts along the route, using the procedural kit and existing foliage; densities within the perf budget (raised 2026-10-07 by owner direction for the dense corridor: about 90k tris, about 50 draws; spec 003 section 11.5 table is the old 60k/60 hint).
- UX polish: world banner and music crossfade per world, Duko idle chatter, a dev start-distance option, route log.
- Review follow-ups: boulder hitbox vs look (owner decision), coin instancing, remaining audit items.

## Phase B: the other worlds
- River, Mountains, Ruins kits (procedural first; 45 Meshy credits for three hero pieces only after the owner approves a contact sheet).
- Per-world obstacles skins and path styles from spec 003 sections 10 and 13.

## Phase C: quality
- Repair and extend tests (PlayMode view count, tutorial rescue ordering, save codec, exactly-once recording).
- Second code review and performance measurement on iPhone 11 / SE 2 (Profiler, Xcode Instruments), fix list from docs/perf.
- Balance pass with the simulator on the new route and worlds; fairness validator for late chunks.

## Phase D: product
- Monetization design with the owner (ad tolerance, prices), then ads/IAP with consent and ATT.
- App icon (1024), screenshots, privacy manifest, App Store compliance pass, age rating.
- TestFlight, beta, store submission. Meshy licence check on the owner's plan.

Gates for the owner: G3 feel check, G4 hook check (is the swing fun), G5 monetization, G6 name/icon, G7 beta, G8 ship.
