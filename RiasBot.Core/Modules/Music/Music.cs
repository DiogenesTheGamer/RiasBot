using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Modules.Music.Commons;
using RiasBot.Modules.Music.Services;

namespace RiasBot.Modules.Music
{
    public class Music : RiasModule<MusicService>
    {
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        public async Task PlayAsync([Remainder] string query)
        {
            var voiceChannel = ((IGuildUser) Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id) ?? await Service.InitializePlayerAsync(voiceChannel, Context.Channel);

            if (player?.OutputChannel is null)
                return;

            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            await player.PlayAsync((ShardedCommandContext) Context, query);
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        public async Task LeaveAsync()
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await player.LeaveAndDisposeAsync();
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        public async Task PauseAsync()
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            if (player.OutputChannel.Id != Context.Channel.Id)
            {
                await ReplyErrorAsync("output_channel_commands");
                return;
            }

            await player.PauseAsync();
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        public async Task ResumeAsync()
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            if (player.OutputChannel.Id != Context.Channel.Id)
            {
                await ReplyErrorAsync("output_channel_commands");
                return;
            }

            await player.ResumeAsync();
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task QueueAsync(int page = 1)
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            if (player.OutputChannel.Id != Context.Channel.Id)
            {
                await ReplyErrorAsync("output_channel_commands");
                return;
            }

            await player.QueueAsync(page);
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task NowPlayingAsync()
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            if (player.OutputChannel.Id != Context.Channel.Id)
            {
                await ReplyErrorAsync("output_channel_commands");
                return;
            }

            await player.NowPlayingAsync();
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task SkipAsync()
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            if (player.OutputChannel.Id != Context.Channel.Id)
            {
                await ReplyErrorAsync("output_channel_commands");
                return;
            }

            await player.SkipAsync();
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task SkipToAsync([Remainder] string title)
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            if (player.OutputChannel.Id != Context.Channel.Id)
            {
                await ReplyErrorAsync("output_channel_commands");
                return;
            }

            await player.SkipToAsync(title);
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task SeekAsync(string time)
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            if (player.OutputChannel.Id != Context.Channel.Id)
            {
                await ReplyErrorAsync("output_channel_commands");
                return;
            }

            await player.SeekAsync(time);
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task ReplayAsync()
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            if (player.OutputChannel.Id != Context.Channel.Id)
            {
                await ReplyErrorAsync("output_channel_commands");
                return;
            }

            await player.PlayNextTrackAsync((Track) player.CurrentTrack);
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task VolumeAsync(int? volume = null)
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            if (player.OutputChannel.Id != Context.Channel.Id)
            {
                await ReplyErrorAsync("output_channel_commands");
                return;
            }

            await player.SetVolumeAsync(volume);
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task ShuffleAsync()
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            if (player.OutputChannel.Id != Context.Channel.Id)
            {
                await ReplyErrorAsync("output_channel_commands");
                return;
            }

            await player.ShuffleAsync();
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task ClearAsync()
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            if (player.OutputChannel.Id != Context.Channel.Id)
            {
                await ReplyErrorAsync("output_channel_commands");
                return;
            }

            await player.ClearAsync();
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task RemoveAsync([Remainder] string title)
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            if (player.OutputChannel.Id != Context.Channel.Id)
            {
                await ReplyErrorAsync("output_channel_commands");
                return;
            }

            await player.RemoveAsync(title);
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task RepeatAsync()
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            var player = Service.GetPlayer(Context.Guild.Id);
            if (player is null)
                return;

            await ValidateOutputChannelAsync(player);
            if (player.OutputChannel.Id != Context.Channel.Id)
            {
                await ReplyErrorAsync("output_channel_commands");
                return;
            }

            await player.RepeatAsync();
        }

        private async Task<bool> CheckAsync(IGuildChannel voiceChannel)
        {
            if (voiceChannel is null)
            {
                await ReplyErrorAsync("user_not_in_vc");
                return false;
            }

            var currentUser = await Context.Guild.GetCurrentUserAsync();

            var botVoiceChannel = currentUser.VoiceChannel;
            if (botVoiceChannel != null && voiceChannel.Id != botVoiceChannel.Id)
            {
                await ReplyErrorAsync("not_same_vc");
                return false;
            }

            var preconditions = currentUser.GetPermissions(voiceChannel);
            if (!preconditions.Connect)
            {
                await ReplyErrorAsync("no_connect_permission", voiceChannel.Name);
                return false;
            }

            return true;
        }

        private async Task ValidateOutputChannelAsync(MusicPlayer player)
        {
            var outputChannelState = MusicUtils.CheckOutputChannel((DiscordShardedClient)Context.Client, Context.Guild.Id, player.OutputChannel);
            switch (outputChannelState)
            {
                case OutputChannelState.Null:
                    ChangeMusicOutputChannel(player);
                    await Context.Channel.SendErrorMessageAsync($"{GetText("output_channel_null")} {GetText("new_output_channel")}");
                    break;
                case OutputChannelState.NoViewPermission:
                    ChangeMusicOutputChannel(player);
                    await Context.Channel.SendErrorMessageAsync($"{GetText("output_channel_no_view_perm", player.OutputChannel.Name)} " +
                                                                $"{GetText("new_output_channel")}");
                    break;
                case OutputChannelState.NoSendPermission:
                    ChangeMusicOutputChannel(player);
                    await Context.Channel.SendErrorMessageAsync($"{GetText("output_channel_no_send_perm", player.OutputChannel.Name)} " +
                                                                $"{GetText("new_output_channel")}");
                    break;
                default: return;
            }
        }

        private void ChangeMusicOutputChannel(MusicPlayer player)
            => player.ChangeOutputChannel(Context.Channel);
    }
}