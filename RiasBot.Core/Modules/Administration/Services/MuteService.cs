using System.Linq;
using System.Threading.Tasks;
using Discord;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Services;
using RiasBot.Database.Models;

namespace RiasBot.Modules.Administration.Services
{
    [Service]
    public class MuteService
    {
        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly ITranslations _translations;

        private const string LowerTypeModule = "administration";
        private const string MuteRole = "rias-mute";

        public MuteService(IBotCredentials creds, DbService db, ITranslations translations)
        {
            _creds = creds;
            _db = db;
            _translations = translations;
        }

        public async Task MuteUserAsync(IGuild guild, IGuildUser moderator, IGuildUser user,
            IMessageChannel channel, string reason = null)
        {
            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(x => x.GuildId == guild.Id);
                var userGuildDb = db.UserGuilds.Where(x => x.GuildId == guild.Id);

                IRole role;
                if (guildDb != null)
                {
                    role = guild.GetRole(guildDb.MuteRole) ??
                           (guild.Roles.FirstOrDefault(x => x.Name == MuteRole) ??
                            await guild.CreateRoleAsync(MuteRole));
                }
                else
                {
                    role = guild.Roles.FirstOrDefault(x => x.Name == MuteRole) ??
                           await guild.CreateRoleAsync(MuteRole);
                }

                if (user.RoleIds.Any(r => r == role.Id))
                {
                    await channel.SendErrorMessageAsync(_translations.GetText(guild.Id, LowerTypeModule,
                        "user_already_muted", user));
                }
                else
                {
                    await Task.Run(async () => await AddMuteRoleToChannelsAsync(role, guild));
                    await user.AddRoleAsync(role);

                    var muteUser = userGuildDb.FirstOrDefault(y => y.UserId == user.Id);
                    if (muteUser != null)
                    {
                        muteUser.IsMuted = true;
                    }
                    else
                    {
                        var muteUserGuild = new UserGuildConfig { GuildId = guild.Id, UserId = user.Id, IsMuted = true };
                        await db.AddAsync(muteUserGuild);
                    }

                    await db.SaveChangesAsync();

                    var embed = new EmbedBuilder().WithColor(0xffff00)
                        .WithDescription(_translations.GetText(guild.Id, LowerTypeModule, "user_muted"))
                        .AddField(_translations.GetText(guild.Id, LowerTypeModule, "user"), user, true)
                        .AddField(_translations.GetText(guild.Id, LowerTypeModule, "id"), user.Id, true)
                        .AddField(_translations.GetText(guild.Id, LowerTypeModule, "moderator"), moderator, true);

                    if (!string.IsNullOrEmpty(reason))
                        embed.AddField(_translations.GetText(guild.Id, LowerTypeModule, "reason"), reason, true);

                    embed.WithThumbnailUrl(user.GetRealAvatarUrl());
                    embed.WithCurrentTimestamp();

                    if (guildDb != null)
                    {
                        var modlog = await guild.GetTextChannelAsync(guildDb.ModLogChannel);
                        if (modlog != null)
                        {
                            var currentUser = await guild.GetCurrentUserAsync();
                            var preconditions = currentUser.GetPermissions(modlog);
                            if (preconditions.ViewChannel && preconditions.SendMessages)
                            {
                                await modlog.SendMessageAsync(embed: embed.Build());
                            }
                            else
                            {
                                await channel.SendMessageAsync(embed: embed.Build());
                            }
                        }
                        else
                            await channel.SendMessageAsync(embed: embed.Build());
                    }
                    else
                    {
                        await channel.SendMessageAsync(embed: embed.Build());
                    }
                }
            }
        }

        public async Task UnmuteUserAsync(IGuild guild, IGuildUser moderator, IGuildUser user,
            IMessageChannel channel, string reason = null)
        {
            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(x => x.GuildId == guild.Id);
                var userGuildDb = db.UserGuilds.Where(x => x.GuildId == guild.Id);
                var muteUser = userGuildDb.FirstOrDefault(x => x.UserId == user.Id);

                IRole role;
                if (guildDb != null)
                {
                    role = guild.GetRole(guildDb.MuteRole);
                    if (role is null)
                    {
                        role = guild.Roles.FirstOrDefault(x => x.Name == MuteRole);
                        if (role is null)
                        {
                            await channel.SendErrorMessageAsync(_translations.GetText(guild.Id, LowerTypeModule,
                                "user_is_not_muted", user));
                            return;
                        }
                    }
                }
                else
                {
                    role = guild.Roles.FirstOrDefault(x => x.Name == "rias-mute");
                    if (role is null)
                    {
                        await channel.SendErrorMessageAsync(_translations.GetText(guild.Id, LowerTypeModule,
                            "user_is_not_muted", user));
                        return;
                    }
                }

                if (user.RoleIds.Any(r => r == role.Id))
                {
                    await user.RemoveRoleAsync(role);

                    if (muteUser != null)
                    {
                        muteUser.IsMuted = false;
                    }

                    await db.SaveChangesAsync();

                    var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                        .WithDescription(_translations.GetText(guild.Id, LowerTypeModule, "user_unmuted"))
                        .AddField(_translations.GetText(guild.Id, LowerTypeModule, "user"), user, true)
                        .AddField(_translations.GetText(guild.Id, LowerTypeModule, "id"), user.Id, true)
                        .AddField(_translations.GetText(guild.Id, LowerTypeModule, "moderator"), moderator, true);

                    if (!string.IsNullOrEmpty(reason))
                        embed.AddField(_translations.GetText(guild.Id, LowerTypeModule, "reason"), reason, true);

                    embed.WithThumbnailUrl(user.GetRealAvatarUrl());
                    embed.WithCurrentTimestamp();

                    if (guildDb != null)
                    {
                        var modlog = await guild.GetTextChannelAsync(guildDb.ModLogChannel);
                        if (modlog != null)
                        {
                            var currentUser = await guild.GetCurrentUserAsync();
                            var preconditions = currentUser.GetPermissions(modlog);
                            if (preconditions.ViewChannel && preconditions.SendMessages)
                            {
                                await modlog.SendMessageAsync(embed: embed.Build());
                            }
                            else
                            {
                                await channel.SendMessageAsync(embed: embed.Build());
                            }
                        }
                        else
                            await channel.SendMessageAsync(embed: embed.Build());
                    }
                    else
                    {
                        await channel.SendMessageAsync(embed: embed.Build());
                    }
                }
                else
                {
                    await channel.SendErrorMessageAsync(_translations.GetText(guild.Id, LowerTypeModule,
                        "user_is_not_muted", user));
                }
            }
        }
        
        public async Task AddMuteRoleToChannelsAsync(IRole role, IGuild guild)
        {
            var permissions = new OverwritePermissions().Modify(addReactions: PermValue.Deny, sendMessages: PermValue.Deny, speak: PermValue.Deny);
            
            var categories = await guild.GetCategoriesAsync();
            foreach (var category in categories)
            {
                await AddPermissionOverwriteAsync(category, role, permissions);
            }

            var channels = await guild.GetChannelsAsync();
            foreach (var channel in channels)
            {
                await AddPermissionOverwriteAsync(channel, role, permissions);
            }
        }

        private static async Task AddPermissionOverwriteAsync(IGuildChannel channel, IRole role, OverwritePermissions permissions)
        {
            var addPermissionOverwrite = false;
            
            var rolePermissions = channel.GetPermissionOverwrite(role);
            if (rolePermissions != null)
            {
                if (rolePermissions.Value.SendMessages != PermValue.Deny)
                {
                    rolePermissions.Value.Modify(sendMessages: PermValue.Deny);
                    addPermissionOverwrite = true;
                }
                    
                if (rolePermissions.Value.AddReactions != PermValue.Deny)
                {
                    rolePermissions.Value.Modify(addReactions: PermValue.Deny);
                    addPermissionOverwrite = true;
                }
                
                if (rolePermissions.Value.Speak != PermValue.Deny)
                {
                    rolePermissions.Value.Modify(addReactions: PermValue.Deny);
                    addPermissionOverwrite = true;
                }
                    
                if (addPermissionOverwrite)
                    await channel.AddPermissionOverwriteAsync(role, rolePermissions.Value);
            }
            else
            {
                await channel.AddPermissionOverwriteAsync(role, permissions);
            }
                
            await channel.AddPermissionOverwriteAsync(role, permissions);
        }
    }
}