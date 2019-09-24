using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using RiasBot.Commons.Attributes;
using Serilog;

namespace RiasBot.Services.Implementation
{
    [Service(typeof(ITranslations))]
    public class Translations : ITranslations
    {
        private readonly ILocalization _localization;
        private readonly ImmutableDictionary<string, ImmutableDictionary<string, string>> _translations;

        public Translations(ILocalization localization)
        {
            _localization = localization;

            var stw = new Stopwatch();
            stw.Start();

            var translationsPath = Path.Combine(Environment.CurrentDirectory, "assets/translations");
            var translations = Directory.GetFiles(translationsPath);

            var translationsDictionary = new Dictionary<string, ImmutableDictionary<string, string>>();
            foreach (var translation in translations)
            {
                var translationKey = Path.GetFileName(translation).Replace(".json", "");
                var translationValue = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(translation));
                translationsDictionary.TryAdd(translationKey, translationValue.ToImmutableDictionary());
            }

            _translations = translationsDictionary.ToImmutableDictionary();

            stw.Stop();
            Log.Information($"Translations loaded: {stw.ElapsedMilliseconds} ms");
        }

        private string GetText(ulong guildId, string key)
        {
            var locale = _localization.GetGuildLocale(guildId);
            if (!_translations.TryGetValue(locale, out var strings)) return null;

            if (strings.TryGetValue(key, out var translation)) return translation;

            if (!_translations.TryGetValue("en-US", out var enStrings)) return null;
            return enStrings.TryGetValue(key, out var enTranslation) ? enTranslation : null;
        }

        /// <summary>
        /// Get a translation text.
        /// If the key starts with "#", the first word delimited by "_" is the prefix for the translation.
        /// If the key doesn't start with "#", the prefix of the translation is the lower module type of this class
        /// </summary>
        public string GetText(ulong guildId, string lowerModuleTypeName, string key)
        {
            return key.StartsWith("#") ? GetText(guildId, key.Substring(1)) : GetText(guildId, lowerModuleTypeName + "_" + key);
        }

        /// <summary>
        /// Get a translation text with arguments.
        /// If the key starts with "#", the first word delimited by "_" is the prefix for the translation.
        /// If the key doesn't start with "#", the prefix of the translation is the lower module type of this class
        /// </summary>
        public string GetText(ulong guildId, string lowerModuleTypeName, string key, params object[] args)
        {
            var format = key.StartsWith("#") ? GetText(guildId, key.Substring(1)) : GetText(guildId, lowerModuleTypeName + "_" + key);
            return string.Format(format, args);
        }
    }
}