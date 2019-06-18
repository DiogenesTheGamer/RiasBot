using System.Collections.Generic;
using System.Threading;
using Discord;
using Victoria;
using Victoria.Entities;

namespace RiasBot.Modules.Music.Commons
{
    public class MusicPlayer
    {
        public LavaPlayer Player { get; set; }
        public TrackContent CurrentTrack { get; set; }
        public List<TrackContent> Queue { get; } = new List<TrackContent>();
        public bool Repeat { get; set; }
        public IGuild Guild { get; set; }
        public IVoiceChannel VoiceChannel { get; set; }
        public IMessageChannel Channel { get; set; }
        public PatreonPlayerFeatures Features { get; set; }
        public Timer AutoDisconnectTimer { get; set; }
    }

    public class PatreonPlayerFeatures
    {
        public bool Volume { get; set; }
        //Over 3 hours
        public bool LongTracks { get; set; }
        public bool Livestreams { get; set; }
    }
        
    public class TrackContent
    {
        public LavaTrack Track { get; set; }
        public IGuildUser User { get; set; }
    }

    public class MusicSearchResult
    {
        public SearchType SearchType { get; set; }
        public LavaTrack Track { get; set; }
        public IEnumerable<LavaTrack> Tracks { get; set; }
    }

    public class YoutubeContent
    {
        public string VideoId { get; set; }
        public string PlaylistId { get; set; }
        public int Index { get; set; }
    }

    public enum SearchType
    {
        Keywords,
        Url
    }
}