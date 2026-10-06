# App Store Submission Checklist

Owned by **appstore-compliance**. Re-check Apple's current App Review Guidelines before every submission; this list is a starting point, not a substitute.
Mark each item PASS/FAIL with evidence (screenshot, file path, build number).

## Build & technical
- [ ] Built with the Xcode / iOS SDK version Apple currently requires
- [ ] Runs on all supported iPhones; safe areas respected; no crashes in QA soak test
- [ ] `ITSAppUsesNonExemptEncryption = NO` in Info.plist (unless that changes)
- [ ] No private APIs; no leftover debug menus or cheats in release builds
- [ ] App works fully offline (ads/IAP gracefully unavailable)

## 2.1 Completeness / 2.3 Metadata
- [ ] No placeholder art, text, "beta", "test", or "coming soon"
- [ ] Every button works; every feature reachable without an account
- [ ] Screenshots and preview video show the real, current game
- [ ] Name, subtitle, keywords contain no third-party trademarks
- [ ] Review notes explain how to test ads, purchases, and every feature

## 3.1 Payments
- [ ] All digital goods via StoreKit (Unity IAP)
- [ ] **Restore Purchases** button visible (shop and/or settings)
- [ ] Prices shown come from StoreKit, not hard-coded
- [ ] Any randomized paid item discloses odds (or avoid loot boxes entirely)
- [ ] IAP products created, reviewed, and attached to the version in App Store Connect

## 4.3 Spam / originality
- [ ] Original gameplay, characters, and art (not a reskin of another app, including our own)

## 5.1 Privacy
- [ ] Privacy policy URL live and linked in app + App Store Connect
- [ ] `PrivacyInfo.xcprivacy` present for the app; every SDK ships its own manifest
- [ ] App Privacy labels match actual data collected by app + SDKs
- [ ] ATT prompt shown before any tracking; app works if user declines
- [ ] EU/UK consent (GDPR) flow for ads
- [ ] No account required; if accounts are ever added, in-app account deletion
- [ ] Kids category NOT selected; age rating questionnaire answered honestly

## Store listing
- [ ] App icon (1024×1024, no transparency), screenshots for required device sizes
- [ ] Description, promotional text, support URL, copyright
- [ ] Localized listing for target markets (at least EN-US, EN-GB; EU languages later)
