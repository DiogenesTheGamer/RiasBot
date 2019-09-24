using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using RiasBot.Extensions;
using RiasBot.Modules.Music.Services;
using RiasBot.Services;
using Timer = System.Threading.Timer;

namespace RiasBot.Modules.Music.Commons
{
    public class MusicPlayer : LavalinkPlayer
    {
        private MusicService _service;
        private IBotCredentials _creds;

        public IMessageChannel OutputChannel { get; private set; }
        public IVoiceChannel VoiceChannel { get; private set; }

        public TrackTime CurrentTime { get; private set; }
        public Timer AutoDisconnectTimer;

        private PatreonPlayerFeatures _playerFeatures;
        private LavalinkQueue _queue;
        private TimeSpan _totalDuration;
        private bool _repeat;

        private const string YoutubeUrl = "https://youtu.be/{0}?list={1}";

        public MusicPlayer(LavalinkSocket lavalinkSocket, IDiscordClientWrapper client, ulong guildId, bool disconnectOnStop)
            : base(lavalinkSocket, client, guildId, disconnectOnStop) {}

        public void Initialize(MusicService service, IBotCredentials creds, IMessageChannel channel,
            IVoiceChannel voiceChannel, PatreonPlayerFeatures playerFeatures)
        {
            _service = service;
            _creds = creds;
            OutputChannel = channel;
            VoiceChannel = voiceChannel;

            CurrentTime = new TrackTime();

            _playerFeatures = playerFeatures;
            _queue = new LavalinkQueue();
            _totalDuration = TimeSpan.Zero;
        }

        public void ChangeOutputChannel(IMessageChannel channel)
            => OutputChannel = channel;

        public void ChangeVoiceChannel(IVoiceChannel voiceChannel)
            => VoiceChannel = voiceChannel;

        public async Task PlayAsync(ShardedCommandContext context, string query)
        {
            var trackLoadResponse = await LoadTracksAsync(query);
            if (trackLoadResponse is null)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "no_youtube_url");
                return;
            }

            Track track = null;
            switch (trackLoadResponse.LoadType)
            {
                case TrackLoadType.NoMatches:
                    await _service.ReplyErrorAsync(OutputChannel, GuildId, "track_load_no_matches");
                    return;
                case TrackLoadType.LoadFailed:
                    await _service.ReplyErrorAsync(OutputChannel, GuildId, "track_load_failed");
                    return;
                case TrackLoadType.SearchResult:
                    track = await ChooseTrack(context, trackLoadResponse.Tracks);
                    break;
                case TrackLoadType.TrackLoaded:
                    track = new Track(trackLoadResponse.Tracks[0], context.User);
                    break;
                case TrackLoadType.PlaylistLoaded:
                    await AddToQueueAsync(trackLoadResponse, context.User);
                    break;
            }

            if (track is null)
                return;

            if (track.IsLiveStream && !_playerFeatures.HasFlag(PatreonPlayerFeatures.Livestream))
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "player_feature_livestream", _creds.Patreon);
                return;
            }

            if (track.Duration > TimeSpan.FromHours(3) && !_playerFeatures.HasFlag(PatreonPlayerFeatures.LongTracks))
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "player_feature_long_tracks", 3, _creds.Patreon);
                return;
            }

            switch (State)
            {
                case PlayerState.NotPlaying:
                    await PlayNextTrackAsync(track);
                    break;
                case PlayerState.Playing:
                    await AddToQueueAsync(track);
                    break;
            }
        }

        private async Task<TrackLoadResponsePayload> LoadTracksAsync(string query)
        {
            if (Uri.IsWellFormedUriString(query, UriKind.Absolute))
            {
                if (!(query.Contains("youtube.com") || query.Contains("youtu.be")))
                    return null;

                YoutubeUrl youtubeUrl = null;
                if (!query.Contains("playlist"))
                {
                    youtubeUrl = MusicUtils.SanitizeYoutubeUrl(query);
                    query = string.Format(YoutubeUrl, youtubeUrl.VideoId, youtubeUrl.ListId);
                }

                if (!string.IsNullOrEmpty(youtubeUrl?.ListId))
                    await _service.ReplyConfirmationAsync(OutputChannel, GuildId, "enqueuing_tracks");
            }

            return await _service.LoadTracksAsync(query, SearchMode.YouTube);
        }

        private async Task<Track> ChooseTrack(ShardedCommandContext context, IEnumerable<LavalinkTrack> tracks)
        {
            var tracksList = tracks.ToList();

            var description = new StringBuilder();

            var length = tracksList.Count;
            if (length > 10)
                length = 10;

            for (var i = 0; i < length; i++)
            {
                var track = tracksList[i];
                description.Append($"#{i+1} [{track.Title}]({track.Source}) `{track.Duration.DigitalTimeSpanString()}`").Append("\n");
            }

            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                .WithTitle(_service.GetText(context.Guild.Id, "choose_track"))
                .WithDescription(description.ToString());

            await OutputChannel.SendMessageAsync(embed: embed.Build());

            var getUserInput = await _service.NextMessageAsync(context, TimeSpan.FromMinutes(1));
            if (getUserInput != null)
            {
                var userInput = getUserInput.Content.Replace("#", "");
                if (int.TryParse(userInput, out var input))
                {
                    if (input > 0 && input <= tracksList.Count)
                    {
                        return new Track(tracksList[input - 1], context.User);
                    }
                }
            }

            return null;
        }

        private async Task AddToQueueAsync(TrackLoadResponsePayload trackLoadResponse, IUser user)
        {
            _queue.AddRange(trackLoadResponse.Tracks.Where(x =>
            {
                if (x.IsLiveStream && !_playerFeatures.HasFlag(PatreonPlayerFeatures.Livestream))
                    return false;
                return x.Duration <= TimeSpan.FromMinutes(3) || _playerFeatures.HasFlag(PatreonPlayerFeatures.LongTracks);
            }).Select(t =>
            {
                if (!t.IsLiveStream)
                    _totalDuration += t.Duration;
                return new Track(t, user);
            }));
            var trackListPosition = trackLoadResponse.PlaylistInfo.SelectedTrack;
            if (trackListPosition > 0)
            {
                var track = _queue[trackListPosition];
                _queue.RemoveAt(trackListPosition);
                _queue.Insert(0, track);
            }

            await _service.ReplyConfirmationAsync(OutputChannel, GuildId, "tracks_enqueued",
                trackLoadResponse.Tracks.Length, trackLoadResponse.PlaylistInfo.Name);
        }

        private async Task AddToQueueAsync(Track track)
        {
            _queue.Add(track);
            if (!track.IsLiveStream)
                _totalDuration += track.Duration;

            var currentTrackDuration = CurrentTrack.IsLiveStream ? TimeSpan.Zero : CurrentTrack.Duration;
            var etp = _totalDuration + (currentTrackDuration - CurrentTime.Elapsed);

            var embed = new EmbedBuilder()
                .WithColor(_creds.ConfirmColor)
                .WithAuthor(_service.GetText(GuildId, "track_enqueued"), track.User.GetRealAvatarUrl())
                .WithDescription($"[{track.Title}]({track.Source})")
                .AddField(_service.GetText(GuildId, "track_channel"), track.Author, true)
                .AddField(_service.GetText(GuildId, "track_duration"), track.IsLiveStream
                    ? _service.GetText(GuildId, "livestream")
                    : track.Duration.DigitalTimeSpanString(), true)
                .AddField(_service.GetText(GuildId, "track_etp"), etp.DigitalTimeSpanString(), true);

            if (_repeat)
                embed.AddField(_service.GetText(GuildId, "repeat"), _service.GetText(GuildId, "#utility_enabled"), true);

            await OutputChannel.SendMessageAsync(embed: embed.Build());
        }

        public async Task PlayNextTrackAsync(Track track = null)
        {
            if (track is null)
            {
                if (_repeat)
                {
                    track = (Track) CurrentTrack;
                }
                else
                {
                    if (!_queue.TryDequeue(out var lavalinkTrack))
                        return;

                    track = (Track) lavalinkTrack;
                    if (!track.IsLiveStream)
                        _totalDuration -= track.Duration;
                }
            }
            else
            {
                var rangeDuration = TimeSpan.Zero;
                var index = 0;
                foreach (var tr in _queue)
                {
                    if (!tr.IsLiveStream)
                        rangeDuration += tr.Duration;
                    if (tr == track)
                    {
                        _queue.RemoveRange(0, index + 1);
                        _totalDuration -= rangeDuration;
                        break;
                    }

                    index++;
                }
            }

            await PlayAsync(track);
            CurrentTime.Restart();

            var outputChannelState = MusicUtils.CheckOutputChannel(_service.Client, GuildId, OutputChannel);
            if (outputChannelState != OutputChannelState.Available)
                return;

            var embed = new EmbedBuilder()
                .WithColor(_creds.ConfirmColor)
                .WithAuthor(_service.GetText(GuildId, "now_playing"), track.User.GetRealAvatarUrl())
                .WithDescription($"[{track.Title}]({track.Source})")
                .AddField(_service.GetText(GuildId, "track_channel"), track.Author, true)
                .AddField(_service.GetText(GuildId, "track_duration"), track.IsLiveStream
                    ? _service.GetText(GuildId, "livestream")
                    : track.Duration.DigitalTimeSpanString(), true)
                .AddField(_service.GetText(GuildId, "requested_by"), track.User, true);

            if (_repeat)
                embed.AddField(_service.GetText(GuildId, "repeat"), _service.GetText(GuildId, "#utility_enabled"), true);

            await OutputChannel.SendMessageAsync(embed: embed.Build());
        }

        public async Task LeaveAndDisposeAsync(bool sendMessage = true)
        {
            Dispose();
            if (!sendMessage) return;
            await _service.ReplyConfirmationAsync(OutputChannel, GuildId, "channel_disconnected", VoiceChannel.Name);
        }

        public async Task PauseAsync(bool sendMessage = true)
        {
            if (State == PlayerState.NotPlaying)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "player_not_playing");
                return;
            }

            if (State == PlayerState.Paused)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "player_is_paused");
                return;
            }

            await base.PauseAsync();
            CurrentTime.Stop();
            if (sendMessage)
                await _service.ReplyConfirmationAsync(OutputChannel, GuildId, "player_paused");
        }

        public async Task ResumeAsync(bool sendMessage = true)
        {
            if (State == PlayerState.NotPlaying)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "player_not_playing");
                return;
            }

            if (State == PlayerState.Playing)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "player_is_playing");
                return;
            }

            await base.ResumeAsync();
            CurrentTime.Start();
            if (sendMessage)
                await _service.ReplyConfirmationAsync(OutputChannel, GuildId, "player_resumed");
        }

        public async Task QueueAsync(int page)
        {
            if (CurrentTrack is null)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "queue_empty");
                return;
            }

            var pages = (int) Math.Ceiling((double) _queue.Count / 15);
            if (pages == 0)
                pages = 1;

            if (page > pages)
                page = pages;

            page--;

            if (page < 0)
                page = 0;

            var status = "⏹";

            switch (State)
            {
                case PlayerState.Playing:
                    status = "⏸";
                    break;
                case PlayerState.Paused:
                    status = "▶";
                    break;
                case PlayerState.NotPlaying:
                    status = "⏹";
                    break;
            }

            var description = new StringBuilder();
            if (CurrentTrack.IsLiveStream)
            {
                description.Append($"♾ {CurrentTrack.Title} `{_service.GetText(GuildId, "livestream")}`\n" +
                                   "▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬");
            }
            else
            {
                description.Append($"{status} {CurrentTrack.Title} `{CurrentTrack.Duration.DigitalTimeSpanString()}`\n" +
                                   "▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬");
            }

            if (!_queue.IsEmpty)
            {
                var index = page * 15;
                var tracks = _queue.Skip(page * 15).Take(15).Cast<Track>().ToList();
                foreach (var track in tracks)
                {
                    description.Append("\n").Append($"#{index + 1} {track.Title} | ")
                        .Append($"`{(track.IsLiveStream ? _service.GetText(GuildId, "livestream"): track.Duration.DigitalTimeSpanString())}`");
                    index++;
                }
            }

            var currentTrackDuration = CurrentTrack.IsLiveStream ? TimeSpan.Zero : CurrentTrack.Duration;
            var totalDuration = _totalDuration + (currentTrackDuration - CurrentTime.Elapsed);

            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                .WithTitle(_service.GetText(GuildId, "queue"))
                .WithDescription(description.ToString())
                .WithFooter($"{_service.GetText(GuildId, "#searches_page")} {page + 1}/{pages} | " +
                            _service.GetText(GuildId, "total_duration", totalDuration.DigitalTimeSpanString()));

            await OutputChannel.SendMessageAsync(embed: embed.Build());
        }

        public async Task NowPlayingAsync()
        {
            if (CurrentTrack is null)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "player_not_playing");
                return;
            }

            var currentPositionElapsed = CurrentTime.Elapsed;
            var elapsedTime = currentPositionElapsed.DigitalTimeSpanString();

            if (!CurrentTrack.IsLiveStream)
            {
                var timerBar = new StringBuilder();
                var position = currentPositionElapsed.TotalMilliseconds / CurrentTrack.Duration.TotalMilliseconds * 30;
                for (var i = 0; i < 30; i++)
                {
                    timerBar.Append(i == (int) position ? "⚫" : "▬");
                }

                elapsedTime = $"`{timerBar}`\n`{currentPositionElapsed.DigitalTimeSpanString()}/{CurrentTrack.Duration.DigitalTimeSpanString()}`";
            }

            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                .WithTitle(_service.GetText(GuildId, "now_playing"))
                .WithDescription($"[{CurrentTrack.Title}]({CurrentTrack.Source})\n\n{elapsedTime}")
                .AddField(_service.GetText(GuildId, "track_channel"), CurrentTrack.Author, true)
                .AddField(_service.GetText(GuildId, "track_duration"), CurrentTrack.IsLiveStream
                    ? _service.GetText(GuildId, "livestream")
                    : CurrentTrack.Duration.DigitalTimeSpanString(), true)
                .AddField(_service.GetText(GuildId, "requested_by"), ((Track) CurrentTrack).User, true);

            if (_repeat)
                embed.AddField(_service.GetText(GuildId, "repeat"), _service.GetText(GuildId, "#utility_enabled"), true);

            await OutputChannel.SendMessageAsync(embed: embed.Build());
        }

        public async Task SkipAsync()
        {
            if (_queue.IsEmpty)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "no_next_track");
            }
            else
            {
                await PlayNextTrackAsync();
            }
        }

        public async Task SkipToAsync(string title)
        {
            if (_queue.IsEmpty)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "no_next_track");
                return;
            }

            int? index = null;
            if (title.StartsWith("#"))
            {
                if (int.TryParse(title.Substring(1), out var ind))
                    index = ind;
            }

            Track track;

            if (index.HasValue)
            {
                index--;
                if (index < 0)
                {
                    await _service.ReplyErrorAsync(OutputChannel, GuildId, "track_index_less_than", 1);
                    return;
                }

                if (index >= _queue.Count)
                {
                    await _service.ReplyErrorAsync(OutputChannel, GuildId, "track_index_above");
                    return;
                }

                track = (Track) _queue[index.Value];
            }
            else
            {
                track = (Track) _queue.FirstOrDefault(x => x.Title.Contains(title, StringComparison.InvariantCultureIgnoreCase));
            }

            if (track is null)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "queue_no_track_found");
            }
            else
            {
                await PlayNextTrackAsync(track);
            }
        }

        public async Task SeekAsync(string time)
        {
            var position = global::RiasBot.Extensions.Extensions.ConvertToTimeSpan(time);

            if (State == PlayerState.NotPlaying)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "player_not_playing");
                return;
            }

            if (State == PlayerState.Paused)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "player_is_paused");
                return;
            }

            if (position > CurrentTrack.Duration)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "seek_position_over");
                return;
            }

            var currentPosition = CurrentTime.Elapsed;
            await SeekPositionAsync(position);
            CurrentTime.Update(position);

            var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                .WithTitle(_service.GetText(GuildId, "seek"))
                .AddField(_service.GetText(GuildId, "seek_from"), currentPosition.DigitalTimeSpanString(), true)
                .AddField(_service.GetText(GuildId, "seek_to"), position.DigitalTimeSpanString(), true);

            await OutputChannel.SendMessageAsync(embed: embed.Build());
        }

        public async Task SetVolumeAsync(int? volume)
        {
            if (!volume.HasValue)
            {
                await _service.ReplyConfirmationAsync(OutputChannel, GuildId, "current_volume", Volume * 100);
                return;
            }

            if (!_playerFeatures.HasFlag(PatreonPlayerFeatures.Volume))
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "player_feature_volume", _creds.Patreon);
                return;
            }

            if (volume < 0)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "volume_lower_zero");
                return;
            }

            if (volume > 100)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "volume_higher_than", 100);
                return;
            }

            if (State == PlayerState.NotPlaying)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "player_not_playing", 100);
                return;
            }

            await SetVolumeAsync((float) volume.Value / 100);
            await _service.ReplyErrorAsync(OutputChannel, GuildId, "volume_set", volume);
        }

        public async Task ShuffleAsync()
        {
            if (!_queue.Any())
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "queue_empty");
                return;
            }

            _queue.Shuffle();
            await _service.ReplyConfirmationAsync(OutputChannel, GuildId, "queue_shuffled");
        }

        public async Task ClearAsync()
        {
            if (!_queue.Any())
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "queue_empty");
                return;
            }

            _queue.Clear();
            _totalDuration = TimeSpan.Zero;
            await _service.ReplyConfirmationAsync(OutputChannel, GuildId, "queue_cleared");
        }

        public async Task RemoveAsync(string title)
        {
            if (_queue.IsEmpty)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "queue_empty");
                return;
            }

            int? index = null;
            if (title.StartsWith("#"))
            {
                if (int.TryParse(title.Substring(1), out var ind))
                    index = ind;
            }

            Track track;

            if (index.HasValue)
            {
                index--;
                if (index < 0)
                {
                    await _service.ReplyErrorAsync(OutputChannel, GuildId, "track_index_less_than", 1);
                    return;
                }

                if (index >= _queue.Count)
                {
                    await _service.ReplyErrorAsync(OutputChannel, GuildId, "track_index_above");
                    return;
                }

                track = (Track) _queue[index.Value];
            }
            else
            {
                track = (Track) _queue.FirstOrDefault(x => x.Title.Contains(title, StringComparison.InvariantCultureIgnoreCase));
            }

            if (track is null)
            {
                await _service.ReplyErrorAsync(OutputChannel, GuildId, "queue_no_track_found");
            }
            else
            {
                _queue.Remove(track);
                if (!track.IsLiveStream)
                    _totalDuration -= track.Duration;
                await _service.ReplyConfirmationAsync(OutputChannel, GuildId, "track_removed", track.Title);
            }
        }

        public async Task RepeatAsync()
        {
            _repeat = !_repeat;
            if (_repeat)
                await _service.ReplyConfirmationAsync(OutputChannel, GuildId, "repeat_enabled");
            else
                await _service.ReplyConfirmationAsync(OutputChannel, GuildId, "repeat_disabled");
        }

        public override async Task OnTrackEndAsync(TrackEndEventArgs eventArgs)
        {
            if (eventArgs.Reason != TrackEndReason.Finished)
                return;

            await base.OnTrackEndAsync(eventArgs);
            CurrentTime.Stop();

            if (!eventArgs.MayStartNext)
                return;

            await PlayNextTrackAsync();
        }

        public override async Task OnTrackExceptionAsync(TrackExceptionEventArgs eventArgs)
        {
            var outputChannelState = MusicUtils.CheckOutputChannel(_service.Client, GuildId, OutputChannel);
            if (outputChannelState != OutputChannelState.Available)
            {
                await PlayNextTrackAsync();
                return;
            }

            await _service.ReplyErrorAsync(OutputChannel, GuildId, "track_exception", eventArgs.Player.CurrentTrack.Title);
            await PlayNextTrackAsync();
        }

        public override async Task OnTrackStuckAsync(TrackStuckEventArgs eventArgs)
        {
            var outputChannelState = MusicUtils.CheckOutputChannel(_service.Client, GuildId, OutputChannel);
            if (outputChannelState != OutputChannelState.Available)
            {
                await PlayNextTrackAsync();
                return;
            }

            await _service.ReplyErrorAsync(OutputChannel, GuildId, "track_stuck", eventArgs.Player.CurrentTrack.Title);
            await PlayNextTrackAsync();
        }

        public class TrackTime
        {
            private readonly Stopwatch _stopwatch;
            private TimeSpan _offset;

            public TimeSpan Elapsed => _stopwatch.Elapsed + _offset;

            public TrackTime()
            {
                _stopwatch = new Stopwatch();
                _offset = TimeSpan.Zero;
            }

            public void Start()
            {
                _stopwatch.Start();
            }

            public void Stop()
            {
                _stopwatch.Stop();
                _offset = TimeSpan.Zero;
            }

            public void Restart()
            {
                _stopwatch.Restart();
                _offset = TimeSpan.Zero;
            }

            public void Update(TimeSpan offset)
            {
                _offset = offset;
                _stopwatch.Restart();
            }
        }
    }
}