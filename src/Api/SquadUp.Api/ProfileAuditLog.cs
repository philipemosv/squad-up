using Microsoft.Extensions.Logging;

namespace SquadUp.Api;

public static partial class ProfileAuditLog
{
    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Information,
        Message = "Profile audit event {AuditAction} completed with {AuditResult} for {AuditActorId} on {AuditTargetType} {AuditTargetId}, correlation {CorrelationId}.")]
    public static partial void MutationCompleted(
        ILogger logger,
        string auditAction,
        string auditResult,
        Guid auditActorId,
        string auditTargetType,
        Guid auditTargetId,
        string correlationId);
}
