namespace DtrCheck.Core.Models;

public sealed class RulesFile
{
    public string? Questionnaire { get; init; }
    public string? Description { get; init; }
    public required Dictionary<string, RuleDefinition> Rules { get; init; }
}
