using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RiasBot.Commons.Attributes;
using RiasBot.Services;

namespace RiasBot.Modules.Searches
{
    public partial class Searches : RiasModule
    {
        private readonly IBotCredentials _creds;

        public Searches(IBotCredentials creds)
        {
            _creds = creds;
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        public async Task WikipediaAsync([Remainder]string keyword)
        {
            await Context.Channel.TriggerTypingAsync();

            using (var http = new HttpClient())
            {
                var response = await http.GetAsync("https://en.wikipedia.org//w/api.php?action=query&format=json&prop=info&redirects=1&formatversion=2&inprop=url&titles="
                                                   + Uri.EscapeDataString(keyword));
                if (response.IsSuccessStatusCode)
                {
                    //is not necessary to make a class to deserialize the json result, this is not used much and it's small
                    var result = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(result);
                    var item = json.Value<JToken>("query");
                    var page = item.Value<JToken>("pages")[0];
                    var missing = page.Value<bool>("missing");
                    var url = page.Value<string>("fullurl");

                    if (!missing)
                        await Context.Channel.SendMessageAsync(url);
                    else
                        await ReplyErrorAsync("not_found");
                }
                else
                {
                    await ReplyErrorAsync("not_found");
                }
            }
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        public async Task UrbanDictionaryAsync([Remainder]string keyword)
        {
            if (string.IsNullOrEmpty(_creds.UrbanDictionaryApiKey))
            {
                await ReplyErrorAsync("ud_no_api_key");
                return;
            }

            await Context.Channel.TriggerTypingAsync();

            var pageProvided = false;
            var page = 0;
            var spacePosition = keyword.IndexOf(" ", StringComparison.InvariantCultureIgnoreCase);
            if (spacePosition > -1)
                pageProvided = int.TryParse(keyword.Substring(0, spacePosition + 1).TrimEnd(), out page);

            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Add("X-Mashape-Key", _creds.UrbanDictionaryApiKey);
                http.DefaultRequestHeaders.Add("Accept", "application/json");

                var request = await http.GetAsync("https://mashape-community-urban-dictionary.p.mashape.com/define?term=" +
                                                  Uri.EscapeUriString(pageProvided ? keyword.Substring(spacePosition + 1).TrimStart() : keyword));
                if (request.IsSuccessStatusCode)
                {
                    var response = await request.Content.ReadAsStringAsync();
                    var udContent = JsonConvert.DeserializeObject<UrbanDictionaryContent>(response);

                    UrbanDictionaryWordDetails udWordDetails = null;
                    if (udContent.List.Any())
                    {
                        page--;
                        if (page < 0)
                        {
                            udWordDetails = udContent.List.First();
                            page = 0;
                        }
                        else
                        {
                            if (page < udContent.List.Count)
                            {
                                udWordDetails = udContent.List[page];
                            }
                        }

                        if (udWordDetails is null)
                        {
                            await ReplyErrorAsync("ud_word_not_found");
                            return;
                        }

                        var udWordDefinition = udWordDetails.Definition;
                        if (udWordDefinition.Length > 2000)
                            udWordDefinition = udWordDefinition.Substring(0, 2000) + "... " +
                                               $"[{GetText("more").ToLowerInvariant()}]({udWordDetails.Permalink})";

                        var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                            .WithUrl(udWordDetails.Permalink)
                            .WithAuthor(udWordDetails.Word, "https://i.imgur.com/re5jokL.jpg")
                            .WithDescription(udWordDefinition)
                            .AddField(GetText("#help_example"), udWordDetails.Example)
                            .WithFooter($"{GetText("page")}: {page + 1}/{udContent.List.Count}");

                        await Context.Channel.SendMessageAsync(embed: embed.Build());
                    }
                    else
                    {
                        await ReplyErrorAsync("ud_word_not_found");
                    }
                }
                else
                {
                    await ReplyErrorAsync("ud_word_not_found");
                }
            }
        }

        private class UrbanDictionaryContent
        {
            public IList<UrbanDictionaryWordDetails> List { get; set; }
        }

        private class UrbanDictionaryWordDetails
        {
            public string Definition { get; set; }
            public string Permalink { get; set; }
            public string Word { get; set; }
            public string Example { get; set; }
        }
    }
}