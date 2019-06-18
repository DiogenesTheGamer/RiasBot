using System.Linq;
using Discord;
using Discord.Rest;

namespace RiasBot.Extensions
{
    public static class UserExtensions
    {
        public static string GetRealAvatarUrl(this IGuildUser user, ushort size = 1024)
        {
            if (!string.IsNullOrEmpty(user.AvatarId))
                return user.AvatarId.StartsWith("a_")
                    ? $"{DiscordConfig.CDNUrl}avatars/{user.Id}/{user.AvatarId}.gif?size={size}"
                    : user.GetAvatarUrl(ImageFormat.Auto, size);

            return GetDefaultAvatarUrl(user);
        }

        private static string GetDefaultAvatarUrl(this IGuildUser user)
        {
            return $"{DiscordConfig.CDNUrl}embed/avatars/{user.DiscriminatorValue % 5}.png";
        }

        public static string GetRealAvatarUrl(this IUser user, ushort size = 1024)
        {
            if (!string.IsNullOrEmpty(user.AvatarId))
                return user.AvatarId.StartsWith("a_")
                    ? $"{DiscordConfig.CDNUrl}avatars/{user.Id}/{user.AvatarId}.gif?size={size}"
                    : user.GetAvatarUrl(ImageFormat.Auto, size);

            return GetDefaultAvatarUrl(user);
        }

        private static string GetDefaultAvatarUrl(this IUser user)
        {
            return $"{DiscordConfig.CDNUrl}embed/avatars/{user.DiscriminatorValue % 5}.png";
        }

        public static string GetRealAvatarUrl(this RestUser user, ushort size = 1024)
        {
            if (!string.IsNullOrEmpty(user.AvatarId))
                return user.AvatarId.StartsWith("a_")
                    ? $"{DiscordConfig.CDNUrl}avatars/{user.Id}/{user.AvatarId}.gif?size={size}"
                    : user.GetAvatarUrl(ImageFormat.Auto, size);

            return GetDefaultAvatarUrl(user);
        }

        private static string GetDefaultAvatarUrl(this RestUser user)
        {
            return $"{DiscordConfig.CDNUrl}embed/avatars/{user.DiscriminatorValue % 5}.png";
        }

        /// <summary> Check the hierarchy roles between the bot and an user.
        /// returns true - if the user is below the bot; false - if the user is above the bot
        /// </summary>
        public static bool CheckHierarchyRoles(IGuild guild, IGuildUser user, IGuildUser bot)
        {
            var userRoles = user.RoleIds.Select(guild.GetRole).ToList();
            var botRoles = bot.RoleIds.Select(guild.GetRole).ToList();

            return botRoles.Any(x => userRoles.All(y => x.Position > y.Position));
        }

        /// <summary> Check the hierarchy roles between the bot and a role.
        /// returns true - if the role is below the bot; false - if the role is above the bot
        /// </summary>
        public static bool CheckHierarchyRoles(IRole role, IGuild guild, IGuildUser bot)
        {
            var botRoles = bot.RoleIds.Select(guild.GetRole).ToList();

            return botRoles.Any(x => x.Position > role.Position);
        }
    }
}