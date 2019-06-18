using Discord;
using RiasBot.Services.Implementation;

namespace RiasBot.Services
{
    public interface ILocalization
    {
        void SetGuildLocale(ulong guildId, string locale);
        void SetGuildLocale(IGuild guild, string locale);
        string GetGuildLocale(ulong guildId);
        string GetGuildLocale(IGuild guild);
        void RemoveGuildLocale(ulong guildId);
        void RemoveGuildLocale(IGuild guild);
    }
}