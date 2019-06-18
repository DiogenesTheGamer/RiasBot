using System.Linq;
using System.Runtime.CompilerServices;
using Discord.Commands;
using RiasBot.Services.Implementation;

namespace RiasBot.Commons.Attributes
{
    public class Aliases : AliasAttribute
    {
        public Aliases([CallerMemberName] string memberName = "")
            : base(Localization.LoadCommand(memberName.ToLowerInvariant()).Command.Split(' ').Skip(1).ToArray())
        {
        }
    }
}