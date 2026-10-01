using Microsoft.Extensions.Logging;

namespace Questao5.Tests.Infrastructure.Services;

internal sealed record TestLogEntry(
    LogLevel Level,
    EventId EventId,
    string Category,
    string Message);