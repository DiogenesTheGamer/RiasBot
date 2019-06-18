using Discord;
using Discord.Commands;

namespace RiasBot.Commons.Attributes
{
    public sealed class UserPermission : RequireUserPermissionAttribute
    {
        public UserPermission(GuildPermission permission) : base(permission)
        {
            if (GuildPermission != null)
                ErrorMessage = $"You must have {GuildPermission.Value} guild permission.";
        }

        public UserPermission(ChannelPermission permission) : base(permission)
        {
            if (ChannelPermission != null)
                ErrorMessage = $"You must have {ChannelPermission.Value} channel permission.";
        }
    }
}