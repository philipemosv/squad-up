using Microsoft.Extensions.Logging;

namespace SquadUp.Api;

public static partial class IdentityAuditLog
{
    [LoggerMessage(
        EventId = 2200,
        Level = LogLevel.Information,
        Message = "Identity audit event {AuditAction} completed with {AuditResult} for {AuditActorId} on {AuditTargetType} {AuditTargetId}, correlation {CorrelationId}.")]
    public static partial void ActionCompleted(
        ILogger logger,
        string auditAction,
        string auditResult,
        Guid auditActorId,
        string auditTargetType,
        Guid auditTargetId,
        string correlationId);
}
