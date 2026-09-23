# Ludo Fight - release guide (written 2026-09-21)

## What is ready
| File | What it is |
|---|---|
| `LudoFight-1.0.0.aab` | **Upload this to Google Play.** Signed with your upload key, real AdMob ads, no test/trace code, version 1.0.0 (code 1), package `com.devorbit.ludofight`, 64-bit, target API 36. |
| `LudoFight-1.0.0-testads.apk` | Same build but with Google TEST ads. Install it on a phone (`adb install` or copy the file) to try the game without ever tapping your real ads. |
| `Store/privacy-policy.html` | Privacy policy: host it on any web page (see step 2). |
| `Store/store-listing.md` | Title, short/full description, category, rating answers. |
| `Store/data-safety-answers.md` | Answers for the Data safety form, ads and permissions declarations. |
| `..\Keystore\ludovibe-upload.keystore` + `keystore-info.txt` | Your upload key and its password. **Back both up somewhere safe. Never share or commit them.** |

## Where the online data lives (nothing on a server of ours)
The game has no server or database of its own. Online data is kept by **Unity Gaming Services** (free tier, inside your Unity project `d8976e16-...`, visible in cloud.unity.com > your project > *Player Management*):
| Data | Where | Who can see it |
|---|---|---|
| Player ID, login (guest or username + password hash) | Unity Authentication | Only Unity/you (password is never seen by the game) |
| Profile name + picture number, rating, wins, streaks, Weekly Cup points | Unity **Cloud Save** (public player data `stats`) | Other players (friends' boards, room screen) |
| Friend list, friend requests, online status | Unity Friends | The players involved |
| Room, seats, room code | Unity Lobby/Relay (deleted when the room closes) | Players in the room |
| World leaderboards (rating, Weekly Cup) | Unity Leaderboards | Everyone |
| Voice | Unity Vivox, live only, not recorded | Players in the room |
| Names, avatar choice, settings on the phone | The phone (PlayerPrefs) | Only that phone |
Deleting the account inside the game removes the Authentication record and the linked Cloud Save data.

## One-time Unity Dashboard setup (3 minutes, only you can; needs your Unity login)
Open cloud.unity.com > your project, then:
1. **Authentication > Identity providers > Username-Password: turn ON.** Until then "Sign Up / Log In with a password" says "Accounts are not switched on yet" and everybody plays as a guest (guest play always works).
2. **Leaderboards > Create leaderboard** twice:
   * ID `ludovibe_rating`: sort *Descending*, update *Latest score*, no reset.
   * ID `ludovibe_weekly`: sort *Descending*, update *Best score*, **Reset: weekly, Monday 00:00 UTC** (this is the Weekly Cup). Optionally keep the last few weeks' archives.
   Until they exist the Leaderboards screen shows "The world leaderboard is not switched on yet"; your own results are still saved and shown, and the Friends tab already works.
3. Nothing to do for Cloud Save, Lobby/Relay, Friends or Vivox (already working).

## Do these before you upload (only you can)
1. **Set your support e-mail and privacy-policy address.** Open the project in Unity, select `Assets/_Game/Resources/OnlineConfig.asset` and fill `Support Email` (players' reports arrive there - Google requires a reporting way for a game with voice chat) and later `Privacy Policy Url` and `Store Url`. Then rebuild the AAB: menu **Ludo > Release > Build Play Store AAB**. One click does everything (prepares the settings, recompiles, builds, then restores your normal development settings); it is finished when `Builds/Release/result-aab.txt` says `Succeeded` (Unity looks frozen while it builds; that is normal). The file appears in `Builds/Release/`.
2. **Host the privacy policy.** Replace `[SUPPORT EMAIL]` in `Store/privacy-policy.html`, put the file on a web page you control (GitHub Pages, Google Sites, your website) and paste its address in Play Console.
3. **Create the app in Play Console** (developer account needed): name from `store-listing.md`, App content: privacy policy URL, Ads = yes, Target audience = 13+ (not for children), Data safety = `data-safety-answers.md`, Content rating (IARC) = as written in the listing file. Turn on **Play App Signing** when uploading the first AAB and upload `LudoFight-1.0.0.aab`.
4. **Graphics:** a 512x512 icon (`Assets/_Game/Art/Branding/app_icon_source.png`), a 1024x500 feature graphic and at least 2 phone screenshots. These are your art; not created here.
5. **AdMob:** in AdMob link the app to its Play Store listing after it is live (Apps > your app > App settings), and add the "Privacy & messaging" (GDPR) message for the EEA/UK. Real ad units are already in the AAB (`Assets/_Game/Resources/AdsConfig.asset`). Keep **test ads on** in every development build.
6. **Test first:** run an Internal testing track with the AAB on a real phone. Voice chat and online play were verified between two computers (Unity Editor + Windows build) but NOT on a phone; test rooms/voice on two phones before going public.

## Not included in 1.0 (needs credentials only you can create)
* **Sign in with Google / Facebook.** Online play uses an anonymous guest account (kept on the phone; "Delete Online Account" removes it). Unity's Authentication package already contains `SignInWithGoogleAsync` / `SignInWithFacebookAsync` / `SignInWithGooglePlayGamesAsync`; adding them needs a Google Cloud OAuth client + Play Games project and a Facebook developer app (and Facebook's app review). Ask for this as version 1.1 once you have those IDs.
* Rewarded ads (nothing to reward), in-app purchases, tablets/very short screens (the room screen assumes a 16:9 or taller phone).

## Costs
Only free tiers are used: Unity Gaming Services (Authentication, Lobby, Relay, Friends, Vivox) and Google AdMob. Check the Unity dashboard now and then; if the game becomes popular, Unity's free limits (for example Vivox voice minutes / concurrent users, Relay) can be exceeded and paid plans are then needed. No paid service is switched on.

## How the game is tested
* 110 automated tests (rules, AI, ads policy, settings, online sync: two engines fed the same announced dice/moves stay identical).
* Two-computer online tests with `Builds/WinTest/Ludo Fight.exe -joincode ABC123 -autoplay` (menu **Ludo > Build Windows Test Client**): full match identical on both sides, player drop-out, host drop-out, Quick Match, voice channel, friends and invites.
