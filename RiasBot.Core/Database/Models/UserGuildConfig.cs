using System;

namespace RiasBot.Database.Models
{
    public class UserGuildConfig : DbEntity
    {
        public ulong GuildId { get; set; }
        public ulong UserId { get; set; }
        public bool IsMuted { get; set; }
        public DateTime MuteUntil { get; set; }
    }
}