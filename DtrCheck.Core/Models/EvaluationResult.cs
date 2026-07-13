namespace DtrCheck.Core.Models;

public sealed class EvaluationResult
{
    public required string LinkId { get; init; }
    public required string Text { get; init; }
    public required EvaluationStatus Status { get; init; }
    public string? Value { get; init; }
    public List<object>? Evidence { get; init; }
    public string? Note { get; init; }
}
