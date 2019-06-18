using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using RiasBot.Commons;
using RiasBot.Services;

namespace RiasBot.Extensions
{
    public static class Extensions
    {
        public static ModuleInfo GetModule(this ModuleInfo module)
        {
            if (module.Parent != null) module = module.Parent;
            return module;
        }

        /// <summary>
        ///     Convert a TimeSpan to a fancy string format: 1d23h59m59s
        /// </summary>
        /// <param name="timeSpan"></param>
        public static string FancyTimeSpanString(this TimeSpan timeSpan)
        {
            var formatBuilder = new StringBuilder();

            if (timeSpan.Days > 0)
                formatBuilder.Append($"{timeSpan.Days}d");
            if (timeSpan.Hours > 0)
                formatBuilder.Append($" {timeSpan.Hours}h");
            if (timeSpan.Minutes > 0)
                formatBuilder.Append($" {timeSpan.Minutes}m");
            if (timeSpan.Hours > 0)
                formatBuilder.Append($" {timeSpan.Seconds}s");

            return formatBuilder.ToString();
        }

        /// <summary>
        ///     Convert a TimeSpan to a digital string format: HH:mm:ss
        /// </summary>
        /// <param name="timeSpan"></param>
        public static string DigitalTimeSpanString(this TimeSpan timeSpan)
        {
            var hoursInt = (int) timeSpan.TotalHours;
            var minutesInt = timeSpan.Minutes;
            var secondsInt = timeSpan.Seconds;

            var hours = hoursInt.ToString();
            var minutes = minutesInt.ToString();
            var seconds = secondsInt.ToString();

            if (hoursInt < 10)
                hours = "0" + hours;
            if (minutesInt < 10)
                minutes = "0" + minutes;
            if (secondsInt < 10)
                seconds = "0" + seconds;

            return hours + ":" + minutes + ":" + seconds;
        }

        /// <summary>
        ///     Convert a string to TimeSpan
        ///     Example 1mo2w3d4h5m6s to TimeSpan
        ///     It will return TimeSpan.Zero if the input doesn't match the Regex (mo w d m s)
        /// </summary>
        /// <param name="input"></param>
        public static TimeSpan ConvertToTimeSpan(string input)
        {
            var regex = new Regex(@"^(?:(?<months>\d)mo)?(?:(?<weeks>\d{1,2})w)?(?:(?<days>\d{1,2})d)?(?:(?<hours>\d{1,4})h)?(?:(?<minutes>\d{1,5})m)?(?:(?<seconds>\d{1,5})s)?$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);

            var match = regex.Match(input);

            if (match.Length == 0)
                return TimeSpan.Zero;

            var timeValues = new Dictionary<string, int>();

            foreach (var group in regex.GetGroupNames())
            {
                if (group == "0") continue;
                if (!int.TryParse(match.Groups[group].Value, out var value))
                {
                    timeValues[group] = 0;
                    continue;
                }

                timeValues[group] = value;
            }

            return new TimeSpan(30 * timeValues["months"] + 7 * timeValues["weeks"] + timeValues["days"],
                timeValues["hours"], timeValues["minutes"], timeValues["seconds"]);
        }

        public static uint HexToUint(this string hex)
        {
            hex = hex?.Replace("#", "");
            if (string.IsNullOrWhiteSpace(hex)) return 0xFFFFFF;
            return uint.TryParse(hex, NumberStyles.HexNumber, null, out var result) ? result : 0xFFFFFF;
        }

        public static bool TryParseEmbed(string json, out EmbedBuilder embed, IBotCredentials creds = null)
        {
            embed = new EmbedBuilder();
            try
            {
                var embedDeserialized = JsonConvert.DeserializeObject<JsonEmbed>(json);

                var author = embedDeserialized.Author;
                var title = embedDeserialized.Title;
                var description = embedDeserialized.Description;

                var colorString = embedDeserialized.Color;
                var thumbnail = embedDeserialized.Thumbnail;
                var image = embedDeserialized.Image;
                var fields = embedDeserialized.Fields;
                var footer = embedDeserialized.Footer;
                var timestamp = embedDeserialized.Timestamp;

                if (author != null)
                {
                    embed.WithAuthor(author);
                }

                if (!string.IsNullOrEmpty(title))
                {
                    embed.WithTitle(title);
                }

                if (!string.IsNullOrEmpty(description))
                {
                    if (creds != null)
                    {
                        description = description.Replace("[currency]", creds.Currency);
                        description = description.Replace("%currency%", creds.Currency);
                    }

                    embed.WithDescription(description);
                }

                if (!string.IsNullOrEmpty(colorString))
                {
                    colorString = colorString.Replace("#", "");
                    var color = HexToUint(colorString);
                    embed.WithColor(color);
                }

                if (!string.IsNullOrEmpty(thumbnail))
                    embed.WithThumbnailUrl(thumbnail);
                if (!string.IsNullOrEmpty(image))
                    embed.WithImageUrl(image);

                if (fields != null)
                {
                    foreach (var field in embedDeserialized.Fields)
                    {
                        var fieldName = field.Name;
                        var fieldValue = field.Value;
                        var fieldInline = field.Inline;

                        if (!string.IsNullOrEmpty(fieldName) && !string.IsNullOrEmpty(fieldValue))
                        {
                            embed.AddField(fieldName, fieldValue, fieldInline);
                        }
                    }
                }


                if (footer != null)
                {
                    embed.WithFooter(footer);
                }

                if (!timestamp.Equals(DateTimeOffset.MinValue))
                {
                    embed.WithTimestamp(timestamp);
                }
                else
                {
                    if (embedDeserialized.WithCurrentTimestamp)
                        embed.WithCurrentTimestamp();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Swap the two items of a list
        /// </summary>
        public static void Swap<T>(this IList<T> list, int indexA, int indexB)
        {
            var tmp = list[indexA];
            list[indexA] = list[indexB];
            list[indexB] = tmp;
        }

        /// <summary>
        ///     Shuffle the items of a list using a ThreadSafeRandom number generator
        /// </summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private static class ThreadSafeRandom
        {
            [ThreadStatic] private static Random _local;

            public static Random ThisThreadsRandom => _local = _local ?? new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId));
        }
    }
}