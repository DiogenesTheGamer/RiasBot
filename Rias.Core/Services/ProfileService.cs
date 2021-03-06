using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Disqord;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rias.Core.Database;
using Rias.Core.Database.Entities;
using Rias.Core.Implementation;
using Color = Disqord.Color;

namespace Rias.Core.Services
{
    public class ProfileService : RiasService
    {
        private readonly AnimeService _animeService;
        private readonly HttpClient _httpClient;
        
        public ProfileService(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _animeService = serviceProvider.GetRequiredService<AnimeService>();
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        public string CurrencyLocalization = Localization.GamblingCurrency;
        
        private readonly string _defaultBackgroundPath = Path.Combine(Environment.CurrentDirectory, "assets/images/default_background.png");

        private readonly MagickColor _dark = MagickColor.FromRgb(36, 36, 36);
        private readonly MagickColor _darker = MagickColor.FromRgb(32, 32, 32);
        
        private readonly string _arialFontPath = Path.Combine(Environment.CurrentDirectory, "assets/fonts/ArialBold.ttf");
        private readonly string _meiryoFontPath = Path.Combine(Environment.CurrentDirectory, "assets/fonts/Meiryo.ttf");

        private readonly Color[] _colors =
        {
            new Color(255, 255, 255),    //white
            new Color(255, 0, 0),        //red
            new Color(0, 255, 0),        //green
            new Color(0, 255, 255),      //cyan/aqua
            new Color(255, 165, 0),      //orange
            new Color(255, 105, 180),    //hot pink
            new Color(255, 0, 255)       //magenta
        };
        
        public async Task<Stream> GenerateProfileImageAsync(CachedMember member)
        {
            var profileInfo = await GetProfileInfoAsync(member);

            var height = profileInfo.Waifus!.Count == 0 && profileInfo.SpecialWaifu is null ? 500 : 750;
            using var image = new MagickImage(_dark, 500, height);

            await AddBackgroundAsync(image, profileInfo);
            await AddAvatarAndUsernameAsync(image, member, profileInfo);
            AddInfo(image, member, profileInfo);

            if (profileInfo.Waifus!.Count != 0 || profileInfo.SpecialWaifu != null)
                await AddWaifusAsync(image, profileInfo);
            
            var imageStream = new MemoryStream();
            image.Write(imageStream, MagickFormat.Png);
            imageStream.Position = 0;
            return imageStream;
        }
        
        private void AddInfo(MagickImage image, CachedMember member, ProfileInfo profileInfo)
        {
            var settings = new MagickReadSettings
            {
                BackgroundColor = MagickColors.Transparent,
                FillColor = profileInfo.Color,
                Font = _arialFontPath,
                FontPointsize = 20,
                Width = 125,
                TextGravity = Gravity.Center
            };

            var segmentLength = 500 / 6;
            
            using var currencyImage = new MagickImage($"caption:{profileInfo.Currency}", settings);
            image.Draw(new DrawableComposite(segmentLength - (double) currencyImage.Width / 2, 280, CompositeOperator.Over, currencyImage));

            var waifusCount = profileInfo.Waifus!.Count;
            if (profileInfo.SpecialWaifu != null)
                waifusCount++;
            
            using var waifusImage = new MagickImage($"caption:{waifusCount}", settings);
            image.Draw(new DrawableComposite(segmentLength * 2 - (double) waifusImage.Width / 2, 280, CompositeOperator.Over, waifusImage));

            using var levelImage = new MagickImage($"caption:{profileInfo.Level}", settings);
            image.Draw(new DrawableComposite(segmentLength * 3 - (double) levelImage.Width / 2, 280, CompositeOperator.Over, levelImage));
            
            using var totalXpImage = new MagickImage($"caption:{profileInfo.Xp}", settings);
            image.Draw(new DrawableComposite(segmentLength * 4 - (double) totalXpImage.Width / 2, 280, CompositeOperator.Over, totalXpImage));
            
            using var rankImage = new MagickImage($"caption:{profileInfo.Rank}", settings);
            image.Draw(new DrawableComposite(segmentLength * 5 - (double) rankImage.Width / 2, 280, CompositeOperator.Over, rankImage));
            
            settings.FillColor = MagickColors.White;
            settings.FontPointsize = 15;

            var guildId = member.Guild.Id;
            using var currencyTextImage = new MagickImage($"caption:{GetText(guildId, CurrencyLocalization)}", settings);
            image.Draw(new DrawableComposite(segmentLength - (double) currencyTextImage.Width / 2, 315, CompositeOperator.Over, currencyTextImage));
            
            using var waifusTextImage = new MagickImage($"caption:{GetText(guildId, Localization.WaifuWaifus)}", settings);
            image.Draw(new DrawableComposite(segmentLength * 2 - (double) waifusTextImage.Width / 2, 315, CompositeOperator.Over, waifusTextImage));
            
            using var levelTextImage = new MagickImage($"caption:{GetText(guildId, Localization.XpLevel)}", settings);
            image.Draw(new DrawableComposite(segmentLength * 3 - (double) levelTextImage.Width / 2, 315, CompositeOperator.Over, levelTextImage));
            
            using var totalXpTextImage = new MagickImage($"caption:{GetText(guildId, Localization.XpTotalXp)}", settings);
            image.Draw(new DrawableComposite(segmentLength * 4 - (double) totalXpTextImage.Width / 2, 315, CompositeOperator.Over, totalXpTextImage));
            
            using var rankTextImage = new MagickImage($"caption:{GetText(guildId, Localization.CommonRank)}", settings);
            image.Draw(new DrawableComposite(segmentLength * 5 - (double) rankTextImage.Width / 2, 315, CompositeOperator.Over, rankTextImage));
            
            image.Draw(new Drawables()
                .RoundRectangle(50, 360, 450, 370, 5, 5)
                .FillColor(_darker));

            var currentXp = RiasUtilities.LevelXp(profileInfo.Level, profileInfo.Xp, XpService.XpThreshold);
            var nextLevelXp = (profileInfo.Level + 1) * 30;
            
            var xpBarLength = (double) currentXp  / nextLevelXp * 400;
            image.Draw(new Drawables()
                .RoundRectangle(50, 360, 50 + xpBarLength, 370, 5, 5)
                .FillColor(profileInfo.Color));

            settings.Width = 0;
            using var xpImage = new MagickImage($"caption:{currentXp}", settings);
            image.Draw(new DrawableComposite(50, 380, CompositeOperator.Over, xpImage));
            
            using var nextLevelXpImage = new MagickImage($"caption:{nextLevelXp}", settings);
            image.Draw(new DrawableComposite(450 - nextLevelXpImage.Width, 380, CompositeOperator.Over, nextLevelXpImage));

            image.Draw(new Drawables()
                .RoundRectangle(25, 415, 475, 495, 10, 10)
                .FillColor(_darker));

            settings.Font = _meiryoFontPath;
            settings.FontPointsize = 12;
            settings.TextGravity = Gravity.West;
            settings.Width = 440;
            
            using var bioImage = new MagickImage($"caption:{profileInfo.Biography}", settings);
            image.Draw(new DrawableComposite(30, 420, CompositeOperator.Over, bioImage));
        }
        
        public async Task<Stream> GenerateProfileBackgroundAsync(CachedUser user, Stream backgroundStream)
        {
            using var image = new MagickImage(_dark, 500, 500);
            
            using var tempBackgroundImage = new MagickImage(backgroundStream);
            tempBackgroundImage.Resize(new MagickGeometry
            {
                Width = 500,
                Height = 250,
                IgnoreAspectRatio = false,
                FillArea = true
            });
                
            using var backgroundImageLayer = new MagickImage(MagickColors.Black, 500, 250);
            backgroundImageLayer.Draw(new DrawableComposite(0, 0, CompositeOperator.Over, tempBackgroundImage));
            
            image.Draw(new DrawableComposite(0, 0, backgroundImageLayer));
            image.Draw(new Drawables().Rectangle(0, 0, 500, 250).FillColor(MagickColor.FromRgba(0, 0, 0, 128)));
            
            await AddAvatarAndUsernameAsync(image, user);
            
            var imageStream = new MemoryStream();
            image.Write(imageStream, MagickFormat.Png);
            imageStream.Position = 0;
            return imageStream;
        }

        public async Task<bool> CheckColorAsync(CachedUser user, Color color, int? tier = null)
        {
            if (Credentials.PatreonConfig is null)
                return true;
            
            if (user.Id == Credentials.MasterId)
                return true;

            if (_colors.Any(x => x == color))
                return true;
            
            if (tier.HasValue)
                return tier.Value >= PatreonService.ProfileColorTier;

            using var scope = RiasBot.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RiasDbContext>();
            tier = (await db.Patreon.FirstOrDefaultAsync(x => x.UserId == user.Id))?.Tier ?? 0;
            return tier >= PatreonService.ProfileColorTier;
        }
        
        private async Task AddBackgroundAsync(MagickImage image, ProfileInfo profileInfo)
        {
            if (string.IsNullOrEmpty(profileInfo.BackgroundUrl))
            {
                AddBackground(null, image, profileInfo);
                return;
            }
            
            try
            {
                using var response = await _httpClient.GetAsync(profileInfo.BackgroundUrl);
                if (!response.IsSuccessStatusCode)
                {
                    AddBackground(null, image, profileInfo);
                    return;
                }
                
                await using var backgroundStream = await response.Content.ReadAsStreamAsync();
                
                if (!(RiasUtilities.IsPng(backgroundStream) || RiasUtilities.IsJpg(backgroundStream)))
                {
                    AddBackground(null, image, profileInfo);
                    return;
                }

                backgroundStream.Position = 0;
                using var tempBackgroundImage = new MagickImage(backgroundStream);

                tempBackgroundImage.Resize(new MagickGeometry
                {
                    Width = 500,
                    Height = 250,
                    IgnoreAspectRatio = false,
                    FillArea = true
                });
                
                using var backgroundImageLayer = new MagickImage(MagickColors.Black, 500, 250);
                backgroundImageLayer.Draw(new DrawableComposite(0, 0, CompositeOperator.Over, tempBackgroundImage));
                AddBackground(backgroundImageLayer, image, profileInfo);
            }
            catch
            {
                AddBackground(null, image, profileInfo);
            }
        }
        
        private void AddBackground(MagickImage? backgroundImage, MagickImage image, ProfileInfo profileInfo)
        {
            using var background = backgroundImage ?? new MagickImage(_defaultBackgroundPath);
            var backgroundDrawable = new DrawableComposite
            (
                (double) (background.Width - 500) / 2,
                (double) (background.Height - 250) / 2,
                background
            );
            image.Draw(backgroundDrawable);
            
            var dim = (float) profileInfo.Dim / 100 * 255;
            image.Draw(new Drawables().Rectangle(0, 0, 500, 250).FillColor(MagickColor.FromRgba(0, 0, 0, (byte) dim)));
        }
        
        private async Task AddAvatarAndUsernameAsync(MagickImage image, CachedUser user, ProfileInfo? profileInfo = null)
        {
            await using var avatarStream = await _httpClient.GetStreamAsync(user.GetAvatarUrl());
            using var avatarImage = new MagickImage(avatarStream);
            avatarImage.Resize(new MagickGeometry
            {
                Width = 100,
                Height = 100
            });
            
            using var avatarLayer = new MagickImage(MagickColors.Transparent, 100, 100);
            avatarLayer.Draw(new Drawables().RoundRectangle(0, 0, avatarLayer.Width, avatarLayer.Height, 15, 15)
                .FillColor(MagickColors.White));
            avatarLayer.Composite(avatarImage, CompositeOperator.Atop);

            image.Draw(new DrawableComposite(30, 120, CompositeOperator.Over, avatarLayer));
            
            var usernameSettings = new MagickReadSettings
            {
                BackgroundColor = MagickColors.Transparent,
                FillColor = MagickColors.White,
                Font = _meiryoFontPath,
                FontWeight = FontWeight.Bold,
                Width = 250,
                Height = 50
            };
            
            using var usernameImage = new MagickImage($"caption:{user}", usernameSettings);
            image.Draw(new DrawableComposite(150, 130, CompositeOperator.Over, usernameImage));

            if (profileInfo is null)
                return;
            
            var badgeSettings = new MagickReadSettings
            {
                BackgroundColor = MagickColors.Transparent,
                FillColor = profileInfo.Color,
                Font = _arialFontPath,
                FontPointsize = 15
            };

            if (profileInfo.PatreonTier == 0 && user.Id != Credentials.MasterId)
                return;

            var xBadge = user.Id == Credentials.MasterId ? "Master" : "Supporter";

            if (profileInfo.Badges is null)
                profileInfo.Badges = new List<string>() {xBadge};
            else if (user.Id != Credentials.MasterId)
            {
                if (profileInfo.PatreonTier < PatreonService.ProfileThirdBadgeTier && profileInfo.Badges.Count > 2)
                    profileInfo.Badges.RemoveAt(2);
                if (profileInfo.PatreonTier < PatreonService.ProfileSecondBadgeTier && profileInfo.Badges.Count > 1) 
                    profileInfo.Badges.RemoveAt(1);
                if (profileInfo.PatreonTier < PatreonService.ProfileFirstBadgeTier && profileInfo.Badges.Count != 0)
                    profileInfo.Badges.RemoveAt(0);
            }
            
            if (profileInfo.Badges.Count == 0)
                profileInfo.Badges.Add(xBadge);

            var x = 150.0;
            foreach (var badge in profileInfo.Badges)
            {
                AddBadge(image, profileInfo, badgeSettings, badge, ref x);
            }
        }
        
        private void AddBadge(MagickImage image, ProfileInfo profileInfo, MagickReadSettings badgeSettings, string badge, ref double x)
        {
            using var badgeTextImage = new MagickImage($"label:{badge}", badgeSettings);

            var extraWidth = 30;
            var badgeWidth = badgeTextImage.Width + extraWidth;
            var badgeHeight = 30;
            
            using var badgeImage = new MagickImage(MagickColors.Transparent, badgeWidth, badgeHeight);
            badgeImage.Composite(badgeTextImage, Gravity.Center, CompositeOperator.Over);

            var badgeStrokeWidth = 2;
            badgeImage.Draw(new Drawables().Arc(0, 0, extraWidth - badgeStrokeWidth, extraWidth - badgeStrokeWidth, 90, 270)
                .FillColor(MagickColors.Transparent)
                .StrokeWidth(badgeStrokeWidth)
                .StrokeColor(profileInfo.Color));
            
            badgeImage.Draw(new Drawables().Arc(badgeWidth - extraWidth, 0, badgeWidth - badgeStrokeWidth, extraWidth - badgeStrokeWidth, 270, 90)
                .FillColor(MagickColors.Transparent)
                .StrokeWidth(badgeStrokeWidth)
                .StrokeColor(profileInfo.Color));

            var leftX = (double) extraWidth / 2;
            var rightX = badgeWidth - (double) extraWidth / 2;
            var lineY = badgeHeight - badgeStrokeWidth;

            badgeImage.Draw(new Drawables().Line(leftX, 0, rightX, 0)
                .FillColor(profileInfo.Color)
                .StrokeColor(profileInfo.Color)
                .StrokeWidth(badgeStrokeWidth));
            
            badgeImage.Draw(new Drawables().Line(leftX, lineY, rightX, lineY)
                .FillColor(profileInfo.Color)
                .StrokeColor(profileInfo.Color)
                .StrokeWidth(badgeStrokeWidth));
            
            image.Draw(new DrawableComposite(x, 190, CompositeOperator.Over, badgeImage));
            x += badgeWidth + 10;
        }
        
        private async Task AddWaifusAsync(MagickImage image, ProfileInfo profileInfo)
        {
            var waifuSize = new MagickGeometry
            {
                Width = 100,
                Height = 150,
                IgnoreAspectRatio = false,
                FillArea = true
            };
            
            var settings = new MagickReadSettings
            {
                BackgroundColor = MagickColors.Transparent,
                FillColor = MagickColors.White,
                Font = _arialFontPath,
                FontPointsize = 15,
                Width = 150,
                TextGravity = Gravity.Center
            };

            var waifuY = 530;
            
            if (profileInfo.Waifus!.Count != 0)
                await AddWaifu(image, profileInfo.Waifus![0], new Point(100, waifuY), waifuSize, settings);

            if (profileInfo.SpecialWaifu is null)
            {
                if (profileInfo.Waifus!.Count > 1)
                    await AddWaifu(image, profileInfo.Waifus[1], new Point(250, waifuY), waifuSize, settings);
                if (profileInfo.Waifus!.Count > 2)
                    await AddWaifu(image, profileInfo.Waifus[2], new Point(400, waifuY), waifuSize, settings);
            }
            else
            {
                if (profileInfo.Waifus!.Count > 1)
                    await AddWaifu(image, profileInfo.Waifus[1], new Point(400, waifuY), waifuSize, settings);
                
                settings.FillColor = profileInfo.Color;
                await AddWaifu(image, profileInfo.SpecialWaifu, new Point(250, waifuY), waifuSize, settings);
            }
        }
        
        private async Task AddWaifu(MagickImage image, IWaifusEntity waifu, Point position, MagickGeometry waifuSize, MagickReadSettings settings)
        {
            await using var waifuStream = await GetWaifuStreamAsync(waifu, waifu.IsSpecial);
            if (waifuStream != null)
            {
                using var waifuImage = new MagickImage(waifuStream);
                waifuImage.Resize(waifuSize);

                var waifuX = position.X - waifuSize.Width / 2;
                using var waifuImageLayer = new MagickImage(MagickColors.Black, waifuSize.Width, waifuSize.Height);
                var waifuDrawable = new DrawableComposite
                (
                    (double) (waifuSize.Width - waifuImage.Width) / 2,
                    (double) (waifuSize.Height - waifuImage.Height) / 2,
                    CompositeOperator.Over,
                    waifuImage
                );
                waifuImageLayer.Draw(waifuDrawable);
                
                image.Draw(new DrawableComposite(waifuX, position.Y, waifuImageLayer));
                image.Draw(new Drawables()
                    .RoundRectangle(waifuX - 1, position.Y - 1, waifuX + waifuSize.Width + 1, position.Y + waifuSize.Height + 1, 5, 5)
                    .StrokeWidth(2)
                    .StrokeColor(settings.FillColor)
                    .FillColor(MagickColors.Transparent));
            }
            
            using var waifuNameImage = new MagickImage($"caption:{waifu.Name}", settings);
            image.Draw(new DrawableComposite(position.X - waifuNameImage.Width / 2, 690, CompositeOperator.Over, waifuNameImage));
        }
        
        private async Task<Stream?> GetWaifuStreamAsync(IWaifusEntity waifu, bool useCustomImage = false)
        {
            try
            {
                var imageUrl = waifu.ImageUrl;
                if (useCustomImage && waifu is WaifusEntity waifus && !string.IsNullOrEmpty(waifus.CustomImageUrl))
                    imageUrl = waifus.CustomImageUrl;

                using var response = await _httpClient.GetAsync(imageUrl);
                if (response.IsSuccessStatusCode)
                {
                    await using var waifuStream = await response.Content.ReadAsStreamAsync();
                    if ((RiasUtilities.IsPng(waifuStream) || RiasUtilities.IsJpg(waifuStream)))
                    {
                        waifuStream.Position = 0;
                        var ms = new MemoryStream();
                        await waifuStream.CopyToAsync(ms);
                        ms.Position = 0;
                        return ms;
                    }
                }
            }
            catch
            {
                // ignored
            }

            if (!(waifu is WaifusEntity waifuDb))
                return null;

            if (useCustomImage)
                return await GetWaifuStreamAsync(waifu);

            if (!waifuDb.CharacterId.HasValue)
                return null;

            var aniListCharacter = await _animeService.GetAniListCharacterById(waifuDb.CharacterId.Value);
            if (aniListCharacter is null)
                return null;

            var characterImage = aniListCharacter.Image.Large;
            if (!string.IsNullOrEmpty(characterImage))
            {
                await _animeService.SetCharacterImageUrlAsync(aniListCharacter.Id, characterImage);
                await using var characterStream = await _httpClient.GetStreamAsync(characterImage);
                var ms = new MemoryStream();
                await characterStream.CopyToAsync(ms);
                ms.Position = 0;
                return ms;
            }
                
            return null;
        }

        private async Task<ProfileInfo> GetProfileInfoAsync(CachedMember member)
        {
            using var scope = RiasBot.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RiasDbContext>();
            var userDb = await db.Users.FirstOrDefaultAsync(x => x.UserId == member.Id);
            var profileDb = await db.Profile.FirstOrDefaultAsync(x => x.UserId == member.Id);
            var patreonDb = Credentials.PatreonConfig != null ? await db.Patreon.FirstOrDefaultAsync(x => x.UserId == member.Id) : null;

            var waifus = db.Waifus
                .Include(x => x.Character)
                .Include(x => x.CustomCharacter)
                .Where(x => x.UserId == member.Id)
                .AsEnumerable()
                .Cast<IWaifusEntity>()
                .ToList();
            
            waifus.AddRange(db.CustomWaifus.Where(x => x.UserId == member.Id));

            var xp = userDb?.Xp ?? 0;

            var color = profileDb?.Color != null ? new Color(RiasUtilities.HexToInt(profileDb.Color[1..]).GetValueOrDefault()) : _colors[0];
            if (color != _colors[0] && !await CheckColorAsync(member, color, patreonDb?.Tier ?? 0))
                color = _colors[0];

            var specialWaifu = waifus.FirstOrDefault(x => x.IsSpecial);
            waifus.Remove(specialWaifu);
            
            return new ProfileInfo
            {
                Currency = userDb?.Currency ?? 0,
                Xp = xp,
                Level = RiasUtilities.XpToLevel(xp, XpService.XpThreshold),
                Rank = userDb != null
                    ? (await db.Users.Select(x => x.Xp)
                          .OrderByDescending(y => y)
                          .ToListAsync())
                      .IndexOf(userDb.Xp) + 1
                    : 0,
                BackgroundUrl = profileDb?.BackgroundUrl,
                Dim = profileDb?.BackgroundDim ?? 50,
                Biography = profileDb?.Biography ?? GetText(member.Guild.Id, Localization.ProfileDefaultBiography),
                Color = new MagickColor(color.ToString()),
                Badges = profileDb?.Badges?.Where(x => !string.IsNullOrEmpty(x)).ToList(),
                Waifus = waifus.Where(x => x.Position != 0)
                    .OrderBy(x => x.Position)
                    .Concat(waifus.Where(x=> x.Position == 0))
                    .ToList(),
                SpecialWaifu = specialWaifu,
                PatreonTier = patreonDb?.Tier ?? 0
            };
        }
        
        private class ProfileInfo
        {
            public int Currency { get; set; }
            public int Xp { get; set; }
            public int Level { get; set; }
            public int Rank { get; set; }
            public string? BackgroundUrl { get; set; }
            public int Dim { get; set; }
            public string? Biography { get; set; }
            public MagickColor? Color { get; set; }
            public IList<string>? Badges { get; set; }
            public IList<IWaifusEntity>? Waifus { get; set; }
            public IWaifusEntity? SpecialWaifu { get; set; }
            public int PatreonTier { get; set; }
        }
    }
}