using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Modules.Profile.Services;
using RiasBot.Services;
using DBModels = RiasBot.Database.Models;

namespace RiasBot.Modules.Utility
{
    public class Profile : RiasModule<ProfileService>
        {
            private readonly IBotCredentials _creds;
            private readonly DbService _db;
            private readonly InteractiveService _is;

            public Profile(IBotCredentials creds, DbService db, InteractiveService iss)
            {
                _creds = creds;
                _db = db;
                _is = iss;
            }

            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RateLimit(1, 30, RateLimitType.GuildUser)]
            public async Task ProfileAsync([Remainder] IUser user = null)
            {
                user = user ?? Context.User;
                var typing = Context.Channel.EnterTypingState();

                var rolesIds = ((IGuildUser)user).RoleIds;
                var roles = rolesIds.Select(role => Context.Guild.GetRole(role)).ToList();
                var highestRole = roles.OrderByDescending(x => x.Position).Select(y => y).FirstOrDefault();

                try
                {
                    using (var img = await Service.GenerateProfileImageAsync((IGuildUser) user, highestRole))
                    {
                        if (img != null)
                            await Context.Channel.SendFileAsync(img, $"{user.Id}_profile.png");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    // ignored
                }
                typing.Dispose();
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            [RateLimit(1, 30, RateLimitType.GuildUser)]
            public async Task BackgroundImageAsync(string url)
            {
                if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    await ReplyErrorAsync("#utility_url_not_valid");
                    return;
                }
                if (!url.Contains("https"))
                {
                    await ReplyErrorAsync("#utility_url_not_https");
                    return;

                }
                if (!url.Contains(".png") && !url.Contains(".jpg") && !url.Contains(".jpeg"))
                {
                    await ReplyErrorAsync("#utility_url_not_png_jpg");
                    return;
                }

                var typing = Context.Channel.EnterTypingState();

                try
                {
                    using (var preview = await Service.GenerateProfilePreviewAsync((IGuildUser) Context.User, url))
                    {
                        if (preview != null)
                        {
                            await Context.Channel.SendFileAsync(preview, $"{Context.User.Id}_profile_preview.png");
                        }
                        else
                        {
                            await ReplyErrorAsync("preview_error");
                            typing.Dispose();
                            return;
                        }
                    }
                }
                catch
                {
                    await ReplyErrorAsync("preview_error");
                    typing.Dispose();
                    return;
                }
                
                typing.Dispose();

                await ReplyConfirmationAsync("background_set_confirmation", 1000, _creds.Currency);
                var input = await _is.NextMessageAsync((ShardedCommandContext)Context, timeout: TimeSpan.FromMinutes(1));
                if (input != null)
                {
                    if (!string.Equals(input.Content, GetText("#bot_confirm"), StringComparison.InvariantCultureIgnoreCase))
                    {
                        await ReplyErrorAsync("#bot_canceled");
                        return;
                    }

                    var db = _db.GetDbContext();
                    var userDb = db.Users.FirstOrDefault(x => x.UserId == Context.User.Id);
                    var profileDb = db.Profile.FirstOrDefault(x => x.UserId == Context.User.Id);
                    if (userDb != null)
                    {
                        if (userDb.Currency >= 1000)
                        {
                            if (profileDb != null)
                            {
                                profileDb.BackgroundUrl = url;
                            }
                            else
                            {
                                var profile = new DBModels.Profile { UserId = Context.User.Id, BackgroundUrl = url, BackgroundDim = 50 };
                                await db.AddAsync(profile);
                            }
                            userDb.Currency -= 1000;

                            await db.SaveChangesAsync();
                            await ReplyConfirmationAsync("background_set");
                        }
                        else
                        {
                            await ReplyErrorAsync("#gambling_currency_not_enough", _creds.Currency);
                        }
                    }
                    else
                    {
                        await ReplyErrorAsync("#gambling_currency_not_enough", _creds.Currency);
                    }
                }
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            public async Task BackgroundDimAsync(int? dim = null)
            {
                using (var db = _db.GetDbContext())
                {
                    var profileDb = db.Profile.FirstOrDefault(x => x.UserId == Context.User.Id);

                    if (dim is null)
                    {
                        dim = 50;
                        if (profileDb != null)
                            dim = profileDb.BackgroundDim;
                        await ReplyConfirmationAsync("dim", dim);
                    }
                    else if (dim >= 0 && dim <= 100)
                    {
                        if (profileDb != null)
                        {
                            profileDb.BackgroundDim = dim.Value;
                        }
                        else
                        {
                            var dimDb = new DBModels.Profile { UserId = Context.User.Id, BackgroundDim = dim.Value };
                            await db.AddAsync(dimDb);
                        }

                        await db.SaveChangesAsync();
                        await ReplyConfirmationAsync("dim_set", dim);
                    }
                }
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireContext(ContextType.Guild)]
            public async Task BiographyAsync([Remainder]string bio)
            {
                if (bio.Length <= 150)
                {
                    using (var db = _db.GetDbContext())
                    {
                        var profileDb = db.Profile.FirstOrDefault(x => x.UserId == Context.User.Id);
                        if (profileDb != null)
                        {
                            profileDb.Bio = bio;
                        }
                        else
                        {
                            var bioDb = new DBModels.Profile { UserId = Context.User.Id, BackgroundDim = 50, Bio = bio };
                            await db.AddAsync(bioDb);
                        }

                        await db.SaveChangesAsync();
                        await ReplyConfirmationAsync("bio_set");
                    }
                }
                else
                {
                    await ReplyErrorAsync("bio_length_limit", 150);
                }
            }
        }
}