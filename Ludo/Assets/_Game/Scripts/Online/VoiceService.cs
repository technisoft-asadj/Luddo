using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Ludo.Online
{
    /// <summary>
    /// Voice chat for a room, through Unity's Vivox. Players in the same room share one voice channel. The microphone is
    /// asked for ONLY when the player taps the mic button (never at app start). Every player can mute their own mic and
    /// mute any other player just for themselves. Nothing here throws.
    /// </summary>
    public static class VoiceService
    {
        public enum VoiceStatus { Off, Starting, On, NoPermission, Failed }

        static string channel = "";
        static bool vivoxReady;
        static readonly Dictionary<string, VivoxParticipant> people = new Dictionary<string, VivoxParticipant>();

        public static VoiceStatus Status { get; private set; } = VoiceStatus.Off;
        public static string LastError { get; private set; } = "";
        public static bool MicMuted { get; private set; }
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            channel = "";
            vivoxReady = false;
            people.Clear();
            Status = VoiceStatus.Off;
            LastError = "";
            MicMuted = false;
            Changed = null;
        }

        public static bool IsOn => Status == VoiceStatus.On;

        /// <summary>Join the voice channel of a room. Asks for the microphone if it has not been allowed yet.</summary>
        public static async Task<bool> JoinAsync(string roomId)
        {
            if (Status == VoiceStatus.On || Status == VoiceStatus.Starting) return IsOn;
            LastError = "";
            Set(VoiceStatus.Starting);
            try
            {
                if (!await EnsureMicrophonePermission())
                {
                    LastError = "The microphone is not allowed. You can allow it in the phone's Settings.";
                    Set(VoiceStatus.NoPermission);
                    return false;
                }
                if (!await OnlineService.ConnectAsync()) { LastError = OnlineService.LastError; Set(VoiceStatus.Failed); return false; }

                if (!vivoxReady)
                {
                    await VivoxService.Instance.InitializeAsync();
                    vivoxReady = true;
                    VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
                    VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
                }
                if (!VivoxService.Instance.IsLoggedIn)
                    await VivoxService.Instance.LoginAsync(new LoginOptions { DisplayName = OnlineService.CloudNameFrom(Ludo.Game.GameSettings.PlayerName(0)) });

                channel = "room-" + roomId;
                await VivoxService.Instance.JoinGroupChannelAsync(channel, ChatCapability.AudioOnly);
                if (MicMuted) VivoxService.Instance.MuteInputDevice(); else VivoxService.Instance.UnmuteInputDevice();
                Set(VoiceStatus.On);
                return true;
            }
            catch (Exception e)
            {
                LastError = OnlineService.Describe(e);
                Debug.LogWarning("[Ludo] Voice: " + LastError);
                Set(VoiceStatus.Failed);
                return false;
            }
        }

        public static async Task LeaveAsync()
        {
            try
            {
                if (vivoxReady && VivoxService.Instance.IsLoggedIn)
                {
                    if (!string.IsNullOrEmpty(channel)) await VivoxService.Instance.LeaveChannelAsync(channel);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Ludo] Voice leave: " + e.Message); }
            channel = "";
            people.Clear();
            Set(VoiceStatus.Off);
        }

        /// <summary>Mute or unmute my own microphone.</summary>
        public static void SetMicMuted(bool muted)
        {
            MicMuted = muted;
            if (!vivoxReady) { Changed?.Invoke(); return; }
            try
            {
                if (muted) VivoxService.Instance.MuteInputDevice(); else VivoxService.Instance.UnmuteInputDevice();
            }
            catch (Exception e) { Debug.LogWarning("[Ludo] Mic mute: " + e.Message); }
            Changed?.Invoke();
        }

        /// <summary>Is this player talking right now (for the glow round their picture)?</summary>
        public static bool IsSpeaking(string playerId)
        {
            return IsOn && people.TryGetValue(playerId, out var p) && p.SpeechDetected && !p.IsMuted;
        }

        public static bool IsMutedByMe(string playerId) => people.TryGetValue(playerId, out var p) && p.IsMuted && !p.IsSelf;

        /// <summary>Stop hearing one player (only for me). Toggles.</summary>
        public static void ToggleMuteFor(string playerId)
        {
            if (!people.TryGetValue(playerId, out var p) || p.IsSelf) return;
            if (p.IsMuted) p.UnmutePlayerLocally(); else p.MutePlayerLocally();
            Changed?.Invoke();
        }

        public static bool IsInVoice(string playerId) => people.ContainsKey(playerId);

        // ---------- helpers ----------

        static void OnParticipantAdded(VivoxParticipant p)
        {
            people[p.PlayerId] = p;
            p.ParticipantMuteStateChanged += () => Changed?.Invoke();
            Changed?.Invoke();
        }

        static void OnParticipantRemoved(VivoxParticipant p)
        {
            people.Remove(p.PlayerId);
            Changed?.Invoke();
        }

        static void Set(VoiceStatus s)
        {
            Status = s;
            Changed?.Invoke();
        }

        static async Task<bool> EnsureMicrophonePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Permission.Microphone)) return true;
            var done = new TaskCompletionSource<bool>();
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => done.TrySetResult(true);
            callbacks.PermissionDenied += _ => done.TrySetResult(false);
            callbacks.PermissionDeniedAndDontAskAgain += _ => done.TrySetResult(false);
            Permission.RequestUserPermission(Permission.Microphone, callbacks);
            return await done.Task;
#else
            await Task.Yield();
            return true;
#endif
        }
    }
}
