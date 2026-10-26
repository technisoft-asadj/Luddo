# Turning on "Continue with Facebook"

## Status (done in the project)
* Facebook SDK for Unity 19.0.0 imported (`Assets/FacebookSDK`); App ID `1759980011780675` and the Client Token are in
  `FacebookSettings` and in `Assets/Plugins/Android/LudoFacebook.androidlib` (manifest entries + login redirect strings).
* Scripting symbol `LUDO_FACEBOOK` is on for Android, so `FacebookLogin.Enabled` is true and the Facebook buttons show on the
  Welcome and Account screens. Analytics and advertising-ID collection by the SDK are switched OFF (login only).
* **Still yours to do** (the game cannot log in until these are done): the key hashes + Live mode below, and the App Secret in the
  Unity Dashboard (Authentication > Identity providers > Facebook). **Never put the App Secret in the project.**
* Not yet tested on a phone (no build was made); the first build also downloads the Facebook Android libraries through the
  External Dependency Manager.

## Copy-paste values (everything Facebook asks for)
| Field in Facebook | Value |
|---|---|
| App name | Ludo Fight |
| App contact email | trisoftic@gmail.com |
| App icon (1024 x 1024, under 1 MB) | `Documentation/Facebook/ludofight_logo_1024.jpg` |
| Privacy Policy URL | https://technisoft-asadj.github.io/Privacy_pages/ludofight/privacy.html |
| User data deletion (instructions URL) | the same privacy URL (it explains: Settings > Delete Online Account inside the game) |
| Category | Games |
| Platform | Android |
| Google Play package name | com.devorbit.ludofight |
| Class name | com.unity3d.player.UnityPlayerActivity |
| Key hash 1 (upload key) | 9fiG4Z5emD/yeZ7w3Lg7KZ1csG4= |
| Key hash 2 (Google Play signing key) | Chv0RHGEJ25B8Y2nSeKzP/2RHOk= |
| Permissions | public_profile only (no app review needed) |
| Single Sign On | Yes |

The game side is already written (`FacebookLogin.cs`, `OnlineService.FacebookAsync`, the buttons on the Welcome and Account
screens). It stays hidden until these steps are done, because they need your own Facebook account.

## 1. Create the Facebook app (you)
1. Go to https://developers.facebook.com > **My Apps > Create App**. Use case: **Authenticate and request data from users with Facebook Login**.
2. App name `Ludo Fight`. Contact email: the support email you use for the store.
3. **Settings > Basic**: add the Privacy Policy URL (the one in `OnlineConfig`) and a category (Games).
4. Add the **Android** platform:
   * Package name: `com.devorbit.ludofight`
   * Class name: `com.unity3d.player.UnityPlayerActivity`
   * Key hashes (add BOTH, one for the upload key and one for Google's Play signing key):
     * Upload key: `9fiG4Z5emD/yeZ7w3Lg7KZ1csG4=`
     * Google Play signing key: `Chv0RHGEJ25B8Y2nSeKzP/2RHOk=`
5. Copy the **App ID**, the **App Secret** and the **Client Token** (Settings > Advanced).
6. Only `public_profile` is used, which needs no app review. Switch the app from Development to **Live** when ready
   (until then only people you add as testers can log in).

## 2. Tell Unity Authentication about it (you)
Unity Dashboard > your project > **Authentication > Identity providers > Facebook**: enter the App ID and App Secret and save.

## 3. Send the App ID and Client Token to the developer
The developer then:
1. Imports the Facebook SDK for Unity (https://github.com/facebook/facebook-sdk-for-unity/releases).
2. Facebook > Edit Settings: App ID, Client Token, Android package name.
3. Adds the scripting symbol `LUDO_FACEBOOK` (Project Settings > Player > Android > Scripting Define Symbols).
4. Runs **Ludo > Build Menu Scene** so the Facebook buttons appear, then builds.
