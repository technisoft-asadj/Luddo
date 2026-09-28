# Ludo Fight 1.0.1 - release guide (updated 2026-09-28)

Everything needed to publish is in this folder. Package `com.devorbit.ludofight`, version 1.0.1 (code 2), Android only,
target API 36, 64-bit, min Android 7.1 (API 25).

> **1.0.1 status (2026-09-28): READY to upload.** Bug-fix release over 1.0.0 (which is already on Google Play Internal
> testing as code 1): fixed the daily reward calendar showing "day 1" again instead of "day 2" for a returning player
> whose save had lost its streak counter; fixed the game-mode banner ("MASTER MODE", "TEAM UP MODE", ...) overlapping
> the Voice/Chat buttons; added a boxed, clipped preview bubble for an incoming chat message so it can no longer render
> outside its box. 263/263 EditMode tests pass. Real AdMob app ID `ca-app-pub-2502837360597894~1360211875`, signed with
> the upload key, not debuggable, no Facebook entries.
>
> Three builds, same version/signing, different ads and package format:
> - `LudoFight-1.0.1.aab` - real ads, App Bundle - **Google Play**.
> - `LudoFight-1.0.1-amazon.apk` - real ads, APK - **Amazon Appstore** (Amazon takes an APK, not an AAB).
> - `LudoFight-1.0.1-testads.apk` - Google TEST ads, APK - install on a phone to try the release build; never tap real ads yourself.

## 1. Files in this folder

| File / folder | What it is |
|---|---|
| `LudoFight-1.0.1.aab` | The bundle to upload to Google Play (real ads, signed with the upload key). |
| `LudoFight-1.0.1-amazon.apk` | The APK to upload to Amazon Appstore (real ads, same signing key). |
| `LudoFight-1.0.1-testads.apk` | Same game with Google TEST ads: install on a phone to try it. Never tap real ads yourself. |
| `LudoFight-1.0.0.aab` / `LudoFight-1.0.0-testads.apk` | The previous release (code 1), already on Google Play Internal testing. Kept for reference. |
| `PlayStoreUpload/Graphics/` | App icon 512x512, feature graphic 1024x500, phone screenshots. |
| `PlayStoreUpload/Docs/` | Store text, Data safety answers, privacy policy (copies of `Store/`). |
| `Store/` | The same three documents (master copies). |
| `..\Keystore\ludovibe-upload.keystore` + `keystore-info.txt` | Upload key and password. **Back both up. Never share or commit them.** |

## 2. What is in version 1.0.0 (and what is not)

In: Online (Quick Match ranked, Create/Join Room by code, friends, invites, Add Friend from a match or lobby), Play with
Friends, Play with AI (Easy/Medium/Hard), Pass & Play (2-4 on one phone), Offline, game modes (Classic, Master, Arrow,
Blitz, Team Up), Auto play in online games, voice chat (opt-in), chat with emojis, player profile with rank/level/XP/coins,
Ranked and Weekly Cup leaderboards, free chest every 4 hours (plus a "watch an ad" top-up when out of coins), daily
rewards, dice collection (8 designs, cosmetic only), Google login or guest, account deletion, ads (banner, interstitial,
rewarded).

Not in this version (deliberately removed or hidden, no half-working buttons): Facebook login, gifts, tournament brackets,
seasons/events, clubs, gems, Lucky Spin, country leaderboard, tap-to-choose among pending dice.

## 3. Before you build the final AAB (checklist)

1. Support e-mail and privacy URL are already set in `Assets/_Game/Resources/OnlineConfig.asset`
   (`trisoftic@gmail.com`, https://technisoft-asadj.github.io/Privacy_pages/ludofight/privacy.html).
2. Real AdMob IDs are in `AdsConfig.asset` (the release menu switches test ads off for the AAB and back on afterwards).
3. Build: Unity menu **Ludo > Release > Build Play Store AAB**. It finishes when `Builds/Release/result-aab.txt`
   says `Succeeded`. Then copy `Builds/Release/LudoFight-1.0.0.aab` into this folder.
4. Verify the manifest of the result before uploading (see section 6): it must NOT contain `debuggable`,
   nothing named `facebook`, and the only exported components should be the launcher activity and SDK ones.
5. Run the EditMode tests (263 must pass) and play one online match on a phone with the test-ads APK.

## 4. Play Console - create the app (your account: devorbit)

1. **All apps > Create app**: name *Ludo Fight: Play with Friends*, default language English, App (not game type: choose
   **Game**), Free. Accept the declarations.
2. **Set up your app** (Dashboard) - fill each item:
   * **Privacy policy:** https://technisoft-asadj.github.io/Privacy_pages/ludofight/privacy.html
   * **App access:** all features available without special access (guest login exists).
   * **Ads:** Yes, the app contains ads.
   * **Content rating:** start the IARC questionnaire; use the answers in `Store/store-listing.md`
     (no violence, no real-money gambling, users can interact via voice chat and names, contains ads).
   * **Target audience:** 13+ (do not select younger groups; do not join the Families programme).
   * **News app / COVID / Government:** No.
   * **Data safety:** copy from `Store/data-safety-answers.md`.
   * **Advertising ID:** Yes (ads). **Financial features / Health:** No.
   * **Data deletion:** in-app *Settings > Delete online account* and the web page
     https://technisoft-asadj.github.io/Privacy_pages/ludofight/delete-data.html
3. **Store listing:** title, short and full description from `Store/store-listing.md`; app icon
   `Graphics/app_icon_512.png`; feature graphic `Graphics/feature_graphic.png`; phone screenshots from
   `Graphics/Screenshots-Phone/` (2 to 8 needed). Category **Board**; contact e-mail trisoftic@gmail.com.
4. **Testing > Internal testing > Create release:** turn on **Play App Signing**, upload `LudoFight-1.0.1.aab`, add
   yourself as a tester, roll out, install from the Play link and test on two phones (online, voice, Google login).
5. **Production:** only after the internal test passes. New personal developer accounts must first run a closed test with
   at least 12 testers for 14 days before production access is granted - check your account's Dashboard for this.

## 4b. Amazon Appstore - create the app (a separate store, separate account at developer.amazon.com)

1. **Apps & Games > Add a New App**: same name, description, icon and screenshots as the Play listing (reuse
   `Store/store-listing.md` and `PlayStoreUpload/Graphics/`); category **Games > Board**.
2. **Binary files:** upload `LudoFight-1.0.1-amazon.apk` (not the `.aab` - Amazon takes an APK). Same package name
   `com.devorbit.ludofight` and version as the Play build is fine; the two stores are independent.
3. **Content rating:** run Amazon's own IARC-based questionnaire with the same honest answers as `Store/store-listing.md`
   (no violence, no real-money gambling, ads, users can interact via voice/text chat and names).
4. **Important - devices without Google Play Services:** some Fire tablets do not have Google Play Services installed.
   On those devices Google Sign-In and AdMob may not initialize; the game already falls back to **guest login** when
   Google Sign-In is unavailable, and ads simply fail to load rather than crashing, so the game stays playable - but
   this has not been verified on a real Fire tablet. Test on one before publishing if you have one, and mention Google
   login as "optional, guest play always works" rather than a required feature in the Amazon listing.
5. **Submit for review.** Amazon's review is typically a few days; no separate signing key or App Signing step is
   needed (Amazon distributes the APK you uploaded, signed with our own upload key).

## 5. After the first upload (Google sign-in for Play Store installs)

* Play re-signs the app. Copy the **App signing key SHA-1** (Play Console > Test and release > App integrity).
* Google Cloud project **ludovibe-509313** > Google Auth Platform > Clients: create a **third** Android OAuth client
  (package `com.devorbit.ludofight`, that SHA-1). Add it as a credential in Play Console > Play Games Services.
* Before the public release: Google Auth Platform > Audience > **Publish app**; Play Console > Play Games Services >
  **Review and publish**. While unpublished only the listed test users can log in with Google
  (workdone197@gmail.com, mohsinsultanharrie@gmail.com, mohsinsultanharri@gmail.com). Guest login always works.
* Unity Dashboard (cloud.unity.com, project Ludo, production) > Authentication > Identity providers > Google Play
  Games: Client ID `564891670587-2pkkg3uo5p2veritvkkvkb694pnspgli.apps.googleusercontent.com`, Client Secret from
  `Keystore\google-oauth-web-client.txt`, enable, Add provider. Until then "Continue with Google" reports that Google
  login is not switched on.
* AdMob: link the app to its Play listing once live (Apps > App settings) and add the GDPR message
  (Privacy & messaging) for EEA/UK.

## 6. How to check the built package (manifest)

```
aapt2 dump xmltree --file AndroidManifest.xml LudoFight-1.0.0-testads.apk
```
(aapt2 is in `Unity\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0`.) Look for `debuggable`,
`facebook` and `android:exported`. Google Play rejects a debuggable build.

## 7. Where online data lives (no server of ours; free tiers only)

Unity Gaming Services: Authentication (guest / Google / username), Cloud Save (profile, rating, coins, dice owned),
Friends, Lobby + Relay (rooms), Leaderboards (`ludovibe_rating`, `ludovibe_weekly`, weekly reset Monday 00:00 UTC), Vivox
(voice, live only). Google AdMob for ads. Names/settings stay on the phone. Deleting the account in the game removes the
Authentication record and Cloud Save data. No paid service is switched on; watch the Unity free limits if the game grows.

## 8. Known limits (honest list)

* Coins, XP and rating are written by each phone, so a modified phone could cheat its own numbers (fixing it needs a
  server function, which needs a paid or dashboard-side setup).
* Reconnect after a dropped connection is built and unit-tested but not verified on real mobile networks.
* Voice chat and Google login are untested on two real phones from the Play Store build.
* Room and menu screens assume a phone of 16:9 or taller.
* Store screenshots (`PlayStoreUpload/Graphics/Screenshots-Phone`, 1080x2160, taken 2026-09-25 on a real phone with the
  final design): main menu, 4-player game, My Dice, Free Chest, profile. The test-ad banner is cropped out. The profile
  picture shows the tester's own guest name and Friend ID; retake it with a neutral account if you prefer.
