# Compliance review: AURELIA design (App Review risk)

**Owner:** appstore-compliance | **Date:** 2026-10-09 | **Status:** design-stage review. Not a submission run.

Inputs: `CLAUDE.md`, `design/aurelia/BLUEPRINT.md`, `design/aurelia/GDD.md` (v1), `design/aurelia/specs/101-movement-and-camera.md`,
`design/aurelia/ART_DIRECTION.md` §7.4, `design/DECISIONS.md`, `docs/STATUS.md`, `docs/LICENSES.md`,
`UnityProject/ProjectSettings/ProjectSettings.asset`, `UnityProject/ProjectSettings/UnityConnectSettings.asset`,
`UnityProject/Packages/manifest.json`. Earlier lane-game review: `docs/compliance/2026-10-name-and-early-review.md`
(only its Pista name evidence still applies).

Apple sources read 2026-10-09: App Review Guidelines (page shows **Last Updated: June 8, 2026**), Upcoming
requirements, Age ratings values and definitions, Third-party SDK requirements. Links at the end.

> **Not legal advice.** The name check uses public web search only. Official registers (USPTO, EUIPO, UKIPO, WIPO)
> were not queried directly. A web search cannot see pending filings or unregistered rights.

---

## 1. Ranked risks

Severity: **H** = would likely cause rejection, a forced rename, or a privacy-label violation if not handled.
**M** = rejection or rework possible. **L** = hygiene; cheap to get right now.

| # | Sev | Risk | Evidence | Action | Owner agent |
|---|---|---|---|---|---|
| 1 | **H** | **"AURELIA" alone is a weak, crowded name** (2.3.7 unique name; trademark exposure) | App Store: *Kingdom of Aurelia: Adventure* (Absolutist, iOS hidden-object game, IAP). Games: *Wheels of Aurelia* (Santa Ragione, delisted from iOS July 2025, still on Steam); *Aurelia* by Mirthal (**adult** visual novel, itch.io and a Steam page): an association we don't want for a 9+ game with a 16-year-old hero. Software: Aurelia is a known JavaScript framework (class 9 software). Generic: a Latin name, a jellyfish genus, cosmetics brands. No registered AURELIA mark for games found in public search, but none could be ruled out. | Treat AURELIA as a working title. Use a composite store name with a distinctive coined element (e.g. "Aurelia: <coined word>"), then a paid attorney search (US/EU/UK, classes 9, 28, 41) before any store art, logo, or voice line uses the name. Owner question Q1 | producer (owner question), appstore-compliance (shortlist + search) |
| 2 | **H** | **Hidden data leaving the device breaks "Data Not Collected"** (5.1.1, 5.1.2, privacy label accuracy) | `ProjectSettings.asset` line 96: `submitAnalytics: 1` (Unity hardware statistics). Unity Connect services, crash reporting and Engine Diagnostics are off (`UnityConnectSettings.asset`). GDD §22 plans a TestFlight-only upload to an owner endpoint. | (a) Set `submitAnalytics: 0` and check that the Unity license tier lets it take effect; (b) capture device traffic of a release build (proxy) and attach the capture as evidence: zero requests; (c) the TestFlight upload is behind a compile flag that CI proves is absent from App Store builds, and testers opt in on a consent screen first (5.1.1(ii) applies even to anonymous data); (d) if anything does leave the device in the App Store build, label it exactly | release-engineer (a, c), qa-engineer (b), appstore-compliance (label) |
| 3 | **H** | **Old-direction identifiers about to become permanent** | Bundle ID `com.pistaduko.junglerunner` (Duko dropped; a bundle ID can never change after the first upload, including TestFlight). `productName: Jungle Runner` (generic; a placeholder name in a build is a 2.1 risk). `companyName: DefaultCompany`. | Pick a neutral bundle ID now, not tied to a character or title that may change (e.g. `com.<owner-studio>.<codename>`). Set product/company names before the first TestFlight upload. Owner question Q2 | release-engineer |
| 4 | **M** | **Age rating drifts above 9+ as features ship** | Apple's table: 9+ = any *infrequent* horror/fear, cartoon/fantasy violence, mature themes, or loot boxes. **13+ = *frequent* contests**, frequent horror, infrequent realistic violence. Blueprint adds leaderboards, daily expeditions, weekly events (= Contests) and "giant creature" events, caves, falls. | MVP answers: Violence **None** (falls/stumbles with no conflict or injury, ART_DIRECTION §7.4), Horror/Fear **Infrequent** only if caves or giant creatures are threatening, rest None → 9+ (or 4+ if honest answers give 4+; never inflate answers). Before leaderboards/events ship: keep contests "infrequent" or accept 13+. Creatures must not attack Pista on screen (that becomes realistic violence → 13+). Record the answers in `docs/compliance/` per release | game-designer (keep content in band), appstore-compliance (questionnaire) |
| 5 | **M** | **Age-assurance laws (Declared Age Range)** | Texas SB 2420 enforceable since the Fifth Circuit stay (2026-06-04); the Supreme Court declined to intervene (2026-07-06). Utah (2026-05-07) and Louisiana (2026-07-01) in force. Apple's Declared Age Range API and Significant Update API are the safe-harbour route. Duties fall mostly on the stores, but developers must use the age signals they receive. | MVP (no account, no purchases, no ads, no data): low exposure. Before IAP, ads or a "significant change" update: native plugin for Declared Age Range, gate purchases on the age/consent signal, lawyer opinion on file. Re-check the Fifth Circuit outcome | monetization-engineer (plugin), producer (lawyer), appstore-compliance (re-check) |
| 6 | **M** | **4.3(b) spam: endless runners are a saturated category** | Guideline 4.3(b), revised 2026-06-08: no apps "indistinguishable from what's already widely available". Realistic jungle runner with a teen explorer reads as Temple Run-like at a glance. | Lead every screenshot, the preview and the review notes with what is unique: free steering (no lanes), visible safe/risky/secret forks, discovery journal, ability-gated secrets. No "Run" in the name. Never upload a Phase 1/2 build for review (also 4.2 minimum functionality) | art-director (screenshots), appstore-compliance (notes) |
| 7 | **M** | **Asset licence gaps** | (a) Unity Asset Store EULA §2.2.1.1(g) forbids using Asset Store assets "as inputs for artificial intelligence or machine learning model programs": feeding a pack mesh/texture into Meshy or OpenAI breaks the licence. Asset Store packs also need owner spend approval (2026-10-08: "no paid asset packs"). (b) Meshy Terms of Service updated 2026-03-07 mention use limited by account type; ownership of paid-plan output is stated in Meshy's pricing FAQ, not confirmed in the ToS text we could read; library animations (ids 0–539) are Meshy-provided motions, not our output. (c) `docs/LICENSES.md` still lists ElevenLabs audio in `Assets/_Game/Audio/`, which no longer exists, and OpenAI concepts in the archive. (d) CC0 Poly Haven: no issue. | (a) Rule: Asset Store content never goes into an AI tool; record seat count and pack licence type per pack. (b) Save a dated PDF of Meshy's ToS, pricing page and the owner's plan invoice; get written confirmation from Meshy support that library animations may ship in a commercial game. (c) Clean stale rows; add a "shipped: yes/no" column | asset-pipeline (a, c), producer (b, owner email) |
| 8 | **M** | **Minimum device vs what the App Store can enforce** | Owner may raise the minimum to iPhone 12/13 (2026-10-08). iOS 26 runs on iPhone 11 / SE 2 and later; Apple offers no capability key that excludes A13 devices. Deployment target is iOS 15.0. | Whatever the target, iPhone 11 / SE 2 users can install: the game must not crash there and should hit the 30 fps fallback (Blueprint XLVI). Optionally raise deployment target (e.g. iOS 17/18) to cut older OS testing, not devices. Test on an A13 device before submission | performance-engineer, qa-engineer |
| 9 | **L** | **iPhone-only: iPad, Mac and Vision Pro exposure** | `targetDevice: 0` (iPhone only), `uIRequiresFullScreen: 1`. Guideline 2.4.1: "iPhone apps should run on iPad whenever possible" (encouraged, not required). iPhone-only apps install on iPad in compatibility mode and reviewers may test there. iPhone apps are offered on Apple silicon Macs and Vision Pro by default. | Smoke-test on an iPad in compatibility mode (touch input, both orientations, safe areas). In App Store Connect, untick Mac and Vision Pro availability. Only iPhone screenshots needed | qa-engineer, release-engineer |
| 10 | **L** | **Orientation mismatch** | GDD §7: build follows device rotation until the owner picks. Project: `allowedAutorotateToPortrait: 0` (portrait off), landscape left/right on. | Enable portrait for the owner's Phase 1 test builds; after the decision, lock to the chosen orientation(s). Screenshots: one orientation for the whole set, the shipped one | release-engineer |
| 11 | **L** | **Screenshots and preview must be real gameplay** (2.3.3, 2.3.4, 2.3.8) | Look-test renders and Blender turnarounds exist and are tempting for store art. | Capture on device from the release build (preview video: screen capture only, overlays allowed). Pista rules (§7.4) and a 4+-safe icon/screens. No other runner, brand, or platform shown | art-director, qa-engineer |
| 12 | **L** | **Monetization plan** | Cosmetics, optional rewarded ads later, revive with crystals, no loot boxes, no ATT. | Compliant as designed. Rules when built: StoreKit only; Restore Purchases; purchased crystals never expire (3.1.1); revive offer always skippable; no randomized paid items; ads only after an ad-rating, report-an-ad and targeting-info review (2.5.18); Advertising answered "Yes" in the rating questionnaire. Companions never ask for purchases | monetization-engineer, ui-engineer |
| 13 | **L** | **Privacy manifest and SDK signatures** | UnityFramework is on Apple's list (manifest required; signature required as a binary dependency). Unity 6 writes engine required-reason entries automatically (UserDefaults CA92.1 for PlayerPrefs, file timestamp, boot time, disk space). Our local JSON save uses file APIs. | Check `PrivacyInfo.xcprivacy` in the final `.xcarchive`; add entries for any of our own required-reason calls (e.g. reading file timestamps). Each future SDK must ship its own manifest | release-engineer, appstore-compliance |
| 14 | **L** | **AI-generated content disclosure** | Guidelines (June 8, 2026) have no rule on AI-made art/audio. 5.1.2(i) covers only sending personal data to third-party AI. Secondary sources claim an Apple AI disclosure rule; not found in Apple's text. | No App Store disclosure needed for pre-made assets. No in-game generative AI, so no consent flow. Re-check at submission; Google Play and Steam have their own rules for later ports | appstore-compliance |
| 15 | **L** | **Accessibility Nutrition Labels** | Voluntary now; Apple says they will become mandatory with notice. | Claim only what passes Apple's criteria. Likely: Reduced Motion (GDD §20). Do not claim VoiceOver, Voice Control, or Larger Text unless tested | ui-engineer, qa-engineer |
| 16 | **L** | **Backend and accounts (Blueprint XLIX, later)** | Account, cloud save, leaderboard. | If accounts arrive: in-app account deletion (5.1.1(v)), Sign in with Apple when third-party login exists (4.8), privacy label update, IPv6 test. Prefer Game Center + iCloud (no own accounts) | tech-architect |

## 2. Topic notes

**Age rating (target 9+).** A realistic 16-year-old hero is not itself a rating factor. Rating comes from content.
Falls into gaps with a fade-out and no injury are peril, not violence by Apple's definitions (violence = "physical
conflict or harm"). The art rules (no ragdoll, no pain faces, no weapons, covered torso) keep it there. Our honest
answers may produce 4+; that is fine. Never tick a descriptor that isn't true just to reach 9+.

**Monetization.** Earned-crystal revive is not a 3.1 issue. Once crystals can be bought, revive becomes a paid
convenience: fine under 3.1.1 if purchased currency never expires and the offer is skippable. No loot boxes means no
odds disclosure and no Australian 16+ trigger. No tracking means no ATT prompt and no IDFA. Contextual ads will
still need the 2.5.18 items and the Advertising answer in the questionnaire.

**On-device analytics → privacy labels.** Apple's label counts data as "collected" only when it leaves the device.
On-device-only events (GDD §22) → "Data Not Collected", **provided** the traffic capture shows zero requests. The
TestFlight upload does not affect the App Store label, but external TestFlight testers still need consent and a
privacy policy. A privacy policy URL is required even for "Data Not Collected".

**Pista.** Earlier review (2026-10-06) stands: no conflicting game/toy mark found; common word in Italian/Spanish
("track", "clues"); Ferrari's "488 PISTA" mark (classes 12, 28) means: don't pair "Pista" with racing or car imagery,
and don't use "Pista" alone as the app name. Fine as a character name. Cheap check: include "Pista" in the attorney
search for classes 9/28/41.

**Asset licences.** CC0 Poly Haven: clean. Meshy paid outputs: owned by the owner per Meshy's pricing FAQ; keep
dated evidence. Unity Asset Store Standard EULA allows embedding in a game with IAP; forbids redistribution of
source files, use in AI inputs or training, and per-seat use of editor extensions beyond two machines.

**iPhone-only.** Allowed. Consequences: compatibility mode on iPad (must work, may be tested), Mac and Vision Pro
availability to switch off, iPhone screenshots only.

**Landscape + portrait.** Both are allowed; supporting both doubles UI, camera and screenshot QA. Ship what the
owner picks; screenshots in that orientation.

## 3. What was not verified
- Official trademark registers (USPTO, EUIPO, UKIPO, WIPO) were not searched directly.
- Whether Unity 6 Personal honours `submitAnalytics: 0` on iOS: needs a traffic capture.
- The outcome of the Fifth Circuit's August 2026 hearing on SB 2420: no indexed ruling found.
- Meshy ToS ownership clause text: only the pricing FAQ statement was read.
- Whether App Store Connect lets a developer choose a higher rating than the questionnaire result.

## Sources
- [App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/) (Last Updated June 8, 2026)
- [Upcoming requirements](https://developer.apple.com/news/upcoming-requirements/)
- [Age ratings values and definitions](https://developer.apple.com/help/app-store-connect/reference/app-information/age-ratings-values-and-definitions/)
- [Third-party SDK requirements](https://developer.apple.com/support/third-party-SDK-requirements/)
- [Accessibility Nutrition Labels overview](https://developer.apple.com/help/app-store-connect/manage-app-accessibility/overview-of-accessibility-nutrition-labels)
- [Unity: Apple privacy manifest policy](https://docs.unity3d.com/Manual/apple-privacy-manifest-policy.html)
- [Unity Asset Store Terms (EULA)](https://unity.com/legal/as-terms)
- [Meshy Terms of Service](https://www.meshy.ai/cs/terms-of-use), [Meshy pricing FAQ](https://www.meshy.ai/fr/pricing)
- Texas SB 2420: [Pearl Cohen](https://www.pearlcohen.com/fifth-circuit-stays-injunctions-against-texas-app-store-accountability-act/), [Texas Policy Research](https://www.texaspolicyresearch.com/texas-app-store-law-remains-in-effect-as-legal-fight-continues/), [Apple: new requirements for apps in Texas](https://developer.apple.com/news/?id=btkirlj8)
- Age rating social media questions: [PocketGamer.biz](https://www.pocketgamer.biz/apple-updates-app-store-age-rating-questionnaire-with-social-media-questions/); Australia/Vietnam: [9to5Mac](https://9to5mac.com/2026/05/21/apple-to-update-app-store-age-ratings-in-australia-and-vietnam-next-month/)
- Name collisions: [Kingdom of Aurelia: Adventure](https://apps.apple.com/app/id6743487065), [Wheels of Aurelia delisting](https://www.pocketgamer.biz/wheels-of-aurelia-dev-says-apple-undermining-value-and-sustainability-of-games-as-it-looks-to-delist-title/), [Aurelia (Mirthal) devlog](https://mirthal.itch.io/aurelia/devlog/380134/v241-is-now-public)
- iOS 26 devices: [9to5Mac](https://9to5mac.com/2025/09/15/ios-26-supports-these-recent-iphones-but-drops-three-models/)
- AI disclosure (no Apple rule found): [Stuff](https://stuff.co.za/2026/02/12/are-video-game-developers-using-ai-players-want-to-know-but-the-rules-are-patchy/)
