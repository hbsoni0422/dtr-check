using System.Text.Json;
using System.Text.Json.Nodes;
using DtrCheck.Core;
using DtrCheck.Core.Cql;
using DtrCheck.Core.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace DtrCheck.Api.Controllers;

[ApiController]
[Route("api")]
public class DtrCheckController(Matcher matcher, DataPaths dataPaths) : ControllerBase
{
#pragma warning disable CS0618 // FhirJsonParser/FhirJsonSerializer are obsolete but still functional; see README.
    private static readonly FhirJsonParser FhirParser = new();
    private static readonly FhirJsonSerializer FhirSerializer = new();
#pragma warning restore CS0618

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    [HttpGet("sample")]
    public IActionResult GetSample()
    {
        var response = new JsonObject
        {
            ["patient"] = JsonNode.Parse(System.IO.File.ReadAllText(dataPaths.PatientPath)),
            ["questionnaire"] = JsonNode.Parse(System.IO.File.ReadAllText(dataPaths.QuestionnairePath)),
            ["rules"] = JsonNode.Parse(System.IO.File.ReadAllText(dataPaths.RulesPath)),
        };
        return Content(response.ToJsonString(), "application/json");
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("patient", out var patientJson) ||
            !doc.RootElement.TryGetProperty("questionnaire", out var questionnaireJson) ||
            !doc.RootElement.TryGetProperty("rules", out var rulesJson))
        {
            return BadRequest(new { error = "Request body must include patient, questionnaire, and rules." });
        }

        Bundle patient;
        Questionnaire questionnaire;
        RulesFile rules;
        try
        {
            patient = FhirParser.Parse<Bundle>(patientJson.GetRawText());
            questionnaire = FhirParser.Parse<Questionnaire>(questionnaireJson.GetRawText());
            rules = JsonSerializer.Deserialize<RulesFile>(rulesJson.GetRawText(), JsonOptions)
                    ?? throw new InvalidOperationException("Rules file could not be parsed.");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var valueSets = ValueSetLoader.Load(dataPaths.ValuesetsPath);
        var results = matcher.Evaluate(questionnaire, rules, patient, valueSets);
        var gaps = Matcher.GapReport(results);
        var questionnaireResponse = Matcher.BuildQuestionnaireResponse(questionnaire, results);

        var response = new JsonObject
        {
            ["title"] = questionnaire.Title ?? questionnaire.Id,
            ["results"] = JsonSerializer.SerializeToNode(results, JsonOptions),
            ["gaps"] = JsonSerializer.SerializeToNode(gaps, JsonOptions),
            ["questionnaireResponse"] = JsonNode.Parse(await FhirSerializer.SerializeToStringAsync(questionnaireResponse)),
        };
        return Content(response.ToJsonString(), "application/json");
    }
}
