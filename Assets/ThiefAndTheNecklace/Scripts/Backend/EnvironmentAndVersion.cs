using System;

namespace ThiefAndTheNecklace.Backend
{
    public enum EnvironmentType { Development, Staging, Production, Qa }

    /// <summary>Injected by the build/bootstrap configuration; never inferred from a scene name.</summary>
    public sealed class EnvironmentConfig
    {
        public EnvironmentType Environment { get; }
        public string ServiceProfile { get; }
        public bool EnableDebugTools { get; }
        public bool EnableSyntheticAccounts { get; }

        public EnvironmentConfig(EnvironmentType environment, string serviceProfile, bool enableDebugTools, bool enableSyntheticAccounts)
        {
            if (!Enum.IsDefined(typeof(EnvironmentType), environment)) throw new ArgumentOutOfRangeException(nameof(environment));
            if (string.IsNullOrWhiteSpace(serviceProfile)) throw new ArgumentException("A service profile identifier is required.", nameof(serviceProfile));
            Environment = environment;
            ServiceProfile = serviceProfile;
            EnableDebugTools = environment != EnvironmentType.Production && enableDebugTools;
            EnableSyntheticAccounts = environment != EnvironmentType.Production && enableSyntheticAccounts;
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(ServiceProfile) && Enum.IsDefined(typeof(EnvironmentType), Environment);
    }

    public sealed class ClientVersionInfo
    {
        public string GameVersion { get; }
        public int ContentVersion { get; }
        public int ProfileSchemaVersion { get; }
        public int NetworkProtocolVersion { get; }
        public ClientVersionInfo(string gameVersion, int contentVersion, int profileSchemaVersion, int networkProtocolVersion)
        {
            GameVersion = gameVersion ?? string.Empty;
            ContentVersion = contentVersion;
            ProfileSchemaVersion = profileSchemaVersion;
            NetworkProtocolVersion = networkProtocolVersion;
        }
    }

    public sealed class ProtocolHandshake
    {
        public string PlayerId { get; }
        public ClientVersionInfo Version { get; }
        public string CorrelationId { get; }
        public ProtocolHandshake(string playerId, ClientVersionInfo version, string correlationId)
        { PlayerId = playerId; Version = version; CorrelationId = correlationId; }
    }

    public enum CompatibilityFailure { None, Unauthenticated, InvalidRequest, GameVersion, ContentVersion, ProfileSchema, NetworkProtocol }

    public static class VersionCompatibility
    {
        public static CompatibilityFailure Validate(ProtocolHandshake hello, string authenticatedPlayerId, ClientVersionInfo required)
        {
            if (hello == null || hello.Version == null || required == null) return CompatibilityFailure.InvalidRequest;
            if (string.IsNullOrWhiteSpace(authenticatedPlayerId) || hello.PlayerId != authenticatedPlayerId) return CompatibilityFailure.Unauthenticated;
            if (!string.Equals(hello.Version.GameVersion, required.GameVersion, StringComparison.Ordinal)) return CompatibilityFailure.GameVersion;
            if (hello.Version.ContentVersion != required.ContentVersion) return CompatibilityFailure.ContentVersion;
            if (hello.Version.ProfileSchemaVersion != required.ProfileSchemaVersion) return CompatibilityFailure.ProfileSchema;
            if (hello.Version.NetworkProtocolVersion != required.NetworkProtocolVersion) return CompatibilityFailure.NetworkProtocol;
            return CompatibilityFailure.None;
        }
    }
}
