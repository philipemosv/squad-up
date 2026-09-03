namespace SquadUp.Profile.Application;

public sealed record ProfileDto(
    Guid PlayerId,
    string Nickname,
    string? TimeZoneId,
    string Status,
    string Version);

public sealed record UpdateProfileRequest(
    string Nickname,
    string? TimeZoneId,
    string? Status,
    string? ExpectedVersion);

public enum ProfileMutationOutcome
{
    Success,
    ValidationFailed,
    VersionRequired,
    VersionConflict
}

public sealed record ProfileMutationResult(
    ProfileMutationOutcome Outcome,
    ProfileDto? Profile = null,
    string? Error = null)
{
    public static ProfileMutationResult Success(ProfileDto profile) =>
        new(ProfileMutationOutcome.Success, profile);

    public static ProfileMutationResult Failed(ProfileMutationOutcome outcome, string error) =>
        new(outcome, Error: error);
}
