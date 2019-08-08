using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using ImageMagick;
using RiasBot.Commons.Attributes;
using RiasBot.Database.Models;
using RiasBot.Extensions;
using RiasBot.Services;

namespace RiasBot.Modules.Xp.Services
{
    [Service]
    public class XpService
    {
        private readonly DbService _db;
        private readonly ITranslations _tr;

        public XpService(DbService db, ITranslations tr)
        {
            _db = db;
            _tr = tr;
        }

        private readonly string _xpWhitePatternPath = Path.Combine(Environment.CurrentDirectory, "assets/images/xp/xp_white_pattern.png");
        private readonly string _xpBlackPatternPath = Path.Combine(Environment.CurrentDirectory, "assets/images/xp/xp_black_pattern.png");
        private readonly string _globalXpBarBgPath = Path.Combine(Environment.CurrentDirectory, "assets/images/xp/global_xp_bar_bg.png");
        private readonly string _guildXpBarBgPath = Path.Combine(Environment.CurrentDirectory, "assets/images/xp/guild_xp_bar_bg.png");

        private readonly string _aweryFontPath = Path.Combine(Environment.CurrentDirectory, "assets/fonts/Awery.ttf");
        private readonly string _meiryoFontPath = Path.Combine(Environment.CurrentDirectory, "assets/fonts/Meiryo.ttf");

        public async Task GiveXpUserMessageAsync(IGuildUser user)
        {
            using (var db = _db.GetDbContext())
            {
                var userDb = db.Users.FirstOrDefault(x => x.UserId == user.Id);

                if (userDb is null)
                {
                    var xp = new UserConfig { UserId = user.Id, Xp = 0, Level = 0, MessageDateTime = DateTime.UtcNow };
                    await db.Users.AddAsync(xp);
                    await db.SaveChangesAsync();

                    return;
                }

                var timeout = userDb.MessageDateTime;
                if (DateTime.UtcNow < timeout.Add(TimeSpan.FromMinutes(5))) return;

                var level = userDb.Level;
                var currentXp = userDb.Xp - (30 + level * 30) * level / 2;
                var nextLevelXp = (level + 1) * 30;

                if (currentXp + 5 >= nextLevelXp)
                {
                    userDb.Level++;
                }

                userDb.Xp += 5;
                userDb.MessageDateTime = DateTime.UtcNow;

                await db.SaveChangesAsync();
            }
        }

        public async Task GiveGuildXpUserMessageAsync(IGuildUser user, IMessageChannel channel, bool sendXpNotificationMessage)
        {
            using (var db = _db.GetDbContext())
            {
                var xpDb = db.XpSystem.FirstOrDefault(x => x.GuildId == user.GuildId && x.UserId == user.Id);

                if (xpDb is null)
                {
                    var xp = new XpSystem { GuildId = user.GuildId, UserId = user.Id, Xp = 0, Level = 0, MessageDateTime = DateTime.UtcNow };
                    await db.XpSystem.AddAsync(xp);
                    await db.SaveChangesAsync();

                    return;
                }

                var timeout = xpDb.MessageDateTime;
                if (DateTime.UtcNow < timeout.Add(TimeSpan.FromMinutes(5))) return;

                var level = xpDb.Level;
                var currentXp = xpDb.Xp - (30 + level * 30) * level / 2;
                var nextLevelXp = (level + 1) * 30;

                var levelUp = false;
                if (currentXp + 5 >= nextLevelXp)
                {
                    xpDb.Level++;
                    levelUp = true;
                }

                xpDb.Xp += 5;
                xpDb.MessageDateTime = DateTime.UtcNow;

                await db.SaveChangesAsync();

                var guildDb = db.Guilds.FirstOrDefault(g => g.GuildId == user.GuildId);
                var xpNotify = guildDb != null && guildDb.XpGuildNotification;
                if (levelUp)
                {
                    if (sendXpNotificationMessage && xpNotify)
                    {
                        await channel.SendConfirmationMessageAsync(_tr.GetText(user.GuildId, "xp", "guild_level_up", user, xpDb.Level));
                    }
                    await RewardUserRoleAsync(user, xpDb.Level);
                }
            }
        }

        private async Task RewardUserRoleAsync(IGuildUser user, int level)
        {
            var currentUser = await user.Guild.GetCurrentUserAsync();
            if (!currentUser.GuildPermissions.ManageRoles) return;

            using (var db = _db.GetDbContext())
            {
                var roleReward = db.XpRolesSystem.FirstOrDefault(r => r.GuildId == user.GuildId && r.Level == level);
                if (roleReward != null)
                {
                    var role = user.Guild.GetRole(roleReward.RoleId);
                    if (role != null)
                    {
                        if (currentUser.CheckRoleHierarchy(role) > 0)
                            await user.AddRoleAsync(role);
                    }
                    else
                    {
                        db.Remove(roleReward);
                        await db.SaveChangesAsync();
                    }
                }
            }

        }

        public async Task<MemoryStream> GenerateXpImageAsync(IGuildUser user, IRole highestRole)
        {
            //Init
            var accentColor = GetUserHighRoleColor(highestRole);

            using (var http = new HttpClient())
            using (var img = new MagickImage(accentColor, 500, 300))
            {
                var foreColor = (ImageExtensions.PerceivedBrightness(accentColor) > 130) ? MagickColors.Black : MagickColors.White;

            //Pattern
            if (foreColor == MagickColors.White)
            {
                using (var whitePatternTemp = new MagickImage(_xpWhitePatternPath))
                {
                    img.Draw(new DrawableComposite(0, 0, whitePatternTemp));
                }
            }
            else
            {
                using (var blackPatternTemp = new MagickImage(_xpBlackPatternPath))
                {
                    img.Draw(new DrawableComposite(0, 0, blackPatternTemp));
                }
            }

            //Avatar
            using (var avatar = await http.GetStreamAsync(user.GetRealAvatarUrl()))
            {
                using (var avatarTemp = new MagickImage(avatar))
                {
                    var size = new MagickGeometry(70, 70)
                    {
                        IgnoreAspectRatio = false,
                        FillArea = true
                    };
                    avatarTemp.Resize(size);
                    avatarTemp.Roundify();
                    img.Draw(new DrawableComposite(215, 10, avatarTemp));
                }
            }

            //Avatar Circle
            img.Draw(new Drawables().StrokeWidth(3).StrokeColor(foreColor).FillColor(MagickColors.Transparent).RoundRectangle(213, 8, 286, 81, 45, 45));

            //Username
            var usernameSettings = new MagickReadSettings()
            {
                BackgroundColor = MagickColors.Transparent,
                FillColor = foreColor,
                Font = _meiryoFontPath,
                Width = 400,
                Height = 35,
                TextGravity = Gravity.Center
            };

            using (var username = new MagickImage("caption:" + user, usernameSettings))
            {
                img.Draw(new DrawableComposite(50, 90, username));
            }

            //GlobalXp Box, GuildXp Box
            img.Draw(new Drawables().StrokeWidth(2).StrokeColor(foreColor).FillColor(MagickColors.Transparent).Rectangle(10, 130, 115, 205));
            img.Draw(new Drawables().StrokeWidth(2).StrokeColor(foreColor).FillColor(MagickColors.Transparent).Rectangle(10, 215, 115, 290));

            using (var globalXpBarBg = new MagickImage(_globalXpBarBgPath))
            using (var guildXpBarBg = new MagickImage(_guildXpBarBgPath))
            {
                img.Draw(new DrawableComposite(125, 130, globalXpBarBg));
                img.Draw(new DrawableComposite(125, 215, guildXpBarBg));
            }

            //Xp Info
            var xpInfo = GetXpInfo(user);

            img.Draw(new Drawables().FillColor(foreColor).Text(60, 150, "GLOBAL").TextAlignment(TextAlignment.Center).Font(_aweryFontPath).FontPointSize(17));
            img.Draw(new Drawables().FillColor(foreColor).Text(60, 234, "SERVER").TextAlignment(TextAlignment.Center).Font(_aweryFontPath).FontPointSize(17));
            img.Draw(new Drawables().FillColor(foreColor).Text(60, 170, $"LVL. {xpInfo.GlobalLevel}").TextAlignment(TextAlignment.Center).Font(_aweryFontPath).FontPointSize(15));
            img.Draw(new Drawables().FillColor(foreColor).Text(60, 254, $"LVL. {xpInfo.GuildLevel}").TextAlignment(TextAlignment.Center).Font(_aweryFontPath).FontPointSize(15));
            img.Draw(new Drawables().FillColor(foreColor).Text(60, 190, $"#{xpInfo.GlobalRank}").TextAlignment(TextAlignment.Center).Font(_aweryFontPath).FontPointSize(15));
            img.Draw(new Drawables().FillColor(foreColor).Text(60, 276, $"#{xpInfo.GuildRank}").TextAlignment(TextAlignment.Center).Font(_aweryFontPath).FontPointSize(15));

            //Xp Levels
            var xpBgColor = MagickColor.FromRgba((byte)accentColor.R, (byte)accentColor.G, (byte)accentColor.B, 127);

            var globalLevel = xpInfo.GlobalLevel;
            var globalCurrentXp = xpInfo.GlobalXp - (30 + globalLevel * 30) * globalLevel / 2;
            var globalNextLevelXp = (globalLevel + 1) * 30;

            img.Draw(new Drawables().FillColor(xpBgColor).Rectangle(125, 130, 125 + 350 * ((double)globalCurrentXp / globalNextLevelXp), 205));
            img.Draw(new Drawables().FillColor(MagickColors.Black).Text(300, 175, $"{globalCurrentXp}/{globalNextLevelXp}").TextAlignment(TextAlignment.Center).Font(_aweryFontPath).FontPointSize(17));

            var guildLevel = xpInfo.GuildLevel;
            var guildCurrentXp = xpInfo.GuildXp - (30 + guildLevel * 30) * guildLevel / 2;
            var guildNextLevelXp = (guildLevel + 1) * 30;

            img.Draw(new Drawables().FillColor(xpBgColor).Rectangle(125, 215, 125 + 350 * ((double)guildCurrentXp / guildNextLevelXp), 290));
            img.Draw(new Drawables().FillColor(MagickColors.Black).Text(300, 255, $"{guildCurrentXp}/{guildNextLevelXp}").TextAlignment(TextAlignment.Center).Font(_aweryFontPath).FontPointSize(17));

            var imageStream = new MemoryStream();
            img.Write(imageStream, MagickFormat.Png);
            imageStream.Position = 0;
            return imageStream;
            }
        }

        private XpInfo GetXpInfo(IGuildUser user)
        {
            var globalLevel = 0;
            var guildLevel = 0;
            var globalXp = 0;
            var guildXp = 0;
            var globalRank = 0;
            var guildRank = 0;

            using (var db = _db.GetDbContext())
            {
                var xpGlobalDb = db.Users.Select(x => x.Xp).OrderByDescending(x => x).ToList();
                var globalUserXp = db.Users.FirstOrDefault(u => u.UserId == user.Id);
                if (globalUserXp != null)
                {
                    globalLevel = globalUserXp.Level;
                    globalXp = globalUserXp.Xp;
                    globalRank = xpGlobalDb.IndexOf(globalUserXp.Xp) + 1;
                }

                var xpGuildDb = db.XpSystem.Where(x => x.GuildId == user.GuildId).Select(x => x.Xp).OrderByDescending(x => x).ToList();
                var guildUserXp = db.XpSystem.FirstOrDefault(x => x.GuildId == user.GuildId && x.UserId == user.Id);
                if (guildUserXp != null)
                {
                    guildLevel = guildUserXp.Level;
                    guildXp = guildUserXp.Xp;
                    guildRank = xpGuildDb.IndexOf(guildUserXp.Xp) + 1;
                }
            }

            return new XpInfo
            {
                GuildLevel = guildLevel,
                GlobalLevel = globalLevel,
                GlobalXp = globalXp,
                GuildXp = guildXp,
                GlobalRank = globalRank,
                GuildRank = guildRank
            };
        }

        private static MagickColor GetUserHighRoleColor(IRole role)
        {
            if (string.Equals(role.Name, "@everyone") || role.Color.Equals(Color.Default))
                return MagickColor.FromRgb(255, 255, 255);

            return MagickColor.FromRgb(role.Color.R, role.Color.G, role.Color.B);
        }

        private class XpInfo
        {
            public int GlobalLevel { get; set; }
            public int GuildLevel { get; set; }
            public int GlobalXp { get; set; }
            public int GuildXp { get; set; }
            public int GlobalRank { get; set; }
            public int GuildRank { get; set; }
        }
    }
}