using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;
using RiasBot.Commons.Attributes;
using RiasBot.Services;

namespace RiasBot.Modules.Bot.Services
{
    [Service]
    public class BotService
    {
        private readonly DiscordShardedClient _client;
        private readonly DbService _db;
        private readonly IServiceProvider _services;

        private readonly string[] _codeLanguages = { "c#", "cs", "csharp" };

        public BotService(IServiceProvider services)
        {
            _client = services.GetRequiredService<DiscordShardedClient>();
            _db = services.GetRequiredService<DbService>();
            _services = services;
        }

        public async Task<EvaluationDetails> EvaluateAsync(ICommandContext context, string code)
        {
            var references = new[]
            {
                typeof(RiasBot).Assembly,
            };

            var globals = new Globals
            {
                Context = context,
                Client = _client,
                Services = _services,
                Db = _db
            };

            var imports = new[]
            {
                "System", "System.Collections.Generic", "System.Linq", "Discord", "Discord.WebSocket",
                "System.Threading.Tasks", "System.Text", "RiasBot.Extensions", "Microsoft.Extensions.DependencyInjection", "System.Web"
            };

            var scriptOptions = ScriptOptions.Default.WithReferences(references).AddImports(imports);
            code = SanitizeCode(code);

            var sw = Stopwatch.StartNew();

            var script = CSharpScript.Create(code, scriptOptions, typeof(Globals));
            var diagnostics = script.Compile();
            sw.Stop();

            var compilationTime = sw.Elapsed;

            if (diagnostics.Any(x => x.Severity == DiagnosticSeverity.Error))
            {
                return new EvaluationDetails
                {
                    CompilationTime = compilationTime,
                    Code = code,
                    IsCompiled = false,
                    Exception = string.Join('\n', diagnostics.Select(x => x.ToString()))
                };
            }

            sw.Restart();

            try
            {
                var result = await script.RunAsync(globals);
                sw.Stop();

                if (result.ReturnValue is null)
                    return null;

                var evaluationDetails = new EvaluationDetails
                {
                    CompilationTime = compilationTime,
                    ExecutionTime = sw.Elapsed,
                    Code = code,
                    IsCompiled = true,
                    Success = true
                };

                var returnValue = result.ReturnValue;
                var type = result.ReturnValue.GetType();

                switch (returnValue)
                {
                    case string str:
                        evaluationDetails.Result = str;
                        evaluationDetails.ReturnType = type.Name;
                        break;

                    case IEnumerable enumerable:
                        var list = enumerable.Cast<object>().ToList();
                        var enumType = enumerable.GetType();

                        evaluationDetails.Result = list.Any() ? $"[{string.Join(", ", list)}]" : "The collection is empty";
                        evaluationDetails.ReturnType = $"{enumType.Name}<{string.Join(", ", enumType.GenericTypeArguments.Select(t => t.Name))}>";
                        break;

                    case Enum @enum:
                        evaluationDetails.Result = @enum.ToString();
                        evaluationDetails.ReturnType = @enum.GetType().Name;
                        break;

                    default:
                        evaluationDetails.Result = returnValue.ToString();
                        evaluationDetails.ReturnType = type.Name;
                        break;
                }

                return evaluationDetails;
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new EvaluationDetails
                {
                    CompilationTime = compilationTime,
                    ExecutionTime = sw.Elapsed,
                    Code = code,
                    IsCompiled = true,
                    Success = false,
                    Exception = ex.Message
                };
            }
            finally
            {
                GC.Collect();
            }
        }

        private string SanitizeCode(string code)
        {
            code = code.Trim('`');

            foreach (var language in _codeLanguages)
            {
                var nIndex = code.IndexOf('\n');
                if (nIndex == -1)
                    break;

                var substring = code.Substring(0, code.IndexOf('\n'));
                if (!string.IsNullOrWhiteSpace(substring) && string.Equals(substring, language))
                {
                    return code.Substring(language.Length);
                }
            }

            return code;
        }

        public class Globals
        {
            public ICommandContext Context { get; set; }
            public DiscordShardedClient Client { get; set; }
            public IServiceProvider Services { get; set; }
            public DbService Db { get; set; }
        }
    }

    public class EvaluationDetails
    {
        public TimeSpan CompilationTime { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public string Code { get; set; }
        public string Result { get; set; }
        public string ReturnType { get; set; }
        public bool IsCompiled { get; set; }
        public bool Success { get; set; }
        public string Exception { get; set; }
    }
}