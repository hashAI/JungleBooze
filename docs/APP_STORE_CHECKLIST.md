# App Store Submission Checklist: AURELIA

Owned by **appstore-compliance**. Re-read Apple's current App Review Guidelines and the "Upcoming requirements" page
before every submission. This list is a starting point, not a substitute.
Mark each item PASS/FAIL with evidence (file path, build number, screenshot, network capture). Any FAIL blocks submission.

**Last checked against Apple sources:** 2026-10-09. App Review Guidelines "Last Updated: June 8, 2026".
Sources: [App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/),
[Upcoming requirements](https://developer.apple.com/news/upcoming-requirements/),
[Age ratings values and definitions](https://developer.apple.com/help/app-store-connect/reference/app-information/age-ratings-values-and-definitions/),
[Third-party SDK requirements](https://developer.apple.com/support/third-party-SDK-requirements/),
[Screenshot specifications](https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/).
Design-stage risk review and reasons for the changes below: `docs/compliance/2026-10-aurelia-design-review.md`.
The lane-game review (`docs/compliance/2026-10-name-and-early-review.md`) is kept for its Pista name evidence only.

Product facts this list assumes (change the list if they change): iPhone-only, landscape and/or portrait (owner picks
at Phase 1), free, no account, no ads and no purchases in MVP, no tracking, analytics on device only, English only,
hero Pista is a realistic 16-year-old, target rating 9+.

## Build & technical
- [ ] Built with the Xcode / iOS SDK Apple requires. **Now: Xcode 26+ with the iOS 26 SDK (since 2026-04-28).** Apple has not yet announced the 2027 step (it is usually announced in the autumn for the following April); re-check at submission
- [ ] Deployment target iOS 13 or later (Apple, since 2026-09-09). Project is at iOS 15.0 (`ProjectSettings.asset`). If the owner raises the minimum device to iPhone 12/13, remember no `UIRequiredDeviceCapabilities` key can exclude iPhone 11 / SE 2: the game must still run without crashes and acceptably there
- [ ] Unity editor version (now 6000.3.25f1) officially supports the Xcode version used for the upload
- [ ] UnityFramework privacy manifest present in the archive and the framework signed (UnityFramework is on Apple's list of SDKs that need a manifest and, as a binary dependency, a signature)
- [ ] Bundle ID final before the first upload (it can never change). Current `com.pistaduko.junglerunner` references dropped Duko and the old game: replace
- [ ] Product name / display name final (current `Jungle Runner` is a placeholder); company name not `DefaultCompany`
- [ ] Device family is iPhone only (`targetDevice: 0`). In App Store Connect, untick "Make this app available" on Mac (Apple silicon) and Apple Vision Pro unless tested there
- [ ] Runs without crashing in iPad compatibility mode (App Review can test iPhone-only apps on iPad, 2.4.1)
- [ ] Orientation settings match the owner's decision (now: portrait autorotation is off while the GDD says "follows device rotation")
- [ ] Safe areas respected in every supported orientation (Dynamic Island, home indicator); no crashes in QA soak test
- [ ] iPhone Duo (foldable), if Apple ships it in the Simulator by submission: outer and inner display checked, no clipped UI
- [ ] Works on IPv6-only networks if any networking is added (2.5.5)
- [ ] `ITSAppUsesNonExemptEncryption = NO` in Info.plist via a build post-processor (not present yet)
- [ ] No private APIs; no debug overlay (F1), keyboard shortcuts, cheats or `LookTest` scenes in release builds
- [ ] App works fully offline
- [ ] The word "booze" (any case) appears nowhere user-visible: display name, Info.plist, splash/title, strings, bundle ID, IAP product IDs, Game Center IDs, privacy/support URLs (CI check)

## 2.1 Completeness / 2.3 Metadata
- [ ] Never submit a Phase 1 "feel" build or vertical slice to the App Store (4.2 minimum functionality, 2.1 completeness). TestFlight only until MVP
- [ ] No placeholder art, text, audio, "beta", "test", "coming soon", or empty post-MVP buttons (Map, Gear, Camp)
- [ ] Every button works; every feature reachable without an account
- [ ] Screenshots and preview video are captures of the real, current build on device (2.3.3, 2.3.4). No look-test renders, no offline Blender/Unity beauty renders, no unreachable camera angles
- [ ] Icon, screenshots and preview suitable for 4+ even though the app is 9+ (2.3.8); Pista art rules in `design/aurelia/ART_DIRECTION.md` §7.4 apply to every store asset
- [ ] Name ≤ 30 chars, subtitle ≤ 30 chars, keywords ≤ 100 chars; name unique on the App Store (2.3.7)
- [ ] Name, subtitle, keywords, icon and art contain no third-party trademarks or competitor names (no "Temple Run", "Subway Surfers", "Avatar", "Pandora") (2.3.7, 4.1)
- [ ] No "for kids" / "for children" wording anywhere (2.3.8)
- [ ] No other platforms (Android, Google Play, Steam) in the app or metadata (2.3.10)
- [ ] "What's New" lists significant changes for every update (2.3.12)
- [ ] Review notes: no login; how to reach every feature (journal, abilities, secret routes, revive); a debug-free way to show later content (e.g. a short video link, or a note that the secret route is at ~600 m); IAP sandbox steps when IAP exists
- [ ] App name, and any name used as a brand, cleared by a trademark attorney (US, EU, UK; classes 9, 28, 41) before store art, voice or marketing use it

## Age rating (target 9+)
- [ ] Questionnaire answered honestly (tiers 4+, 9+, 13+, 16+, 18+) (2.3.6). Expected: Violence none (falls and stumbles, no conflict), Horror/Fear "Infrequent" only if caves/giant creatures feel threatening, everything else none. Record the answers and reasons in `docs/compliance/`
- [ ] Social media questions (mandatory since September 2026): no feed of user content → "No". Friends leaderboards via Game Center are not user-generated content
- [ ] **Contests:** leaderboards, weekly events and daily expeditions count as Contests. Infrequent = 4+, **frequent = 13+**. Re-answer when they ship
- [ ] Loot boxes: none (any loot box is at least 9+, 16+ in Australia)
- [ ] Advertising capability answered "Yes" as soon as any ad SDK ships
- [ ] Age assurance: Declared Age Range API decision on file. Texas SB 2420 is enforceable (Fifth Circuit stay, 2026-06-04; Supreme Court declined to intervene 2026-07-06); Utah (2026-05-07) and Louisiana (2026-07-01) in force. Required in practice once purchases, ads or significant updates need age signals; legal opinion on file before IAP ships
- [ ] Regional ratings checked if App Store Connect asks (Australia and Vietnam changed 2026-06-18; Korea, Brazil)

## 3.1 Payments (post-MVP; N/A while the MVP has no purchases)
- [ ] All digital goods via StoreKit (Unity IAP)
- [ ] **Restore Purchases** visible (shop and settings)
- [ ] Prices come from StoreKit, never hard-coded
- [ ] Purchased crystals/currency never expire (3.1.1)
- [ ] No randomized paid items, ever (owner rule). If that changes: odds shown before purchase (3.1.1)
- [ ] Revive offer: skip button always visible, timer not deceptive, never the only way to continue (GDD §11)
- [ ] Every IAP delivers something real; IAP products attached to the version and findable by the reviewer (2.1(b))
- [ ] Rewarded ads optional; nothing rewards ratings, reviews or tracking consent (3.2.2(x), 5.1.2(i))

## Ads (2.5.18; post-MVP, only after retention is proven)
- [ ] Ads only in the main app binary; ad SDK listed in `docs/LICENSES.md` with its privacy manifest and signature
- [ ] Ad network maximum content rating matches 9+
- [ ] Users can see why an ad was targeted, without leaving the app
- [ ] In-app way to report an inappropriate ad
- [ ] No interstitials planned; if ever added: clearly an ad, easy visible close
- [ ] Contextual only (owner: no tracking for anyone); child-directed settings for under-13 / unknown age

## 4.x Design / spam / originality
- [ ] Original gameplay, characters, creatures, names and art (4.1). Not a reskin of another app, including our own archived lane game
- [ ] Clearly distinct from other endless runners (4.3(b)): free steering, route choice, discovery journal and ability-gated secrets shown first in screenshots, preview and review notes
- [ ] Every generated or third-party asset recorded in `docs/LICENSES.md` with a license that allows commercial use (Meshy paid-plan terms and library-animation terms re-checked before launch)
- [ ] No Unity Asset Store asset was ever used as an input to Meshy, OpenAI or any AI tool (Asset Store EULA §2.2.1.1(g))
- [ ] If accounts are ever added with third-party login, Sign in with Apple offered (4.8) and in-app account deletion (5.1.1(v))

## 5.1 Privacy
- [ ] Privacy policy URL live (neutral domain/path), linked in the app and in App Store Connect, required even for "Data Not Collected" (5.1.1(i))
- [ ] App `PrivacyInfo.xcprivacy`: Unity's generated manifest covers engine required-reason APIs (UserDefaults CA92.1, file timestamp, boot time, disk space); our own code and plug-ins add theirs. Verified in the final `.xcarchive`
- [ ] **Release build makes zero network requests** (proxy capture on device). Unity "hardware statistics" (`submitAnalytics: 1`) is on now: turn it off if the license allows; if it cannot be turned off, declare what Unity collects
- [ ] App Privacy label = "Data Not Collected" only if the capture proves it; otherwise label exactly what leaves the device
- [ ] TestFlight analytics upload: opt-in consent screen first (5.1.1(ii), even for anonymous data), never compiled into App Store builds (compile flag, CI check)
- [ ] No tracking, no ATT prompt, no IDFA, no fingerprinting (owner, 2026-10-06)
- [ ] EU/UK consent flow before any analytics or ad SDK initializes (only if they ever ship)
- [ ] No personal data sent to third-party AI services (5.1.2(i))
- [ ] Kids category NOT selected

## Store listing
- [ ] App icon 1024×1024, no transparency; iPhone screenshots only (6.9" set 1320×2868 / 1290×2796 / 1260×2736, or 6.5"). One orientation for the whole set, matching the shipped orientation
- [ ] Description, promotional text, support URL, copyright (owner's legal name or company)
- [ ] Localized listing EN-US, EN-GB, EN-CA, EN-AU (English only at launch)
- [ ] Accessibility Nutrition Labels (voluntary now, mandatory later): claim only features that meet Apple's criteria (Reduced Motion is a candidate; do not claim VoiceOver or Larger Text unless tested against Apple's criteria)
- [ ] No AI-generated-content disclosure is required by Apple's guidelines (June 8, 2026); none of our AI assets depict real people or events. Re-check at submission

## Account & legal (owner)
- [ ] EU DSA trader status declared
- [ ] Paid Apps agreement, tax and banking complete before IAP testing
- [ ] App name reserved in App Store Connect only after the trademark check (check Apple's current name-reservation rules; an unused name can be lost)
