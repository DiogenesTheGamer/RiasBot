using System;
using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using RiasBot.Services;

namespace RiasBot.Commons.Attributes
{
    public class RateLimitAttribute : PreconditionAttribute
    {
        private readonly RateLimitType _rateLimitType;
        private readonly uint _invokesLimit;
        private readonly TimeSpan _drainRate;

        /// <summary>
        /// Set the maximum invokes limit, the drain rate in seconds and the rate limit type.
        /// </summary>
        public RateLimitAttribute(uint invokesLimit, int drainRate, RateLimitType rateLimitType = RateLimitType.Global)
        {
            _invokesLimit = invokesLimit;
            _drainRate = TimeSpan.FromSeconds(drainRate);
            _rateLimitType = rateLimitType;
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var service = services.GetRequiredService<RateLimitService>();
            var rule = GetRule(service, context, command);

            var limit = service.GetOrAdd(rule, new RateLimitInfo(_invokesLimit, _drainRate));
            if (limit.CanEnter())
                return Task.FromResult(PreconditionResult.FromSuccess());

            var timeLeft = limit.DrainRate - (DateTimeOffset.UtcNow - limit.LastInvoke);
            return Task.FromResult(PreconditionResult.FromError($"#rate_limit_exceeded:{timeLeft.Seconds}"));
        }

        private string GetRule(RateLimitService service, ICommandContext context, CommandInfo command)
        {
            switch (_rateLimitType)
            {
                case RateLimitType.User:
                    return service.GetUserRule(context.User, command);
                case RateLimitType.Guild:
                    return service.GetGuildRule(context.Guild, command);
                case RateLimitType.GuildUser:
                    return service.GetGuildUserRule(context.Guild, context.User, command);
                default:
                    return service.GetRule(command);
            }
        }
    }

    public enum RateLimitType
    {
        Global,
        User,
        Guild,
        GuildUser
    }
}