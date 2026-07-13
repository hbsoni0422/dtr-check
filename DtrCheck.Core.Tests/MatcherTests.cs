using DtrCheck.Core.Cql;
using DtrCheck.Core.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;

namespace DtrCheck.Core.Tests;

public class MatcherTests
{
    // Rule-less CqlEngine for tests that never exercise a "cql" rule -- avoids paying
    // the real-CQL-compilation cost (several seconds) for every test in this class.
    private static readonly Lazy<CqlEngine> EmptyCqlEngine = new(() =>
    {
        var emptyDir = Directory.CreateTempSubdirectory("dtr-check-empty-cql-");
        return new CqlEngine(emptyDir, LoggerFactory.Create(_ => { }));
    });

    private static Questionnaire TestQuestionnaire() => new()
    {
        Id = "TestQ",
        Url = "urn:test:TestQ",
        Item =
        [
            new Questionnaire.ItemComponent
            {
                LinkId = "group1",
                Type = Questionnaire.QuestionnaireItemType.Group,
                Text = "Patient Information",
                Item = [new Questionnaire.ItemComponent { LinkId = "q1", Type = Questionnaire.QuestionnaireItemType.String, Text = "Last Name" }],
            },
            new Questionnaire.ItemComponent { LinkId = "q3", Type = Questionnaire.QuestionnaireItemType.String, Text = "Diagnosis" },
            new Questionnaire.ItemComponent { LinkId = "q4", Type = Questionnaire.QuestionnaireItemType.String, Text = "Unmapped item" },
        ],
    };

    private static RulesFile TestRules() => new()
    {
        Rules = new Dictionary<string, RuleDefinition>
        {
            ["q1"] = new RuleDefinition { Type = "patient_field", Path = "name.0.family" },
            ["q3"] = new RuleDefinition { Type = "resource_search", ResourceType = "Condition" },
        },
    };

    private static Bundle ParseBundle(string json)
    {
#pragma warning disable CS0618
        return new FhirJsonParser().Parse<Bundle>(json);
#pragma warning restore CS0618
    }

    private static Bundle BundleWithCondition() => ParseBundle("""
        {
          "resourceType": "Bundle",
          "type": "collection",
          "entry": [
            { "resource": { "resourceType": "Patient", "id": "p1", "name": [ { "family": "Doe" } ] } },
            { "resource": { "resourceType": "Condition", "id": "c1", "subject": { "reference": "Patient/p1" }, "code": { "text": "OSA" } } }
          ]
        }
        """);

    private static Bundle BundleWithoutCondition() => ParseBundle("""
        {
          "resourceType": "Bundle",
          "type": "collection",
          "entry": [
            { "resource": { "resourceType": "Patient", "id": "p1", "name": [ { "family": "Doe" } ] } }
          ]
        }
        """);

    [Fact]
    public void MarksUnmappedItemsAsNotApplicable()
    {
        var matcher = new Matcher(EmptyCqlEngine.Value);
        var results = matcher.Evaluate(TestQuestionnaire(), TestRules(), BundleWithCondition());
        var q4 = results.Single(r => r.LinkId == "q4");
        Assert.Equal(EvaluationStatus.NotApplicable, q4.Status);
    }

    [Fact]
    public void AnswersAPatientFieldItemFromChartData()
    {
        var matcher = new Matcher(EmptyCqlEngine.Value);
        var results = matcher.Evaluate(TestQuestionnaire(), TestRules(), BundleWithCondition());
        var q1 = results.Single(r => r.LinkId == "q1");
        Assert.Equal(EvaluationStatus.Answered, q1.Status);
        Assert.Equal("Doe", q1.Value);
    }

    [Fact]
    public void FlagsAResourceSearchItemAsAGapWhenTheResourceIsAbsent()
    {
        var matcher = new Matcher(EmptyCqlEngine.Value);
        var results = matcher.Evaluate(TestQuestionnaire(), TestRules(), BundleWithoutCondition());
        var q3 = results.Single(r => r.LinkId == "q3");
        Assert.Equal(EvaluationStatus.Gap, q3.Status);
    }

    [Fact]
    public void AnswersAResourceSearchItemWithEvidenceWhenTheResourceIsPresent()
    {
        var matcher = new Matcher(EmptyCqlEngine.Value);
        var results = matcher.Evaluate(TestQuestionnaire(), TestRules(), BundleWithCondition());
        var q3 = results.Single(r => r.LinkId == "q3");
        Assert.Equal(EvaluationStatus.Answered, q3.Status);
        Assert.NotNull(q3.Evidence);
        Assert.Single(q3.Evidence!);
    }

    [Fact]
    public void GapReportReturnsOnlyGapStatusResults()
    {
        var matcher = new Matcher(EmptyCqlEngine.Value);
        var results = matcher.Evaluate(TestQuestionnaire(), TestRules(), BundleWithoutCondition());
        var gaps = Matcher.GapReport(results);
        Assert.Single(gaps);
        Assert.Equal("q3", gaps[0].LinkId);
    }

    [Fact]
    public void BuildQuestionnaireResponseCarriesAnsweredValuesIntoAFhirQuestionnaireResponse()
    {
        var matcher = new Matcher(EmptyCqlEngine.Value);
        var results = matcher.Evaluate(TestQuestionnaire(), TestRules(), BundleWithCondition());
        var qr = Matcher.BuildQuestionnaireResponse(TestQuestionnaire(), results);

        Assert.Equal("QuestionnaireResponse", qr.TypeName);
        var q1 = qr.Item.Single(i => i.LinkId == "q1");
        var answer = Assert.Single(q1.Answer);
        Assert.Equal("Doe", ((FhirString)answer.Value!).Value);
    }
}
