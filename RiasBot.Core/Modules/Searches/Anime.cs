using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Modules.Searches.Services;
using RiasBot.Services;

namespace RiasBot.Modules.Searches
{
    public partial class Searches
    {
        public class Anime : RiasSubmodule<AnimeService>
        {
            private readonly IBotCredentials _creds;
            private readonly CommandHandler _ch;
            private readonly InteractiveService _is;

            public Anime(IBotCredentials creds, CommandHandler ch, InteractiveService iss)
            {
                _creds = creds;
                _ch = ch;
                _is = iss;
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RateLimit(3, 5, RateLimitType.GuildUser)]
            public async Task AnimeAsync([Remainder] string title)
            {
                var anime = await Service.GetAnimeAsync(title);
                if (anime is null || anime.Id == 0)
                {
                    await ReplyErrorAsync("anime_not_found");
                    return;
                }

                var titleRomaji = anime.Title.Romaji;
                var titleEnglish = anime.Title.English;
                var titleNative = anime.Title.Native;

                var fullTitle = $"{(string.IsNullOrEmpty(titleRomaji) ? titleEnglish : titleRomaji)} (AniList)";

                if (string.IsNullOrEmpty(titleRomaji))
                    titleRomaji = "-";
                if (string.IsNullOrEmpty(titleEnglish))
                    titleEnglish = "-";
                if (string.IsNullOrEmpty(titleNative))
                    titleNative = "-";

                var format = anime.Format;
                if (string.IsNullOrEmpty(format))
                    format = "-";

                var episodes = anime.Episodes?.ToString() ?? "-";
                var duration = anime.Duration != null ? anime.Duration + " mins" : "-";

                var status = anime.Status;
                if (string.IsNullOrEmpty(status))
                    status = "-";

                var startDate = "-";
                if (anime.StartDate.Day != null && anime.StartDate.Month != null && anime.StartDate.Year != null)
                    startDate = new DateTime(anime.StartDate.Year.Value, anime.StartDate.Month.Value, anime.StartDate.Day.Value).ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);

                var endDate = "-";
                if (anime.EndDate.Day != null && anime.EndDate.Month != null && anime.EndDate.Year != null)
                    endDate = new DateTime(anime.EndDate.Year.Value, anime.EndDate.Month.Value, anime.EndDate.Day.Value).ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);

                var season = anime.Season;
                if (string.IsNullOrEmpty(season))
                    season = "-";

                var averageScore = anime.AverageScore > 0 ? anime.AverageScore + "%" : "-";
                var meanScore = anime.MeanScore > 0 ? anime.MeanScore + "%" : "-";
                var popularity = anime.Popularity > 0 ? anime.Popularity.ToString() : "-";
                var favourites = anime.Favourites > 0 ? anime.Favourites.ToString() : "-";

                var source = anime.Source;
                if (string.IsNullOrEmpty(source))
                    source = "-";

                var genres = anime.Genres.Any() ? string.Join("\n", anime.Genres) : "-";

                var description = new StringBuilder(anime.Description);
                if (description.Length > 0)
                {
                    description.Replace("<br>", "");
                    if (description.Length > 1000)
                    {
                        description.Remove(1000, description.Length - 1000);
                        description.Append("... ").Append($"[{GetText("more")}]({anime.SiteUrl})");
                    }
                }
                else
                {
                    description.Append("-");
                }

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithUrl(anime.SiteUrl)
                    .WithTitle(fullTitle)
                    .AddField(GetText("title_romaji"), titleRomaji, true).AddField(GetText("title_english"), titleEnglish, true).AddField("title_native", titleNative, true)
                    .AddField(GetText("#administration_id"), anime.Id, true).AddField(GetText("format"), format, true).AddField(GetText("episodes"), episodes, true)
                    .AddField(GetText("episode_duration"), duration, true).AddField(GetText("#utility_status"), status, true).AddField(GetText("start_date"), startDate, true)
                    .AddField(GetText("end_date"), endDate, true).AddField(GetText("season"), season, true).AddField(GetText("average_score"), averageScore, true)
                    .AddField(GetText("mean_score"), meanScore, true).AddField(GetText("popularity"), popularity, true).AddField(GetText("favorites"), favourites, true)
                    .AddField(GetText("source"), source, true).AddField(GetText("genres"), genres, true).AddField(GetText("is_adult"), anime.IsAdult, true)
                    .AddField(GetText("description"), description)
                    .WithImageUrl(anime.CoverImage.Large);

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RateLimit(3, 5, RateLimitType.GuildUser)]
            public async Task MangaAsync([Remainder] string title)
            {
                var manga = await Service.GetMangaAsync(title);
                if (manga is null || manga.Id == 0)
                {
                    await ReplyErrorAsync("manga_not_found");
                    return;
                }

                var titleRomaji = manga.Title.Romaji;
                var titleEnglish = manga.Title.English;
                var titleNative = manga.Title.Native;

                var fullTitle = $"{(string.IsNullOrEmpty(titleRomaji) ? titleEnglish : titleRomaji)} (AniList)";

                if (string.IsNullOrEmpty(titleRomaji))
                    titleRomaji = "-";
                if (string.IsNullOrEmpty(titleEnglish))
                    titleEnglish = "-";
                if (string.IsNullOrEmpty(titleNative))
                    titleNative = "-";

                var format = manga.Format;
                if (string.IsNullOrEmpty(format))
                    format = "-";

                var chapters = manga.Chapters?.ToString() ?? "-";
                var volumes = manga.Volumes?.ToString() ?? "-";

                var status = manga.Status;
                if (string.IsNullOrEmpty(status))
                    status = "-";

                var startDate = "-";
                if (manga.StartDate.Day != null && manga.StartDate.Month != null && manga.StartDate.Year != null)
                    startDate = new DateTime(manga.StartDate.Year.Value, manga.StartDate.Month.Value, manga.StartDate.Day.Value).ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);

                var endDate = "-";
                if (manga.EndDate.Day != null && manga.EndDate.Month != null && manga.EndDate.Year != null)
                    endDate = new DateTime(manga.EndDate.Year.Value, manga.EndDate.Month.Value, manga.EndDate.Day.Value).ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);

                var averageScore = manga.AverageScore > 0 ? manga.AverageScore + "%" : "-";
                var meanScore = manga.MeanScore > 0 ? manga.MeanScore + "%" : "-";
                var popularity = manga.Popularity > 0 ? manga.Popularity.ToString() : "-";
                var favourites = manga.Favourites > 0 ? manga.Favourites.ToString() : "-";

                var source = manga.Source;
                if (string.IsNullOrEmpty(source))
                    source = "-";

                var genres = manga.Genres.Any() ? string.Join("\n", manga.Genres) : "-";
                var synonyms = manga.Synonyms.Any() ? string.Join("\n", manga.Synonyms) : "-";

                var description = new StringBuilder(manga.Description);
                if (description.Length > 0)
                {
                    description.Replace("<br>", "");
                    if (description.Length > 1000)
                    {
                        description.Remove(1000, description.Length - 1000);
                        description.Append("... ").Append($"[{GetText("more")}]({manga.SiteUrl})");
                    }
                }
                else
                {
                    description.Append("-");
                }

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithUrl(manga.SiteUrl)
                    .WithTitle(fullTitle)
                    .AddField(GetText("title_romaji"), titleRomaji, true).AddField(GetText("title_english"), titleEnglish, true).AddField("title_native", titleNative, true)
                    .AddField(GetText("#administration_id"), manga.Id, true).AddField(GetText("format"), format, true).AddField(GetText("chapters"), chapters, true)
                    .AddField(GetText("volumes"), volumes, true).AddField(GetText("#utility_status"), status, true).AddField(GetText("start_date"), startDate, true)
                    .AddField(GetText("end_date"), endDate, true).AddField(GetText("average_score"), averageScore, true).AddField(GetText("mean_score"), meanScore, true)
                    .AddField(GetText("popularity"), popularity, true).AddField(GetText("favorites"), favourites, true).AddField(GetText("source"), source, true)
                    .AddField(GetText("genres"), genres, true).AddField(GetText("synonyms"),  synonyms, true).AddField(GetText("is_adult"), manga.IsAdult, true)
                    .AddField(GetText("description"), description)
                    .WithImageUrl(manga.CoverImage.Large);

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RateLimit(3, 5, RateLimitType.GuildUser)]
            public async Task CharacterAsync([Remainder] string name)
            {
                var character = await Service.GetCharacterAsync(name);

                if (character is null || character.Id == 0)
                {
                    await ReplyErrorAsync("character_not_found");
                    return;
                }

                var firstName = character.Name.First;
                var lastName = character.Name.Last;
                var nativeName = character.Name.Last;

                var fullName = $"{firstName} {lastName} (AniList)";

                if (string.IsNullOrEmpty(firstName))
                    firstName = "-";
                if (string.IsNullOrEmpty(lastName))
                    lastName = "-";
                if (string.IsNullOrEmpty(nativeName))
                    nativeName = "-";

                var alternative = character.Name.Alternative.Any() ? string.Join("\n", character.Name.Alternative) : "-";

                var description = new StringBuilder(character.Description);
                if (description.Length > 0)
                {
                    description.Replace("<br>", "");
                    if (description.Length > 1000)
                    {
                        description.Remove(1000, description.Length - 1000);
                        description.Append("... ").Append($"[{GetText("more")}]({character.SiteUrl})");
                    }
                }
                else
                {
                    description.Append("-");
                }

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithUrl(character.SiteUrl)
                    .WithTitle(fullName)
                    .AddField(GetText("first_name"), firstName, true).AddField(GetText("last_name"), lastName, true).AddField(GetText("native_name"), nativeName, true)
                    .AddField(GetText("alternative"), alternative, true).AddField(GetText("#administration_id"), character.Id, true)
                    .AddField(GetText("description"), description)
                    .WithImageUrl(character.Image.Large);

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RateLimit(2, 5, RateLimitType.GuildUser)]
            public async Task CharactersAsync([Remainder] string name)
            {
                var characters = await Service.GetCharactersAsync(name);

                if (characters is null || !characters.Any())
                {
                    await ReplyErrorAsync("characters_not_found");
                    return;
                }

                var charactersList = characters.Select(character => $"{character.Name.First} {character.Name.Last} | {GetText("#administration_id")}: {character.Id}\n").ToList();

                var pager = new PaginatedMessage
                {
                    Title = GetText("characters_found", characters.Count, name, _ch.GetPrefix(Context.Guild)),
                    Color = new Color(_creds.ConfirmColor),
                    Pages = charactersList,
                    Options = new PaginatedAppearanceOptions
                    {
                        ItemsPerPage = 10,
                        Timeout = TimeSpan.FromMinutes(1),
                        DisplayInformationIcon = false,
                        JumpDisplayOptions = JumpDisplayOptions.Never
                    }

                };

                await _is.SendPaginatedMessageAsync((ShardedCommandContext)Context, pager);
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RateLimit(2, 5, RateLimitType.GuildUser)]
            public async Task AnimeListAsync([Remainder] string name)
            {
                var anime = await Service.GetAnimeListAsync(name);

                if (anime is null || !anime.Any())
                {
                    await ReplyErrorAsync("anime_list_not_found");
                    return;
                }

                var animeList = anime.Select(a =>
                {
                    var fullName = string.IsNullOrEmpty(a.Title.Romaji) ? a.Title.English : a.Title.Romaji;
                    return $"{fullName} | {GetText("#administration_id")}: {a.Id}\n";
                }).ToList();

                var pager = new PaginatedMessage
                {
                    Title = GetText("anime_found", name, _ch.GetPrefix(Context.Guild)),
                    Color = new Color(_creds.ConfirmColor),
                    Pages = animeList,
                    Options = new PaginatedAppearanceOptions
                    {
                        ItemsPerPage = 10,
                        Timeout = TimeSpan.FromMinutes(1),
                        DisplayInformationIcon = false,
                        JumpDisplayOptions = JumpDisplayOptions.Never
                    }

                };

                await _is.SendPaginatedMessageAsync((ShardedCommandContext)Context, pager);
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RateLimit(2, 5, RateLimitType.GuildUser)]
            public async Task MangaListAsync([Remainder] string name)
            {
                var manga = await Service.GetMangaListAsync(name);

                if (manga is null || !manga.Any())
                {
                    await ReplyErrorAsync("manga_list_not_found");
                    return;
                }

                var animeList = manga.Select(a =>
                {
                    var fullName = string.IsNullOrEmpty(a.Title.Romaji) ? a.Title.English : a.Title.Romaji;
                    return $"{fullName} | {GetText("#administration_id")}: {a.Id}\n";
                }).ToList();

                var pager = new PaginatedMessage
                {
                    Title = GetText("manga_found", name, _ch.GetPrefix(Context.Guild)),
                    Color = new Color(_creds.ConfirmColor),
                    Pages = animeList,
                    Options = new PaginatedAppearanceOptions
                    {
                        ItemsPerPage = 10,
                        Timeout = TimeSpan.FromMinutes(1),
                        DisplayInformationIcon = false,
                        JumpDisplayOptions = JumpDisplayOptions.Never
                    }

                };

                await _is.SendPaginatedMessageAsync((ShardedCommandContext)Context, pager);
            }
        }
    }
}