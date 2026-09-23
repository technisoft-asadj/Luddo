using UnityEngine;

namespace Ludo.Online
{
    /// <summary>
    /// Settings for the online part that belong to YOU (the developer), not to the code.
    /// (An asset class must live in a file with the same name, or Unity cannot load it in a build.)
    /// </summary>
    [CreateAssetMenu(menuName = "Ludo/Online Config", fileName = "OnlineConfig")]
    public sealed class OnlineConfig : ScriptableObject
    {
        [Tooltip("Where players' reports are sent (an email address you read). Required for public matchmaking on Google Play.")]
        public string supportEmail = "";
        [Tooltip("The web address of your privacy policy (shown in Settings and in the Play Store listing).")]
        public string privacyPolicyUrl = "";
        [Tooltip("Text of the link players send to friends together with the room code, e.g. your Play Store URL.")]
        public string storeUrl = "";
        [Tooltip("ID of the all-time ranked leaderboard you create in the Unity Dashboard (sort: highest first, keep: latest score).")]
        public string ratingBoardId = "ludovibe_rating";
        [Tooltip("ID of the weekly tournament leaderboard (sort: highest first, keep: best score, reset: weekly on Monday 00:00 UTC).")]
        public string weeklyBoardId = "ludovibe_weekly";

        public static OnlineConfig Load() => Resources.Load<OnlineConfig>("OnlineConfig");
    }
}
