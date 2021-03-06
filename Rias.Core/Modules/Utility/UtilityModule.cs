using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disqord;
using Humanizer;
using Humanizer.Localisation;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using NCalc;
using Qmmands;
using Rias.Core.Attributes;
using Rias.Core.Commons;
using Rias.Core.Database.Entities;
using Rias.Core.Implementation;
using Rias.Core.Models;
using Rias.Core.Services;

namespace Rias.Core.Modules.Utility
{
    [Name("Utility")]
    public partial class UtilityModule : RiasModule
    {
        private readonly UnitsService _unitsService;
        public UtilityModule(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _unitsService = serviceProvider.GetRequiredService<UnitsService>();
        }
        
        [Command("prefix"), Context(ContextType.Guild)]
        public async Task PrefixAsync()
        => await ReplyConfirmationAsync(Localization.UtilityPrefixIs, Context.Prefix);
        
        [Command("setprefix"), Context(ContextType.Guild),
         UserPermission(Permission.Administrator)]
        public async Task SetPrefixAsync([Remainder] string prefix)
        {
            if (prefix.Length > 15)
            {
                await ReplyErrorAsync(Localization.UtilityPrefixLimit, 15);
                return;
            }
            
            var guildDb = await DbContext.GetOrAddAsync(x => x.GuildId == Context.Guild!.Id, () => new GuildsEntity {GuildId = Context.Guild!.Id});
            guildDb.Prefix = prefix;

            await DbContext.SaveChangesAsync();
            await ReplyConfirmationAsync(Localization.UtilityPrefixChanged, Context.Prefix, prefix);
        }
        
        [Command("languages"), Context(ContextType.Guild)]
        public async Task LanguagesAsync()
        {
            var embed = new LocalEmbedBuilder
            {
                Color = RiasUtilities.ConfirmColor,
                Title = GetText(Localization.UtilityLanguages),
                Description = string.Join("\n", Localization.Locales.Select(x => $"{x.Language} ({x.Locale})")),
                Footer = new LocalEmbedFooterBuilder().WithText(GetText(Localization.UtilityLanguagesFooter, Credentials.Prefix))
            };

            await ReplyAsync(embed);
        }
        
        [Command("setlanguage"), Context(ContextType.Guild)]
        public async Task SetLanguageAsync(string language)
        {
            var (locale, lang) = Localization.Locales.FirstOrDefault(x =>
                string.Equals(x.Locale, language, StringComparison.OrdinalIgnoreCase) || x.Language.StartsWith(language, StringComparison.OrdinalIgnoreCase));
            
            if (string.IsNullOrEmpty(locale))
            {
                await ReplyErrorAsync(Localization.UtilityLanguageNotFound);
                return;
            }

            if (string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase))
            {
                Localization.RemoveGuildLocale(Context.Guild!.Id);
                
                var guildDb = await DbContext.GetOrAddAsync(x => x.GuildId == Context.Guild.Id, () => new GuildsEntity() {GuildId = Context.Guild.Id});
                guildDb.Locale = null;
            }
            else
            {
                Localization.SetGuildLocale(Context.Guild!.Id, locale.ToLowerInvariant());
                
                var guildDb = await DbContext.GetOrAddAsync(x => x.GuildId == Context.Guild.Id, () => new GuildsEntity() {GuildId = Context.Guild.Id});
                guildDb.Locale = locale.ToLower();
            }
            
            await DbContext.SaveChangesAsync();
            await ReplyConfirmationAsync(Localization.UtilityLanguageSet, $"{lang} ({locale})");
        }
        
        [Command("invite")]
        public async Task InviteAsync()
        {
            if (!string.IsNullOrEmpty(Credentials.Invite))
                await ReplyConfirmationAsync(Localization.UtilityInviteInfo, Credentials.Invite);
        }
        
        [Command("patreon")]
        public async Task DonateAsync()
        {
            if (!string.IsNullOrEmpty(Credentials.Patreon))
                await ReplyConfirmationAsync(Localization.UtilityPatreonInfo, Credentials.Patreon, Credentials.Currency);
        }
        
        [Command("patrons")]
        public async Task PatronsAsync()
        {
            var patrons = await DbContext.GetOrderedListAsync<PatreonEntity, int>(
                x => x.PatronStatus == PatronStatus.ActivePatron && x.Tier > 0,
                y => y.AmountCents, true);
            
            if (patrons.Count == 0)
            {
                await ReplyErrorAsync(Localization.UtilityNoPatrons, Credentials.Patreon, Credentials.Currency);
                return;
            }

            await SendPaginatedMessageAsync(patrons, 15, (items, index) => new LocalEmbedBuilder
            {
                Color = RiasUtilities.ConfirmColor,
                Title = GetText(Localization.UtilityAllPatrons),
                Description = string.Join("\n", items.Select(p => $"{++index}. {RiasBot.GetUser(p.UserId)?.ToString() ?? p.UserId.ToString()}"))
            });
        }
        
        [Command("vote")]
        public async Task VoteAsync()
        {
            await ReplyConfirmationAsync(Localization.UtilityVoteInfo, $"{Credentials.DiscordBotList}/vote", Credentials.Currency);
        }
        
        [Command("votes")]
        public async Task VotesAsync(TimeSpan? timeSpan = null)
        {
            timeSpan ??= TimeSpan.FromHours(12);

            var locale = Localization.GetGuildLocale(Context.Guild?.Id);

            var lowestTime = TimeSpan.FromMinutes(1);
            if (timeSpan < lowestTime)
            {
                await ReplyErrorAsync(Localization.UtilityTimeLowest, lowestTime.Humanize(1, new CultureInfo(locale)));
                return;
            }
            
            var now = DateTime.UtcNow;
            var highestTime = now.AddMonths(1) - now;
            if (timeSpan > highestTime)
            {
                await ReplyErrorAsync(Localization.UtilityTimeHighest, highestTime.Humanize(1, new CultureInfo(locale), maxUnit: TimeUnit.Month));
                return;
            }
            
            var dateAdded = DateTime.UtcNow - timeSpan;
            var votesGroup = (await DbContext.GetListAsync<VotesEntity>(x => x.DateAdded >= dateAdded))
                .GroupBy(x => x.UserId)
                .ToList();

            var index = 0;
            var votesList = (from votes in votesGroup
                let user = RiasBot.GetUser(votes.Key)
                select $"{++index}. {(user != null ? user.ToString() : votes.Key.ToString())} | {GetText(Localization.UtilityVotes)}: {votes.Count()}").ToList();

            if (votesList.Count == 0)
            {
                await ReplyErrorAsync(Localization.UtilityNoVotes);
                return;
            }

            var timeSpanHumanized = timeSpan.Value.Humanize(5, new CultureInfo(Localization.GetGuildLocale(Context.Guild?.Id)), TimeUnit.Month);
            await SendPaginatedMessageAsync(votesList, 15, (items, _) => new LocalEmbedBuilder
            {
                Color = RiasUtilities.ConfirmColor,
                Title = GetText(Localization.UtilityAllVotes, timeSpanHumanized),
                Description = string.Join("\n", items)
            });
        }
        
        [Command("ping")]
        public async Task PingAsync()
        {
            var sw = Stopwatch.StartNew();
            await Context.Channel.TriggerTypingAsync();
            sw.Stop();

            await ReplyConfirmationAsync(Localization.UtilityPingInfo, RiasBot.Latency.GetValueOrDefault().TotalMilliseconds.ToString("F3"), sw.ElapsedMilliseconds);
        }

        [Command("choose")]
        public async Task ChooseAsync([Remainder] string list)
        {
            var choices = list.Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries);

            var choice = new Random().Next(choices.Length);
            await ReplyConfirmationAsync(Localization.UtilityChose, choices[choice].Trim());
        }
        
        [Command("color"),
         Cooldown(1, 3, CooldownMeasure.Seconds, BucketType.User)]
        public async Task ColorAsync([Remainder] Color color)
        {
            var currentMember = Context.Guild?.CurrentMember;
            if (currentMember != null && !currentMember.Permissions.AttachFiles)
            {
                await ReplyErrorAsync(Localization.UtilityColorNoAttachFilesPermission);
                return;
            }

            if (currentMember != null && !currentMember.GetPermissionsFor((CachedTextChannel) Context.Channel).AttachFiles)
            {
                await ReplyErrorAsync(Localization.UtilityColorNoAttachFilesChannelPermission);
                return; 
            }
            
            var hexColor = color.ToString();
            var magickColor = new MagickColor(hexColor);
            var hsl = ColorHSL.FromMagickColor(magickColor);
            var yuv = ColorYUV.FromMagickColor(magickColor);
            var cmyk = ColorCMYK.FromMagickColor(magickColor);

            var ushortMax = (double) ushort.MaxValue;
            var byteMax = byte.MaxValue;
            var colorDetails = new StringBuilder()
                .Append($"**Hex:** {hexColor}").AppendLine()
                .Append($"**Rgb:** {magickColor.R / ushortMax * byteMax} {magickColor.G / ushortMax * byteMax} {magickColor.B / ushortMax * byteMax}").AppendLine()
                .Append($"**Hsl:** {hsl.Hue:F2}% {hsl.Saturation:F2}% {hsl.Lightness:F2}%").AppendLine()
                .Append($"**Yuv:** {yuv.Y:F2} {yuv.U:F2} {yuv.V:F2}").AppendLine()
                .Append($"**Cmyk:** {cmyk.C / ushortMax * byteMax} {cmyk.M / ushortMax * byteMax} {cmyk.Y / ushortMax * byteMax} {cmyk.K / ushortMax * byteMax}");

            var fileName = $"{color.RawValue.ToString()}.png";
            var embed = new LocalEmbedBuilder()
                .WithColor(color)
                .WithDescription(colorDetails.ToString())
                .WithImageUrl($"attachment://{fileName}");
            
            using var magickImage = new MagickImage(MagickColor.FromRgb(color.R, color.G, color.B), 300, 300);
            var image = new MemoryStream();
            magickImage.Write(image, MagickFormat.Png);
            image.Position = 0;
            await Context.Channel.SendMessageAsync(new LocalAttachment(image, fileName), embed: embed.Build());
        }
        
        [Command("calculator")]
        public async Task CalculatorAsync([Remainder] string expression)
        {
            var expr = new Expression(expression, EvaluateOptions.IgnoreCase);
            expr.EvaluateParameter += ExpressionEvaluateParameter;

            var embed = new LocalEmbedBuilder
            {
                Color = RiasUtilities.ConfirmColor,
                Title = GetText(Localization.UtilityCalculator)
            }.AddField(GetText(Localization.UtilityExpression), expression);

            try
            {
                var result = expr.Evaluate();
                embed.AddField(GetText(Localization.UtilityResult), result);
            }
            catch
            {
                embed.AddField(GetText(Localization.UtilityResult), !string.IsNullOrEmpty(expr.Error) ? expr.Error : GetText(Localization.UtilityExpressionFailed));
            }

            await ReplyAsync(embed);
        }

        [Command("converter"),
        Priority(3)]
        public async Task ConverterAsync(double value, string unit1, string unit2)
            => await ConverterAsync(unit1, unit2, value);

        [Command("converter"),
        Priority(2)]
        public async Task ConverterAsync(string unit1Name, string unit2Name, double value)
        {
            var units1 = _unitsService.GetUnits(unit1Name).ToList();
            if (units1.Count == 0)
            {
                await ReplyErrorAsync(Localization.UtilityUnitNotFound, unit1Name);
                return;
            }
            
            var units2 = _unitsService.GetUnits(unit2Name).ToList();
            if (units2.Count == 0)
            {
                await ReplyErrorAsync(Localization.UtilityUnitNotFound, unit2Name);
                return;
            }

            Unit? unit1 = null;
            Unit? unit2 = null;
            
            foreach (var u1 in units1.TakeWhile(u1 => unit1 is null && unit2 is null))
            {
                var u2 = units2.FirstOrDefault(x => string.Equals(x.Category.Name, u1.Category.Name));
                if (u2 is null)
                    continue;
                
                unit1 = u1;
                unit2 = u2;
                break;
            }

            if (unit1 is null || unit2 is null)
            {
                await ReplyErrorAsync(Localization.UtilityUnitsNotCompatible,
                    $"{units1[0].Name.Singular} ({units1[0].Category.Name})",
                    $"{units2[0].Name.Singular} ({units2[0].Category.Name})");
                
                return;
            }

            var result = _unitsService.Convert(unit1, unit2, value);

            unit1Name = value == 1 ? unit1.Name.Singular! : unit1.Name.Plural!;
            unit2Name = result == 1 ? unit2.Name.Singular! : unit2.Name.Plural!;
            
            var embed = new LocalEmbedBuilder
            {
                Color = RiasUtilities.ConfirmColor,
                Title = GetText(Localization.UtilityConverter),
                Description = $"**[{unit1.Category.Name}]**\n" +
                              $"{value} {unit1Name} = {Format(result)} {unit2Name}"
            };

            await ReplyAsync(embed);
        }
        
        [Command("converter"),
         Priority(1)]
        public async Task ConverterAsync(string category, double value, string unit1, string unit2)
            => await ConverterAsync(category, unit1, unit2, value);

        [Command("converter"),
         Priority(0)]
        public async Task ConverterAsync(string category, string unit1Name, string unit2Name, double value)
        {
            var unitsCategory = _unitsService.GetUnitsByCategory(category);
            if (unitsCategory is null)
            {
                await ReplyErrorAsync(Localization.UtilityUnitsCategoryNotFound, category);
                return;
            }

            var units1 = _unitsService.GetUnits(unit1Name).ToList();
            if (units1.Count == 0)
            {
                await ReplyErrorAsync(Localization.UtilityUnitNotFound, unit1Name);
                return;
            }

            var units2 = _unitsService.GetUnits(unit2Name).ToList();
            if (units2.Count == 0)
            {
                await ReplyErrorAsync(Localization.UtilityUnitNotFound, unit2Name);
                return;
            }

            var unit1 = units1.FirstOrDefault(x => string.Equals(x.Category.Name, unitsCategory.Name));
            if (unit1 is null)
            {
                await ReplyErrorAsync(Localization.UtilityUnitNotFoundInCategory, unit1Name, unitsCategory.Name);
                return;
            }
            
            var unit2 = units2.FirstOrDefault(x => string.Equals(x.Category.Name, unitsCategory.Name));
            if (unit2 is null)
            {
                await ReplyErrorAsync(Localization.UtilityUnitNotFoundInCategory, unit2Name, unitsCategory.Name);
                return;
            }

            var result = _unitsService.Convert(unit1, unit2, value);

            unit1Name = value == 1 ? unit1.Name.Singular! : unit1.Name.Plural!;
            unit2Name = result == 1 ? unit2.Name.Singular! : unit2.Name.Plural!;

            var embed = new LocalEmbedBuilder
            {
                Color = RiasUtilities.ConfirmColor,
                Title = GetText(Localization.UtilityConverter),
                Description = $"**[{unit1.Category.Name}]**\n" +
                              $"{value} {unit1Name} = {Format(result)} {unit2Name}"
            };

            await ReplyAsync(embed);
        }

        [Command("converterlist")]
        public async Task ConverterList(string? category = null)
        {
            if (category is null)
            {
                await SendPaginatedMessageAsync(_unitsService.GetAllUnits().OrderBy(x => x.Name).ToList(), 15,
                    (items, index) => new LocalEmbedBuilder
                    {
                        Color = RiasUtilities.ConfirmColor,
                        Title = GetText(Localization.UtilityAllUnitsCategories),
                        Description = string.Join("\n", items.Select(x => $"{++index}. {x.Name}")),
                        Footer = new LocalEmbedFooterBuilder().WithText(GetText(Localization.UtilityConvertListFooter, Context.Prefix))
                    });
                
                return;
            }

            var units = _unitsService.GetUnitsByCategory(category);
            if (units is null)
            {
                await ReplyErrorAsync(Localization.UtilityUnitsCategoryNotFound, category);
                return;
            }

            await SendPaginatedMessageAsync(units.Units.ToList(), 15, (items, index) => new LocalEmbedBuilder
            {
                Color = RiasUtilities.ConfirmColor,
                Title = GetText(Localization.UtilityCategoryAllUnits, category),
                Description = string.Join("\n", items.Select(x =>
                {
                    var abbreviations = x.Name.Abbreviations?.ToList();
                    var abbreviationsString = string.Empty;
                    if (abbreviations != null && abbreviations.Count != 0)
                    {
                        abbreviationsString = $" [{string.Join(", ", abbreviations)}]";
                    }
                    
                    return $"{++index}. {x.Name.Singular}{abbreviationsString}";
                }))
            });
        }
        
        private static void ExpressionEvaluateParameter(string name, ParameterArgs args)
        {
            args.Result = name.ToLowerInvariant() switch
            {
                "pi" => Math.PI,
                "e" => Math.E,
                _ => default
            };
        }

        /// <summary>
        /// If the number is higher than 1 or lower than -1 then it rounds on the first 2 digits.
        /// Otherwise it rounds after the number of leading zeros.<br/>
        /// Ex: 0.00234 = 0.0023, 0.0000234 = 2.3E-5, 1.23 = 1.23, 1E20 = 1E+20
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        private string Format(double d)
        {
            if (Math.Abs(d) >= 1)
            {
                d = Math.Round(d, 2);
                return d < 1E9 ? d.ToString(CultureInfo.InvariantCulture) : d.ToString("0.##E0");
            }

            var fractionPart = d % 1.0;
            if (fractionPart == 0)
                return d.ToString(CultureInfo.InvariantCulture);
            
            var count = -2;
            while (fractionPart < 10 && count < 7)
            {
                fractionPart *= 10;
                count++;
            }
            
            return count < 7 ? Math.Round(d, count + 2).ToString(CultureInfo.InvariantCulture) : d.ToString("0.##E0");
        }
    }
}