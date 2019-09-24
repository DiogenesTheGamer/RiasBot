using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;

namespace RiasBot.Modules.Music.Commons
{
    public class MusicUtils
    {
        /// <summary>
        /// Checks the music output channel. Returns one of the reasons: TRUE, NULL, NO_SEND_MESSAGES_PERMISSION, NO_VIEW_CHANNEL_PERMISSION
        /// </summary>
        public static OutputChannelState CheckOutputChannel(DiscordShardedClient client, ulong guildId, IMessageChannel oldChannel)
        {
            var guild = client.GetGuild(guildId);

            var channel = guild?.GetChannel(oldChannel.Id);
            if (channel is null)
                return OutputChannelState.Null;

            var permissions = guild.CurrentUser?.GetPermissions(channel);
            if (!permissions.HasValue)
                return OutputChannelState.Null;

            if (!permissions.Value.ViewChannel)
            {
                return OutputChannelState.NoViewPermission;
            }

            if (!permissions.Value.SendMessages)
            {
                return OutputChannelState.NoSendPermission;
            }

            return OutputChannelState.Available;
        }

        public static YoutubeUrl SanitizeYoutubeUrl(string url)
        {
            var regex = new Regex(@"(?:(?:youtube\.com/watch\?v=)|(?:youtu.be/))(?<videoId>[a-zA-Z0-9-_]+)(?:(?:.*list=)(?<listId>[a-zA-Z0-9-_]+))?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);

            var match = regex.Match(url);

            if (match.Length == 0)
                return null;


            var groups = match.Groups;
            if (groups.Count > 2)
            {
                return new YoutubeUrl
                {
                    VideoId = match.Groups["videoId"].Value,
                    ListId = match.Groups["listId"].Value
                };
            }

            return null;
        }
    }
}