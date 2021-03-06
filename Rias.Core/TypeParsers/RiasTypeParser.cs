using System.Threading.Tasks;
using Qmmands;
using Rias.Core.Implementation;

namespace Rias.Core.TypeParsers
{
    public abstract class RiasTypeParser<T> : TypeParser<T>
    {
        public override ValueTask<TypeParserResult<T>> ParseAsync(Parameter parameter, string value, CommandContext context)
            => ParseAsync(parameter, value, (RiasCommandContext) context);

        public abstract ValueTask<TypeParserResult<T>> ParseAsync(Parameter parameter, string value, RiasCommandContext context);
    }
}