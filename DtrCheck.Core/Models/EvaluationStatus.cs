namespace DtrCheck.Core.Models;

/// <summary>
/// Serialized as snake_case ("answered" / "gap" / "not_applicable") via a
/// JsonStringEnumConverter registered globally in the Web API's JSON options,
/// to match the field names the Angular UI expects.
/// </summary>
public enum EvaluationStatus
{
    Answered,
    Gap,
    NotApplicable,
}
