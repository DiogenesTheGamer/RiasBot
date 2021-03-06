using System;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;
using Rias.Core.Commons;
using Rias.Core.Implementation;

namespace Rias.Core.Attributes
{
    public class ContextAttribute : RiasCheckAttribute
    {
        private readonly ContextType _contexts;

        public ContextAttribute(ContextType contexts)
        {
            _contexts = contexts;
        }

        public override ValueTask<CheckResult> CheckAsync(RiasCommandContext context)
        {
            var localization = context.ServiceProvider.GetRequiredService<Localization>();

            var isValid = false;

            if ((_contexts & ContextType.Guild) != 0)
                isValid = context.Channel is CachedGuildChannel;
            if ((_contexts & ContextType.DM) != 0)
                isValid = isValid || context.Channel is CachedDmChannel;

            if (isValid)
                return CheckResult.Successful;

            var guildId = context.Guild?.Id;
            var contexts = _contexts.ToString()
                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                .Select(x => localization.GetText(guildId, Localization.CommonContextType(x.ToLower())));

            var contextsHumanized = contexts.Humanize(x => $"**{x}**", localization.GetText(guildId, Localization.CommonOr).ToLowerInvariant());
            return CheckResult.Unsuccessful(localization.GetText(guildId, Localization.AttributeContext, contextsHumanized));
        }
    }
}