using UnityEngine;

namespace Ludo.Online
{
    /// <summary>
    /// The small red dots on the main menu's Rewards and Chest buttons. They ask the same two questions the pop-ups
    /// themselves answer, so a dot can never disagree with what the player finds when they tap it, and both stay hidden
    /// until a cloud profile has actually loaded rather than guessing.
    /// </summary>
    public sealed class MenuAlertDots : MonoBehaviour
    {
        [SerializeField] GameObject rewardsDot;
        [SerializeField] GameObject chestDot;

        void OnEnable()
        {
            StatsService.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => StatsService.Changed -= Refresh;

        /// <summary>The chest fills up on a timer, so the dot is checked again every couple of seconds while the menu is open.</summary>
        float next;

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 2f;
            Refresh();
        }

        void Refresh()
        {
            if (rewardsDot != null) rewardsDot.SetActive(DailyRewardPanel.Claimable);
            if (chestDot != null) chestDot.SetActive(ChestPanel.Claimable);
        }
    }
}
