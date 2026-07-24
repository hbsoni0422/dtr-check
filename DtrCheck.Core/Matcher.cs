using System.Collections;
using DtrCheck.Core.Cql;
using DtrCheck.Core.Fhir;
using DtrCheck.Core.Models;
using Hl7.Fhir.Model;

namespace DtrCheck.Core;

public sealed class Matcher(CqlEngine cqlEngine)
{
    private const string NoRuleNote =
        "No chart-derivable rule defined for this item (likely requires ordering-provider, " +
        "signature, or order-form data outside the patient record).";

    public List<EvaluationResult> Evaluate(
        Questionnaire questionnaire,
        RulesFile rules,
        Bundle bundle,
        IEnumerable<ValueSet>? valueSets = null)
    {
        var index = new ResourceBundleIndex(bundle);
        var items = QuestionnaireFlattener.Flatten(questionnaire);
        var valueSetList = valueSets?.ToList();

        return items
            .Select(item => EvaluateItem(item, rules.Rules.GetValueOrDefault(item.LinkId), index, bundle, valueSetList))
            .ToList();
    }

    private EvaluationResult EvaluateItem(
        QuestionnaireItem item,
        RuleDefinition? rule,
        ResourceBundleIndex index,
        Bundle bundle,
        List<ValueSet>? valueSets)
    {
        if (rule is null)
        {
            return new EvaluationResult
            {
                LinkId = item.LinkId,
                Text = item.Text,
                Status = EvaluationStatus.NotApplicable,
                Note = NoRuleNote,
            };
        }

        return rule.Type switch
        {
            "patient_field" => EvaluatePatientField(item, rule, index),
            "resource_search" => EvaluateResourceSearch(item, rule, index),
            "keyword_search" => EvaluateKeywordSearch(item, rule, index),
            "cql" => EvaluateCql(item, rule, bundle, valueSets),
            _ => throw new InvalidOperationException($"Unknown rule type: {rule.Type}"),
        };
    }

    private static EvaluationResult EvaluatePatientField(QuestionnaireItem item, RuleDefinition rule, ResourceBundleIndex index)
    {
        var patient = index.GetPatient();
        var value = FieldPathResolver.Resolve(patient, rule.Path!);
        if (value is null)
        {
            return Gap(item, "Field not present on Patient resource.");
        }
        return Answered(item, value: FormatValue(value));
    }

    private static EvaluationResult EvaluateResourceSearch(QuestionnaireItem item, RuleDefinition rule, ResourceBundleIndex index)
    {
        var resources = index[rule.ResourceType!];
        if (resources.Count == 0)
        {
            return Gap(item, $"No {rule.ResourceType} resource found in chart.");
        }
        return Answered(item, evidence: resources.Select(SummarizeResource).ToList<object>());
    }

    private static EvaluationResult EvaluateKeywordSearch(QuestionnaireItem item, RuleDefinition rule, ResourceBundleIndex index)
    {
        var matches = new List<object>();
        foreach (var resource in index[rule.ResourceType!])
        {
            var text = resource is DocumentReference docRef ? DocumentTextDecoder.DecodeText(docRef) : string.Empty;
            var hits = rule.Keywords!.Where(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase)).ToList();
            if (hits.Count > 0)
            {
                matches.Add(new
                {
                    resourceType = resource.TypeName,
                    id = resource.Id,
                    matchedKeywords = hits,
                    excerpt = text.Trim() is var trimmed && trimmed.Length > 280 ? trimmed[..280] : trimmed,
                });
            }
        }

        if (matches.Count == 0)
        {
            return Gap(item, $"No supporting documentation found containing: {string.Join(", ", rule.Keywords!)}.");
        }
        return Answered(item, evidence: matches);
    }

    private EvaluationResult EvaluateCql(QuestionnaireItem item, RuleDefinition rule, Bundle bundle, List<ValueSet>? valueSets)
    {
        var result = cqlEngine.EvaluateExpression(bundle, rule.Library!, rule.Version!, rule.Expression!, valueSets);

        if (result is null)
        {
            return rule.Optional
                ? Answered(item, value: string.Empty)
                : Gap(item, $"CQL expression \"{rule.Library}.{rule.Expression}\" returned no value.");
        }

        if (result is IEnumerable enumerable and not string)
        {
            var evidence = enumerable.Cast<object>().Select(SummarizeResource).ToList();
            if (evidence.Count == 0)
            {
                return rule.Optional
                    ? Answered(item, value: string.Empty)
                    : Gap(item, $"CQL expression \"{rule.Library}.{rule.Expression}\" returned no results.");
            }
            return Answered(item, evidence: evidence);
        }

        var formatted = FormatValue(result);
        if (string.IsNullOrEmpty(formatted))
        {
            return rule.Optional
                ? Answered(item, value: string.Empty)
                : Gap(item, $"CQL expression \"{rule.Library}.{rule.Expression}\" returned no value.");
        }
        return Answered(item, value: formatted);
    }

    private static object SummarizeResource(object resourceObj)
    {
        if (resourceObj is not Resource resource)
        {
            return new { value = resourceObj?.ToString() };
        }

        string? text = resource switch
        {
            Condition c => c.Code?.Text ?? c.Code?.Coding?.FirstOrDefault()?.Display,
            Observation o => o.Code?.Text ?? o.Code?.Coding?.FirstOrDefault()?.Display,
            DocumentReference d => d.Type?.Text ?? d.Type?.Coding?.FirstOrDefault()?.Display,
            _ => null,
        };

        string? date = resource switch
        {
            Condition c => c.Onset is FhirDateTime onset ? onset.Value : c.RecordedDate?.ToString(),
            Observation o => o.Effective is FhirDateTime eff ? eff.Value : null,
            DocumentReference d => d.Date?.ToString(),
            _ => null,
        };

        return new { resourceType = resource.TypeName, id = resource.Id, text, date };
    }

    private static string FormatValue(object value) => value switch
    {
        Date d => d.Value ?? string.Empty,
        FhirDateTime dt => dt.Value ?? string.Empty,
        _ => value.ToString() ?? string.Empty,
    };

    private static EvaluationResult Answered(QuestionnaireItem item, string? value = null, List<object>? evidence = null) => new()
    {
        LinkId = item.LinkId,
        Text = item.Text,
        Status = EvaluationStatus.Answered,
        Value = value,
        Evidence = evidence,
    };

    private static EvaluationResult Gap(QuestionnaireItem item, string note) => new()
    {
        LinkId = item.LinkId,
        Text = item.Text,
        Status = EvaluationStatus.Gap,
        Note = note,
    };

    public static List<EvaluationResult> GapReport(IEnumerable<EvaluationResult> results) =>
        results.Where(r => r.Status == EvaluationStatus.Gap).ToList();

    public static QuestionnaireResponse BuildQuestionnaireResponse(Questionnaire questionnaire, IEnumerable<EvaluationResult> results)
    {
        var qr = new QuestionnaireResponse
        {
            Questionnaire = questionnaire.Url ?? questionnaire.Id,
            Status = QuestionnaireResponse.QuestionnaireResponseStatus.InProgress,
            Item = [],
        };

        foreach (var r in results)
        {
            var entry = new QuestionnaireResponse.ItemComponent { LinkId = r.LinkId, Text = r.Text };
            if (r.Status == EvaluationStatus.Answered && !string.IsNullOrEmpty(r.Value))
            {
                entry.Answer = [new QuestionnaireResponse.AnswerComponent { Value = new FhirString(r.Value) }];
            }
            qr.Item.Add(entry);
        }

        return qr;
    }
}
