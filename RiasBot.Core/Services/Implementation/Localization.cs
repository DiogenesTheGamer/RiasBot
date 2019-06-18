using System;
using System.Collections.Concurrent;
using System.IO;
using Discord;
using Newtonsoft.Json;
using RiasBot.Commons.Attributes;

namespace RiasBot.Services.Implementation
{
    [Service(typeof(ILocalization))]
    public class Localization : ILocalization
    {
        private static readonly ConcurrentDictionary<string, CommandData> CommandData =
            JsonConvert.DeserializeObject<ConcurrentDictionary<string, CommandData>>(
                File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "assets/commands_strings.json")));

        private readonly ConcurrentDictionary<ulong, string> _guildLocales = new ConcurrentDictionary<ulong, string>();

        public Localization(DbService db)
        {
            using (var dbContext = db.GetDbContext())
            {
                foreach (var guildDb in dbContext.Guilds)
                {
                    var locale = guildDb.Locale;
                    if (!string.IsNullOrEmpty(locale))
                    {
                        SetGuildLocale(guildDb.GuildId, guildDb.Locale);
                    }
                }
            }
        }

        public static CommandData LoadCommand(string key)
        {
            key = key.Replace("async", "", StringComparison.InvariantCultureIgnoreCase);
            CommandData.TryGetValue(key, out var command);

            if (command == null)
                return new CommandData
                {
                    Command = key,
                    Description = key,
                    Remarks = new[] {key}
                };

            return command;
        }

        public void SetGuildLocale(ulong guildId, string locale)
        {
            _guildLocales.AddOrUpdate(guildId, locale, (id, old) => locale);
        }

        public void SetGuildLocale(IGuild guild, string locale)
        {
            SetGuildLocale(guild.Id, locale);
        }

        public string GetGuildLocale(ulong guildId)
        {
            return _guildLocales.TryGetValue(guildId, out var locale) ? locale : "en-US";
        }

        public string GetGuildLocale(IGuild guild)
        {
            return guild is null ? "en-US" : GetGuildLocale(guild.Id);
        }

        public void RemoveGuildLocale(ulong guildId)
        {
            _guildLocales.TryRemove(guildId, out _);
        }

        public void RemoveGuildLocale(IGuild guild)
        {
            RemoveGuildLocale(guild.Id);
        }
    }

    public class CommandData
    {
        public string Command { get; set; }
        public string Description { get; set; }
        public string[] Remarks { get; set; }
    }
}