using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace SquadUp.ServiceDefaults;

/// <summary>
/// Removes Restricted values from structured log events before they are written.
/// </summary>
public sealed class RedactingTextWriterSink(ITextFormatter formatter, TextWriter writer) : ILogEventSink
{
    private const string RedactedValue = "[REDACTED]";

    private static readonly Regex SensitiveQueryParameter = new(
        @"([?&](?:code|state|access_token|refresh_token|token|invite(?:_code)?|client_secret)=[^&#\s]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex ConnectionStringFragment = new(
        @"(?:^|[;\s])(?:password|pwd|user\s*id|uid|username|host|server|database)\s*=",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        formatter.Format(Redact(logEvent), writer);
    }

    /// <summary>
    /// Produces a safe copy of an event. Exception messages and stacks are deliberately omitted.
    /// </summary>
    public static LogEvent Redact(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var properties = new List<LogEventProperty>(logEvent.Properties.Count + 1);
        foreach (var property in logEvent.Properties)
        {
            if (logEvent.Exception is not null &&
                property.Key.Equals("exception_type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            properties.Add(new LogEventProperty(
                property.Key,
                RedactValue(property.Key, property.Value)));
        }

        if (logEvent.Exception is not null)
        {
            properties.Add(new LogEventProperty(
                "exception_type",
                new ScalarValue(logEvent.Exception.GetType().Name)));
        }

        return new LogEvent(
            logEvent.Timestamp,
            logEvent.Level,
            exception: null,
            logEvent.MessageTemplate,
            properties);
    }

    private static LogEventPropertyValue RedactValue(string name, LogEventPropertyValue value)
    {
        if (IsSensitiveName(name))
        {
            return new ScalarValue(RedactedValue);
        }

        return value switch
        {
            ScalarValue { Value: string text } => new ScalarValue(RedactText(text)),
            SequenceValue sequence => new SequenceValue(sequence.Elements.Select(element => RedactValue(name, element))),
            StructureValue structure => new StructureValue(
                structure.Properties.Select(property => new LogEventProperty(
                    property.Name,
                    RedactValue(property.Name, property.Value))),
                structure.TypeTag),
            DictionaryValue dictionary => new DictionaryValue(dictionary.Elements.Select(pair => new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                pair.Key,
                pair.Key.Value is string key && IsSensitiveName(key)
                    ? new ScalarValue(RedactedValue)
                    : RedactValue(name, pair.Value)))),
            _ => value
        };
    }

    private static bool IsSensitiveName(string name) =>
        name.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("connectionstring", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("connection_string", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("invite", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("body", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("authorizationcode", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("oauthcode", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("code", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("state", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("body", StringComparison.OrdinalIgnoreCase);

    private static string RedactText(string value)
    {
        if (ConnectionStringFragment.IsMatch(value))
        {
            return RedactedValue;
        }

        return SensitiveQueryParameter.Replace(value, match =>
        {
            var separator = match.Value[0];
            var parameterName = match.Value[1..match.Value.IndexOf('=')];
            return $"{separator}{parameterName}={RedactedValue}";
        });
    }
}
