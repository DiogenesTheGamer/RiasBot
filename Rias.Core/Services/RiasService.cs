using System;
using System.Threading.Tasks;
using Disqord;
using Microsoft.Extensions.DependencyInjection;
using Rias.Core.Extensions;
using Rias.Core.Implementation;
using Serilog;

namespace Rias.Core.Services
{
    public class RiasService
    {
        public readonly Rias RiasBot;
        public readonly Credentials Credentials;
        public readonly Localization Localization;

        public RiasService(IServiceProvider serviceProvider)
        {
            RiasBot = serviceProvider.GetRequiredService<Rias>();
            Credentials = serviceProvider.GetRequiredService<Credentials>();
            Localization = serviceProvider.GetRequiredService<Localization>();
        }
        
        /// <summary>
        /// Send a confirmation message with arguments. The form is an embed with the confirm color.<br/>
        /// If the key starts with "#", the first word delimited by "_" is the prefix for the translation.<br/>
        /// If the key doesn't start with "#", the prefix of the translation is the lower module name of this class.
        /// </summary>
        public Task<IUserMessage> ReplyConfirmationAsync(IMessageChannel channel, Snowflake guildId, string key, params object[] args)
        {
            return channel.SendConfirmationMessageAsync(Localization.GetText(guildId, key, args));
        }

        /// <summary>
        /// Send an error message with arguments. The form is an embed with the error color.<br/>
        /// If the key starts with "#", the first word delimited by "_" is the prefix for the translation.<br/>
        /// If the key doesn't start with "#", the prefix of the translation is the lower module type of this class.
        /// </summary>
        public Task<IUserMessage> ReplyErrorAsync(IMessageChannel channel, Snowflake guildId, string key, params object[] args)
        {
            return channel.SendErrorMessageAsync(Localization.GetText(guildId, key, args));
        }

        /// <summary>
        /// Get a translation text with or without arguments.<br/>
        /// If the key starts with "#", the first word delimited by "_" is the prefix for the translation.<br/>
        /// If the key doesn't start with "#", the prefix of the translation is the lower module type of this class.
        /// </summary>
        public string GetText(Snowflake? guildId, string key, params object[] args)
        {
            return Localization.GetText(guildId, key, args);
        }
        
        /// <summary>
        /// Run a task in an async way.
        /// </summary>
        /// <param name="func"></param>
        public Task RunTaskAsync(Task func)
        {
            Task.Run(async () =>
            {
                try
                {
                    await func;
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            });

            return Task.CompletedTask;
        }
        
        public void SplitPrefixKey(ref string? prefix, ref string key)
        {
            if (!key.StartsWith('#')) return;

            var index = key.IndexOf('_');
            prefix = key[1..index];
            key = key[++index..];
        }
    }
}