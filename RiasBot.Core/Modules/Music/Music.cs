using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Modules.Music.Commons;
using RiasBot.Modules.Music.Extensions;
using RiasBot.Modules.Music.Services;

namespace RiasBot.Modules.Music
{
    public class Music : RiasModule<MusicService>
    {
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        public async Task PlayAsync([Remainder] string keywords)
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            if (Service.MusicPlayers.TryGetValue(Context.Guild.Id, out var musicPlayer)) 
                await ValidateOutputChannelAsync(musicPlayer);

            await Service.PlayAsync((ShardedCommandContext)Context, voiceChannel, keywords);
        }
        
        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        public async Task LeaveAsync()
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;
            
            await Service.StopAsync(Context.Guild);
        }

        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        public async Task PauseAsync()
            => await ExecuteMusicCommandAsync(async () => await Service.PauseAsync(Context.Guild));
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        public async Task ResumeAsync()
            => await ExecuteMusicCommandAsync(async () => await Service.ResumeAsync(Context.Guild));
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task QueueAsync(int page = 1)
            => await ExecuteMusicCommandAsync(async () => await Service.QueueAsync(Context.Guild, page));
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task NowPlayingAsync()
            => await ExecuteMusicCommandAsync(async () => await Service.NowPlayingAsync(Context.Guild));
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task SkipAsync()
            => await ExecuteMusicCommandAsync(async () => await Service.SkipAsync(Context.Guild));
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task SkipToAsync([Remainder] string title)
            => await ExecuteMusicCommandAsync(async () => await Service.SkipToAsync(Context.Guild, title));
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task SeekAsync(string time) 
            => await ExecuteMusicCommandAsync(async () => await Service.SeekAsync(Context.Guild, time));
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task ReplayAsync() 
            => await ExecuteMusicCommandAsync(async () => await Service.ReplayAsync(Context.Guild));
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task VolumeAsync(int? volume = null) 
            => await ExecuteMusicCommandAsync(async () => await Service.VolumeAsync(Context.Guild, volume));
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        public async Task ShuffleAsync(int? volume = null) 
            => await ExecuteMusicCommandAsync(async () => await Service.ShuffleAsync(Context.Guild));
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        public async Task ClearAsync(int? volume = null) 
            => await ExecuteMusicCommandAsync(async () => await Service.ClearAsync(Context.Guild));
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task RemoveAsync([Remainder] string title) 
            => await ExecuteMusicCommandAsync(async () => await Service.RemoveAsync(Context.Guild, title));
        
        [RiasCommand]
        [Aliases]
        [Description]
        [Usages]
        [RequireContext(ContextType.Guild)]
        [RateLimit(1, 5, RateLimitType.GuildUser)]
        public async Task RepeatAsync() 
            => await ExecuteMusicCommandAsync(async () => await Service.RepeatAsync(Context.Guild));

        private async Task ExecuteMusicCommandAsync(Action command)
        {
            var voiceChannel = ((IVoiceState)Context.User).VoiceChannel;
            if (!await CheckAsync(voiceChannel))
                return;

            if (Service.MusicPlayers.TryGetValue(Context.Guild.Id, out var musicPlayer))
            {
                await ValidateOutputChannelAsync(musicPlayer);
                if (musicPlayer.Channel.Id == Context.Channel.Id)
                {
                    command.Invoke();
                }
                else
                {
                    await ReplyErrorAsync("output_channel_commands");
                }
            }
        }

        private async Task<bool> CheckAsync(IGuildChannel voiceChannel)
        {
            if (voiceChannel is null)
            {
                await ReplyErrorAsync("user_not_in_vc");
                return false;
            }

            var botVoiceChannel = (await Context.Guild.GetCurrentUserAsync()).VoiceChannel;
            if (botVoiceChannel != null)
                if (voiceChannel.Id != botVoiceChannel.Id)
                {
                    await ReplyErrorAsync("not_same_vc");
                    return false;
                }

            var socketGuildUser = await Context.Guild.GetCurrentUserAsync();
            var preconditions = socketGuildUser.GetPermissions(voiceChannel);
            if (!preconditions.Connect)
            {
                await ReplyErrorAsync("no_connect_permission", voiceChannel.Name);
                return false;
            }

            return true;
        }

        private async Task ValidateOutputChannelAsync(MusicPlayer musicPlayer)
        {
            var reason = MusicExtensions.CheckOutputChannel((DiscordShardedClient)Context.Client, musicPlayer.Guild, musicPlayer.Channel);
            switch (reason)
            {
                case "NULL":
                    ChangeMusicOutputChannel(musicPlayer);
                    await Context.Channel.SendErrorMessageAsync($"{GetText("output_channel_null")} {GetText("new_output_channel")}");
                    break;
                case "NO_SEND_MESSAGES_PERMISSION":
                    ChangeMusicOutputChannel(musicPlayer);
                    await Context.Channel.SendErrorMessageAsync($"{GetText("output_channel_no_send_perm", musicPlayer.Channel.Name)} " +
                                                                $"{GetText("new_output_channel")}");
                    break;
                case "NO_VIEW_CHANNEL_PERMISSION":
                    ChangeMusicOutputChannel(musicPlayer);
                    await Context.Channel.SendErrorMessageAsync($"{GetText("output_channel_no_view_perm", musicPlayer.Channel.Name)} " +
                                                                $"{GetText("new_output_channel")}");
                    break;
                default: return;
            }
        }

        private void ChangeMusicOutputChannel(MusicPlayer musicPlayer) => musicPlayer.Channel = Context.Channel;
    }
}