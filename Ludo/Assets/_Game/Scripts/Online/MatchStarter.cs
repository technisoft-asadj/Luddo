using Ludo.AI;
using Ludo.Core;
using Ludo.Game;

namespace Ludo.Online
{
    /// <summary>
    /// Turns "the host said go" into a running match: builds the seats, hands the room's link to the game, wires up voice,
    /// the result recorder and "leave" and loads the Game scene. Used by the waiting room (friends) and the Quick Match screen.
    /// </summary>
    public static class MatchStarter
    {
        public static void Begin(MatchStart start)
        {
            var slots = new PlayerSlot[start.seats.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var s = start.seats[i];
                slots[i] = s.ai >= 0
                    ? PlayerSlot.OnlineCpu(s.name, (AiDifficulty)s.ai)
                    : PlayerSlot.Online(s.name, s.avatar, remote: s.playerId != OnlineService.PlayerId, playerId: s.playerId, country: CountryOf(s));
            }
            GameSession.ConfigureOnline(MatchLink.Current, slots, RoomService.ModeFrom(start.mode));
            GameSession.SpeakingProbe = VoiceService.IsSpeaking;      // the game screen lights up whoever is talking
            var recorder = new MatchRecorder(start, OnlineService.PlayerId);     // rating, statistics and Weekly Cup for this phone
            GameSession.Settler = recorder;
            GameSession.LeaveOnline = () =>
            {
                recorder.Left(ResultKind.Forfeit);                                     // leaving a ranked match early is a loss
                SocialService.SetBusy(false);
                _ = RoomService.LeaveAsync();
                _ = VoiceService.LeaveAsync();
            };
            SocialService.SetBusy(true);
            SceneLoader.Load(SceneLoader.Game);
        }

        /// <summary>
        /// A seat's country comes from that player's OWN lobby data (only they can write it), not from the host's seating plan;
        /// my own seat uses my saved profile. The host's copy is only a fallback when the lobby no longer lists the player.
        /// </summary>
        public static string CountryOf(MatchSeat seat)
        {
            if (seat.playerId == OnlineService.PlayerId) return GameSettings.Country;
            foreach (var p in RoomService.Players())
                if (p.Id == seat.playerId) return p.Country;
            return Countries.Normalize(seat.country);
        }
    }
}
