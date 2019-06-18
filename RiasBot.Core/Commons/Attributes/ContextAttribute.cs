using Discord.Commands;

namespace RiasBot.Commons.Attributes
{
    public class ContextAttribute : RequireContextAttribute
    {
        public ContextAttribute(ContextType contexts) : base(contexts)
        {
            
        }
    }
}