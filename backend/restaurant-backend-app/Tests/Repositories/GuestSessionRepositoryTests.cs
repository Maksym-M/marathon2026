using Amazon.DynamoDBv2.DataModel;
using APIHandler.Entities;
using APIHandler.Repositories.GuestSessions;
using Moq;

namespace APIHandler.Tests.Repositories;

public class GuestSessionRepositoryTests
{
    private const string GroupReservationId = "group-1";

    private readonly Mock<IDynamoDBContext> _mockContext = new();
    private readonly Mock<IBatchGet<GuestSession>> _mockBatchGet = new();
    private readonly GuestSessionRepository _repository;

    public GuestSessionRepositoryTests()
    {
        Environment.SetEnvironmentVariable("guestSessionTable", "GuestSessions");
        _mockContext
            .Setup(c => c.CreateBatchGet<GuestSession>(It.IsAny<BatchGetConfig>()))
            .Returns(_mockBatchGet.Object);
        _repository = new GuestSessionRepository(_mockContext.Object);
    }

    [Fact]
    public async Task GetAllByGroupReservationIdAsync_IndexStillListsDeletedSession_LeavesItOut()
    {
        var deleted = Session("s1", isSubmitted: true);
        var kept = Session("s2", isSubmitted: true);
        SetupIndex(deleted, kept);
        _mockBatchGet.SetupGet(b => b.Results).Returns([kept]);

        var sessions = await _repository.GetAllByGroupReservationIdAsync(GroupReservationId);

        Assert.Equal(["s2"], sessions.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAllByGroupReservationIdAsync_IndexIsBehind_ReturnsTheSessionAsStoredNow()
    {
        SetupIndex(Session("s1", isSubmitted: false));
        _mockBatchGet.SetupGet(b => b.Results).Returns([Session("s1", isSubmitted: true)]);

        var sessions = await _repository.GetAllByGroupReservationIdAsync(GroupReservationId);

        Assert.True(Assert.Single(sessions).IsSubmitted);
    }

    [Fact]
    public async Task GetAllByGroupReservationIdAsync_ReadsTheTableConsistently()
    {
        SetupIndex(Session("s1", isSubmitted: true));
        _mockBatchGet.SetupGet(b => b.Results).Returns([Session("s1", isSubmitted: true)]);

        await _repository.GetAllByGroupReservationIdAsync(GroupReservationId);

        _mockContext.Verify(c => c.CreateBatchGet<GuestSession>(It.Is<BatchGetConfig>(config => config.ConsistentRead == true)), Times.Once);
        _mockBatchGet.Verify(b => b.AddKey((object)"s1"), Times.Once);
    }

    [Fact]
    public async Task GetAllByGroupReservationIdAsync_NoSessions_DoesNotReadTheTable()
    {
        SetupIndex();

        var sessions = await _repository.GetAllByGroupReservationIdAsync(GroupReservationId);

        Assert.Empty(sessions);
        _mockContext.Verify(c => c.CreateBatchGet<GuestSession>(It.IsAny<BatchGetConfig>()), Times.Never);
    }

    private void SetupIndex(params GuestSession[] sessions)
    {
        var search = new Mock<IAsyncSearch<GuestSession>>();
        search.Setup(s => s.GetRemainingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(sessions.ToList());
        _mockContext
            .Setup(c => c.QueryAsync<GuestSession>(GroupReservationId, It.IsAny<QueryConfig>()))
            .Returns(search.Object);
    }

    private static GuestSession Session(string id, bool isSubmitted) => new()
    {
        Id = id,
        GroupReservationId = GroupReservationId,
        GuestName = id,
        IsSubmitted = isSubmitted,
        Dishes = [],
    };
}
