using System.Text.Json;
using Hl7.Fhir.Model;

namespace DtrCheck.Core.Cql;

/// <summary>
/// Loads the project's simple valuesets.json format (url -> version -> [{code, system}])
/// into real FHIR ValueSet resources the CQL engine can register as terminology.
/// </summary>
public static class ValueSetLoader
{
    public static List<ValueSet> Load(string valuesetsJsonPath)
    {
        var json = File.ReadAllText(valuesetsJsonPath);
        using var doc = JsonDocument.Parse(json);

        var valueSets = new List<ValueSet>();
        foreach (var urlProperty in doc.RootElement.EnumerateObject())
        {
            foreach (var versionProperty in urlProperty.Value.EnumerateObject())
            {
                var contains = new List<ValueSet.ContainsComponent>();
                foreach (var codeElement in versionProperty.Value.EnumerateArray())
                {
                    contains.Add(new ValueSet.ContainsComponent
                    {
                        Code = codeElement.GetProperty("code").GetString(),
                        System = codeElement.GetProperty("system").GetString(),
                    });
                }

                valueSets.Add(new ValueSet
                {
                    Url = urlProperty.Name,
                    Version = versionProperty.Name,
                    Status = PublicationStatus.Active,
                    Expansion = new ValueSet.ExpansionComponent { Contains = contains },
                });
            }
        }
        return valueSets;
    }
}
