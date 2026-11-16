using System;
using System.Collections.Generic;
using UnityEngine;
#if LUDO_FIREBASE
using Firebase;
using Firebase.Analytics;
#endif

namespace Ludo.Services
{
    /// <summary>
    /// The only class that talks to Firebase Analytics. The game calls the small named methods below ("a match started", "a reward was
    /// earned"); the event names and parameters live here, in one place, so the same structure can be copied to every TriSoftic game.
    ///
    /// What is NOT logged by hand, because Firebase / AdMob already collect it: first_open, session_start, app_version, country (Firebase
    /// derives a country from the connection, never a precise location) and ad_impression (collected for AdMob units once the app is linked
    /// to Firebase). No names, e-mails, online IDs or other personal data are ever sent.
    ///
    /// Safety: if the Firebase SDK is not in the project (LUDO_FIREBASE not defined), or google-services.json is missing, or the player has
    /// not agreed to measurement, every call is a quiet no-op and the game is unaffected. Events fired before the SDK is ready are kept and
    /// sent once it is.
    /// </summary>
    public static class GameAnalytics
    {
        // ---------- event names (the specification) ----------
        public const string TutorialBegin = "tutorial_begin";
        public const string TutorialComplete = "tutorial_complete";
        public const string RegistrationComplete = "registration_complete";
        public const string Login = "login";
        public const string GameModeSelected = "game_mode_selected";
        public const string MatchCreated = "match_created";
        public const string MatchJoined = "match_joined";
        public const string MatchStarted = "match_started";
        public const string MatchCompleted = "match_completed";
        public const string MatchAbandoned = "match_abandoned";
        public const string GameInviteSent = "game_invite_sent";
        public const string RewardedAdStarted = "rewarded_ad_started";
        public const string RewardedAdCompleted = "rewarded_ad_completed";
        public const string RewardEarned = "reward_earned";

        // ---------- parameter names ----------
        public const string PGameMode = "game_mode";                  // Classic, Master, Arrow, Blitz, Team Up
        public const string PMatchType = "match_type";                // online, private, offline
        public const string PPlayerCount = "player_count";
        public const string PDuration = "match_duration_seconds";
        public const string PResult = "match_result";                 // win, loss, draw
        public const string PAppVersion = "app_version";
        public const string PPlacement = "placement";                 // where a rewarded ad was offered: result_card, chest, daily_reward
        public const string PRewardType = "reward_type";              // rank_points_bonus, chest_open, daily_double ...
        public const string PMethod = "method";                       // login / registration / invite method

        public const string TypeOnline = "online";
        public const string TypePrivate = "private";
        public const string TypeOffline = "offline";

        const int MaxPending = 100;

        struct Pending { public string name; public KeyValuePair<string, object>[] args; }

        static readonly List<Pending> pending = new List<Pending>();
        static volatile bool wanted;  // the player has agreed (or no consent is required in their region)
        static bool ready;            // the SDK is initialised
        static bool starting;

        // the match in progress (for the duration and for "abandoned")
        static bool matchOpen;
        static float matchStartedAt;
        static string matchMode = "", matchType = "";
        static int matchPlayerCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pending.Clear();
            wanted = ready = starting = matchOpen = false;
        }

        // ---------- consent and start-up ----------

        /// <summary>
        /// Called once the privacy consent is settled (see AdsService). Analytics starts only when the player may be measured, and is
        /// switched off again if they withdraw later.
        /// </summary>
        public static void SetConsent(bool allowed)
        {
            // This can be called from a background thread (the consent form reports back there), so it only records the answer;
            // the Pump below acts on it on the game's main thread, where every Firebase call must happen.
            wanted = allowed;
#if LUDO_FIREBASE
            consentChanged = true;
#endif
        }

#if LUDO_FIREBASE
        static System.Threading.Tasks.Task<DependencyStatus> dependencyTask;
        static volatile bool consentChanged;
        static bool collectionSwitchedOff;      // collection is on by default: we only ever call SetAnalyticsCollectionEnabled to turn it off, and back on after that

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.isEditor || Application.platform != RuntimePlatform.Android) return;
            var go = new GameObject("GameAnalyticsPump");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Pump>();
        }

        /// <summary>Runs on the main thread every frame: starts Firebase once the player may be measured, notices when its start-up check has finished, applies a changed privacy choice.</summary>
        sealed class Pump : MonoBehaviour
        {
            void Update()
            {
                if (dependencyTask != null)
                {
                    if (!dependencyTask.IsCompleted) return;
                    var task = dependencyTask;
                    dependencyTask = null;
                    Finish(task);
                    return;
                }
                if (!ready) { if (wanted && !starting) StartSdk(); return; }
                if (consentChanged) { consentChanged = false; Apply(); }
            }
        }

        static void StartSdk()
        {
            starting = true;
            try
            {
                Debug.Log("[Ludo] Analytics: checking Firebase");
                dependencyTask = FirebaseApp.CheckAndFixDependenciesAsync();
            }
            catch (Exception e) { starting = false; Debug.LogWarning("[Ludo] Analytics could not start: " + e.Message); }
        }

        static void Finish(System.Threading.Tasks.Task<DependencyStatus> task)
        {
            starting = false;
            if (task.IsFaulted || task.Result != DependencyStatus.Available)
            {
                Debug.LogWarning("[Ludo] Analytics unavailable: " + (task.IsFaulted ? task.Exception?.GetBaseException().Message : task.Result.ToString()));
                return;
            }
            Debug.Log("[Ludo] Analytics: Firebase ready");
            ready = true;
            consentChanged = true;      // Update applies the privacy choice (and sends what was waiting) next frame
        }

        static void Apply()
        {
            if (wanted)
            {
                if (collectionSwitchedOff)
                {
                    Debug.Log("[Ludo] Analytics: collection on");
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                    collectionSwitchedOff = false;
                }
                Flush();
            }
            else
            {
                pending.Clear();
                if (!collectionSwitchedOff)
                {
                    Debug.Log("[Ludo] Analytics: collection off");
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(false);
                    collectionSwitchedOff = true;
                }
            }
        }

        static void Flush()
        {
            var copy = pending.ToArray();
            pending.Clear();
            foreach (var p in copy) Send(p.name, p.args);
        }

        static void Send(string name, KeyValuePair<string, object>[] args)
        {
            try
            {
                var list = new List<Parameter>(args.Length);
                foreach (var a in args)
                {
                    if (a.Value is int i) list.Add(new Parameter(a.Key, (long)i));
                    else if (a.Value is long l) list.Add(new Parameter(a.Key, l));
                    else if (a.Value is float f) list.Add(new Parameter(a.Key, (double)f));
                    else if (a.Value is double d) list.Add(new Parameter(a.Key, d));
                    else list.Add(new Parameter(a.Key, Clip(a.Value?.ToString() ?? "")));
                }
                FirebaseAnalytics.LogEvent(name, list.ToArray());
                Debug.Log("[Ludo] Analytics: sent " + name);
            }
            catch (Exception e) { Debug.LogWarning("[Ludo] Analytics event failed: " + e.Message); }
        }
#endif

        static string Clip(string s) => s.Length <= 100 ? s : s.Substring(0, 100);      // Firebase accepts at most 100 characters

        // ---------- the one logging call ----------

        public static void Log(string name, params KeyValuePair<string, object>[] args)
        {
            if (Application.isEditor) Debug.Log("[Analytics] " + name + " " + Describe(args));
#if LUDO_FIREBASE
            if (Application.isEditor || Application.platform != RuntimePlatform.Android) return;
            if (ready && wanted && !consentChanged) { Send(name, args); return; }
            if ((!ready || consentChanged) && pending.Count < MaxPending) pending.Add(new Pending { name = name, args = args });
#endif
        }

        static string Describe(KeyValuePair<string, object>[] args)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var a in args) sb.Append(a.Key).Append('=').Append(a.Value).Append(' ');
            return sb.ToString();
        }

        static KeyValuePair<string, object> P(string key, object value) => new KeyValuePair<string, object>(key, value);

        // ---------- onboarding ----------

        public static void LogTutorialBegin() => Log(TutorialBegin);
        public static void LogTutorialComplete() => Log(TutorialComplete);
        public static void LogRegistrationComplete(string method) => Log(RegistrationComplete, P(PMethod, method));
        public static void LogLogin(string method) => Log(Login, P(PMethod, method));

        // ---------- gameplay ----------

        public static void LogModeSelected(string mode) => Log(GameModeSelected, P(PGameMode, mode));

        /// <summary>A room was opened (private room, or a public room nobody had yet) - or an offline game was set up.</summary>
        public static void LogMatchCreated(string mode, string type, int players) =>
            Log(MatchCreated, P(PGameMode, mode), P(PMatchType, type), P(PPlayerCount, players), P(PAppVersion, Application.version));

        /// <summary>This player entered somebody else's room.</summary>
        public static void LogMatchJoined(string mode, string type, int players) =>
            Log(MatchJoined, P(PGameMode, mode), P(PMatchType, type), P(PPlayerCount, players), P(PAppVersion, Application.version));

        /// <summary>The first dice is about to be thrown. Opens the match so its length can be measured.</summary>
        public static void LogMatchStarted(string mode, string type, int players)
        {
            matchOpen = true;
            matchStartedAt = Time.realtimeSinceStartup;
            matchMode = mode; matchType = type; matchPlayerCount = players;
            Log(MatchStarted, P(PGameMode, mode), P(PMatchType, type), P(PPlayerCount, players), P(PAppVersion, Application.version));
        }

        /// <summary>The match reached its end. result: "win", "loss" or "draw" from this player's side.</summary>
        public static void LogMatchCompleted(string result)
        {
            if (!matchOpen) return;
            matchOpen = false;
            Log(MatchCompleted, P(PGameMode, matchMode), P(PMatchType, matchType), P(PPlayerCount, matchPlayerCount),
                P(PDuration, Mathf.RoundToInt(Time.realtimeSinceStartup - matchStartedAt)), P(PResult, result), P(PAppVersion, Application.version));
        }

        /// <summary>The player left before the end (menu, back button, connection lost). Does nothing if no match is open.</summary>
        public static void LogMatchAbandoned()
        {
            if (!matchOpen) return;
            matchOpen = false;
            Log(MatchAbandoned, P(PGameMode, matchMode), P(PMatchType, matchType), P(PPlayerCount, matchPlayerCount),
                P(PDuration, Mathf.RoundToInt(Time.realtimeSinceStartup - matchStartedAt)), P(PAppVersion, Application.version));
        }

        public static void LogInviteSent(string method) => Log(GameInviteSent, P(PMethod, method));

        // ---------- rewarded ads (ad_impression itself is collected by AdMob/Firebase) ----------

        public static void LogRewardedAdStarted(string placement) => Log(RewardedAdStarted, P(PPlacement, placement));
        public static void LogRewardedAdCompleted(string placement) => Log(RewardedAdCompleted, P(PPlacement, placement));
        public static void LogRewardEarned(string placement, string rewardType) => Log(RewardEarned, P(PPlacement, placement), P(PRewardType, rewardType));
    }
}
