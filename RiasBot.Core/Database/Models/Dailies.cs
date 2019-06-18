using System;

namespace RiasBot.Database.Models
{
    public class Dailies : DbEntity
    {
        public ulong UserId { get; set; }
        public DateTime NextDaily { get; set; }
    }
}