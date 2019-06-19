using RiasBot.Commons.Configs;

namespace RiasBot.Services
{
    public interface IBotCredentials
    {
        string Prefix { get; }
        string Token { get; }

        ulong MasterId { get; }
        string Currency { get; }
        string Invite { get; }
        string OwnerServerInvite { get; }
        ulong OwnerServerId { get; }

        uint ConfirmColor { get; }
        uint ErrorColor { get; }

        string Patreon { get; }
        string Website { get; }
        string WeebApi { get; }
        string DblVote { get; }

        string GoogleApiKey { get; }
        string UrbanDictionaryApiKey { get; }
        string PatreonAccessToken { get; }
        string DiscordBotsListApiKey { get; }

        string WeebServicesToken { get; }

        DatabaseConfig DatabaseConfig { get; }
        LavalinkConfig LavalinkConfig { get; }
        VotesManagerConfig VotesManagerConfig { get; }
        bool IsBeta { get; } //beta bool is too protect things to run only on the public version, like apis
    }
}