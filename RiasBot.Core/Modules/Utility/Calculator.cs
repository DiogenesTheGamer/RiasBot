using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using NCalc;
using RiasBot.Commons.Attributes;
using RiasBot.Extensions;
using RiasBot.Services;

namespace RiasBot.Modules.Utility
{
    public partial class Utility
    {
        public class Calculator : RiasSubmodule
        {
            private readonly IBotCredentials _creds;
            
            public Calculator(IBotCredentials creds)
            {
                _creds = creds;
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            public async Task CalculatorAsync([Remainder]string expression)
            {
                var expr = new Expression(expression, EvaluateOptions.IgnoreCase);
                expr.EvaluateParameter += ExpressionEvaluateParameter;
                expr.EvaluateFunction += ExpressionEvaluateFunction;

                var embed = new EmbedBuilder().WithColor(_creds.ConfirmColor)
                    .WithTitle(GetText("calculator"))
                    .AddField(GetText("expression"), expression);

                try
                {
                    var result = expr.Evaluate();
                    embed.AddField(GetText("result"), result);
                }
                catch
                {
                    embed.AddField(GetText("error"), !string.IsNullOrEmpty(expr.Error) ? expr.Error : GetText("expression_failed"));
                }

                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }
            
            [RiasCommand][Aliases]
            [Description][Usages]
            public async Task CalcOpsAsync()
            {
                var selection = typeof(Math).GetTypeInfo()
                    .GetMethods()
                    .Distinct(new MethodInfoEqualityComparer())
                    .Select(x => x.Name)
                    .Except(new[]
                    {
                        "ToString",
                        "Equals",
                        "GetHashCode",
                        "GetType"
                    });

                await Context.Channel.SendConfirmationMessageAsync(string.Join(", ", selection));
            }
            
            private static void ExpressionEvaluateParameter(string name, ParameterArgs args)
            {
                switch (name.ToLowerInvariant())
                {
                    case "pi":
                        args.Result = Math.PI;
                        break;
                    case "e":
                        args.Result = Math.E;
                        break;
                    default:
                        args.Result = default;
                        break;
                }
            }

            private static void ExpressionEvaluateFunction(string name, FunctionArgs args)
            {
                if (string.Equals(name, "daysbetween", StringComparison.InvariantCultureIgnoreCase))
                {
                    var date1 = (DateTime) args.Parameters[0].Evaluate();
                    var date2 = (DateTime) args.Parameters[1].Evaluate();
                    var timespan = date2 - date1;
                    args.Result = timespan.TotalDays;
                }
                
                //TODO: Add more date-time operations
            }
            
            private class MethodInfoEqualityComparer : IEqualityComparer<MethodInfo>
            {
                public bool Equals(MethodInfo x, MethodInfo y) => x.Name == y.Name;

                public int GetHashCode(MethodInfo obj) => obj.Name.GetHashCode();
            }
        }
    }
}