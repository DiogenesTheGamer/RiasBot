using System;
using System.Collections.Generic;

namespace RiasBot.Commons
{
    public class DBL
    {
        public List<Votes> Votes { get; set; }
        public DateTime Date { get; set; }
    }
    
    public class Votes
    {
        public ulong Bot { get; set; }
        public ulong User { get; set; }
        public string Type { get; set; }
        public bool IsWeekend { get; set; }
        public string Query { get; set; }
        public DateTime Date { get; set; }
        public bool IsChecked { get; set; }
    }
}