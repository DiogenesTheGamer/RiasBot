using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Services;
using RiasBot.Database.Models;

namespace RiasBot.Modules.Administration
{
    public partial class Administration
    {
        public class Roles : RiasSubmodule
        {
            private readonly IBotCredentials _creds;
            private readonly DbService _db;
            private readonly InteractiveService _is;

            public Roles(IBotCredentials creds, DbService db, InteractiveService ins)
            {
                _creds = creds;
                _db = db;
                _is = ins;
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            public async Task RolesAsync()
            {
                var everyoneRole = Context.Guild.Roles.Where(x => x.Name == "@everyone");
                var roles = Context.Guild.Roles.OrderByDescending(x => x.Position).Except(everyoneRole).Select(y => y.Name).ToList();
                if (roles.Count > 0)
                {
                    var pager = new PaginatedMessage
                    {
                        Title = GetText("list_roles"),
                        Color = new Color(_creds.ConfirmColor),
                        Pages = roles,
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
                    await ReplyErrorAsync("no_roles");
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            [RequireBotPermission(GuildPermission.ManageRoles)]
            public async Task CreateRoleAsync([Remainder]string name)
            {
                await Context.Guild.CreateRoleAsync(name);
                await ReplyConfirmationAsync("role_created", name);
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            [RequireBotPermission(GuildPermission.ManageRoles)]
            public async Task DeleteRoleAsync([Remainder]IRole role)
            {
                if (Extensions.UserExtensions.CheckHierarchyRoles(role, Context.Guild, await Context.Guild.GetCurrentUserAsync()))
                {
                    if (!role.IsManaged)
                    {
                        var roleName = role.Name;
                        await role.DeleteAsync();
                        await ReplyConfirmationAsync("role_deleted", roleName);
                    }
                    else
                    {
                        await ReplyErrorAsync("role_not_deleted", role.Name);
                    }
                }
                else
                {
                    await ReplyErrorAsync("role_above");
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            [RequireBotPermission(GuildPermission.ManageRoles)]
            public async Task RoleColorAsync(string color, [Remainder]IRole role)
            {
                color = color.Replace("#", "");
                if (color.Length != 6)
                {
                    await ReplyErrorAsync("invalid_hex_color");
                    return;
                }
                if (Extensions.UserExtensions.CheckHierarchyRoles(role, Context.Guild, await Context.Guild.GetCurrentUserAsync()))
                {
                    if (int.TryParse(color.Substring(0, 2), NumberStyles.HexNumber, null, out var redColor) &&
                        int.TryParse(color.Substring(2, 2), NumberStyles.HexNumber, null, out var greenColor) &&
                        int.TryParse(color.Substring(4, 2), NumberStyles.HexNumber, null, out var blueColor))
                    {
                        var red = Convert.ToByte(redColor);
                        var green = Convert.ToByte(greenColor);
                        var blue = Convert.ToByte(blueColor);
                        await role.ModifyAsync(r => r.Color = new Color(red, green, blue));
                        await ReplyConfirmationAsync("role_color_changed", role.Name);
                    }
                    else
                    {
                        await ReplyErrorAsync("invalid_hex_color");
                    }
                }
                else
                {
                    await ReplyErrorAsync("role_above");
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            [RequireBotPermission(GuildPermission.ManageRoles)]
            public async Task RenameRoleAsync([Remainder]string names)
            {
                var roles = names.Split("->");
                var oldName = roles[0].TrimEnd();
                var newName = roles[1].TrimStart();

                var oldRole = Context.Guild.Roles.First(r => string.Equals(r.Name, oldName, StringComparison.InvariantCultureIgnoreCase));
                if (oldRole != null)
                {
                    if (Extensions.UserExtensions.CheckHierarchyRoles(oldRole, Context.Guild, await Context.Guild.GetCurrentUserAsync()))
                    {
                        oldName = oldRole.Name;
                        await oldRole.ModifyAsync(r => r.Name = newName);
                        await ReplyConfirmationAsync("role_renamed", oldName, newName);
                    }
                    else
                    {
                        await ReplyErrorAsync("role_above");
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
            [RequireUserPermission(GuildPermission.ManageRoles)]
            [RequireBotPermission(GuildPermission.ManageRoles)]
            public async Task SetRoleAsync(IGuildUser user, [Remainder]IRole role)
            {
                if (Extensions.UserExtensions.CheckHierarchyRoles(Context.Guild, user, await Context.Guild.GetCurrentUserAsync()))
                {
                    if (!role.IsManaged)
                    {
                        await user.AddRoleAsync(role);
                        await ReplyConfirmationAsync("role_set", role.Name, user);
                    }
                    else
                    {
                        await ReplyErrorAsync("role_not_set", role.Name);
                    }
                }
                else
                {
                    await ReplyErrorAsync("role_above");
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            [RequireBotPermission(GuildPermission.ManageRoles)]
            public async Task RemoveRoleAsync(IGuildUser user, [Remainder]IRole role)
            {
                if (Extensions.UserExtensions.CheckHierarchyRoles(Context.Guild, user, await Context.Guild.GetCurrentUserAsync()))
                {
                    await user.RemoveRoleAsync(role);
                    await ReplyConfirmationAsync("role_removed", role.Name, user);
                }
                else
                {
                    await ReplyErrorAsync("role_above");
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task AutoAssignableRoleAsync([Remainder]string role = null)
            {
                using (var db = _db.GetDbContext())
                {
                    var guildDb = db.Guilds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);

                    if (!string.IsNullOrEmpty(role))
                    {
                        var getRole = Context.Guild.Roles.FirstOrDefault(x => string.Equals(x.Name, role, StringComparison.InvariantCultureIgnoreCase));
                        if (getRole != null)
                        {
                            if (!getRole.IsManaged)
                            {
                                if (guildDb != null)
                                {
                                    guildDb.AutoAssignableRole = getRole.Id;
                                    await db.SaveChangesAsync();
                                }
                                else
                                {
                                    var aar = new GuildConfig { GuildId = Context.Guild.Id, AutoAssignableRole = getRole.Id };
                                    await db.AddAsync(aar);
                                    await db.SaveChangesAsync();
                                }

                                await ReplyConfirmationAsync("aar_set", getRole.Name);
                            }
                            else
                            {
                                await ReplyErrorAsync("arr_not_set", getRole.Name);
                            }
                        }
                        else
                        {
                            await ReplyErrorAsync("role_not_found");
                        }
                    }
                    else
                    {
                        if (guildDb != null)
                        {
                            guildDb.AutoAssignableRole = 0;
                            await db.SaveChangesAsync();
                        }

                        await ReplyConfirmationAsync("aar_disabled");
                    }
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            [RequireBotPermission(GuildPermission.ManageRoles)]
            public async Task HoistRoleAsync([Remainder]IRole role)
            {
                if (Extensions.UserExtensions.CheckHierarchyRoles(role, Context.Guild, await Context.Guild.GetCurrentUserAsync()))
                {
                    if (role.IsHoisted)
                    {
                        await role.ModifyAsync(x => x.Hoist = false);
                        await ReplyConfirmationAsync("role_not_displayed", role.Name);
                    }
                    else
                    {
                        await role.ModifyAsync(x => x.Hoist = true);
                        await ReplyConfirmationAsync("role_displayed", role.Name);
                    }
                }
                else
                {
                    await ReplyErrorAsync("role_above");
                }
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            [RequireBotPermission(GuildPermission.ManageRoles)]
            public async Task MentionRoleAsync([Remainder]IRole role)
            {
                if (Extensions.UserExtensions.CheckHierarchyRoles(role, Context.Guild, await Context.Guild.GetCurrentUserAsync()))
                {
                    if (role.IsMentionable)
                    {
                        await role.ModifyAsync(x => x.Mentionable = false);
                        await ReplyConfirmationAsync("role_not_mentionable", role.Name);
                    }
                    else
                    {
                        await role.ModifyAsync(x => x.Mentionable = true);
                        await ReplyConfirmationAsync("role_mentionable", role.Name);
                    }
                }
                else
                {
                    await ReplyErrorAsync("role_above");
                }
            }
        }
    }
}