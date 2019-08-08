using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Modules.Administration.Services;
using RiasBot.Services;
using RiasBot.Database.Models;
using RiasBot.Extensions;

namespace RiasBot.Modules.Administration
{
    public partial class Administration
    {
        public class Mute : RiasSubmodule<MuteService>
        {
            private readonly DbService _db;

            public Mute(DbService db)
            {
                _db = db;
            }

            [RiasCommand]
            [Description]
            [Aliases]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.MuteMembers)]
            [RequireBotPermission(GuildPermission.ManageRoles | GuildPermission.ManageChannels | GuildPermission.MuteMembers)]
            [Priority(1)]
            public async Task MuteAsync(IGuildUser user, [Remainder] string reason = null)
            {
                if (user.Id == Context.User.Id)
                    return;
                if (user.Id == Context.Guild.OwnerId)
                {
                    await ReplyErrorAsync("cannot_mute_owner");
                    return;
                }

                if ((await Context.Guild.GetCurrentUserAsync()).CheckHierarchy(user) <= 0)
                {
                    await ReplyErrorAsync("user_above");
                    return;
                }

                await Service.MuteUserAsync(Context.Guild, (IGuildUser) Context.User, user,
                    Context.Channel, reason);
            }

            [RiasCommand]
            [Description]
            [Aliases]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.MuteMembers)]
            [RequireBotPermission(GuildPermission.ManageRoles | GuildPermission.ManageChannels | GuildPermission.MuteMembers)]
            public async Task UnMuteAsync(IGuildUser user, [Remainder]string reason = null)
            {
                if (user.Id == Context.User.Id)
                    return;
                if (user.Id != Context.Guild.OwnerId)
                {
                    if ((await Context.Guild.GetCurrentUserAsync()).CheckHierarchy(user) <= 0)
                    {
                        await ReplyErrorAsync("user_above");
                        return;
                    }

                    await Service.UnmuteUserAsync(Context.Guild, (IGuildUser) Context.User, user,
                        Context.Channel, reason);
                }
            }

            [RiasCommand]
            [Description]
            [Aliases]
            [Usages]
            [RequireUserPermission(GuildPermission.ManageRoles | GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageRoles | GuildPermission.ManageChannels)]
            [RequireContext(ContextType.Guild)]
            public async Task SetMuteAsync([Remainder] string name)
            {
                using (var db = _db.GetDbContext())
                {
                    var guildDb = db.Guilds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
                    var role = Context.Guild.Roles.FirstOrDefault(x => x.Name == name) ?? await Context.Guild.CreateRoleAsync(name);
                    if (guildDb != null)
                    {
                        guildDb.MuteRole = role.Id;
                    }
                    else
                    {
                        var muteRole = new GuildConfig { GuildId = Context.Guild.Id, MuteRole = role.Id };
                        await db.AddAsync(muteRole);
                    }

                    await db.SaveChangesAsync();
                    await ReplyConfirmationAsync("new_mute_role_set");
                    _ = Task.Run(async () => await Service.AddMuteRoleToChannelsAsync(role, Context.Guild));
                }
            }
        }
    }
}