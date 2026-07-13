using DtrCheck.Core.Cql;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;

namespace DtrCheck.Core.Tests;

public class CqlEngineTests
{
    private static readonly Lazy<CqlEngine> SharedEngine = new(() =>
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var cqlDirectory = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "cql"));
        return new CqlEngine(cqlDirectory, loggerFactory);
    });

    private static Bundle ParseBundle(string json)
    {
#pragma warning disable CS0618
        return new FhirJsonParser().Parse<Bundle>(json);
#pragma warning restore CS0618
    }

    private static Bundle PatientBundle(string extraEntriesJson = "")
    {
        var json = $$"""
        {
          "resourceType": "Bundle",
          "type": "collection",
          "entry": [
            {
              "resource": {
                "resourceType": "Patient",
                "id": "p1",
                "identifier": [ { "value": "9EX9-XY9-XY99" } ],
                "name": [ { "family": "Doe", "given": ["Jane"] } ],
                "gender": "female",
                "birthDate": "1958-04-12"
              }
            }
            {{extraEntriesJson}}
          ]
        }
        """;
        return ParseBundle(json);
    }

    [Fact]
    public void ExtractsScalarPatientFieldsViaRealCql()
    {
        var bundle = PatientBundle();
        var engine = SharedEngine.Value;

        Assert.Equal("Doe", engine.EvaluateExpression(bundle, "BasicPatientInfoPrepopulation", "1.0.0", "LastName"));
        Assert.Equal("Jane", engine.EvaluateExpression(bundle, "BasicPatientInfoPrepopulation", "1.0.0", "FirstName"));
        Assert.Equal("female", engine.EvaluateExpression(bundle, "BasicPatientInfoPrepopulation", "1.0.0", "Gender"));
        Assert.Equal("9EX9-XY9-XY99", engine.EvaluateExpression(bundle, "BasicPatientInfoPrepopulation", "1.0.0", "MedicareId"));
    }

    [Fact]
    public void ReturnsNullForAMiddleInitialThatDoesNotExist()
    {
        var bundle = PatientBundle();
        var engine = SharedEngine.Value;

        var result = engine.EvaluateExpression(bundle, "BasicPatientInfoPrepopulation", "1.0.0", "MiddleInitial");

        Assert.True(result is null or "");
    }

    [Fact]
    public void RetrievesAllCodedConditions()
    {
        var bundle = PatientBundle("""
            ,{
              "resource": {
                "resourceType": "Condition",
                "id": "condition-osa",
                "subject": { "reference": "Patient/p1" },
                "code": { "text": "Obstructive sleep apnea", "coding": [ { "system": "http://hl7.org/fhir/sid/icd-10-cm", "code": "G47.33" } ] }
              }
            }
        """);
        var engine = SharedEngine.Value;

        var result = engine.EvaluateExpression(bundle, "RespiratoryAssistDevicesPrepopulation", "1.0.0", "RADCodings");

        var conditions = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result).Cast<Condition>().ToList();
        Assert.Single(conditions);
        Assert.Equal("condition-osa", conditions[0].Id);
    }

    [Fact]
    public void MatchesConditionsAgainstTheComorbidityValueset()
    {
        var bundle = PatientBundle("""
            ,{
              "resource": {
                "resourceType": "Condition",
                "id": "condition-htn",
                "subject": { "reference": "Patient/p1" },
                "code": { "coding": [ { "system": "http://hl7.org/fhir/sid/icd-10-cm", "code": "I10" } ] }
              }
            },
            {
              "resource": {
                "resourceType": "Condition",
                "id": "condition-unrelated",
                "subject": { "reference": "Patient/p1" },
                "code": { "coding": [ { "system": "http://hl7.org/fhir/sid/icd-10-cm", "code": "Z00.00" } ] }
              }
            }
        """);
        var valueSets = ValueSetLoader.Load(Path.Combine(AppContext.BaseDirectory, "cql", "valuesets.json"));
        var engine = SharedEngine.Value;

        var result = engine.EvaluateExpression(bundle, "RespiratoryAssistDevicesPrepopulation", "1.0.0", "OtherDiagnosesCodings", valueSets);

        var conditions = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result).Cast<Condition>().ToList();
        Assert.Single(conditions);
        Assert.Equal("condition-htn", conditions[0].Id);
    }

    [Fact]
    public void ReturnsAnEmptyListWhenNoConditionsMatchTheValueset()
    {
        var bundle = PatientBundle();
        var valueSets = ValueSetLoader.Load(Path.Combine(AppContext.BaseDirectory, "cql", "valuesets.json"));
        var engine = SharedEngine.Value;

        var result = engine.EvaluateExpression(bundle, "RespiratoryAssistDevicesPrepopulation", "1.0.0", "OtherDiagnosesCodings", valueSets);

        var conditions = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result).Cast<object>().ToList();
        Assert.Empty(conditions);
    }
}
