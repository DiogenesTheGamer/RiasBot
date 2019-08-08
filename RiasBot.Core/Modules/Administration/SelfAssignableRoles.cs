using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Services;
using DBModels = RiasBot.Database.Models;

namespace RiasBot.Modules.Administration
{
    public partial class Administration
    {
        public class SelfAssignableRoles : RiasSubmodule
        {
            private readonly IBotCredentials _creds;
            private readonly DbService _db;
            private readonly InteractiveService _is;

            public SelfAssignableRoles(IBotCredentials creds, DbService db, InteractiveService ins)
            {
                _creds = creds;
                _db = db;
                _is = ins;
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RateLimit(1, 5, RateLimitType.GuildUser)]
            public async Task IamAsync([Remainder]string name)
            {
                var role = Context.Guild.Roles.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.InvariantCultureIgnoreCase));
                if (role != null)
                {
                    using (var db = _db.GetDbContext())
                    {
                        if (db.SelfAssignableRoles.Where(x => x.GuildId == Context.Guild.Id).Any(y => y.RoleId == role.Id))
                        {
                            if ((await Context.Guild.GetCurrentUserAsync()).CheckRoleHierarchy(role) <= 0)
                            {
                                await ReplyErrorAsync("sar_above");
                                return;
                            }

                            var user = (IGuildUser)Context.User;
                            await user.AddRoleAsync(role);
                            await ReplyConfirmationAsync("you_are", role.Name);
                        }
                        else
                        {
                            await ReplyErrorAsync("role_not_self_assignable", role.Name);
                        }
                    }
                }
                else
                {
                    await ReplyErrorAsync("role_not_found");
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RateLimit(1, 5, RateLimitType.GuildUser)]
            public async Task IamNotAsync([Remainder]string name)
            {
                var role = Context.Guild.Roles.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.InvariantCultureIgnoreCase));
                if (role != null)
                {
                    using (var db = _db.GetDbContext())
                    {
                        if (db.SelfAssignableRoles.Where(x => x.GuildId == Context.Guild.Id).Any(y => y.RoleId == role.Id))
                        {
                            if ((await Context.Guild.GetCurrentUserAsync()).CheckRoleHierarchy(role) <= 0)
                            {
                                await ReplyErrorAsync("sar_above");
                                return;
                            }

                            var user = (IGuildUser) Context.User;
                            await user.RemoveRoleAsync(role);
                            await ReplyConfirmationAsync("you_are_not", role.Name);
                        }
                    }
                }
                else
                {
                    await ReplyErrorAsync("role_not_found");
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            [RequireContext(ContextType.Guild)]
            public async Task AddSelfAssignableRoleAsync([Remainder]IRole role)
            {
                if (!role.IsManaged)
                {
                    if ((await Context.Guild.GetCurrentUserAsync()).CheckRoleHierarchy(role) <= 0)
                    {
                        await ReplyErrorAsync("sar_above");
                        return;
                    }

                    using (var db = _db.GetDbContext())
                    {
                        if (!db.SelfAssignableRoles.Where(x => x.GuildId == Context.Guild.Id).Any(x => x.RoleId == role.Id))
                        {
                            var sar = new DBModels.SelfAssignableRoles { GuildId = Context.Guild.Id, RoleName = role.Name, RoleId = role.Id };
                            await db.AddAsync(sar);
                            await db.SaveChangesAsync();

                            await ReplyConfirmationAsync("sar_added", role.Name);
                        }
                        else
                        {
                            await ReplyErrorAsync("sar_in_list", role.Name);
                        }
                    }
                }
                else
                {
                    await ReplyErrorAsync("sar_not_added", role.Name);
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            [RequireContext(ContextType.Guild)]
            public async Task RemoveSelfAssignableRoleAsync([Remainder]string name)
            {
                using (var db = _db.GetDbContext())
                {
                    var sar = db.SelfAssignableRoles.Where(x => x.GuildId == Context.Guild.Id).FirstOrDefault(x => string.Equals(x.RoleName, name, StringComparison.InvariantCultureIgnoreCase));
                    if (sar != null)
                    {
                        db.Remove(sar);
                        await db.SaveChangesAsync();

                        await ReplyConfirmationAsync("sar_removed", sar.RoleName);
                    }
                    else
                    {
                        await ReplyErrorAsync("sar_not_in_list", name);
                    }
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RateLimit(1, 5, RateLimitType.GuildUser)]
            public async Task ListSelfAssignableRolesAsync()
            {
                var sar = new List<DBModels.SelfAssignableRoles>();

                using (var db = _db.GetDbContext())
                {
                    var checkSar = db.SelfAssignableRoles.Where(x => x.GuildId == Context.Guild.Id);
                    foreach (var validSar in checkSar)
                    {
                        var role = Context.Guild.GetRole(validSar.RoleId);
                        if (role != null)
                        {
                            sar.Add(validSar);
                        }
                        else
                        {
                            db.Remove(validSar);
                        }
                    }
                    await db.SaveChangesAsync();
                }

                if (sar.Count > 0)
                {
                    var lsar = new List<string>();
                    var index = 0;
                    foreach (var role in sar)
                    {
                        lsar.Add($"#{index + 1} {role.RoleName}");
                        index++;
                    }
                    var pager = new PaginatedMessage
                    {
                        Title = GetText("sar_list"),
                        Color = new Color(_creds.ConfirmColor),
                        Pages = lsar,
                        Options = new PaginatedAppearanceOptions
                        {
                            ItemsPerPage = 15,
                            Timeout = TimeSpan.FromMinutes(1),
                            DisplayInformationIcon = false,
                            JumpDisplayOptions = JumpDisplayOptions.Never
                        }

                    };
                    await _is.SendPaginatedMessageAsync((ShardedCommandContext)Context, pager);
                }
                else
                {
                    await ReplyErrorAsync("no_sar");
                }
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RateLimit(1, 5, RateLimitType.GuildUser)]
            public async Task UpdateSelfAssignableRolesAsync()
            {
                using (var db = _db.GetDbContext())
                {
                    var sarDb = db.SelfAssignableRoles.Where(x => x.GuildId == Context.Guild.Id);
                    foreach (var sar in sarDb)
                    {
                        var role = Context.Guild.GetRole(sar.RoleId);
                        if (role != null)
                        {
                            sar.RoleName = role.Name;
                        }
                        else
                        {
                            db.Remove(sar);
                        }
                    }
                    await db.SaveChangesAsync();
                }

                await ReplyConfirmationAsync("lsar_updated");
            }
        }
    }
}