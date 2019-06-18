using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Discord.Addons.Interactive
{
    public class PaginatedMessageCallback : IReactionCallback
    {
        public ShardedCommandContext Context { get; }
        public InteractiveService Interactive { get; private set; }
        public IUserMessage Message { get; private set; }

        public RunMode RunMode => RunMode.Sync;
        public ICriterion<SocketReaction> Criterion => _criterion;
        public TimeSpan? Timeout => options.Timeout;

        private readonly ICriterion<SocketReaction> _criterion;
        private readonly PaginatedMessage _pager;

        private PaginatedAppearanceOptions options => _pager.Options;
        private readonly int _pages;
        private int _page = 1;
        

        public PaginatedMessageCallback(InteractiveService interactive, 
            ShardedCommandContext sourceContext,
            PaginatedMessage pager,
            ICriterion<SocketReaction> criterion = null)
        {
            Interactive = interactive;
            Context = sourceContext;
            _criterion = criterion ?? new EmptyCriterion<SocketReaction>();
            _pager = pager;
            _pages = (_pager.Pages.Count() - 1) / options.ItemsPerPage + 1;
        }

        public async Task DisplayAsync()
        {
            var embed = BuildEmbed();
            var message = await Context.Channel.SendMessageAsync(_pager.Content, embed: embed);
            if (_pages == 1)
            {
                return;
            }
            Message = message;
            Interactive.AddReactionCallback(message, this);
            // Reactions take a while to add, don't wait for them
            _ = Task.Run(async () =>
            {
                await message.AddReactionAsync(options.First);
                await message.AddReactionAsync(options.Back);
                await message.AddReactionAsync(options.Next);
                await message.AddReactionAsync(options.Last);

                var manageMessages = (Context.Channel is IGuildChannel guildChannel) && ((IGuildUser) Context.User).GetPermissions(guildChannel).ManageMessages;

                if (options.JumpDisplayOptions == JumpDisplayOptions.Always
                    || (options.JumpDisplayOptions == JumpDisplayOptions.WithManageMessages && manageMessages))
                    await message.AddReactionAsync(options.Jump);

                //await message.AddReactionAsync(options.Stop);

                if (options.DisplayInformationIcon)
                    await message.AddReactionAsync(options.Info);
            });
            // TODO: (Next major version) timeouts need to be handled at the service-level!
            if (Timeout != null)
            {
                _ = Task.Delay(Timeout.Value).ContinueWith(_ =>
                {
                    Interactive.RemoveReactionCallback(message);
                    _ = Message.RemoveAllReactionsAsync();
                });
            }
        }

        public async Task<bool> HandleCallbackAsync(SocketReaction reaction)
        {
            var emote = reaction.Emote;

            if (emote.Equals(options.First))
                _page = 1;
            else if (emote.Equals(options.Next))
            {
                if (_page >= _pages)
                    return false;
                ++_page;
            }
            else if (emote.Equals(options.Back))
            {
                if (_page <= 1)
                    return false;
                --_page;
            }
            else if (emote.Equals(options.Last))
                _page = _pages;
            /*else if (emote.Equals(options.Stop))
            {
                await Message.DeleteAsync();
                return true;
            }*/
            else if (emote.Equals(options.Jump))
            {
                _ = Task.Run(async () =>
                {
                    var criteria = new Criteria<SocketMessage>()
                        .AddCriterion(new EnsureSourceChannelCriterion())
                        .AddCriterion(new EnsureFromUserCriterion(reaction.UserId))
                        .AddCriterion(new EnsureIsIntegerCriterion());
                    var response = await Interactive.NextMessageAsync(Context, criteria, TimeSpan.FromSeconds(15));
                    var request = int.Parse(response.Content);
                    if (request < 1 || request > _pages)
                    {
                        _ = response.DeleteAsync();
                        //await Interactive.ReplyAndDeleteAsync(Context, options.Stop.Name);
                        return;
                    }
                    _page = request;
                    _ = response.DeleteAsync();
                    await RenderAsync();
                });
            }
            else if (emote.Equals(options.Info))
            {
                await Interactive.ReplyAndDeleteAsync(Context, options.InformationText, timeout: options.InfoTimeout);
                return false;
            }
            _ = Message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
            await RenderAsync();
            return false;
        }
        
        protected Embed BuildEmbed()
        {
            return new EmbedBuilder()
                .WithAuthor(_pager.Author)
                .WithColor(_pager.Color)
                .WithDescription(String.Join('\n', _pager.Pages.Skip((_page - 1) * options.ItemsPerPage).Take(options.ItemsPerPage)))
                .WithFooter(f => f.Text = string.Format(options.FooterFormat, _page, _pages))
                .WithTitle(_pager.Title)
                .Build();
        }
        private async Task RenderAsync()
        {
            var embed = BuildEmbed();
            await Message.ModifyAsync(m => m.Embed = embed);
        }
    }
}
