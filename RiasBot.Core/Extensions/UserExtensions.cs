using System;
using Discord;
using Discord.Rest;
using Discord.WebSocket;

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

        /// <summary> Check the hierarchy between the current user and another user in the roles hierarchy.<br/>
        /// returns true - if the user is above or equal with the other user;
        /// false - if the user is below the other user
        /// </summary>
        public static bool CheckHierarchy(this IGuildUser userOne, IGuildUser userTwo)
        {
            if (!(userOne is SocketGuildUser socketGuildUserOne))
                throw new InvalidCastException("The current IGuildUser user is not SocketGuildUser.");

            if (!(userTwo is SocketGuildUser socketGuildUserTwo))
                throw new InvalidCastException("The IGuildUser user to check is not SocketGuildUser.");

            return socketGuildUserOne.Hierarchy >= socketGuildUserTwo.Hierarchy;
        }

        /// <summary> Check the hierarchy roles between the current user and a role.<br/>
        /// returns true - if the role's position is above or equal with the user's hierarchy;
        /// false - if the role's position is below the user's hierarchy
        /// </summary>
        public static bool CheckHierarchyRole(this IGuildUser user, IRole role)
        {
            if (!(user is SocketGuildUser socketGuildUser))
                throw new InvalidCastException("The IGuildUser user is not SocketGuildUser.");

            return socketGuildUser.Hierarchy >= role.Position;
        }
    }
}