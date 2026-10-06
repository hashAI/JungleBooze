# Hero and Companion Concepts (Gate G2)

**Owner:** art-director | **Status:** Decided at G2 (see `design/DECISIONS.md`); binding rules now in `design/STYLE_GUIDE.md` | **Last updated:** 2026-10-06

Fixed by the owner: the hero is a **wild jungle kid** (grew up among animals, barefoot, fearless, playful); the
companion is a **macaw parrot** (loud, colorful, flies overhead, squawks warnings). Mood: pulpy adventure.

Codenames below (Topknot, Mapcloth, Leafcape, Blaze, Goldbelly, Dusk) are **working labels only**. Names are the
owner's call later. Image prompts are in `design/prompts/hero_variants.md` and `design/prompts/macaw_variants.md`.

---

## Design rules for all variants

- **Designed for the back view.** The camera sits 6 m behind and 3.2 m above (GDD 6), so the hero's back, hair,
  and anything worn on the back are what players see 90% of the time. Each variant has a strong back-view landmark.
- **Readable at 60 px tall.** On a phone the hero is roughly 60–90 px tall in play. Jump, slide, and lane change
  must read from the silhouette alone.
- **Color rules.** No hazard red (`#D7263D`) and no plain jungle green as a main color. Each variant has a
  contrast check against all four worlds.
- **Shared backstory hook (treasure hunt).** The kid grew up near a long-abandoned expedition camp deep in the
  jungle, raised by the animals around it. Their outfit is made of jungle materials plus salvaged expedition
  scraps (canvas, rope, a brass trinket). This gives a pulp-adventure flavor without the colonial "explorer"
  look and without borrowing any real culture.
- **Respectful design.** No headdresses, no sacred or real-world tribal patterns, no "noble savage" clichés,
  no loincloth. Face markings, if any, are simple playful clay dots or mud smudges, never red war paint.
- **3D-friendly.** No thin dangling straps, loose strands of hair, or see-through parts (they break AI 3D
  generation and rigging). Chunky shapes, readable hands, closed mouth for the base model.
- **Age range ~9–12.** Kid proportions: head about 1/5 of height in Board A/B, 1/4 in Board C.

---

## Hero variants

### H1 "Topknot" (acrobat)

| | |
|---|---|
| **Silhouette** | Compact and springy. A tall bundle of dark hair tied straight up with a twist of vine into a big spiky topknot, roughly 1/4 extra height. Reads as an upward "flame" shape from behind. |
| **Outfit** | An oversized old expedition shirt worn as a tunic, sleeves torn off, belted with a braided vine; patched knee-length shorts; cloth wraps on wrists and ankles; barefoot. |
| **Colors** | Deep teal tunic `#1F7A80`, saffron vine belt and wraps `#F2A900`, cream patches `#F3E6C8`, warm brown skin `#8D5A3B`, near-black hair `#231A17`. |
| **Signature item** | A dented brass compass on a short cord around the neck. It glints when treasure (coins) is near and is a natural icon motif. |
| **Personality in motion** | Gymnast energy. Runs with a bounce, lane changes are quick side-hops, jumps are tucked flips on big jumps, slides are a low baseball-style slide with a grin. Idle: balances on one foot, spins the compass. |
| **Back-view landmark** | The topknot (moves with every hop, so jumps read even when tiny). |
| **Contrast check** | Jungle: teal vs green is close in value, saffron belt and cream patches save it; needs a warm rim light. River: teal close to water, same fix. Mountains and Ruins: strong. |
| **Risk** | Teal body competes with jungle and river backgrounds. Could switch tunic to cream if chosen. |

### H2 "Mapcloth" (wild one)

| | |
|---|---|
| **Silhouette** | Lean and low. Big round cloud of curly hair (a wide circle on top), long arms, and a crouched, forward-leaning run like a young big cat. A chunky satchel bounces on one hip. |
| **Outfit** | A wrap top and knee-length wrap shorts cut from an old canvas map: faded cream fabric with sepia map lines, a dotted trail, and an "X" visible on the back. A teal cloth sash across the chest holds a salvaged leather satchel. Barefoot, with simple woven ankle bands. Two pale clay dots on each cheek. |
| **Colors** | Map cream `#EFE0BD` with sepia lines `#8A5A2B`, teal sash `#178F8A`, saffron satchel strap `#F2A900`, deep brown skin `#6B4029`, dark brown hair `#2B1B14`. |
| **Signature item** | The map-cloth outfit itself: the treasure "X" sits right between the shoulder blades, so the brand mark is on screen all the time. Also a carved bamboo whistle she uses to call the macaw. |
| **Personality in motion** | Feral and playful. Runs low with long strides, drops to hands and feet for a beat on landings, slides like a cat with a little snarl, lane changes are sideways pounces. Idle: sniffs the air, scratches an ear with a foot, laughs. |
| **Back-view landmark** | The round hair cloud plus the cream map back with the "X". |
| **Contrast check** | Jungle: cream on green, excellent. River: excellent. Ruins: cream vs sandstone is close; dark hair and teal sash carry it, add a cool rim light. Mountains: cream vs snow is close; skin, hair, and sash carry it. |
| **Risk** | The low, animal-like run must still make "slide" clearly lower than "run". Animation needs a clear height difference (GDD 6: 1.8 m standing vs 0.8 m sliding). |

### H3 "Leafcape" (little daredevil)

| | |
|---|---|
| **Silhouette** | Youngest and roundest (~8–9 years old). Short, chunky body, big head, and a huge lobed leaf worn as a cape that flares out behind. The cape makes a wide triangle from behind. |
| **Outfit** | A giant tropical leaf cape tied at the neck with a seed-pod clasp; a sleeveless tunic made from salvaged sailcloth; short rolled-up trousers; barefoot. A hollowed half-gourd worn as a helmet, too big for the head. |
| **Colors** | Leaf cape lime `#9BCB3C` with sunset-orange tips `#F28C28`, sailcloth off-white `#EDE6D6`, gourd ochre `#C9923F`, light olive skin `#C49A6C`, black hair `#1B1512`. |
| **Signature item** | The leaf cape. It flares on jumps and acts like a mini-glider on vine releases, which looks great in clips. |
| **Personality in motion** | Fearless and clumsy-brave. Runs with tiny fast steps, arms out, gourd helmet wobbling. Jumps are star-shaped with the cape spread. Slides are belly-slides on the cape like a sled. Idle: pushes the helmet up from over the eyes. |
| **Back-view landmark** | The cape (very large on screen). |
| **Contrast check** | Jungle: lime cape vs green background is the weakest of all variants. River and Mountains: fine. Ruins: fine. |
| **Risk** | The cape covers the body from behind, which hides arm and leg poses, so jump vs slide reads less clearly. A cape also needs cloth or extra bones in the rig (more animation work and performance cost). The green cape breaks the "no plain jungle green" rule; it would need to change color. |

### Originality check (heroes)

| Known character | What to avoid | H1 Topknot | H2 Mapcloth | H3 Leafcape |
|---|---|---|---|---|
| Tarzan (any version) | Adult, loincloth, long loose hair, vine yell | Clear | Clear | Clear |
| Mowgli (The Jungle Book) | Small boy, red loincloth, shaggy black bob, bare chest | Clear (covered tunic, topknot, teal) | Clear | Clear |
| Moana | Polynesian styling, tapa-cloth patterns, long wavy hair, ocean focus | Clear | Clear (map print, not tapa) | Clear |
| Crash Bandicoot | Orange animal, blue jeans, spin attack, goofy grin | Clear | Clear | Clear |
| Lara Croft | Long braid, tank top, holsters, adult | Clear | Clear | Clear |
| Indiana Jones / pulp explorers | Fedora, whip, leather jacket | Clear | Clear | Clear (gourd, not a hat brim) |
| San (Princess Mononoke) | Wolf-raised girl, red face marks, fur cape, mask | Clear | **Watch:** keep cheek dots pale clay, never red; no fur | Clear (leaf, not fur) |
| Dora / Diego (kids' TV explorers) | Bob haircut, pink tee and orange shorts, purple backpack; khaki rescue vest | Clear | Clear | Clear |
| Kida (Atlantis), Aloy (Horizon) | White hair and tattoos; red braids and tribal gear | Clear | Clear | Clear |
| Kirikou | Very small, unclothed African boy | Clear | Clear | Clear (clothed, older) |
| Temple Run / Subway Surfers runners | Adult explorer in pith helmet; graffiti kid in hoodie | Clear | Clear | **Watch:** gourd must not read as a pith helmet |
| Rayman, Spirou, Peter Pan | Floating limbs; bellhop uniform; green tunic and feathered cap | Clear | Clear | **Watch:** green leaf cape + kid could hint at Peter Pan, another reason to recolor |

Result: all three are original. H2 and H3 each have one point to watch, listed above, and will be checked again
on the generated images.

---

## Macaw companion variants

The macaw flies **above and behind** the hero (GDD 15.1), so from the game camera it is seen mostly from above
and behind as well. Wings and tail are its landmarks.

### M1 "Blaze" (show-off)

| | |
|---|---|
| **Look** | Classic scarlet-style macaw: red-orange body, yellow and blue bands on the wings, long red tail. Slightly lanky with an oversized beak and a cheeky crest of three feathers. |
| **Colors** | Body `#E4572E` (warmer orange than hazard red), wing yellow `#FFC43D`, wing blue `#2667B5`, cream face patch `#F5EBDD`. |
| **Size vs hero** | Large: wingspan about the hero's height; body about the hero's head-to-waist. |
| **Personality** | Loud, vain, theatrical. Struts, poses for the camera, takes credit for every Perfect. Squawks warnings like a sports commentator. |
| **Readability** | Pops against green, blue, and snow. **Problem:** red-orange is close to the hazard accent, and players may glance at it when it swoops. Large size also risks covering the track during call-outs. |

### M2 "Goldbelly" (worrier)

| | |
|---|---|
| **Look** | Blue-and-gold style macaw: bright blue back and wings, golden-yellow chest and belly, a small green forehead patch, chubby round body. |
| **Colors** | Back `#1F6FB2`, belly `#FFC43D`, forehead `#5DBB63`, face `#F5EBDD`. |
| **Size vs hero** | Small: perches on the hero's shoulder when idle; body about the size of the hero's head. |
| **Personality** | Nervous and loyal. Sees danger everywhere and screams about it (perfect for warning call-outs), then faints with relief and pride when the hero pulls off a Perfect. |
| **Readability** | Gold belly echoes the coins (theme-friendly but may compete with them); blue back blends into the River world and the sky, which is what the camera sees most. |

### M3 "Dusk" (treasure thief)

| | |
|---|---|
| **Look** | An original, invented macaw: deep violet body and wings, sunset-orange head and chest, a teal band at the tail tip, and a long tail. Medium build, sleek. |
| **Colors** | Body `#5B3A8C`, head and chest `#F28C28`, tail tip `#2EC4B6`, beak dark gray `#3A3540`, face `#F5EBDD`. |
| **Size vs hero** | Medium: wingspan about 3/4 of the hero's height; can briefly perch on the hero's head. |
| **Personality** | Sly, greedy, loves anything shiny. Steals coins mid-air for fun (which is exactly what the Assist does: grabs every coin for 4 s), hides a coin under a wing during idle, squawks warnings because it does not want its treasure-finder hurt. |
| **Readability** | Violet is the complementary color of the warm golden light and the gold Ruins, so it pops in every world; it does not clash with hazard red or coin gold. The violet-and-orange combination is unusual and ownable for the app icon. |

### Originality check (macaws)

| Known character | What to avoid | M1 Blaze | M2 Goldbelly | M3 Dusk |
|---|---|---|---|---|
| Iago (Aladdin) | Red parrot, squat, wisecracking villain sidekick | **Watch:** keep lanky, long tail, cream face, different crest | Clear | Clear |
| Blu and Jewel (Rio) | All-blue macaws, Blu nerdy and flightless | Clear | **Watch:** blue back; gold belly and chubby shape keep it distinct | Clear (violet with orange head) |
| Pirate parrots (Captain Flint, generic) | Green parrot on a pirate shoulder, "pieces of eight" | Clear | Clear | Clear |
| Zazu (The Lion King), Kevin (Up) | Blue hornbill; giant rainbow bird | Clear | Clear | **Watch:** Dusk is a sleek macaw, not a tall rainbow bird |
| Brand mascots (cereal, airlines, rum labels) | Parrot logos on products, especially alcohol brands | Clear | Clear | Clear |

Result: all three are original. M1 and M2 sit closer to familiar real-world macaw looks (and to Iago and Rio
respectively), so they need more care in shape; M3 is the most distinct.

---

## Recommendations

### Hero: H2 "Mapcloth"

1. **Gameplay readability.** Cream outfit plus dark round hair gives the strongest value contrast in the Jungle and
   River worlds where new players spend most of their time; the low, cat-like run makes lane changes and jumps
   read as big body movements.
2. **AI-generation feasibility.** Simple wrap clothing, a solid hair mass, and one chunky satchel are easy for image-to-3D
   tools to model and easy to rig (no cape, no loose strands). The map print is just a texture.
3. **Clip appeal and brand.** The treasure "X" on the back is on screen all run long and ties the hero to the
   treasure-hunt mood; the "grew up with animals" body language is the most faithful to the owner's brief.

Runner-up: H1 "Topknot" (very clear silhouette and a great compass icon motif, but teal fights the green and blue
worlds). H3 "Leafcape" is the most charming in stills but the most expensive and least readable from behind.

### Macaw: M3 "Dusk"

1. **Gameplay readability.** Violet is the one color that never collides with hazard red, coin gold, or any of the four
   world backgrounds.
2. **AI-generation feasibility.** A medium, sleek bird with clean color blocks generates and rigs easily; medium size
   keeps it off the track during call-outs.
3. **Clip appeal and brand.** The coin-stealing personality links directly to the Assist mechanic and makes funny
   moments for clips; the violet-and-orange look is unique and works as an app icon next to the hero.

Note: color and personality can be mixed. For example, M2's nervous personality could be given M3's colors.
