using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Core;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// Your online profile and statistics: picture, name (Edit Profile changes both), tier and rating, games, wins,
    /// win rate, streaks, ranked results and Weekly Cup points. Friends can see the same numbers.
    /// </summary>
    public sealed class ProfileScreen : MonoBehaviour
    {
        [SerializeField] Image avatar;
        [SerializeField] Image flag;                   // my country (hidden if none chosen)
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text tierText;
        [SerializeField] TMP_Text friendIdText;
        [SerializeField] RectTransform xpFill;         // the coloured part of the XP bar (its right edge = progress)
        [SerializeField] TMP_Text xpText;              // "120 / 300 XP"
        [SerializeField] TMP_Text[] statValues;        // Coins, Games, Wins, Losses, Win rate, Best streak, Ranked wins, Weekly points, Disconnects, Disconnect rate

        // the rank block (Rank Points): separate from Level / XP above it
        [SerializeField] TMP_Text rankName;            // PLATINUM
        [SerializeField] TMP_Text rankPointsText;      // Rank Points  1,240
        [SerializeField] TMP_Text rankNextText;        // Next rank: DIAMOND - needs 1,500
        [SerializeField] RectTransform rankFill;       // progress from this rank to the next
        [SerializeField] TMP_Text rankBarText;         // 1,240 / 1,500

        void OnEnable()
        {
            StatsService.Changed += Refresh;
            GameSettings.Changed += Refresh;
            Refresh();
            Load();
        }

        void OnDisable()
        {
            StatsService.Changed -= Refresh;
            GameSettings.Changed -= Refresh;
        }

        async void Load()
        {
            await StatsService.LoadMineAsync(force: true);
            if (this != null) Refresh();
        }

        void RefreshRank(PlayerStats s)
        {
            if (rankName == null) return;
            rankName.text = s.Tier.ToUpperInvariant();
            rankPointsText.text = "Rank Points  " + s.rating.ToString("N0");
            rankFill.anchorMax = new Vector2(Mathf.Clamp01(Rating.TierFraction(s.rating)), 1f);
            if (Rating.NextTier(s.rating, out string next, out int required))
            {
                rankNextText.text = "Next rank: " + next.ToUpperInvariant() + "   needs " + required.ToString("N0");
                rankBarText.text = s.rating.ToString("N0") + " / " + required.ToString("N0");
            }
            else
            {
                rankNextText.text = "You hold the highest rank!";
                rankBarText.text = s.rating.ToString("N0");
            }
        }

        void Refresh()
        {
            if (this == null) return;
            var s = StatsService.Mine;
            avatar.sprite = GameSettings.Picture(0);
            nameText.text = GameSettings.PlayerName(0);
            FlagLibrary.Apply(flag, GameSettings.Country);
            tierText.text = "Level " + s.Level;                 // Level / XP: progression
            RefreshRank(s);                                     // Rank / Rank Points: competition
            string how = OnlineService.ProviderLabel.Length > 0 ? OnlineService.ProviderLabel : "Guest";
            friendIdText.text = OnlineService.IsReady ? how + "  -  Friend ID: " + SocialService.MyFriendId.Replace('_', ' ') : how;

            int level = s.Level;
            if (xpFill != null) xpFill.anchorMax = new Vector2(Mathf.Clamp01(Progression.Fraction(s.xp)), 1f);
            if (xpText != null)
                xpText.text = level >= Progression.MaxLevel ? "MAX LEVEL" : Progression.XpIntoLevel(s.xp) + " / " + Progression.XpToNext(level) + " XP";

            string[] values =
            {
                s.coins.ToString(), s.games.ToString(), s.wins.ToString(), s.Losses.ToString(), s.WinRatePercent + "%",
                s.bestStreak.ToString(), s.rankedWins + "/" + s.rankedGames, s.weeklyPoints.ToString(),
                s.disconnects.ToString(), s.DisconnectRatePercent + "%"
            };
            for (int i = 0; i < statValues.Length && i < values.Length; i++) statValues[i].text = values[i];
        }
    }
}
