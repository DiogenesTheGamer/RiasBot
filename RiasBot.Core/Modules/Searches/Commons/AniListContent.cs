using Newtonsoft.Json;

namespace RiasBot.Modules.Searches.Commons
{
    public class AniListContent
    {
        public int Id { get; set; }
        public string SiteUrl { get; set; }

        public class AMTitle
        {
            public string Romaji { get; set; }
            public string English { get; set; }
            public string Native { get; set; }
        }

        public class AMDate
        {
            public int? Year { get; set; }
            public int? Month { get; set; }
            public int? Day { get; set; }
        }

        public class AMCoverImage
        {
            public string Large { get; set; }
        }
    }

    [JsonObject(Title = "Media")]
    public class AnimeMangaContent : AniListContent
    {
        public AMTitle Title { get; set; }
        public string Format { get; set; }

        //Anime
        public int? Episodes { get; set; }
        public int? Duration { get; set; }

        //Manga
        public int? Chapters { get; set; }
        public int? Volumes { get; set; }

        public string Status { get; set; }

        public AMDate StartDate { get; set; }
        public AMDate EndDate { get; set; }
        public string Season { get; set; }
        public int AverageScore { get; set; }
        public int MeanScore { get; set; }
        public int Popularity { get; set; }
        public int Favourites { get; set; }
        public string Source { get; set; }
        public string[] Genres { get; set; }
        public string[] Synonyms { get; set; }
        public bool IsAdult { get; set; }
        public string Description { get; set; }
        public AMCoverImage CoverImage { get; set; }
    }
    
    public class CharacterContent : AniListContent
    {
        public CharacterName Name { get; set; }
        public string Description { get; set; }
        public CharacterImage Image { get; set; }

        public class CharacterName
        {
            public string First { get; set; }
            public string Last { get; set; }
            public string Native { get; set; }
            public string[] Alternative { get; set; }
        }

        public class CharacterImage
        {
            public string Large { get; set; }
        }
    }
}