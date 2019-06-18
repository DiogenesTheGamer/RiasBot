using System.Threading.Tasks;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Modules.Bot.Services;

namespace RiasBot.Modules.Bot
{
    public partial class Bot
    {
        public class Event : RiasSubmodule<EventService>
        {                       
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            public async Task EventAsync(int timeout, int reward, [Remainder]string message)
            {
                if (timeout <= 0)
                {
                    await ReplyErrorAsync("event_timeout_lower", 0);
                    return;
                }

                if (reward <= 0)
                {
                    await ReplyErrorAsync("event_reward_lower", 0);
                    return;
                }
                
                var userMessage = await Context.Channel.SendConfirmationMessageAsync(message);

                await Task.Run(async () => await Service.StartHeartEventAsync(userMessage, timeout, reward));
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            [RequireOwner]
            public async Task StopEvent()
            {
                await Service.StopHeartEventAsync();
                await ReplyConfirmationAsync("event_stopped");
            }
        }
    }
}