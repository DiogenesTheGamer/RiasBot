using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;

namespace RiasBot.Modules.Administration
{
    public partial class Administration
    {
        public class CategoryChannels : RiasSubmodule
        {
            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [RateLimit(1, 3, RateLimitType.Guild)]
            public async Task CreateCategoryAsync([Remainder]string name)
            {
                if (name.Length < 1 || name.Length > 100)
                {
                    await ReplyErrorAsync("channel_name_length_limit");
                    return;
                }
                await Context.Guild.CreateCategoryAsync(name);
                await ReplyConfirmationAsync("category_created", name);
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [RateLimit(1, 3, RateLimitType.Guild)]
            public async Task DeleteCategoryAsync([Remainder] ICategoryChannel category = null)
            {
                if (category != null)
                {
                    var currentUser = await Context.Guild.GetCurrentUserAsync();
                    if (ChannelsExtensions.CheckViewChannelPermission(currentUser, category))
                    {
                        await category.DeleteAsync();
                        await ReplyConfirmationAsync("category_deleted", category.Name);
                    }
                    else
                    {
                        await ReplyErrorAsync("category_no_permission_view");
                    }
                }
                else
                {
                    await ReplyErrorAsync("category_not_found");
                }
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [RateLimit(1, 3, RateLimitType.Guild)]
            public async Task RenameCategoryAsync([Remainder] string names)
            {
                var namesSplit = names.Split("->");

                if (namesSplit.Length < 2)
                    return;

                var oldName = namesSplit[0].TrimEnd();
                var newName = namesSplit[1].TrimStart();

                var category = await ChannelsExtensions.GetCategoryByIdAsync(Context.Guild, oldName) ??
                               (await Context.Guild.GetCategoriesAsync())
                               .FirstOrDefault(x => string.Equals(x.Name, oldName, StringComparison.InvariantCultureIgnoreCase));

                if (category != null)
                {
                    var currentUser = await Context.Guild.GetCurrentUserAsync();
                    if (ChannelsExtensions.CheckViewChannelPermission(currentUser, category))
                    {
                        oldName = category.Name;
                        await category.ModifyAsync(x => x.Name = newName);
                        await ReplyConfirmationAsync("category_renamed", oldName, category.Name);
                    }
                    else
                    {
                        await ReplyErrorAsync("category_no_permission_view");
                    }
                }
                else
                {
                    await ReplyErrorAsync("category_not_found");
                }
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [RateLimit(1, 3, RateLimitType.Guild)]
            public async Task AddTextChannelToCategoryAsync([Remainder] string names)
            {
                var namesSplit = names.Split("->");

                if (namesSplit.Length < 2)
                    return;

                var channelName = namesSplit[0].TrimEnd();
                var categoryName = namesSplit[1].TrimStart();
                var channel = await ChannelsExtensions.GetTextChannelByIdAsync(Context.Guild, channelName) ??
                              (await Context.Guild.GetTextChannelsAsync())
                              .FirstOrDefault(x => x.Name.Equals(channelName, StringComparison.InvariantCultureIgnoreCase));

                if (channel != null)
                {
                    var currentUser = await Context.Guild.GetCurrentUserAsync();
                    if (!ChannelsExtensions.CheckViewChannelPermission(currentUser, channel))
                    {
                        await ReplyErrorAsync("text_channel_no_permission_view");
                        return;
                    }

                    var category = await ChannelsExtensions.GetCategoryByIdAsync(Context.Guild, categoryName)??
                                   (await Context.Guild.GetCategoriesAsync())
                                   .FirstOrDefault(x => x.Name.Equals(categoryName, StringComparison.InvariantCultureIgnoreCase));
                    if (category != null)
                    {
                        if (!ChannelsExtensions.CheckViewChannelPermission(currentUser, category))
                        {
                            await ReplyErrorAsync("category_no_permission_view");
                            return;
                        }

                        await channel.ModifyAsync(x => x.CategoryId = category.Id);
                        await ReplyConfirmationAsync("text_channel_added_to_category", channel.Name, category.Name);
                    }
                    else
                    {
                        await ReplyErrorAsync("category_not_found");
                    }
                }
                else
                {
                    await ReplyErrorAsync("text_channel_not_found");
                }
            }

            [RiasCommand]
            [Aliases]
            [Description]
            [Usages]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            [RequireBotPermission(GuildPermission.ManageChannels)]
            [RateLimit(1, 3, RateLimitType.Guild)]
            public async Task AddVoiceChannelToCategoryAsync([Remainder] string names)
            {
                var namesSplit = names.Split("->");

                if (namesSplit.Length < 2)
                    return;

                var channelName = namesSplit[0].TrimEnd();
                var categoryName = namesSplit[1].TrimStart();
                var channel = await ChannelsExtensions.GetVoiceChannelByIdAsync(Context.Guild, channelName) ??
                              (await Context.Guild.GetVoiceChannelsAsync())
                              .FirstOrDefault(x => x.Name.Equals(channelName, StringComparison.InvariantCultureIgnoreCase));

                if (channel != null)
                {
                    var currentUser = await Context.Guild.GetCurrentUserAsync();
                    if (!ChannelsExtensions.CheckViewChannelPermission(currentUser, channel))
                    {
                        await ReplyErrorAsync("voice_channel_no_permission_view");
                        return;
                    }

                    var category = await ChannelsExtensions.GetCategoryByIdAsync(Context.Guild, categoryName)??
                                   (await Context.Guild.GetCategoriesAsync())
                                   .FirstOrDefault(x => x.Name.Equals(categoryName, StringComparison.InvariantCultureIgnoreCase));
                    if (category != null)
                    {
                        if (!ChannelsExtensions.CheckViewChannelPermission(currentUser, category))
                        {
                            await ReplyErrorAsync("category_no_permission_view");
                            return;
                        }

                        await channel.ModifyAsync(x => x.CategoryId = category.Id);
                        await ReplyConfirmationAsync("voice_channel_added_to_category", channel.Name, category.Name);
                    }
                    else
                    {
                        await ReplyErrorAsync("category_not_found");
                    }
                }
                else
                {
                    await ReplyErrorAsync("voice_channel_not_found");
                }
            }
        }
    }
}