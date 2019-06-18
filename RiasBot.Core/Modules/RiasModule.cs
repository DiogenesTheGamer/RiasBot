using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RiasBot.Extensions;
using RiasBot.Services;

namespace RiasBot.Modules
{
    public abstract class RiasModule : ModuleBase
    {
        public readonly string LowerModuleTypeName;
        public readonly string ModuleTypeName;
        public ITranslations Translations { get; set; }

        protected RiasModule(bool isTopLevelModule = true)
        {
            ModuleTypeName = isTopLevelModule ? GetType().Name : GetType().DeclaringType.Name;
            LowerModuleTypeName = ModuleTypeName.ToLowerInvariant();
        }

        /// <summary>
        /// Send a confirmation message. The form is an embed with the confirm color.
        /// If the key starts with "#", the first word delimited by "_" is the prefix for the translation.
        /// If the key doesn't start with "#", the prefix of the translation is the lower module type of this class
        /// </summary>
        protected async Task<IUserMessage> ReplyConfirmationAsync(string key)
        {
            return await Context.Channel.SendConfirmationMessageAsync(Translations.GetText(Context.Guild.Id, LowerModuleTypeName, key));
        }

        /// <summary>
        /// Send a confirmation message with arguments. The form is an embed with the confirm color.
        /// If the key starts with "#", the first word delimited by "_" is the prefix for the translation.
        /// If the key doesn't start with "#", the prefix of the translation is the lower module type of this class
        /// </summary>
        protected async Task<IUserMessage> ReplyConfirmationAsync(string key, params object[] args)
        {
            return await Context.Channel.SendConfirmationMessageAsync(Translations.GetText(Context.Guild.Id, LowerModuleTypeName, key, args));
        }

        /// <summary>
        /// Send an error message. The form is an embed with the error color.
        /// If the key starts with "#", the first word delimited by "_" is the prefix for the translation.
        /// If the key doesn't start with "#", the prefix of the translation is the lower module type of this class
        /// </summary>
        protected async Task<IUserMessage> ReplyErrorAsync(string key)
        {
            return await Context.Channel.SendErrorMessageAsync(Translations.GetText(Context.Guild.Id, LowerModuleTypeName, key));
        }

        /// <summary>
        /// Send an error message with arguments. The form is an embed with the error color.
        /// If the key starts with "#", the first word delimited by "_" is the prefix for the translation.
        /// If the key doesn't start with "#", the prefix of the translation is the lower module type of this class
        /// </summary>
        protected async Task<IUserMessage> ReplyErrorAsync(string key, params object[] args)
        {
            return await Context.Channel.SendErrorMessageAsync(Translations.GetText(Context.Guild.Id, LowerModuleTypeName, key, args));
        }

        /// <summary>
        /// Get a translation text.
        /// If the key starts with "#", the first word delimited by "_" is the prefix for the translation.
        /// If the key doesn't start with "#", the prefix of the translation is the lower module type of this class
        /// </summary>
        protected string GetText(string key)
        {
            return Translations.GetText(Context.Guild.Id, LowerModuleTypeName, key);
        }

        /// <summary>
        /// Get a translation text with arguments.
        /// If the key starts with "#", the first word delimited by "_" is the prefix for the translation.
        /// If the key doesn't start with "#", the prefix of the translation is the lower module type of this class
        /// </summary>
        protected string GetText(string key, params object[] args)
        {
            return Translations.GetText(Context.Guild.Id, LowerModuleTypeName, key, args);
        }
    }

    public abstract class RiasModule<TService> : RiasModule
    {
        public TService Service { get; set; }

        public RiasModule(bool isTopLevel = true) : base(isTopLevel)
        {

        }
    }

    public abstract class RiasSubmodule : RiasModule
    {
        public RiasSubmodule() : base(false)
        {

        }
    }

    public abstract class RiasSubmodule<TService> : RiasModule<TService>
    {
        public RiasSubmodule() : base(false)
        {

        }
    }
}