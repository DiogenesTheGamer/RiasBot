using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using RiasBot.Commons.Attributes;
using RiasBot.Services;
using RiasBot.Database.Models;

namespace RiasBot.Modules.Commands
{
    public class Commands : RiasModule
    {
        private readonly DbService _db;
        
        public Commands(DbService db)
        {
            _db = db;
        }

        [RiasCommand][Aliases]
        [Description][Usages]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(Discord.GuildPermission.Administrator)]
        public async Task DeleteCommandMessageAsync()
        {
            bool deleteCmdMsg;

            using (var db = _db.GetDbContext())
            {
                var guildDb = db.Guilds.FirstOrDefault(x => x.GuildId == Context.Guild.Id);
                if (guildDb != null)
                {
                    deleteCmdMsg = guildDb.DeleteCommandMessage = !guildDb.DeleteCommandMessage;
                }
                else
                {
                    var deleteCmdMsgDb = new GuildConfig { GuildId = Context.Guild.Id, DeleteCommandMessage = true };
                    await db.AddAsync(deleteCmdMsgDb);
                    deleteCmdMsg = true;
                }
                
                await db.SaveChangesAsync();
            }

            if (deleteCmdMsg)
            {
                await ReplyConfirmationAsync("del_cmd_msg_enabled");
            }
            else
            {
                await ReplyConfirmationAsync("del_cmd_msg_disabled");
            }
        }
    }
}