using SquadUp.LobbyService.Domain;

namespace SquadUp.LobbyService.Domain.Tests;

public sealed class LobbyTests
{
    [Fact]
    public void AddMemberWhenCapacityIsReachedMarksLobbyFullAndEmitsOneCompletionEvent()
    {
        var lobby = CreateLobby(capacity: 2);

        lobby.AddMember(CreateMember(rankOrdinal: 2));
        lobby.AddMember(CreateMember(rankOrdinal: 3));

        Assert.Equal(LobbyStatus.Full, lobby.Status);
        Assert.Equal(2, lobby.MembersCount);
        var completion = Assert.Single(lobby.CompletedEvents);
        Assert.Equal(lobby.Id, completion.LobbyId);
    }

    [Fact]
    public void AddMemberWhenLobbyIsFullRejectsFurtherMembersWithoutAnotherCompletionEvent()
    {
        var lobby = CreateLobby(capacity: 2);
        lobby.AddMember(CreateMember());
        lobby.AddMember(CreateMember());

        var exception = Assert.Throws<InvalidOperationException>(() => lobby.AddMember(CreateMember()));

        Assert.Contains("Recruiting", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, lobby.MembersCount);
        Assert.Single(lobby.CompletedEvents);
    }

    [Fact]
    public void AddMemberWhenPlayerAlreadyJoinedRejectsDuplicate()
    {
        var lobby = CreateLobby();
        var member = CreateMember();
        lobby.AddMember(member);

        var exception = Assert.Throws<InvalidOperationException>(() => lobby.AddMember(member));

        Assert.Contains("only once", exception.Message, StringComparison.Ordinal);
        Assert.Single(lobby.Members);
    }

    [Fact]
    public void AddMemberWhenRankDoesNotMeetRequirementRejectsMember()
    {
        var lobby = CreateLobby();

        var exception = Assert.Throws<InvalidOperationException>(
            () => lobby.AddMember(CreateMember(rankOrdinal: 1)));

        Assert.Contains("rank", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(lobby.Members);
    }

    [Fact]
    public void LifecycleTransitionsThroughTheOnlyHappyPath()
    {
        var lobby = CreateLobby(capacity: 2);
        lobby.AddMember(CreateMember());
        lobby.AddMember(CreateMember());

        lobby.StartProvisioning();
        lobby.MarkReady();
        lobby.Complete();

        Assert.Equal(LobbyStatus.Completed, lobby.Status);
    }

    [Fact]
    public void LifecycleWhenTransitionIsInvalidRejectsIt()
    {
        var lobby = CreateLobby();

        var exception = Assert.Throws<InvalidOperationException>(lobby.MarkReady);

        Assert.Contains("Provisioning", exception.Message, StringComparison.Ordinal);
        Assert.Equal(LobbyStatus.Recruiting, lobby.Status);
    }

    [Fact]
    public void CancelAndExpireAllowOnlyActiveStates()
    {
        var cancelledLobby = CreateLobby();
        cancelledLobby.Cancel();
        Assert.Equal(LobbyStatus.Cancelled, cancelledLobby.Status);
        Assert.Throws<InvalidOperationException>(cancelledLobby.Expire);

        var expiredLobby = CreateLobby();
        expiredLobby.Expire();
        Assert.Equal(LobbyStatus.Expired, expiredLobby.Status);
        Assert.Throws<InvalidOperationException>(expiredLobby.Cancel);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(101)]
    public void ConstructorWhenCapacityIsOutsideDomainBoundsRejectsIt(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateLobby(capacity));
    }

    [Fact]
    public void RankRequirementUsesGameIdAndCatalogOrdinalsInsteadOfRankNames()
    {
        var requirement = new RankRequirement(" DOTA2 ", minimumOrdinal: 2, maximumOrdinal: 4);

        Assert.True(requirement.IsSatisfiedBy(new PlayerRank("dota2", 3)));
        Assert.False(requirement.IsSatisfiedBy(new PlayerRank("dota2", 5)));
        Assert.False(requirement.IsSatisfiedBy(new PlayerRank("cs2", 3)));
    }

    [Fact]
    public void RankRequirementWhenRankIsMissingRejectsIt()
    {
        var requirement = new RankRequirement("dota2", minimumOrdinal: 2);

        Assert.Throws<ArgumentNullException>(() => requirement.IsSatisfiedBy(null!));
    }

    private static Lobby CreateLobby(int capacity = 3) => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        capacity,
        new RankRequirement("dota2", minimumOrdinal: 2, maximumOrdinal: 5));

    private static LobbyMember CreateMember(int rankOrdinal = 2) => new(
        Guid.CreateVersion7(),
        Guid.NewGuid().ToString("N"),
        "Synthetic Player",
        new PlayerRank("dota2", rankOrdinal));
}
