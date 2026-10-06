# App Store Submission Checklist

Owned by **appstore-compliance**. Re-check Apple's current App Review Guidelines before every submission; this list is a starting point, not a substitute.
Mark each item PASS/FAIL with evidence (screenshot, file path, build number). Any FAIL blocks submission.

**Last checked against Apple sources:** 2026-10-06. App Review Guidelines "Last Updated: June 8, 2026".
Sources: [App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/),
[Upcoming requirements](https://developer.apple.com/news/upcoming-requirements/),
[Screenshot specifications](https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/).
Changes since the first version of this list and the reasons: `docs/compliance/2026-10-name-and-early-review.md` section 4.

## Build & technical
- [ ] Built with the Xcode / iOS SDK Apple currently requires. **Now: Xcode 26+ with the iOS 26 SDK (since 2026-04-28). From April 2027: iOS 27 SDK.** Check the "Upcoming requirements" page again at submission
- [ ] Deployment target iOS 13 or later (Apple, since 2026-09-09) and covers the lowest device (iPhone 11 / SE 2nd gen)
- [ ] Unity editor version officially supports the Xcode version used for the upload
- [ ] Runs on all supported iPhones; safe areas respected; no crashes in QA soak test
- [ ] iPhone Duo (foldable) checked on the simulator, outer and inner display: no clipped UI, letterbox or adaptive layout is a deliberate choice
- [ ] Works on IPv6-only networks (2.5.5)
- [ ] `ITSAppUsesNonExemptEncryption = NO` in Info.plist (HTTPS only; re-check if any custom encryption is added)
- [ ] No private APIs; no leftover debug menus or cheats in release builds
- [ ] App works fully offline (ads/IAP gracefully unavailable)
- [ ] The word "booze" (any case) appears nowhere user-visible: display name, Info.plist, splash/title, strings, bundle ID, IAP product IDs, Game Center IDs, privacy/support URLs (CI check)

## 2.1 Completeness / 2.3 Metadata
- [ ] No placeholder art, text, audio (including placeholder voice clips), "beta", "test", or "coming soon"
- [ ] Every button works; every feature reachable without an account
- [ ] Screenshots and preview video show the real, current game
- [ ] Icon, screenshots and preview are suitable for 4+ even if the app is rated higher (2.3.8)
- [ ] Name ≤ 30 chars, subtitle ≤ 30 chars, keywords ≤ 100 chars; name is unique on the App Store (2.3.7)
- [ ] Name, subtitle, keywords, icon and art contain no third-party trademarks, competitor names, or other developers' icons/brands (2.3.7, 4.1(c))
- [ ] No "for kids" / "for children" wording anywhere in metadata (2.3.8, 5.1.4)
- [ ] No other platforms (Android, Google Play) named or shown in metadata (2.3.10)
- [ ] "What's New" lists significant changes for every update (2.3.12)
- [ ] Review notes explain how to reach every feature, how to test ads and purchases (sandbox), and that no login is needed
- [ ] App name and character names cleared by a trademark attorney (US, EU, UK; classes 9, 28, 41) before the name is used in App Store Connect, store art or voice recordings

## Age rating
- [ ] New age rating questionnaire answered honestly (tiers 4+, 9+, 13+, 16+, 18+); expected 9+ (2.3.6)
- [ ] Social media questions answered (mandatory since Sept 2026): no social feed of user content, so "No"
- [ ] Age assurance: Declared Age Range API (and Significant Change API) handled where state law requires it (Texas since 2026-06-04; Utah, Louisiana, Alabama to follow). Legal opinion on file
- [ ] Ratings for other regions checked if Apple asks (e.g. Korea, Australia, Vietnam updates in 2026)

## 3.1 Payments
- [ ] All digital goods via StoreKit (Unity IAP)
- [ ] **Restore Purchases** button visible (shop and/or settings)
- [ ] Prices shown come from StoreKit, not hard-coded
- [ ] No randomized paid items; if any are ever added, odds disclosed before purchase (3.1.1)
- [ ] Every IAP delivers something real (no "Remove Ads" if no ads are removed)
- [ ] IAP products created, reviewed, and attached to the version in App Store Connect; findable by the reviewer (2.1(b))
- [ ] Rewarded ads are optional; nothing rewards ATT consent, ratings or reviews (3.2.2(x), 5.1.2(i))
- [ ] The companion character never asks for purchases, ads, ratings or tracking consent (5.6, children's-design rules)

## Ads (2.5.18)
- [ ] Ads only in the main app binary
- [ ] Ad network maximum content rating matches the app's age rating
- [ ] Users can see why an ad was targeted, without leaving the app
- [ ] **In-app way to report an inappropriate ad** (e.g. Settings > Report an ad)
- [ ] Interstitials (if enabled) are clearly marked as ads, have an easy, visible close/skip button, and never trick users into tapping
- [ ] No ads, and no targeted ads, for users under 13 or of unknown age beyond contextual, child-safe ads

## 4.x Design / spam / originality
- [ ] Original gameplay, characters, and art (not a reskin of another app, including our own) (4.1)
- [ ] Clearly distinct from other endless runners: unique mechanics shown first in screenshots and review notes (4.3(b), revised 2026-06-08)
- [ ] Every generated or third-party asset recorded in `docs/LICENSES.md` with a license that allows commercial use

## 5.1 Privacy
- [ ] Privacy policy URL live (neutral domain/path) and linked in app + App Store Connect; covers data, uses, third parties, retention, deletion and how to withdraw consent (5.1.1(i))
- [ ] `PrivacyInfo.xcprivacy` present for the app with required-reason APIs declared (e.g. UserDefaults/PlayerPrefs); every SDK ships its own manifest
- [ ] App Privacy labels match actual data collected by app + SDKs
- [ ] If tracking is used: ATT prompt shown before any tracking; any pre-prompt has a single "Continue" button leading to the system dialog; app works fully if user declines (5.1.1(iv), 5.1.2(i))
- [ ] No tracking and no ATT prompt for users under 13 or of unknown age; ad and analytics SDKs set to child-directed mode for them (COPPA amended rule, compliance date 2026-04-22)
- [ ] EU/UK consent (GDPR) flow for ads and analytics, before those SDKs initialize
- [ ] No personal data shared with third-party AI services (5.1.2(i)); if ever added, explicit consent first
- [ ] No account required; if accounts are ever added, in-app account deletion (5.1.1(v))
- [ ] Kids category NOT selected

## Store listing
- [ ] App icon (1024×1024, no transparency), screenshots: iPhone 6.9" set (1320×2868, 1290×2796 or 1260×2736) or the 6.5" set; iPhone Duo sizes if the layout is adapted
- [ ] Description, promotional text, support URL, copyright
- [ ] Localized listing for target markets (EN-US, EN-GB, EN-CA, EN-AU; EU languages later)
- [ ] Optional new creative assets (product page header, search result assets, 2026-10) reviewed with the art-director
- [ ] Accessibility Nutrition Labels answered honestly if filled in (only claim features the game supports)

## Account & legal (owner)
- [ ] EU DSA trader status declared (trader contact details are shown publicly in the EU)
- [ ] Paid Apps agreement, tax and banking complete before IAP testing
