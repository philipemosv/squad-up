using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SquadUp.Profile.Application;
using SquadUp.Profile.Domain;

namespace SquadUp.Profile.Infrastructure;

internal sealed class PlayerProfileService(ProfileDbContext context) : IPlayerProfileService
{
    public async Task<ProfileDto?> GetAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var profile = await context.PlayerProfiles
            .FirstOrDefaultAsync(candidate => candidate.PlayerId == playerId, cancellationToken);
        return profile is null ? null : ToDto(profile, ReadVersion(context.Entry(profile)));
    }

    public async Task<ProfileMutationResult> UpsertAsync(
        Guid playerId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseStatus(request.Status, out var status))
        {
            return ProfileMutationResult.Failed(
                ProfileMutationOutcome.ValidationFailed,
                "Status must be 'Active' or 'Hidden' when provided.");
        }

        var existing = await context.PlayerProfiles
            .FirstOrDefaultAsync(candidate => candidate.PlayerId == playerId, cancellationToken);

        EntityEntry<PlayerProfile> entry;
        try
        {
            if (existing is null)
            {
                var created = new PlayerProfile(playerId, request.Nickname, request.TimeZoneId);
                created.Update(request.Nickname, request.TimeZoneId, status ?? ProfileStatus.Active);
                entry = context.PlayerProfiles.Add(created);
            }
            else
            {
                if (!TryParseVersion(request.ExpectedVersion, out var expectedVersion))
                {
                    return ProfileMutationResult.Failed(
                        ProfileMutationOutcome.VersionRequired,
                        "ExpectedVersion is required to update an existing profile.");
                }

                entry = context.Entry(existing);
                entry.Property<uint>("xmin").OriginalValue = expectedVersion;
                existing.Update(request.Nickname, request.TimeZoneId, status ?? existing.Status);
            }
        }
        catch (ArgumentException exception)
        {
            return ProfileMutationResult.Failed(ProfileMutationOutcome.ValidationFailed, exception.Message);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ProfileMutationResult.Failed(
                ProfileMutationOutcome.VersionConflict,
                "The profile was modified by another request.");
        }

        return ProfileMutationResult.Success(ToDto(entry.Entity, ReadVersion(entry)));
    }

    private static bool TryParseStatus(string? status, out ProfileStatus? parsed)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            parsed = null;
            return true;
        }

        if (Enum.TryParse<ProfileStatus>(status, ignoreCase: true, out var value))
        {
            parsed = value;
            return true;
        }

        parsed = null;
        return false;
    }

    private static bool TryParseVersion(string? version, out uint parsed) =>
        uint.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out parsed);

    private static string ReadVersion(EntityEntry<PlayerProfile> entry) =>
        entry.Property<uint>("xmin").CurrentValue.ToString(CultureInfo.InvariantCulture);

    private static ProfileDto ToDto(PlayerProfile profile, string version) => new(
        profile.PlayerId,
        profile.Nickname,
        profile.TimeZoneId,
        profile.Status.ToString(),
        version);
}
