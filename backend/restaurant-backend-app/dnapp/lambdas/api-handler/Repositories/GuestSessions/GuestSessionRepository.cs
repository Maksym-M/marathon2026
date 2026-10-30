using Amazon.DynamoDBv2.DataModel;
using APIHandler.Constants;
using APIHandler.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Repositories.GuestSessions;

public class GuestSessionRepository(IDynamoDBContext context) : IGuestSessionRepository
{
    public async Task<GuestSession?> GetByGuestSessionId(string id) => await context.LoadAsync<GuestSession?>(id, new LoadConfig()
    {
        OverrideTableName = ResourceNames.GuestSessionTableName
    });

    public async Task<GuestSession?> GetByGroupReservationId(string groupReservationId)
    {
        List<GuestSession> result = await context.QueryAsync<GuestSession>(groupReservationId, new QueryConfig
        {
            OverrideTableName = ResourceNames.GuestSessionTableName,
            IndexName = GlobalSecondaryIndexes.GuestSession.GroupReservationId
        }).GetRemainingAsync();

        return result.FirstOrDefault();
    }

    public async Task<GuestSession?> GetByTokenHash(string tokenHash)
    {
        List<GuestSession> result = await context.QueryAsync<GuestSession>(tokenHash, new QueryConfig
        {
            OverrideTableName = ResourceNames.GuestSessionTableName,
            IndexName = GlobalSecondaryIndexes.GuestSession.SessionTokenHash
        }).GetRemainingAsync();

        return result.FirstOrDefault();
    }

    /// <summary>
    /// Every session of the group as it is now. The groupReservationId index is only eventually consistent: right after
    /// a change it can still list a session that was just deleted, or a session as it was before it was submitted or
    /// edited — which let a removed guest reappear in the Owner's list and be merged into a submitted pre-order. So the
    /// index only says which sessions belong to the group; the sessions themselves are read from the table with a
    /// strongly consistent read, and one deleted in the meantime drops out.
    /// </summary>
    public async Task<List<GuestSession>> GetAllByGroupReservationIdAsync(string groupReservationId)
    {
        List<GuestSession> indexed = await context.QueryAsync<GuestSession>(groupReservationId, new QueryConfig
        {
            OverrideTableName = ResourceNames.GuestSessionTableName,
            IndexName = GlobalSecondaryIndexes.GuestSession.GroupReservationId
        }).GetRemainingAsync();

        if (indexed.Count == 0)
            return indexed;

        var batchGet = context.CreateBatchGet<GuestSession>(new BatchGetConfig
        {
            ConsistentRead = true,
            OverrideTableName = ResourceNames.GuestSessionTableName
        });

        foreach (var session in indexed)
            batchGet.AddKey(session.Id);

        await batchGet.ExecuteAsync();

        var current = batchGet.Results.ToDictionary(s => s.Id);
        return indexed.Where(s => current.ContainsKey(s.Id)).Select(s => current[s.Id]).ToList();
    }

    public async Task SaveAsync(GuestSession guestSession) => await context.SaveAsync(guestSession, new SaveConfig
    {
        OverrideTableName = ResourceNames.GuestSessionTableName
    });

    public async Task<int> CountByGroupReservationId(string groupReservationId)
    {
        List<GuestSession> result = await context.QueryAsync<GuestSession>(groupReservationId, new QueryConfig
        {
            OverrideTableName = ResourceNames.GuestSessionTableName,
            IndexName = GlobalSecondaryIndexes.GuestSession.GroupReservationId
        }).GetRemainingAsync();

        // The owner's own session doesn't take a guest seat.
        return result.Count(s => !s.IsOwnerSession);
    }

    public async Task DeleteAsync(GuestSession session)
    {
        await context.DeleteAsync(session, DynamoConfig.DeleteConfig(ResourceNames.GuestSessionTableName));
    }
}
