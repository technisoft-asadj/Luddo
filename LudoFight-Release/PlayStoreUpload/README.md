# Ludo Fight - Play Console upload package (updated 2026-09-25)

Full steps are in `..\RELEASE_GUIDE.md`.

- **App name:** Ludo Fight   **Package (application ID):** `com.devorbit.ludofight`   **Version:** 1.0.0 (code 1)
- **Developer account:** devorbit (Play Console)   **Publisher shown in the game:** Trisoftic
- **Contact e-mail (shown to players, store listing):** trisoftic@gmail.com
- **Business e-mail (developer account):** info@trisoftic.com
- **Privacy policy:** https://technisoft-asadj.github.io/Privacy_pages/ludofight/privacy.html
- **Data deletion page:** https://technisoft-asadj.github.io/Privacy_pages/ludofight/delete-data.html

## Files

- `..\LudoFight-1.0.0.aab` - upload this to Play Console (real ads, signed with the upload key).
- `..\LudoFight-1.0.0-testads.apk` - the same build with Google TEST ads, for installing on a phone.
- `Graphics\app_icon_512.png` - app icon 512x512.
- `Graphics\feature_graphic.png` - feature graphic 1024x500 (Ludo Fight logo).
- `Graphics\Screenshots-Phone\*.png` - 5 phone screenshots, 1080x2160 (2:1), taken on a real phone with the final design:
  main menu, 4-player game, My Dice, Free Chest, profile.
- `Docs\store-listing.md` - title, short and full description, category, content-rating answers.
- `Docs\data-safety-answers.md` - answers for the Data safety form.
- `Docs\privacy-policy.html` - the policy text (already live at the address above).

## Still needs YOU (accounts, legal identity and payments - I am not allowed to do these)

1. **Play Console (devorbit account):** create the app "Ludo Fight" with package `com.devorbit.ludofight`, fill App
   content from the docs above, upload the graphics, create a release and upload the AAB.
2. **After the first upload:** Play Console > Setup > App signing: copy the **App signing key SHA-1** and add a second
   Android OAuth client in Google Cloud (project "ludovibe-509313", package `com.devorbit.ludofight`, that SHA-1), so
   Google sign-in works for players who install from the Play Store.
3. **Google Play Games Services:** in Play Console > Play Games Services, link the new app to the existing Google Cloud
   project and publish it (the old configuration was made for the old package name).
4. **Facebook:** not part of this version (no SDK, no button). Nothing to set up.
5. **AdMob:** rename the app to "Ludo Fight" in AdMob and link it to the Play Store listing once it is published
   (the ad unit IDs stay the same).
6. **Content rating (IARC):** fill Play Console's questionnaire using the answers in `store-listing.md`.
