using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Services;
using RiasBot.Database.Models;

namespace RiasBot.Modules.Administration
{
    public partial class Administration : RiasModule
    {
        private readonly DbService _db;

        public Administration(DbService db)
        {
            _db = db;
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetGreetAsync()
        {
            using (var db = _db.GetDbContext())
            {
                bool isGreetSet;
                var guildDb = db.Guilds.FirstOrDefault(g => g.GuildId == Context.Guild.Id);
                if (guildDb != null)
                {
                    if (!string.IsNullOrEmpty(guildDb.GreetMessage))
                    {
                        isGreetSet = !guildDb.Greet;
                        guildDb.Greet = isGreetSet;
                        guildDb.GreetChannel = Context.Channel.Id;
                        await db.SaveChangesAsync();
                    }
                    else
                    {
                        await ReplyErrorAsync("greet_message_not_set");
                        return;
                    }
                }
                else
                {
                    await ReplyErrorAsync("greet_message_not_set");
                    return;
                }

                if (isGreetSet)
                {
                    await ReplyConfirmationAsync("greet_enable", guildDb.GreetMessage);
                }
                else
                {
                    await ReplyConfirmationAsync("greet_disable");
                }
            }
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task GreetMessageAsync([Remainder] string message)
        {
            if (message.Length > 1500)
            {
                await ReplyErrorAsync("greet_message_length_limit");
                return;
            }

            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(g => g.GuildId == Context.Guild.Id);
                if (guildDb != null)
                {
                    guildDb.GreetMessage = message;
                    await db.SaveChangesAsync();
                }
                else
                {
                    var greetMsg = new GuildConfig { GuildId = Context.Guild.Id, GreetMessage = message };
                    await db.AddAsync(greetMsg);
                    await db.SaveChangesAsync();
                }
            }

            await ReplyConfirmationAsync("greet_message_set");
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetByeAsync()
        {
            bool isByeSet;

            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(g => g.GuildId == Context.Guild.Id);
                if (guildDb != null)
                {
                    if (!string.IsNullOrEmpty(guildDb.ByeMessage))
                    {
                        isByeSet = !guildDb.Bye;
                        guildDb.Bye = isByeSet;
                        guildDb.ByeChannel = Context.Channel.Id;
                        await db.SaveChangesAsync();
                    }
                    else
                    {
                        await ReplyErrorAsync("bye_message_not_set");
                        return;
                    }
                }
                else
                {
                    await ReplyErrorAsync("bye_message_not_set");
                    return;
                }

                if (isByeSet)
                {
                    await ReplyConfirmationAsync("bye_enable", guildDb.ByeMessage);
                }
                else
                {
                    await ReplyConfirmationAsync("bye_disable");
                }
            }
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ByeMessageAsync([Remainder] string message)
        {
            if (message.Length > 1500)
            {
                await ReplyErrorAsync("bye_message_length_limit");
                return;
            }

            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(g => g.GuildId == Context.Guild.Id);
                if (guildDb != null)
                {
                    guildDb.ByeMessage = message;
                    await db.SaveChangesAsync();
                }
                else
                {
                    var byeMsg = new GuildConfig { GuildId = Context.Guild.Id, ByeMessage = message };
                    await db.AddAsync(byeMsg);
                    await db.SaveChangesAsync();
                }
            }

            await ReplyConfirmationAsync("bye_message_set");
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetModLogAsync()
        {
            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
                if (guildDb != null)
                {
                    if (guildDb.ModLogChannel != Context.Channel.Id)
                    {
                        guildDb.ModLogChannel = Context.Channel.Id;
                        await db.SaveChangesAsync();
                        await ReplyConfirmationAsync("modlog_enable");
                    }
                    else
                    {
                        guildDb.ModLogChannel = 0;
                        await db.SaveChangesAsync();
                        await ReplyConfirmationAsync("modlog_disable");
                    }
                }
                else
                {
                    var modlog = new GuildConfig { GuildId = Context.Guild.Id, ModLogChannel = Context.Channel.Id};
                    await db.AddAsync(modlog);
                    await db.SaveChangesAsync();
                    await ReplyConfirmationAsync("modlog_enable");
                }
            }
        }
    }
}