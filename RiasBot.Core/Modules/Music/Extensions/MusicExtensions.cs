using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using RiasBot.Modules.Music.Commons;

namespace RiasBot.Modules.Music.Extensions
{
    public static class MusicExtensions
    {
        /// <summary>
        /// Checks the music output channel. Returns one of the reasons: TRUE, NULL, NO_SEND_MESSAGES_PERMISSION, NO_VIEW_CHANNEL_PERMISSION
        /// </summary>
        public static string CheckOutputChannel(DiscordShardedClient client, IGuild oldGuild, IMessageChannel oldChannel)
        {
            var guild = client.GetGuild(oldGuild.Id);

            var channel = guild?.GetChannel(oldChannel.Id);
            if (channel is null)
                return "NULL";

            var currentUser = guild.CurrentUser;
            var permissions = currentUser.GetPermissions(channel);
            
            if (!permissions.ViewChannel)
            {
                return "NO_VIEW_CHANNEL_PERMISSION";
            }
            
            if (!permissions.SendMessages)
            {
                return "NO_SEND_MESSAGES_PERMISSION";
            }

            return "TRUE";
        }
        
        public static YoutubeContent DecodeYoutubeUrl(string url)
        {
            var regex = new Regex(@"youtu(?:\.be|be\.com)/(?:.*v(?:/|=)|(?:.*/)?)([a-zA-Z0-9-_]*)(?:.*list=|(?:.*/)?)([a-zA-Z0-9-_]*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);

            var match = regex.Match(url);
            var groups = match.Groups;
            if (groups.Count > 2)
            {
                var videoId = groups[1].Value;
                var playlistId = groups[2].Value;

                const string indexString = "&index=";
                var index = url.Contains(indexString) ? (int.TryParse(url.Substring(url.IndexOf(indexString, StringComparison.Ordinal) + 1, 1), out var ind) ? ind : 0) : 0;

                var youtubeContent = new YoutubeContent();
                if (!string.IsNullOrEmpty(videoId))
                    if (!string.Equals(videoId, "playlist"))
                        youtubeContent.VideoId = videoId;

                if (!string.IsNullOrEmpty(playlistId))
                    youtubeContent.PlaylistId = playlistId;

                youtubeContent.Index = index;

                return youtubeContent;
            }

            return null;
        }
    }
}