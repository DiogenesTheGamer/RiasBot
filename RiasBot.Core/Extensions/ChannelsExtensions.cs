using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace RiasBot.Extensions
{
    public static class ChannelsExtensions
    {
        public static async Task<ICategoryChannel> GetCategoryByIdAsync(IGuild guild, string id)
        {
            return ulong.TryParse(id, out var categoryId) ?
                (await guild.GetCategoriesAsync()).FirstOrDefault(x => x.Id == categoryId) : null;
        }
        
        public static async Task<ITextChannel> GetTextChannelByIdAsync(IGuild guild, string id)
        {
            if (ulong.TryParse(id, out var channelId))
            {
                return await guild.GetTextChannelAsync(channelId);
            }

            return null;
        }
            
        public static async Task<IVoiceChannel> GetVoiceChannelByIdAsync(IGuild guild, string id)
        {
            if (ulong.TryParse(id, out var channelId))
            {
                return await guild.GetVoiceChannelAsync(channelId);
            }

            return null;
        }

        public static bool CheckViewChannelPermission(IGuildUser bot, IGuildChannel channel)
        {
            var permissions = bot.GetPermissions(channel);
            return permissions.ViewChannel;
        }
    }
}