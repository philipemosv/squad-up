namespace SquadUp.LobbyService.Domain;

public sealed class Lobby
{
    public const int MinimumCapacity = 2;
    public const int MaximumCapacity = 100;

    private readonly List<LobbyMember> members = [];
    private readonly List<LobbyCompleted> completedEvents = [];

    public Lobby(Guid id, Guid ownerPlayerId, int capacity, RankRequirement rankRequirement)
    {
        Id = id != Guid.Empty
            ? id
            : throw new ArgumentException("Lobby id must not be empty.", nameof(id));
        OwnerPlayerId = ownerPlayerId != Guid.Empty
            ? ownerPlayerId
            : throw new ArgumentException("Owner player id must not be empty.", nameof(ownerPlayerId));
        Capacity = capacity is >= MinimumCapacity and <= MaximumCapacity
            ? capacity
            : throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                $"Capacity must be between {MinimumCapacity} and {MaximumCapacity}.");
        RankRequirement = rankRequirement ?? throw new ArgumentNullException(nameof(rankRequirement));
        Status = LobbyStatus.Recruiting;
    }

    public Guid Id { get; }

    public Guid OwnerPlayerId { get; }

    public int Capacity { get; }

    public RankRequirement RankRequirement { get; }

    public LobbyStatus Status { get; private set; }

    public int MembersCount => members.Count;

    public IReadOnlyList<LobbyMember> Members => members.AsReadOnly();

    public IReadOnlyList<LobbyCompleted> CompletedEvents => completedEvents.AsReadOnly();

    public void AddMember(LobbyMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        EnsureStatus(LobbyStatus.Recruiting, "accept a member");

        if (!RankRequirement.IsSatisfiedBy(member.Rank))
        {
            throw new InvalidOperationException("The player's rank does not satisfy this lobby's rank requirement.");
        }

        if (members.Any(existing => existing.PlayerId == member.PlayerId))
        {
            throw new InvalidOperationException("A player can join a lobby only once.");
        }

        members.Add(member);

        if (MembersCount == Capacity)
        {
            Status = LobbyStatus.Full;
            completedEvents.Add(new LobbyCompleted(Id));
        }
    }

    public void StartProvisioning()
    {
        EnsureStatus(LobbyStatus.Full, "start provisioning");
        Status = LobbyStatus.Provisioning;
    }

    public void MarkReady()
    {
        EnsureStatus(LobbyStatus.Provisioning, "become ready");
        Status = LobbyStatus.Ready;
    }

    public void Complete()
    {
        EnsureStatus(LobbyStatus.Ready, "complete");
        Status = LobbyStatus.Completed;
    }

    public void Cancel()
    {
        if (Status is LobbyStatus.Cancelled or LobbyStatus.Completed or LobbyStatus.Expired)
        {
            throw new InvalidOperationException($"A lobby in {Status} state cannot be cancelled.");
        }

        Status = LobbyStatus.Cancelled;
    }

    public void Expire()
    {
        if (Status is not (LobbyStatus.Recruiting or LobbyStatus.Full))
        {
            throw new InvalidOperationException($"A lobby in {Status} state cannot expire.");
        }

        Status = LobbyStatus.Expired;
    }

    private void EnsureStatus(LobbyStatus expectedStatus, string operation)
    {
        if (Status != expectedStatus)
        {
            throw new InvalidOperationException(
                $"A lobby must be {expectedStatus} to {operation}; its current state is {Status}.");
        }
    }
}
