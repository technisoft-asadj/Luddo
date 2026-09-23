using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// Shows "Ahmad invited you to play" with Join / Later, on whatever menu screen you are looking at, as soon as a
    /// friend sends an invite. Join enters their room.
    /// </summary>
    public sealed class InviteListener : MonoBehaviour
    {
        [SerializeField] ScreenRouter router;
        [SerializeField] int roomScreen = 9;
        [SerializeField] GameObject modal;
        [SerializeField] TMP_Text messageText;
        [SerializeField] Image avatar;

        RoomInvite pending;
        bool joining;

        void OnEnable()
        {
            modal.SetActive(false);
            SocialService.InviteReceived += OnInvite;
        }

        void OnDisable() => SocialService.InviteReceived -= OnInvite;

        void OnInvite(RoomInvite invite, string fromUserId)
        {
            if (SafetyService.IsBlocked(fromUserId)) return;         // blocked people cannot bother you
            if (RoomService.InRoom || joining) return;               // already busy
            pending = invite;
            messageText.text = (string.IsNullOrEmpty(invite.fromName) ? "A friend" : invite.fromName) + " invited you to play!";
            avatar.sprite = AvatarLibrary.Get(invite.fromAvatar);
            modal.SetActive(true);
        }

        public void Later() => modal.SetActive(false);

        public async void Join()
        {
            if (pending == null || joining) return;
            joining = true;
            messageText.text = "Joining...";
            bool ok = await RoomService.JoinByCodeAsync(pending.code);
            joining = false;
            if (this == null) return;
            if (ok)
            {
                modal.SetActive(false);
                router.Show(roomScreen);
            }
            else messageText.text = "That room is not available any more.";
        }
    }
}
