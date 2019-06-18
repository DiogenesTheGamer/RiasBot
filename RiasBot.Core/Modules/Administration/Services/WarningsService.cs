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
    public class WarningsService
    {
        private readonly DbService _db;
        private readonly ITranslations _translations;

        private const string LowerTypeModule = "administration";

        public WarningsService(DbService db, ITranslations translations)
        {
            _db = db;
            _translations = translations;
        }

        /// <summary>
        /// Warn the user and return the punishment method if the number of warnings was reached
        /// </summary>
        public async Task<string> WarnUserAsync(IGuild guild, IGuildUser moderator, IGuildUser user, IMessageChannel channel, IUserMessage message, string reason)
        {
            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(x => x.GuildId == guild.Id);
                var warnings = db.Warnings.Where(x => x.GuildId == guild.Id).Where(y => y.UserId == user.Id).ToList();

                var nrWarnings = warnings.Count;

                if (nrWarnings >= 10)
                {
                    await channel.SendErrorMessageAsync(_translations.GetText(guild.Id, LowerTypeModule, "warn_limit", nrWarnings));
                    return null;
                }

                var warning = new Warnings { GuildId = guild.Id, UserId = user.Id, Reason = reason, Moderator = moderator.Id };

                var embed = new EmbedBuilder().WithColor(0xffff00)
                    .WithTitle(_translations.GetText(guild.Id, LowerTypeModule, "warn"))
                    .AddField(_translations.GetText(guild.Id, LowerTypeModule, "user"), user, true)
                    .AddField(_translations.GetText(guild.Id, LowerTypeModule, "id"), user.Id.ToString(), true)
                    .AddField(_translations.GetText(guild.Id, LowerTypeModule, "warn_no"), nrWarnings + 1, true)
                    .AddField(_translations.GetText(guild.Id, LowerTypeModule, "moderator"), moderator, true)
                    .WithThumbnailUrl(user.GetRealAvatarUrl());
                if (!string.IsNullOrEmpty(reason))
                    embed.AddField(_translations.GetText(guild.Id, LowerTypeModule, "reason"), reason, true);

                if (guildDb != null)
                {
                    var modlog = await guild.GetTextChannelAsync(guildDb.ModLogChannel);
                    if (modlog != null)
                    {
                        var currentUser = await guild.GetCurrentUserAsync();
                        var preconditions = currentUser.GetPermissions(modlog);
                        if (preconditions.ViewChannel && preconditions.SendMessages)
                        {
                            await message.AddReactionAsync(new Emoji("✅"));
                            await modlog.SendMessageAsync(embed: embed.Build());
                        }
                        else
                        {
                            await channel.SendMessageAsync(embed: embed.Build());
                        }
                    }
                    else
                    {
                        await channel.SendMessageAsync(embed: embed.Build());
                    }
                }
                else
                {
                    await channel.SendMessageAsync(embed: embed.Build());
                }

                if (guildDb != null)
                {
                    if (nrWarnings + 1 >= guildDb.WarnsPunishment && guildDb.WarnsPunishment != 0)
                    {
                        db.RemoveRange(warnings);
                        await db.SaveChangesAsync();

                        return guildDb.PunishmentMethod;
                    }

                    await db.AddAsync(warning);
                    await db.SaveChangesAsync();
                }
            
                return null;
            }
        }
    }
}