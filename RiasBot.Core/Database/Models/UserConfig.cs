﻿using System;

namespace RiasBot.Database.Models
{
    public class UserConfig : DbEntity
    {
        public ulong UserId { get; set; }
        public int Currency { get; set; }
        public int Xp { get; set; }
        public int Level { get; set; }
        public DateTime MessageDateTime { get; set; }
        public bool IsBlacklisted { get; set; }
        public bool IsBanned { get; set; }
    }
}