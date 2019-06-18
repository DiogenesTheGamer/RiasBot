using System.Linq;
using System.Runtime.CompilerServices;
using Discord;
using Discord.Commands;
using RiasBot.Services;
using RiasBot.Services.Implementation;

namespace RiasBot.Commons.Attributes
{
    public class Usages : RemarksAttribute
    {
        public Usages([CallerMemberName] string memberName = "")
            : base(GetUsage(memberName))
        {

        }

        private static string GetUsage(string memberName)
        {
            var usage = Localization.LoadCommand(memberName.ToLowerInvariant()).Remarks;
            return string.Join("\n", usage
                .Select(x => Format.Code(x)));
        }
    }
}