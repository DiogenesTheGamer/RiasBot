using System;
using Discord;
using Lavalink4NET.Player;

namespace RiasBot.Modules.Music.Commons
{
    public enum OutputChannelState
    {
        Available,
        Null,
        NoViewPermission,
        NoSendPermission
    }

    public class Track : LavalinkTrack
    {
        /// <summary>
        /// Gets the user that requested this track
        /// </summary>
        public readonly IUser User;

        public Track(LavalinkTrack track, IUser user)
            : base(track.Identifier, track.Author, track.Duration, track.IsLiveStream,
                track.IsSeekable, track.Source, track.Title, track.TrackIdentifier, track.Provider)
        {
            User = user;
        }
    }

    public class YoutubeUrl
    {
        public string VideoId { get; set; }
        public string ListId { get; set; }
    }

    [Flags]
    public enum PatreonPlayerFeatures
    {
        None = 0,
        Volume = 1,
        LongTracks = 2,
        Livestream = 4
    }
}