using System.Threading.Tasks;
using Lavalink4NET.Player;
using Newtonsoft.Json.Linq;

namespace RiasBot.Modules.Music.Extensions
{
    public static class MusicExtensions
    {
        /// <summary>
        /// Fetches thumbnail of the specified track.
        /// </summary>
        /// <param name="track"><see cref="LavaTrack"/></param>
        public static string FetchThumbnailAsync(this LavalinkTrack track)
        {
            var url = string.Empty;

            switch (track.Provider)
            {
                case StreamProvider.YouTube:
                    return $"https://img.youtube.com/vi/{track.Identifier}/maxresdefault.jpg";

                case StreamProvider.Twitch:
                    url = $"https://api.twitch.tv/v4/oembed?url={track.Source}";
                    break;

                case StreamProvider.SoundCloud:
                    url = $"https://soundcloud.com/oembed?url={track.Source}&format=json";
                    break;

                case StreamProvider.Vimeo:
                    url = $"https://vimeo.com/api/oembed.json?url={track.Source}";
                    break;
            }

            return null;
        }
    }
}