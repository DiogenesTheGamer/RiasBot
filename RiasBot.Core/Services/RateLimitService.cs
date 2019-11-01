using System;
using System.Collections.Concurrent;
using Discord;
using Discord.Commands;
using RiasBot.Commons.Attributes;

namespace RiasBot.Services
{
    [Service]
    public class RateLimitService
    {
        private readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimits;

        public RateLimitService()
        {
            _rateLimits = new ConcurrentDictionary<string, RateLimitInfo>();
        }

        public RateLimitInfo GetOrAdd(string rule, RateLimitInfo rateLimitInfo)
            => _rateLimits.GetOrAdd(rule, rateLimitInfo);

        /// <summary>
        /// Get a RateLimit rule for global ratelimits.
        /// </summary>
        public string GetRule(CommandInfo command)
            => $"{command.Module.Name}#{command.Name}";

        /// <summary>
        /// Get a RateLimit rule for user ratelimits.
        /// </summary>
        public string GetUserRule(IUser user, CommandInfo command)
            => $"user:{user.Id}/{GetRule(command)}";

        /// <summary>
        /// Get a RateLimit rule for guild ratelimits.
        /// </summary>
        public string GetGuildRule(IGuild guild, CommandInfo command)
            => $"guild:{guild.Id}/{GetRule(command)}";

        /// <summary>
        /// Get a RateLimit rule for guild-user ratelimits.
        /// </summary>
        public string GetGuildUserRule(IGuild guild, IUser user, CommandInfo command)
            => $"guilduser:{guild.Id}#{user.Id}/{GetRule(command)}";
    }

    public class RateLimitInfo
    {
        private readonly uint _invokesLimit;
        private int _invokes;

        public DateTimeOffset LastInvoke { get; private set; }
        public TimeSpan DrainRate { get; }

        public RateLimitInfo(uint invokesLimit, TimeSpan drainRate)
        {
            _invokesLimit = invokesLimit;
            DrainRate = drainRate;
            LastInvoke = DateTimeOffset.UtcNow;
        }

        public bool CanEnter()
        {
            if (DateTimeOffset.UtcNow - LastInvoke < DrainRate)
            {
                _invokes++;
            }
            else
            {
                LastInvoke = DateTimeOffset.UtcNow;
                _invokes = 0;
                return true;
            }

            return _invokes <= _invokesLimit;
        }
    }
}