using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.Backend
{
    public enum ServiceStatus { Success, InvalidRequest, Unauthenticated, Conflict, RateLimited, Unavailable, IncompatibleVersion }
    public enum BackendHealthState { Healthy, Degraded, Unavailable }
    public sealed class BackendHealth
    {
        public string dependencyId { get; set; }
        public BackendHealthState state { get; set; }
        public int latencyMilliseconds { get; set; }
        public string failureCode { get; set; }
    }
    public interface IBackendHealthProbe
    {
        BackendHealth Check(string dependencyId);
    }
    [Serializable]
    public sealed class SecurityAuditEvent
    {
        public string eventId;
        public string actorPlayerId;
        public string targetPlayerId;
        public string actionCode;
        public string resultCode;
        public string correlationId;
        public long occurredAtUtcTicks;
        // No credentials, request bodies, chat, or private game data.
    }

    public sealed class ServiceReply<T>
    {
        public ServiceStatus Status { get; }
        public T Value { get; }
        public string CorrelationId { get; }
        public ServiceReply(ServiceStatus status, T value = default(T), string correlationId = null)
        { Status = status; Value = value; CorrelationId = correlationId; }
    }

    public sealed class MatchmakingRequest
    {
        public string PlayerId { get; set; }
        public string PartyId { get; set; }
        public string Region { get; set; }
        public string Platform { get; set; }
        public string ModeId { get; set; }
        public string Language { get; set; }
        public ClientVersionInfo Version { get; set; }
        public int PartySize { get; set; }
    }
    public sealed class MatchmakingTicket { public string TicketId { get; set; } public string PlayerId { get; set; } }
    public sealed class MatchedRoster { public string MatchId { get; set; } public string Region { get; set; } public List<string> PlayerIds { get; set; } }
    public interface IMatchmakingService
    {
        ServiceReply<MatchmakingTicket> Enqueue(MatchmakingRequest request, string authenticatedPlayerId);
        ServiceReply<bool> Cancel(string ticketId, string authenticatedPlayerId);
        ServiceReply<MatchedRoster> Poll(string ticketId, string authenticatedPlayerId);
    }

    /// <summary>Explicit offline/development response. Never fabricates tickets, rosters, or other players.</summary>
    public sealed class UnavailableMatchmakingService : IMatchmakingService
    {
        public ServiceReply<MatchmakingTicket> Enqueue(MatchmakingRequest request, string authenticatedPlayerId) => new ServiceReply<MatchmakingTicket>(ServiceStatus.Unavailable);
        public ServiceReply<bool> Cancel(string ticketId, string authenticatedPlayerId) => new ServiceReply<bool>(ServiceStatus.Unavailable);
        public ServiceReply<MatchedRoster> Poll(string ticketId, string authenticatedPlayerId) => new ServiceReply<MatchedRoster>(ServiceStatus.Unavailable);
    }

    public static class MatchmakingRequestValidator
    {
        public static ServiceStatus Validate(MatchmakingRequest request, string authenticatedPlayerId)
        {
            if (request == null || request.Version == null || string.IsNullOrWhiteSpace(request.PlayerId) || string.IsNullOrWhiteSpace(request.ModeId) ||
                string.IsNullOrWhiteSpace(request.Region) || string.IsNullOrWhiteSpace(request.Platform) || string.IsNullOrWhiteSpace(request.Language) || request.PartySize < 1 || request.PartySize > 6)
                return ServiceStatus.InvalidRequest;
            if (!string.Equals(request.PlayerId, authenticatedPlayerId, StringComparison.Ordinal)) return ServiceStatus.Unauthenticated;
            return ServiceStatus.Success;
        }

        public static bool IsValidRoster(MatchedRoster roster, int minimum = 4, int maximum = 6)
        {
            if (roster == null || string.IsNullOrWhiteSpace(roster.MatchId) || roster.PlayerIds == null || roster.PlayerIds.Count < minimum || roster.PlayerIds.Count > maximum) return false;
            return roster.PlayerIds.All(x => !string.IsNullOrWhiteSpace(x)) && roster.PlayerIds.Distinct(StringComparer.Ordinal).Count() == roster.PlayerIds.Count;
        }
    }

    public interface IAuthenticatedPlayer
    {
        bool IsAuthenticated { get; }
        string PlayerId { get; }
        string AccessToken { get; }
    }
    public interface IProductionProfileApi
    {
        ServiceReply<ThiefAndTheNecklace.PlayerProfile> Load(string playerId, string accessToken);
        ServiceReply<ThiefAndTheNecklace.PlayerProfile> Save(string playerId, long expectedProfileVersion, ThiefAndTheNecklace.PlayerProfile profile, string accessToken);
    }

    public sealed class UnavailableProductionProfileApi : IProductionProfileApi
    {
        public ServiceReply<ThiefAndTheNecklace.PlayerProfile> Load(string playerId, string accessToken) => new ServiceReply<ThiefAndTheNecklace.PlayerProfile>(ServiceStatus.Unavailable);
        public ServiceReply<ThiefAndTheNecklace.PlayerProfile> Save(string playerId, long expectedProfileVersion, ThiefAndTheNecklace.PlayerProfile profile, string accessToken) => new ServiceReply<ThiefAndTheNecklace.PlayerProfile>(ServiceStatus.Unavailable);
    }

    /// <summary>Ownership and reward truth belong to the authenticated provider-side implementation.</summary>
    public sealed class ProductionProfileGateway
    {
        private readonly IAuthenticatedPlayer _session;
        private readonly IProductionProfileApi _api;
        public ProductionProfileGateway(IAuthenticatedPlayer session, IProductionProfileApi api)
        { _session = session ?? throw new ArgumentNullException(nameof(session)); _api = api ?? throw new ArgumentNullException(nameof(api)); }
        public ServiceReply<ThiefAndTheNecklace.PlayerProfile> Load(string requestedPlayerId)
        {
            if (!_session.IsAuthenticated || string.IsNullOrWhiteSpace(_session.PlayerId) || string.IsNullOrWhiteSpace(_session.AccessToken)) return new ServiceReply<ThiefAndTheNecklace.PlayerProfile>(ServiceStatus.Unauthenticated);
            if (!string.Equals(requestedPlayerId, _session.PlayerId, StringComparison.Ordinal)) return new ServiceReply<ThiefAndTheNecklace.PlayerProfile>(ServiceStatus.Unauthenticated);
            var reply = _api.Load(_session.PlayerId, _session.AccessToken);
            if (reply.Status == ServiceStatus.Success && (reply.Value == null || reply.Value.playerId != _session.PlayerId)) return new ServiceReply<ThiefAndTheNecklace.PlayerProfile>(ServiceStatus.Conflict);
            return reply;
        }
        public ServiceReply<ThiefAndTheNecklace.PlayerProfile> Save(ThiefAndTheNecklace.PlayerProfile profile, long expectedVersion)
        {
            if (!_session.IsAuthenticated || string.IsNullOrWhiteSpace(_session.PlayerId) || string.IsNullOrWhiteSpace(_session.AccessToken)) return new ServiceReply<ThiefAndTheNecklace.PlayerProfile>(ServiceStatus.Unauthenticated);
            if (profile == null || profile.playerId != _session.PlayerId || profile.profileVersion != expectedVersion) return new ServiceReply<ThiefAndTheNecklace.PlayerProfile>(ServiceStatus.InvalidRequest);
            return _api.Save(_session.PlayerId, expectedVersion, profile, _session.AccessToken);
        }
    }
}
