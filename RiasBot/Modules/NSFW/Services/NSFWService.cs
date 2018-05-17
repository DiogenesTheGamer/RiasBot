﻿using Discord;
using RiasBot.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Text.RegularExpressions;

namespace RiasBot.Modules.NSFW.Services
{
    public class NSFWService : IRService
    {
        public NSFWService()
        {

        }

        public async Task<string> GetImage(string tag)
        {
            var rnd = new Random((int)DateTime.UtcNow.Ticks);
            int site = rnd.Next(3);

            switch(site)
            {
                case 0:
                    return await DownloadImages(NSFWSite.Danbooru, tag).ConfigureAwait(false);
                case 1:
                    return await DownloadImages(NSFWSite.Konachan, tag).ConfigureAwait(false);
                case 2:
                    return await DownloadImages(NSFWSite.Yandere, tag).ConfigureAwait(false);
            }
            return null;
        }

        public async Task<string> DownloadImages(NSFWSite site, string tag = null)
        {
            //if (tag == "loli")
                //return null;
            string api = null;
            string images = null;

            var rnd = new Random((int)DateTime.UtcNow.Ticks);
            var data = new List<Hentai>();
            
            switch(site)
            {
                case NSFWSite.Danbooru:
                    api = $"http://danbooru.donmai.us/posts.json?limit=100&tags=rating:explicit+{tag}";
                    break;
                case NSFWSite.Konachan:
                    api = $"https://konachan.com/post.json?s=post&q=index&limit=100&tags=rating:explicit+{tag}";
                    break;
                case NSFWSite.Yandere:
                    api = $"https://yande.re/post.json?limit=100&tags=rating:explicit+{tag}";
                    break;
            }
            try
            {
                using (var http = new HttpClient())
                {
                    images = await http.GetStringAsync(api).ConfigureAwait(false);

                    data = JsonConvert.DeserializeObject<List<Hentai>>(images);
                }

                if (data.Count > 0)
                {
                    int random = rnd.Next(data.Count);
                    var hentai = data[random];
                    int retry = 0; // don't get in an infinity loop
                    while (Regex.IsMatch(hentai.Tags, @"\bloli\b") && retry < 5)
                    {
                        random = rnd.Next(data.Count);
                        hentai = data[random];
                        retry++;
                    }
                    if (retry == 5)
                        return null;

                    string imageUrl = hentai.File_Url;
                    if (site == NSFWSite.Danbooru)
                    {
                        if (!imageUrl.Contains("donmai.us"))
                            return "http://danbooru.donmai.us" + imageUrl;
                        else
                            return imageUrl;
                    }
                    else
                        return imageUrl;
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        public enum NSFWSite
        {
            Danbooru = 0,
            Konachan = 1,
            Yandere = 2
        }

        public class Hentai
        {
            public string File_Url { get; set; }
            public string Tags { get; set; }
            public string Tag_String { get; set; }
            public string Rating { get; set; }
        }
    }
}
