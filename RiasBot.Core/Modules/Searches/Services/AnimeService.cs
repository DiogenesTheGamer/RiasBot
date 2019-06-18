using System.Collections.Generic;
using System.Threading.Tasks;
using RiasBot.Commons.Attributes;
using RiasBot.Modules.Searches.Commons;

namespace RiasBot.Modules.Searches.Services
{
    [Service]
    public class AnimeService
    {
        public async Task<AnimeMangaContent> GetAnimeAsync(string title)
        {
            var query = int.TryParse(title, out var id)
                ? @"query ($anime: Int) {
                      Media(id: $anime, type: ANIME) {"
                : @"query ($anime: String) {
                      Media(search: $anime, type: ANIME) {";
            query += @"id
                        siteUrl
                        title {
                          romaji
                          english
                          native
                        }
                        format
                        episodes
                        duration
                        status
                        startDate {
                          year
                          month
                          day
                        }
                        endDate {
                          year
                          month
                          day
                        }
                        season
                        averageScore
                        meanScore
                        popularity
                        favourites
                        source
                        genres
                        isAdult
                        description
                        coverImage {
                          large
                        }
                      }
                    }
                    ";

            return (await new AniListGraphQL().QueryAsync(query, new { anime = id > 0 ? id.ToString() : title }))
                .GetData<AnimeMangaContent>("Media");
        }

        public async Task<AnimeMangaContent> GetMangaAsync(string title)
        {
            var query = int.TryParse(title, out var id)
                ? @"query ($manga: Int) {
                      Media(id: $manga, type: MANGA) {"
                : @"query ($manga: String) {
                      Media(search: $manga, type: MANGA) {";
            query += @"id
                        siteUrl
                        title {
                          romaji
                          english
                          native
                        }
                        format
                        chapters
                        volumes
                        status
                        startDate {
                          year
                          month
                          day
                        }
                        endDate {
                          year
                          month
                          day
                        }
                        averageScore
                        meanScore
                        popularity
                        favourites
                        source
                        genres
                        synonyms
                        isAdult
                        description
                        coverImage {
                          large
                        }
                      }
                    }
                    ";

            return (await new AniListGraphQL().QueryAsync(query, new { manga = id > 0 ? id.ToString() : title }))
                .GetData<AnimeMangaContent>("Media");
        }
        
        public async Task<CharacterContent> GetCharacterAsync(string name)
        {
            var query = int.TryParse(name, out var id)
                ? @"query ($character: Int) {
                      Character(id: $character) {"
                : @"query ($character: String) {
                      Character(search: $character) {";
            query += @"id
                        siteUrl
                        name {
                          first
                          last
                          native
                          alternative
                        }
                        description
                        image {
                          large
                        }
                      }
                    }
                    ";
            
            return (await new AniListGraphQL().QueryAsync(query, new { character = id > 0 ? id.ToString() : name }))
                .GetData<CharacterContent>("Character");
        }

        public async Task<IList<CharacterContent>> GetCharactersAsync(string name)
        {
            var query = @"query ($character: String) {
                      Page {
                        characters(search: $character) {
                          id
                          siteUrl
                          name {
                            first
                            last
                            native
                            alternative
                          }
                          description
                          image {
                            large
                          }
                        }
                      }
                    }";
            
            return (await new AniListGraphQL().QueryAsync(query, new { character = name }))
              .GetData<List<CharacterContent>>("Page", "characters");
        }
        
        public async Task<IList<AnimeMangaContent>> GetAnimeListAsync(string title)
        {
            var query = @"query ($anime: String) {
                    Page {
                        media(search: $anime, type: ANIME) {
                            id
                            siteUrl
                            title {
                                romaji
                                english
                                native
                                }
                            }
                        }
                    }
                    ";

            return (await new AniListGraphQL().QueryAsync(query, new { anime = title }))
                .GetData<List<AnimeMangaContent>>("Page", "media");
        }
        
        public async Task<IList<AnimeMangaContent>> GetMangaListAsync(string title)
        {
            var query = @"query ($manga: String) {
                    Page {
                        media(search: $manga, type: MANGA) {
                            id
                            siteUrl
                            title {
                                romaji
                                english
                                native
                                }
                            }
                        }
                    }
                    ";

            return (await new AniListGraphQL().QueryAsync(query, new { manga = title }))
                .GetData<List<AnimeMangaContent>>("Page", "media");
        }
    }
}