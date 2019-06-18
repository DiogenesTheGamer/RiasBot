using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;

namespace RiasBot.Modules.Administration
{
    public partial class Administration
    {
        public class Emotes : RiasSubmodule
        {
            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageEmojis)]
            [RequireBotPermission(GuildPermission.ManageEmojis)]
            [RateLimit(1, 5, RateLimitType.Guild)]
            public async Task AddEmoteAsync(string url, [Remainder] string name)
            {
                var isAnimated = false;
                if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    await Context.Channel.SendErrorMessageAsync(GetText("#utility_url_not_valid"));
                    return;
                }
                if (!url.Contains("https"))
                {
                    await Context.Channel.SendErrorMessageAsync(GetText("#utility_url_not_https"));
                    return;
                }
                if (!url.Contains(".png") && !url.Contains(".jpg") && !url.Contains(".jpeg"))
                {
                    if (!url.Contains(".gif"))
                    {
                        await Context.Channel.SendErrorMessageAsync(GetText("#utility_url_not_png_jpg_gif"));
                        return;
                    }

                    isAnimated = true;
                }

                var emotes = Context.Guild.Emotes;

                var staticEmotes = emotes.Where(x => !x.Animated);
                var animatedEmotes = emotes.Where(x => x.Animated);


                if (isAnimated)
                {
                    if (animatedEmotes.Count() == 50)
                    {
                        await ReplyErrorAsync("animated_emotes_limit");
                        return;
                    }
                }
                else
                {
                    if (staticEmotes.Count() == 50)
                    {
                        await ReplyErrorAsync("static_emotes_limit");
                        return;
                    }
                }

                name = name.Replace(" ", "_");

                using (var http = new HttpClient())
                {
                    try
                    {
                        var res = await http.GetAsync(new Uri(url));
                        if (res.IsSuccessStatusCode)
                        {
                            using (var emote = await res.Content.ReadAsStreamAsync())
                            {
                                if (emote.Length / 1024 <= 256) //in KB
                                {
                                    var emoteImage = new Image(emote);
                                    await Context.Guild.CreateEmoteAsync(name, emoteImage);
                                    await ReplyConfirmationAsync("emote_created", name);
                                }
                                else
                                {
                                    await ReplyErrorAsync("emote_size_limit");
                                }
                            }
                        }
                        else
                        {
                            await Context.Channel.SendErrorMessageAsync(GetText("#utility_image_or_url_not_good"));
                        }
                    }
                    catch
                    {
                        await Context.Channel.SendErrorMessageAsync(GetText("#utility_image_or_url_not_good"));
                    }
                }
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageEmojis)]
            [RequireBotPermission(GuildPermission.ManageEmojis)]
            [RateLimit(1, 5, RateLimitType.Guild)]
            public async Task DeleteEmoteAsync([Remainder] string name)
            {
                try
                {
                    var emote = Context.Guild.Emotes.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.InvariantCultureIgnoreCase));
                    if (emote != null)
                    {
                        await Context.Guild.DeleteEmoteAsync(emote);
                        await ReplyConfirmationAsync("emote_deleted", emote.Name);
                    }
                    else
                    {
                        await ReplyErrorAsync("emote_not_found");
                    }
                }
                catch
                {
                    await ReplyErrorAsync("emote_not_deleted");
                }
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageEmojis)]
            [RequireBotPermission(GuildPermission.ManageEmojis)]
            [RateLimit(1, 5, RateLimitType.Guild)]
            public async Task RenameEmoteAsync([Remainder] string names)
            {
                var emotes = names.Split("->");

                if (emotes.Length < 2)
                    return;

                var oldName = emotes[0].TrimEnd().Replace(" ", "_");
                var newName = emotes[1].TrimStart().Replace(" ", "_");

                try
                {
                    var emote = Context.Guild.Emotes.FirstOrDefault(x => string.Equals(x.Name, oldName, StringComparison.InvariantCultureIgnoreCase));
                    if (emote != null)
                    {
                        await Context.Guild.ModifyEmoteAsync(emote, x => x.Name = newName);
                        await ReplyConfirmationAsync("emote_renamed", oldName, newName);
                    }
                    else
                    {
                        await ReplyErrorAsync("emote_not_found");
                    }
                }
                catch
                {
                    await ReplyErrorAsync("emote_not_renamed");
                }
            }
        }
    }
}