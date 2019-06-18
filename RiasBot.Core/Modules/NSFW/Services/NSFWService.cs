using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using RiasBot.Commons.Attributes;

namespace RiasBot.Modules.Nsfw.Services
{
    [Service]
    public class NsfwService
    {
        private const string DanbooruApi = "https://danbooru.donmai.us/posts.json?limit=100&tags=";
        private const string KonachanApi = "https://konachan.com/post.json?s=post&q=index&limit=100&tags=";
        private const string YandereApi = "https://yande.re/post.json?limit=100&tags=";
        private const string GelbooruApi = "https://gelbooru.com/index.php?page=dapi&s=post&q=index&limit=100&tags=";

        private readonly IList<string> _blacklistedTags = new List<string>
        {
            "loli",
            "lolicon",
            "shota",
            "shotacon"
        };

        public async Task<DapiImage> GetImageAsync(DapiWebsite dapiWeb, string tag)
        {
            tag = tag?.Replace(" ", "%20");

            if (dapiWeb == DapiWebsite.Random)
            {
                var rnd = new Random((int) DateTime.UtcNow.Ticks).Next(4);
                dapiWeb = (DapiWebsite) rnd;
            }
            var dapiImages = await DownloadImagesAsync(dapiWeb, tag);

            if (dapiImages.Count == 0) return null;

            DapiImage hentai;
            var counter = 5;
            do
            {
                var rndImage = new Random((int) DateTime.UtcNow.Ticks).Next(dapiImages.Count);
                hentai = dapiImages[rndImage];
                string[] tags;
                if (dapiWeb == DapiWebsite.Danbooru)
                {
                    tags = hentai.TagString.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                    if (string.IsNullOrEmpty(hentai.FileUrl)) continue;

                    if (!Uri.IsWellFormedUriString(hentai.FileUrl, UriKind.Absolute))
                        hentai.FileUrl = "https://danbooru.donmai.us" + hentai.FileUrl;
                }
                else
                {
                    tags = hentai.Tags.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                }

                if (tags.Length == 0) return null;
                if (tags.Any(t => _blacklistedTags.Any(b => string.Equals(t, b, StringComparison.InvariantCultureIgnoreCase))))
                {
                    counter--;

                    if (counter == 0)
                        return null;
                }
                else
                {
                    break;
                }
            } while (counter > 0);

            return hentai;
        }

        private async Task<List<DapiImage>> DownloadImagesAsync(DapiWebsite dapiWeb, string tag)
        {
            string url;
            var isXml = false;

            switch (dapiWeb)
            {
                case DapiWebsite.Danbooru:
                    url = DanbooruApi;
                    break;
                case DapiWebsite.Konachan:
                    url = KonachanApi;
                    break;
                case DapiWebsite.Yandere:
                    url = YandereApi;
                    break;
                case DapiWebsite.Gelbooru:
                    url = GelbooruApi;
                    isXml = true;
                    break;
                default:
                    url = DanbooruApi;
                    break;
            }

            url += "rating:explicit+" + tag;

            if (isXml) return await DeserializeXmlHentaiAsync(url);

            return await DeserializeJsonHentaiAsync(url);
        }

        private async Task<List<DapiImage>> DeserializeJsonHentaiAsync(string url)
        {
            using (var http = new HttpClient())
            {
                var response = await http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<DapiImage>>(result);
                }
            }

            return null;
        }

        private async Task<List<DapiImage>> DeserializeXmlHentaiAsync(string url)
        {
            var dapiImageList = new List<DapiImage>();
            using (var http = new HttpClient())
            {
                var response = await http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var reader = XmlReader.Create(stream, new XmlReaderSettings
                    {
                        Async = true
                    }))
                    {
                        while (await reader.ReadAsync())
                        {
                            if (reader.NodeType == XmlNodeType.Element &&
                                reader.Name == "post")
                            {
                                dapiImageList.Add(new DapiImage
                                {
                                    FileUrl = reader["file_url"],
                                    Tags = reader["tags"]
                                });
                            }
                        }
                    }

                    return dapiImageList;
                }
            }

            return null;
        }

        public class DapiImage
        {
            [JsonProperty("file_url")]
            public string FileUrl { get; set; }
            public string Tags { get; set; }
            [JsonProperty("tag_string")]
            public string TagString { get; set; }
        }

        public enum DapiWebsite
        {
            Danbooru = 0,
            Konachan = 1,
            Yandere = 2,
            Gelbooru = 3,
            Random = 4
        }
    }
}

//TODO: Optimize this service, make the execution faster