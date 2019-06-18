namespace RiasBot.Services
{
    public interface ITranslations
    {
        string GetText(ulong guildId, string lowerModuleTypeName, string key);
        string GetText(ulong guildId, string lowerModuleTypeName, string key, params object[] args);
    }
}