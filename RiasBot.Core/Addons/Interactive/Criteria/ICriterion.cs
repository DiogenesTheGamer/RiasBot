﻿using System.Threading.Tasks;
using Discord.Commands;

namespace Discord.Addons.Interactive
{
    public interface ICriterion<in T>
    {
        Task<bool> JudgeAsync(ShardedCommandContext sourceContext, T parameter);
    }
}
