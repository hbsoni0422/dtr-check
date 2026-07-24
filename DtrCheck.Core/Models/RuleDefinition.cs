namespace DtrCheck.Core.Models;

/// <summary>
/// A single rule mapping a questionnaire linkId to where its answer should be
/// found in the chart. Fields are a superset across the four rule types
/// (mirrors the loosely-typed rules.json files, not a polymorphic hierarchy)
/// since the rule files are small, trusted, hand-authored config.
/// </summary>
public sealed class RuleDefinition
{
    /// <summary>"patient_field" | "resource_search" | "keyword_search" | "cql"</summary>
    public required string Type { get; init; }

    /// <summary>patient_field: dotted path into the Patient resource, e.g. "name.0.family".</summary>
    public string? Path { get; init; }

    /// <summary>resource_search / keyword_search: the FHIR resource type to look for.</summary>
    public string? ResourceType { get; init; }

    /// <summary>keyword_search: keywords to look for in DocumentReference narrative text.</summary>
    public string[]? Keywords { get; init; }

    /// <summary>cql: the CQL library name (unversioned identifier), e.g. "BasicPatientInfoPrepopulation".</summary>
    public string? Library { get; init; }

    /// <summary>cql: the CQL library version, e.g. "1.0.0".</summary>
    public string? Version { get; init; }

    /// <summary>cql: the named define/expression to invoke, e.g. "LastName".</summary>
    public string? Expression { get; init; }

    /// <summary>Documentation only, not consumed by the evaluator.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// When true, an empty/null result is a legitimate answer (e.g. a patient with
    /// no middle name) rather than missing documentation -- resolved as Answered
    /// with an empty value instead of Gap.
    /// </summary>
    public bool Optional { get; init; }
}
