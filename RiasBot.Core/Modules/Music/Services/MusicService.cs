using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Modules.Music.Commons;
using RiasBot.Modules.Music.Extensions;
using RiasBot.Services;
using Serilog;
using Victoria;
using Victoria.Entities;
using GExtensions = RiasBot.Extensions.Extensions;

namespace RiasBot.Modules.Music.Services
{
    [Service]
    public class MusicService
    {
        private readonly DiscordShardedClient _client;
        private readonly LavaShardClient _lavaShardClient;
        private readonly LavaRestClient _lavaRestClient;
        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly InteractiveService _is;
        private readonly ITranslations _tr;

        public MusicService(DiscordShardedClient client, LavaShardClient lavaShardClient, IBotCredentials creds, DbService db,
            InteractiveService iss, ITranslations tr)
        {
            _client = client;
            _lavaShardClient = lavaShardClient;
            _lavaRestClient = new LavaRestClient(creds.LavalinkConfig.Host, creds.LavalinkConfig.Port, creds.LavalinkConfig.Password);
            _creds = creds;
            _db = db;
            _is = iss;
            _tr = tr;

            _client.ShardDisconnected += ShardDisconnectedAsync;
            _client.LeftGuild += LeftGuildAsync;
            _client.UserVoiceStateUpdated += UserVoiceStateUpdatedAsync;

            _lavaShardClient.OnTrackException += TrackExceptionAsync;
            _lavaShardClient.OnTrackFinished += TrackFinishedAsync;
            _lavaShardClient.OnTrackStuck += TrackStuckAsync;
        }

//        private async Task ReplyConfirmationAsync(IMessageChannel channel, ulong guildId, string lowerModuleTypeName, string key)
//            => await channel.SendConfirmationMessageAsync(_tr.GetText(guildId, lowerModuleTypeName, key));

        private async Task ReplyConfirmationAsync(IMessageChannel channel, ulong guildId, string lowerModuleTypeName, string key, params object[] args)
            => await channel.SendConfirmationMessageAsync(_tr.GetText(guildId, lowerModuleTypeName, key, args));

        private async Task ReplyErrorAsync(IMessageChannel channel, ulong guildId, string lowerModuleTypeName, string key)
            => await channel.SendErrorMessageAsync(_tr.GetText(guildId, lowerModuleTypeName, key));

        private async Task ReplyErrorAsync(IMessageChannel channel, ulong guildId, string lowerModuleTypeName, string key, params object[] args)
            => await channel.SendErrorMessageAsync(_tr.GetText(guildId, lowerModuleTypeName, key, args));

        private const string YoutubeUrl = "https://youtu.be/{0}?list={1}";
        private const string LowerModuleTypeName = "music";

        public readonly ConcurrentDictionary<ulong, MusicPlayer> MusicPlayers = new ConcurrentDictionary<ulong, MusicPlayer>();

        private async Task UserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            var guildUser = (IGuildUser) user;

            if (user.Id == _client.CurrentUser.Id && newState.VoiceChannel is null)
            {
                await StopAsync(guildUser.Guild);
                return;
            }

            if (oldState.VoiceChannel != null)
                await AutoDisconnect(oldState.VoiceChannel.Users, guildUser);

            if (newState.VoiceChannel != null)
                await AutoDisconnect(newState.VoiceChannel.Users, guildUser);
        }

        private async Task AutoDisconnect(IReadOnlyCollection<SocketGuildUser> users, IGuildUser guildUser)
        {
            if (!users.Contains(await guildUser.Guild.GetCurrentUserAsync()))
                return;

            if (users.Count(u => !u.IsBot) < 1)
            {
                await StartAutoDisconnecting(guildUser.Guild, TimeSpan.FromMinutes(2));
            }
            else
            {
                await StopAutoDisconnecting(guildUser.Guild);
            }
        }

        private async Task StartAutoDisconnecting(IGuild guild, TimeSpan dueTime)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                var player = musicPlayer.Player;
                if (player.IsPlaying && !player.IsPaused)
                    await musicPlayer.Player.PauseAsync();

                musicPlayer.AutoDisconnectTimer = new Timer(async _ => await StopAsync(guild), null, dueTime, TimeSpan.Zero);

                var outputChannel = MusicExtensions.CheckOutputChannel(_client, musicPlayer.Guild, musicPlayer.Channel);
                if (string.Equals(outputChannel, "TRUE"))
                    await ReplyConfirmationAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "stop_after");
            }
        }

        private async Task StopAutoDisconnecting(IGuild guild)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                if (musicPlayer.AutoDisconnectTimer is null)
                    return;

                var player = musicPlayer.Player;
                if (player.IsPlaying && player.IsPaused)
                {
                    await player.ResumeAsync();
                    var outputChannel = MusicExtensions.CheckOutputChannel(_client, musicPlayer.Guild, musicPlayer.Channel);
                    if (string.Equals(outputChannel, "TRUE"))
                        await ReplyConfirmationAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "resumed");
                }

                musicPlayer.AutoDisconnectTimer.Dispose();
                musicPlayer.AutoDisconnectTimer = null;
            }
        }

        private async Task ShardDisconnectedAsync(Exception ex, DiscordSocketClient client)
        {
            foreach (var guild in client.Guilds)
            {
                MusicPlayers.TryRemove(guild.Id, out _);
                await StopAsync(guild);
            }
        }

        private async Task LeftGuildAsync(SocketGuild guild)
        {
            await StopAsync(guild);
        }

        public async Task PlayAsync(ShardedCommandContext context, IVoiceChannel voiceChannel, string url)
        {
            if (_lavaShardClient is null)
            {
                await ReplyConfirmationAsync(context.Channel, voiceChannel.GuildId, LowerModuleTypeName, "lavalink_not_ready");
                return;
            }

            MusicSearchResult searchResult;
            try
            {
                searchResult = await LoadTracksAsync(url);
            }
            catch (Exception e)
            {
                await ReplyErrorAsync(context.Channel, voiceChannel.GuildId, LowerModuleTypeName, "search_tracks_error", _creds.OwnerServerInvite);
                Log.Error(e.ToString());
                return;
            }

            if (!MusicPlayers.TryGetValue(voiceChannel.GuildId, out var musicPlayer))
            {
                musicPlayer = new MusicPlayer
                {
                    Player = await _lavaShardClient.ConnectAsync(voiceChannel),
                    Guild = context.Guild,
                    VoiceChannel = voiceChannel,
                    Channel = context.Channel,
                    Features = GetPlayerFeatures((IGuildUser) context.User)
                };

                MusicPlayers.TryAdd(voiceChannel.GuildId, musicPlayer);
                await ReplyConfirmationAsync(context.Channel, voiceChannel.GuildId, LowerModuleTypeName, "channel_connected", voiceChannel.Name);
            }

            if (searchResult is null)
            {
                await ReplyErrorAsync(musicPlayer.Channel, voiceChannel.GuildId, LowerModuleTypeName, "no_tracks_found");
                return;
            }

            var track = searchResult.Track;
            var tracks = searchResult.Tracks.ToList();

            if (searchResult.SearchType == SearchType.Keywords)
            {
                if (tracks.Any())
                    track = await ChooseTrackAsync(context, musicPlayer, tracks);
            }

            var user = (IGuildUser) context.User;

            var player = musicPlayer.Player;
            if (track != null)
            {
                var addTrack = true;

                if (!track.IsStream && track.Length > TimeSpan.FromHours(3) && !musicPlayer.Features.LongTracks)
                {
                    addTrack = false;
                    await ReplyErrorAsync(musicPlayer.Channel, voiceChannel.GuildId, LowerModuleTypeName, "player_feature_long_tracks", 3, _creds.Patreon);
                }

                if (track.IsStream && !musicPlayer.Features.Livestreams)
                {
                    addTrack = false;
                    await ReplyErrorAsync(musicPlayer.Channel, voiceChannel.GuildId, LowerModuleTypeName, "player_feature_livestreams", _creds.Patreon);
                }

                if (addTrack)
                {
                    if (!player.IsPlaying && !player.IsPaused)
                    {
                        var trackContent = new TrackContent
                        {
                            Track = track,
                            User = user
                        };

                        musicPlayer.CurrentTrack = trackContent;

                        await player.PlayAsync(track);
                        await SendNowPlayingMessageAsync(trackContent, musicPlayer);
                    }
                    else
                    {
                        musicPlayer.Queue.Add(new TrackContent
                        {
                            Track = track,
                            User = user
                        });

                        await SendAddedTrackMessageAsync(user, musicPlayer, track, musicPlayer.Queue);
                    }
                }
            }

            if (searchResult.SearchType == SearchType.Keywords) return;

            if (track is null && !tracks.Any())
            {
                await ReplyErrorAsync(musicPlayer.Channel, voiceChannel.GuildId, LowerModuleTypeName, "no_tracks_found");
                return;
            }

            if (!tracks.Any())
                return;

            if (!musicPlayer.Features.LongTracks)
                tracks = tracks.Where(t => t.Length < TimeSpan.FromHours(3)).ToList();

            if (!musicPlayer.Features.Livestreams)
                tracks = tracks.Where(t => !t.IsStream).ToList();

            foreach (var tr in tracks)
            {
                musicPlayer.Queue.Add(new TrackContent
                {
                    Track = tr,
                    User = user
                });
            }

            await ReplyConfirmationAsync(musicPlayer.Channel, voiceChannel.GuildId, LowerModuleTypeName, "tracks_added", searchResult.Tracks.Count());

            if (!player.IsPlaying && !player.IsPaused)
            {
                var trackContent = musicPlayer.Queue.First();
                musicPlayer.Queue.RemoveAt(0);
                musicPlayer.CurrentTrack = trackContent;
                await player.PlayAsync(trackContent.Track);
                await SendNowPlayingMessageAsync(trackContent, musicPlayer);
            }
        }

        public async Task StopAsync(IGuild guild, bool showMessage = true)
        {
            if (MusicPlayers.TryRemove(guild.Id, out var musicPlayer))
            {
                var voiceChannel = musicPlayer.Player.VoiceChannel;
                await _lavaShardClient.DisconnectAsync(voiceChannel);

                if (!showMessage)
                    return;

                var reason = MusicExtensions.CheckOutputChannel(_client, musicPlayer.Guild, musicPlayer.Channel);
                if (string.Equals(reason, "TRUE"))
                    await ReplyConfirmationAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "destroyed", voiceChannel.Name);
            }
        }

        public async Task PauseAsync(IGuild guild)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                var player = musicPlayer.Player;
                if (!player.IsPlaying)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "player_not_playing");
                    return;
                }

                if (player.IsPaused)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "player_is_paused");
                    return;
                }

                await player.PauseAsync();
                await ReplyConfirmationAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "paused");
            }
        }

        public async Task ResumeAsync(IGuild guild)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                var player = musicPlayer.Player;
                if (!player.IsPlaying)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "player_not_playing");
                    return;
                }

                if (!player.IsPaused)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "player_is_playing");
                    return;
                }

                await player.ResumeAsync();
                await ReplyConfirmationAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "resumed");
            }
        }

        public async Task QueueAsync(IGuild guild, int page)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                var player = musicPlayer.Player;
                var queue = musicPlayer.Queue;

                var currentTrack = musicPlayer.CurrentTrack;
                if (currentTrack is null)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "queue_empty");
                    return;
                }

                var totalPage = (int) Math.Ceiling((double) queue.Count / 15);
                if (page > totalPage)
                    page = totalPage;

                page--;

                if (page < 0)
                    page = 0;

                var queueList = musicPlayer.Queue.Skip(page * 15).Take(15).ToList();

                var description = new StringBuilder();
                string status;

                if (player.IsPlaying && !player.IsPaused)
                    status = "⏸";
                else if (player.IsPaused)
                    status = "▶";
                else
                    status = "⏹";

                var totalTime = TimeSpan.Zero;

                if (currentTrack.Track.IsStream)
                {
                    description.Append($"♾ {currentTrack.Track.Title} `{_tr.GetText(guild.Id, LowerModuleTypeName, "livestream")}`\n" +
                                       "▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬");
                }
                else
                {
                    totalTime = currentTrack.Track.Length;
                    description.Append($"{status} {currentTrack.Track.Title} `{currentTrack.Track.Length.DigitalTimeSpanString()}`\n" +
                                       "▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬");
                }

                var index = page * 15;
                foreach (var item in queueList)
                {
                    var track = item.Track;
                    description.Append("\n").Append($"#{index + 1} {track.Title} | ")
                        .Append($"`{(track.IsStream ? _tr.GetText(guild.Id, LowerModuleTypeName, "livestream") : track.Length.DigitalTimeSpanString())}`");

                    if (!track.IsStream)
                        totalTime += track.Length;

                    index++;
                }

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithTitle(_tr.GetText(guild.Id, LowerModuleTypeName, "queue"))
                    .WithDescription(description.ToString())
                    .WithFooter($"{_tr.GetText(guild.Id, null, key: "#searches_page")} {page + 1}/{totalPage} | " +
                        _tr.GetText(guild.Id, LowerModuleTypeName, "total_time", totalTime.DigitalTimeSpanString()));

                await musicPlayer.Channel.SendMessageAsync(embed: embed.Build());
            }
        }

        public async Task NowPlayingAsync(IGuild guild)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                var player = musicPlayer.Player;

                if (musicPlayer.CurrentTrack is null || !player.IsPlaying)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "player_not_playing");
                    return;
                }

                var track = musicPlayer.CurrentTrack.Track;
                string elapsedTime;
                if (!track.IsStream)
                {
                    var timerBar = new StringBuilder();
                    var position = track.Position.TotalMilliseconds / track.Length.TotalMilliseconds * 30;
                    for (var i = 0; i < 30; i++)
                    {
                        timerBar.Append(i == (int) position ? "⚫" : "▬");
                    }

                    elapsedTime = $"`{timerBar}`\n`{track.Position.DigitalTimeSpanString()}/{track.Length.DigitalTimeSpanString()}`";
                }
                else
                {
                    elapsedTime = track.Position.DigitalTimeSpanString();
                }

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithTitle(_tr.GetText(guild.Id, LowerModuleTypeName, "now_playing"))
                    .WithDescription($"[{track.Title}]({track.Uri})\n\n{elapsedTime}")
                    .AddField(_tr.GetText(guild.Id, LowerModuleTypeName, "channel"), track.Author, true)
                    .AddField(_tr.GetText(guild.Id, LowerModuleTypeName, "length"), track.IsStream ? _tr.GetText(guild.Id, LowerModuleTypeName, "livestream")
                        : track.Length.DigitalTimeSpanString(), true)
                    .AddField(_tr.GetText(guild.Id, LowerModuleTypeName, "requested_by"), musicPlayer.CurrentTrack.User, true)
                    .WithThumbnailUrl(await track.FetchThumbnailAsync());

                if (musicPlayer.Repeat)
                    embed.AddField(_tr.GetText(guild.Id, LowerModuleTypeName, "repeat"), _tr.GetText(guild.Id, null, "#utility_enabled"), true);

                await musicPlayer.Channel.SendMessageAsync(embed: embed.Build());
            }
        }

        public async Task SkipAsync(IGuild guild)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                if (musicPlayer.Queue.Count > 0)
                    await PlayNextTrackAsync(musicPlayer);
                else
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "no_next_track");
            }
        }

        public async Task SkipToAsync(IGuild guild, string title)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                var queue = musicPlayer.Queue;
                if (queue.Count == 0)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "no_next_track");
                    return;
                }

                if (title.StartsWith("#"))
                {
                    title = title.Substring(1);
                }

                TrackContent trackContent = null;

                if (int.TryParse(title, out var index))
                {
                    index--;
                    if (index < 0)
                    {
                        await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "track_index_less_than", 1);
                        return;
                    }

                    if (index >= queue.Count)
                    {
                        await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "track_index_above");
                        return;
                    }

                    trackContent = queue[index];
                    queue.RemoveRange(0, index + 1);
                }
                else
                {
                    for (var i = 0; i < queue.Count; i++)
                    {
                        if (queue[i].Track.Title.Contains(title, StringComparison.InvariantCultureIgnoreCase))
                        {
                            trackContent = queue[i];
                            queue.RemoveRange(0, i + 1);
                            break;
                        }
                    }

                    if (trackContent is null)
                    {
                        await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "no_track_found");
                        return;
                    }
                }

                musicPlayer.CurrentTrack = trackContent;
                await musicPlayer.Player.PlayAsync(trackContent.Track);
                await SendNowPlayingMessageAsync(trackContent, musicPlayer);
            }
        }

        public async Task SeekAsync(IGuild guild, string time)
        {
            var position = GExtensions.ConvertToTimeSpan(time);
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                var player = musicPlayer.Player;

                if (!player.IsPlaying)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "player_not_playing");
                    return;
                }

                if (player.IsPaused)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "player_is_paused");
                    return;
                }

                var currentTrack = musicPlayer.CurrentTrack;
                if (position > currentTrack.Track.Length)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "seek_position_over");
                    return;
                }

                var currentPosition = currentTrack.Track.Position;
                await musicPlayer.Player.SeekAsync(position);

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithTitle(_tr.GetText(guild.Id, LowerModuleTypeName, "seek"))
                    .AddField(_tr.GetText(guild.Id, LowerModuleTypeName, "seek_from"), currentPosition.DigitalTimeSpanString(), true)
                    .AddField(_tr.GetText(guild.Id, LowerModuleTypeName, "seek_to"), position.DigitalTimeSpanString(), true);

                await musicPlayer.Channel.SendMessageAsync(embed: embed.Build());
            }
        }

        public async Task ReplayAsync(IGuild guild)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                var player = musicPlayer.Player;
                if (musicPlayer.CurrentTrack is null)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "queue_empty");
                    return;
                }

                await player.PlayAsync(musicPlayer.CurrentTrack.Track);
                await SendNowPlayingMessageAsync(musicPlayer.CurrentTrack, musicPlayer);
            }
        }

        public async Task VolumeAsync(IGuild guild, int? volume)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                var player = musicPlayer.Player;
                if (volume is null)
                {
                    await ReplyConfirmationAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "current_volume", player.CurrentVolume);
                    return;
                }

                if (!musicPlayer.Features.Volume)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "player_feature_volume", _creds.Patreon);
                    return;
                }

                if (volume < 0)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "volume_lower_zero");
                    return;
                }

                if (volume > 100)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "volume_higher_than", 100);
                    return;
                }

                if (!player.IsPlaying)
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "player_not_playing");
                    return;
                }

                await player.SetVolumeAsync(volume.Value);
                await ReplyConfirmationAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "volume_set", volume);
            }
        }

        public async Task ShuffleAsync(IGuild guild)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                if (!musicPlayer.Queue.Any())
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "queue_empty");
                    return;
                }

                musicPlayer.Queue.Shuffle();
                await ReplyConfirmationAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "queue_shuffled");
            }
        }

        public async Task ClearAsync(IGuild guild)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                if (!musicPlayer.Queue.Any())
                {
                    await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "queue_empty");
                    return;
                }

                musicPlayer.Queue.Clear();
                await ReplyConfirmationAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "queue_cleared");
            }
        }

        public async Task RemoveAsync(IGuild guild, string title)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                if (title.StartsWith("#"))
                {
                    title = title.Substring(1);
                }

                TrackContent trackContent = null;

                var queue = musicPlayer.Queue;
                if (int.TryParse(title, out var index))
                {
                    index--;
                    if (index < 0)
                    {
                        await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "track_index_less_than", 1);
                        return;
                    }

                    if (index >= queue.Count)
                    {
                        await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "track_index_above");
                        return;
                    }

                    trackContent = queue[index];
                    queue.RemoveAt(index);
                }
                else
                {
                    for (var i = 0; i < queue.Count; i++)
                    {
                        if (queue[i].Track.Title.Contains(title, StringComparison.InvariantCultureIgnoreCase))
                        {
                            trackContent = queue[i];
                            queue.RemoveAt(i);
                            break;
                        }
                    }

                    if (trackContent is null)
                    {
                        await ReplyErrorAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "no_track_found");
                        return;
                    }
                }

                await ReplyConfirmationAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "track_removed", trackContent.Track.Title);
            }
        }

        public async Task RepeatAsync(IGuild guild)
        {
            if (MusicPlayers.TryGetValue(guild.Id, out var musicPlayer))
            {
                musicPlayer.Repeat = !musicPlayer.Repeat;
                if (musicPlayer.Repeat)
                    await ReplyConfirmationAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "repeat_enabled");
                else
                    await ReplyConfirmationAsync(musicPlayer.Channel, guild.Id, LowerModuleTypeName, "repeat_disabled");
            }
        }

        private async Task PlayNextTrackAsync(MusicPlayer musicPlayer)
        {
            var player = musicPlayer.Player;

            if (musicPlayer.Repeat)
            {
                await player.PlayAsync(musicPlayer.CurrentTrack.Track);
                return;
            }

            if (musicPlayer.Queue.Count > 0)
            {
                var trackContent = musicPlayer.Queue.First();
                musicPlayer.Queue.RemoveAt(0);
                musicPlayer.CurrentTrack = trackContent;

                await player.PlayAsync(trackContent.Track);
                await SendNowPlayingMessageAsync(trackContent, musicPlayer);
            }
            else
            {
                musicPlayer.CurrentTrack = null;
            }
        }

        private async Task<MusicSearchResult> LoadTracksAsync(string url)
        {
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                if (url.Contains("youtube") || url.Contains("youtu.be"))
                {
                    var youtubeContent = MusicExtensions.DecodeYoutubeUrl(url);
                    if (youtubeContent is null)
                        return null;

                    var query = url;
                    if (!url.Contains("playlist"))
                        query = string.Format(YoutubeUrl, youtubeContent.VideoId, youtubeContent.PlaylistId);

                    var searchResult = await _lavaRestClient.SearchTracksAsync(query, true);

                    switch (searchResult.LoadType)
                    {
                        case LoadType.TrackLoaded:
                            return new MusicSearchResult
                            {
                                SearchType = SearchType.Url,
                                Track = searchResult.Tracks.FirstOrDefault(),
                                Tracks = new List<LavaTrack>()
                            };
                        case LoadType.PlaylistLoaded:
                            return new MusicSearchResult
                            {
                                SearchType = SearchType.Url,
                                Tracks = searchResult.Tracks
                            };
                        default:
                            return null;
                        }
                }

                //TODO: add soundcloud links support
            }

            return new MusicSearchResult
            {
                SearchType = SearchType.Keywords,
                Tracks = (await _lavaRestClient.SearchYouTubeAsync(url)).Tracks
            };
        }

        private async Task<LavaTrack> ChooseTrackAsync(ShardedCommandContext context, MusicPlayer musicPlayer, IEnumerable<LavaTrack> tracks)
        {
            var lavaTracks = tracks.ToList();
            if (!lavaTracks.Any())
            {
                await ReplyErrorAsync(musicPlayer.Channel, context.Guild.Id, LowerModuleTypeName, "no_tracks_found");
                return null;
            }

            var description = new StringBuilder();
            var index = 1;

            foreach (var track in lavaTracks)
            {
                if (index > 5) break;

                description.Append($"#{index} {track.Title} `{track.Length.DigitalTimeSpanString()}`").Append("\n");
                index++;
            }

            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                .WithTitle(_tr.GetText(context.Guild.Id, LowerModuleTypeName, "choose_track"))
                .WithDescription(description.ToString());

            var chooseMsg = await musicPlayer.Channel.SendMessageAsync(embed: embed.Build());

            var getUserInput = await _is.NextMessageAsync(context, timeout: TimeSpan.FromMinutes(1));
            if (getUserInput != null)
            {
                var userInput = getUserInput.Content.Replace("#", "");
                if (int.TryParse(userInput, out var input))
                {
                    input--;
                    if (input >= 0 && input < 5)
                    {
                        await chooseMsg.DeleteAsync();
                        return lavaTracks.ElementAt(input);
                    }
                }
            }

            var reason = MusicExtensions.CheckOutputChannel(_client, musicPlayer.Guild, musicPlayer.Channel);
            if (string.Equals(reason, "TRUE"))
                await chooseMsg.DeleteAsync();

            return null;
        }

        private async Task SendNowPlayingMessageAsync(TrackContent trackContent, MusicPlayer musicPlayer)
        {
            var reason = MusicExtensions.CheckOutputChannel(_client, musicPlayer.Guild, musicPlayer.Channel);
            if (!string.Equals(reason, "TRUE"))
                return;

            var track = trackContent.Track;
            var user = trackContent.User;

            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                .WithTitle(_tr.GetText(user.GuildId, LowerModuleTypeName, "now_playing"))
                .WithDescription($"[{track.Title}]({track.Uri})")
                .AddField(_tr.GetText(user.GuildId, LowerModuleTypeName, "channel"), track.Author, true)
                .AddField(_tr.GetText(user.GuildId, LowerModuleTypeName, "length"), track.IsStream ? _tr.GetText(user.GuildId, LowerModuleTypeName, "livestream")
                    : track.Length.DigitalTimeSpanString(), true)
                .AddField(_tr.GetText(user.GuildId, LowerModuleTypeName, "requested_by"), user, true)
                .WithThumbnailUrl(await track.FetchThumbnailAsync());

            if (musicPlayer.Repeat)
                embed.AddField(_tr.GetText(user.GuildId, LowerModuleTypeName, "repeat"), _tr.GetText(user.GuildId, null, "#utility_enabled"), true);

            await musicPlayer.Channel.SendMessageAsync(embed: embed.Build());
        }

        private async Task SendAddedTrackMessageAsync(IGuildUser user, MusicPlayer musicPlayer, LavaTrack track, List<TrackContent> queue)
        {
            var reason = MusicExtensions.CheckOutputChannel(_client, musicPlayer.Guild, musicPlayer.Channel);
            if (!string.Equals(reason, "TRUE"))
                return;

            var etp = "";
            var etpTimeSpan = TimeSpan.Zero;
            foreach (var trackContent in queue)
            {
                if (trackContent.Track.IsStream)
                {
                    etp = "∞";
                    break;
                }

                etpTimeSpan += trackContent.Track.Length;
            }

            if (string.IsNullOrEmpty(etp))
            {
                etp = etpTimeSpan.DigitalTimeSpanString();
            }

            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                .WithAuthor(_tr.GetText(user.GuildId, LowerModuleTypeName, "added_to_queue"), user.GetRealAvatarUrl())
                .WithDescription($"[{track.Title}]({track.Uri})")
                .AddField(_tr.GetText(user.GuildId, LowerModuleTypeName, "channel"), track.Author, true)
                .AddField(_tr.GetText(user.GuildId, LowerModuleTypeName, "length"), track.IsStream ? _tr.GetText(user.GuildId, LowerModuleTypeName, "livestream")
                    : track.Length.DigitalTimeSpanString(), true)
                .AddField(_tr.GetText(user.GuildId, LowerModuleTypeName, "etp"), etp, true)
                .WithThumbnailUrl(await track.FetchThumbnailAsync());

            if (musicPlayer.Repeat)
                embed.AddField(_tr.GetText(user.GuildId, LowerModuleTypeName, "repeat"), _tr.GetText(user.GuildId, null, "#utility_enabled"), true);

            await musicPlayer.Channel.SendMessageAsync(embed: embed.Build());
        }

        private async Task TrackExceptionAsync(LavaPlayer player, LavaTrack track, string error)
        {
            if (MusicPlayers.TryGetValue(player.VoiceChannel.GuildId, out var musicPlayer))
            {
                var reason = MusicExtensions.CheckOutputChannel(_client, musicPlayer.Guild, musicPlayer.Channel);
                if (string.Equals(reason, "TRUE"))
                {
                    await ReplyErrorAsync(musicPlayer.Channel, musicPlayer.Guild.Id, LowerModuleTypeName, "track_exception", track.Title);
                }

                await PlayNextTrackAsync(musicPlayer);
            }
        }

        private async Task TrackFinishedAsync(LavaPlayer player, LavaTrack track, TrackEndReason reason)
        {
            if (reason.ShouldPlayNext())
            {
                if (MusicPlayers.TryGetValue(player.VoiceChannel.GuildId, out var musicPlayer))
                {
                    await PlayNextTrackAsync(musicPlayer);
                    return;
                }
            }

            Log.Error($"Something went wrong on playing the next track!\n Reason: {reason}");
        }

        private async Task TrackStuckAsync(LavaPlayer player, LavaTrack track, long threshold)
        {
            if (MusicPlayers.TryGetValue(player.VoiceChannel.GuildId, out var musicPlayer))
            {
                var reason = MusicExtensions.CheckOutputChannel(_client, musicPlayer.Guild, musicPlayer.Channel);
                if (string.Equals(reason, "TRUE"))
                {
                    await ReplyErrorAsync(musicPlayer.Channel, musicPlayer.Guild.Id, LowerModuleTypeName, "track_stuck", track.Title);
                }

                await PlayNextTrackAsync(musicPlayer);
            }
        }

        private PatreonPlayerFeatures GetPlayerFeatures(IGuildUser user)
        {
            using (var db = _db.GetDbContext())
            {
                var allFeaturesUnlocked = new PatreonPlayerFeatures
                {
                    Volume = true,
                    LongTracks = true,
                    Livestreams = true
                };

                //the features are unlocked if the bot owner joined the bot
                if (user.Id == _creds.MasterId)
                    return allFeaturesUnlocked;

                //the features are unlocked in the bot owner's guild
                if (user.GuildId == _creds.OwnerServerId)
                    return allFeaturesUnlocked;

                var guildOwnerId = user.Guild.OwnerId;
                var patron = db.Patreon.FirstOrDefault(x => x.UserId == guildOwnerId);

                var playerFeatures = new PatreonPlayerFeatures();
                if (patron is null)
                    return playerFeatures;

                if (patron.Reward >= 5000)
                    playerFeatures.Volume = true;

                if (patron.Reward >= 10000)
                    playerFeatures.LongTracks = true;

                if (patron.Reward >= 15000)
                    playerFeatures.Livestreams = true;

                return playerFeatures;
            }
        }
    }
}