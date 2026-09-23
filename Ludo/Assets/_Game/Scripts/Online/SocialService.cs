using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using Unity.Services.Friends.Options;
using UnityEngine;

namespace Ludo.Online
{
    /// <summary>What a friend sends to invite you: "join my room".</summary>
    [Serializable]
    public class RoomInvite
    {
        public string code = "";
        public string fromName = "";
        public int fromAvatar;
    }

    /// <summary>A friend (or a request) as the friends screen shows it.</summary>
    public readonly struct FriendInfo
    {
        public readonly string Id;
        public readonly string Name;         // without the "#1234"
        public readonly bool Online;
        public FriendInfo(string id, string name, bool online) { Id = id; Name = name; Online = online; }
    }

    /// <summary>
    /// Friends: add by their ID (the name with #1234 they can read in their own Friends screen), accept / decline
    /// requests, see who is online, send a room invite, block. Uses Unity's Friends service. Nothing here throws.
    /// </summary>
    public static class SocialService
    {
        static bool started;
        static Task<bool> pending;

        public static string LastError { get; private set; } = "";

        /// <summary>The friends list, a request or an invite changed.</summary>
        public static event Action Changed;
        /// <summary>A friend invited us into their room.</summary>
        public static event Action<RoomInvite, string> InviteReceived;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            started = false;
            pending = null;
            LastError = "";
            Changed = null;
            InviteReceived = null;
        }

        /// <summary>The signed-in player left this phone: stop listening, so the next account starts the friends service afresh.</summary>
        public static void Stop()
        {
            if (started)
            {
                try
                {
                    FriendsService.Instance.RelationshipAdded -= OnRelationshipEvent;
                    FriendsService.Instance.RelationshipDeleted -= OnRelationshipEvent;
                    FriendsService.Instance.PresenceUpdated -= OnPresence;
                    FriendsService.Instance.MessageReceived -= OnMessage;
                }
                catch (Exception e) { Debug.LogWarning("[Ludo] Friends stop: " + e.Message); }
            }
            started = false;
            pending = null;
            Changed?.Invoke();
        }

        /// <summary>My own ID for friends to type: name + #1234.</summary>
        public static string MyFriendId => OnlineService.IsReady ? AuthenticationService.Instance.PlayerName ?? "" : "";

        public static Task<bool> StartAsync()
        {
            if (started) return Task.FromResult(true);
            if (pending != null && !pending.IsCompleted) return pending;
            pending = StartCore();
            return pending;
        }

        static async Task<bool> StartCore()
        {
            try
            {
                if (!await OnlineService.ConnectAsync()) { LastError = OnlineService.LastError; return false; }
                await OnlineService.PushProfileNameAsync();

                await FriendsService.Instance.InitializeAsync(new InitializeOptions().WithEvents(true).WithMemberPresence(true).WithMemberProfile(true));
                FriendsService.Instance.RelationshipAdded += OnRelationshipEvent;
                FriendsService.Instance.RelationshipDeleted += OnRelationshipEvent;
                FriendsService.Instance.PresenceUpdated += OnPresence;
                FriendsService.Instance.MessageReceived += OnMessage;
                await FriendsService.Instance.SetPresenceAvailabilityAsync(Availability.Online);
                started = true;
                Changed?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                LastError = OnlineService.Describe(e);
                Debug.LogWarning("[Ludo] Friends: " + LastError);
                return false;
            }
        }

        // ---------- lists ----------

        public static List<FriendInfo> Friends() => Convert(started ? FriendsService.Instance.Friends : null);
        public static List<FriendInfo> IncomingRequests() => Convert(started ? FriendsService.Instance.IncomingFriendRequests : null);
        public static List<FriendInfo> OutgoingRequests() => Convert(started ? FriendsService.Instance.OutgoingFriendRequests : null);

        static List<FriendInfo> Convert(IReadOnlyList<Relationship> list)
        {
            var result = new List<FriendInfo>();
            if (list == null) return result;
            foreach (var r in list)
            {
                bool online = r.Member?.Presence != null &&
                              (r.Member.Presence.Availability == Availability.Online || r.Member.Presence.Availability == Availability.Busy);
                result.Add(new FriendInfo(r.Member?.Id ?? "", OnlineService.ShownName(r.Member?.Profile?.Name), online));
            }
            // people who are online first
            result.Sort((a, b) => a.Online == b.Online ? string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase) : (a.Online ? -1 : 1));
            return result;
        }

        // ---------- actions ----------

        /// <summary>Send a friend request to a name like "Ahmad_Ali#4821".</summary>
        public static Task<bool> AddByNameAsync(string friendId) => Run(async () =>
        {
            string wanted = (friendId ?? "").Trim().Replace(' ', '_');
            if (wanted.Length == 0) throw new InvalidOperationException("Type your friend's ID first (it looks like Name#1234).");
            if (wanted == AuthenticationService.Instance.PlayerName) throw new InvalidOperationException("That is your own ID.");
            await FriendsService.Instance.AddFriendByNameAsync(wanted);
        }, "Could not find that player. Check the ID (it looks like Name#1234).");

        public static Task<bool> AcceptAsync(string memberId) => Run(() => FriendsService.Instance.AddFriendAsync(memberId));
        public static Task<bool> DeclineAsync(string memberId) => Run(() => FriendsService.Instance.DeleteIncomingFriendRequestAsync(memberId));
        public static Task<bool> CancelRequestAsync(string memberId) => Run(() => FriendsService.Instance.DeleteOutgoingFriendRequestAsync(memberId));
        public static Task<bool> RemoveAsync(string memberId) => Run(() => FriendsService.Instance.DeleteFriendAsync(memberId));
        public static Task<bool> BlockAsync(string memberId) => Run(() => FriendsService.Instance.AddBlockAsync(memberId));

        /// <summary>Tell a friend "come and join my room".</summary>
        public static Task<bool> InviteAsync(string memberId, string roomCode, string myName, int myAvatar) =>
            Run(() => FriendsService.Instance.MessageAsync(memberId, new RoomInvite { code = roomCode, fromName = myName, fromAvatar = myAvatar }));

        /// <summary>Show friends that we are busy playing (or available again).</summary>
        public static async void SetBusy(bool busy)
        {
            if (!started) return;
            try { await FriendsService.Instance.SetPresenceAvailabilityAsync(busy ? Availability.Busy : Availability.Online); }
            catch (Exception e) { Debug.LogWarning("[Ludo] Presence: " + e.Message); }
        }

        static async Task<bool> Run(Func<Task> action, string friendly = null)
        {
            LastError = "";
            try
            {
                if (!await StartAsync()) return false;
                await action();
                Changed?.Invoke();
                return true;
            }
            catch (InvalidOperationException e)
            {
                LastError = e.Message;
                return false;
            }
            catch (Exception e)
            {
                LastError = friendly ?? OnlineService.Describe(e);
                Debug.LogWarning("[Ludo] Friends: " + OnlineService.Describe(e));
                return false;
            }
        }

        // ---------- events from the service ----------

        static void OnRelationshipEvent(IRelationshipAddedEvent e) => Changed?.Invoke();
        static void OnRelationshipEvent(IRelationshipDeletedEvent e) => Changed?.Invoke();
        static void OnPresence(IPresenceUpdatedEvent e) => Changed?.Invoke();

        static void OnMessage(IMessageReceivedEvent e)
        {
            try
            {
                var invite = e.GetAs<RoomInvite>();
                if (invite != null && !string.IsNullOrEmpty(invite.code)) InviteReceived?.Invoke(invite, e.UserId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Ludo] Ignored a message that is not an invite: " + ex.Message);
            }
        }
    }
}
