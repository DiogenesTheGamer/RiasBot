using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using ImageMagick;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Services;
using RiasBot.Database.Models;

namespace RiasBot.Modules.Utility
{
    public partial class Utility : RiasModule
    {
        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly CommandHandler _ch;

        public Utility(CommandHandler ch, IBotCredentials creds, DbService db)
        {
            _ch = ch;
            _creds = creds;
            _db = db;
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        public async Task PrefixAsync([Remainder]string newPrefix = null)
        {
            if (string.IsNullOrEmpty(newPrefix))
            {
                await ReplyConfirmationAsync("prefix_is", _ch.GetPrefix(Context.Guild));
                return;
            }

            var user = (IGuildUser)Context.User;
            if (user.GuildPermissions.Administrator)
            {
                var oldPrefix = _ch.GetPrefix(Context.Guild);

                using (var db = _db.GetDbContext())
                {
                    var guild = db.Guilds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);

                    if (guild != null)
                    {
                        guild.Prefix = newPrefix;
                    }
                    else
                    {
                        var prefix = new GuildConfig { GuildId = Context.Guild.Id, Prefix = newPrefix };
                        await db.Guilds.AddAsync(prefix);
                    }

                    await db.SaveChangesAsync();
                }
                
                await ReplyConfirmationAsync("prefix_changed", oldPrefix, newPrefix);
            }
            else
            {
                await ReplyErrorAsync("prefix_missing_permission");
            }
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        public async Task InviteAsync()
        {
            if (!string.IsNullOrEmpty(_creds.Invite))
                await ReplyConfirmationAsync("invite_info", _creds.Invite);
            else
                await ReplyErrorAsync("invite_link_missing");
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        public async Task DonateAsync()
        {
            await ReplyConfirmationAsync("donate_info");
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task Ping()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            await Context.Channel.TriggerTypingAsync();
            stopwatch.Stop();
            await Context.Channel.SendConfirmationMessageAsync(":ping_pong:" + stopwatch.ElapsedMilliseconds + "ms");
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        public async Task ChooseAsync([Remainder]string list)
        {
            var choices = list.Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries);

            var rnd = new Random((int)DateTime.UtcNow.Ticks);
            var choice = rnd.Next(choices.Length);
            await ReplyConfirmationAsync("chose", choices[choice].Trim());
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RateLimit(3, 5, RateLimitType.GuildUser)]
        public async Task ColorAsync([Remainder]string color)
        {
            color = color.Replace("#", "");
            if (int.TryParse(color.Substring(0, 2), NumberStyles.HexNumber, null, out var redColor) &&
                int.TryParse(color.Substring(2, 2), NumberStyles.HexNumber, null, out var greenColor) &&
                int.TryParse(color.Substring(4, 2), NumberStyles.HexNumber, null, out var blueColor))
            {
                var red = Convert.ToByte(redColor);
                var green = Convert.ToByte(greenColor);
                var blue = Convert.ToByte(blueColor);

                using(var img = new MagickImage(MagickColor.FromRgb(red, green, blue), 100, 100))
                using (var imageStream = new MemoryStream())
                {
                    img.Write(imageStream, MagickFormat.Png);
                    imageStream.Position = 0;
                    await Context.Channel.SendFileAsync(imageStream, $"#{color}.png");
                }
            }
            else
            {
                try
                {
                    var magickColor = new MagickColor(color.Replace(" ", ""));
                    using (var img = new MagickImage(magickColor, 100, 100))
                    using (var imageStream = new MemoryStream())
                    {
                        img.Write(imageStream, MagickFormat.Png);
                        imageStream.Position = 0;
                        await Context.Channel.SendFileAsync(imageStream, $"{color}.png");    
                    }
                }
                catch
                {
                    await ReplyErrorAsync("color_not_valid");
                }
            }
        }
    }
}

//TODO: Add setlanguage command