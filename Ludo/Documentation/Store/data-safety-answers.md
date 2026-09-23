# Play Console answers (derived from the SDKs actually in the game, 2026-09-21)

Re-check when SDKs change. SDKs: Google Mobile Ads 11.5.0 (AdMob + UMP), Unity Gaming Services Authentication 3.8, Multiplayer (Lobby/Relay) 2.3, Friends 1.2, Vivox 16.12, Netcode for GameObjects 2.13.

## Data safety form
Does the app collect or share user data? **Yes.** Is all data encrypted in transit? **Yes.** Can users request deletion? **Yes** (in-app Settings > Delete online account, plus e-mail).

| Data type | Collected | Shared | Purpose | Optional? |
|---|---|---|---|---|
| Device or other IDs (Advertising ID, Unity anonymous player ID) | Yes | Yes (Google AdMob for the ad ID) | Advertising, app functionality | Player ID: online only |
| Approximate location (from IP, by AdMob) | Yes | Yes (AdMob) | Advertising | No |
| User IDs / Name (display name) | Yes (online only) | Yes (other players in the room, Unity) | App functionality | Yes |
| Photos: none (avatar is a built-in picture number) | No | - | - | - |
| Audio: voice or sound recordings | Yes (streamed live, not stored) | Yes (other players in the room, Unity Vivox) | App functionality | Yes (voice is opt-in) |
| App interactions (moves, friends) | Yes (online only) | Unity | App functionality | Yes |
| Crash logs / diagnostics | Yes (AdMob/Unity SDK diagnostics) | Google/Unity | Analytics, fraud prevention | No |

## Other declarations
- **Ads:** Yes, the app contains ads.
- **Advertising ID:** Yes, used for advertising (the manifest has AD_ID; keep it).
- **Target audience:** 13+ ; not primarily for children.
- **User-generated content:** yes (display names, voice). Policy answer: reporting and blocking exist in the room screen (`RoomScreen` > flag button); reports are e-mailed to the support address (set `OnlineConfig.supportEmail`).
- **Account deletion (required for apps with accounts):** in-app button + web page/e-mail (privacy policy section 8).
- **Permissions declared:** INTERNET, ACCESS_NETWORK_STATE, VIBRATE, RECORD_AUDIO (asked only when the player taps the microphone), BLUETOOTH/BLUETOOTH_CONNECT/MODIFY_AUDIO_SETTINGS (from Vivox, headset routing), AD_ID and ACCESS_ADSERVICES_* (from AdMob), WAKE_LOCK, FOREGROUND_SERVICE (from SDKs).
- **Foreground service permission:** the SDK lists it but the game starts none; if Play asks, answer "not used".
- **Gambling:** none (no real money, no rewards).

## Added with accounts, statistics and leaderboards (2026-09-21)
| Data type | Collected | Shared | Purpose | Optional? |
|---|---|---|---|---|
| User IDs / account name (username, only if the player signs up) | Yes | No (Unity Authentication processes it for us) | Account management | Yes |
| Password (never seen by the game; handled by Unity Authentication) | Yes (by Unity) | No | Account management | Yes |
| Game performance / progress (rating, wins, streaks, Weekly Cup points) | Yes (online only) | Yes (visible to other players, Unity) | App functionality, leaderboards | Yes |
Account deletion: Settings > Delete Online Account removes the online account and its Cloud Save data.
