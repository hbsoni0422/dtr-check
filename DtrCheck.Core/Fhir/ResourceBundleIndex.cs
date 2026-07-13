using Hl7.Fhir.Model;

namespace DtrCheck.Core.Fhir;

public sealed class ResourceBundleIndex
{
    private readonly Dictionary<string, List<Resource>> _byType = new();

    public ResourceBundleIndex(Bundle bundle)
    {
        foreach (var entry in bundle.Entry ?? [])
        {
            if (entry.Resource is not { } resource) continue;
            var typeName = resource.TypeName;
            if (!_byType.TryGetValue(typeName, out var list))
            {
                list = [];
                _byType[typeName] = list;
            }
            list.Add(resource);
        }
    }

    public IReadOnlyList<Resource> this[string resourceType] =>
        _byType.TryGetValue(resourceType, out var list) ? list : [];

    public Patient GetPatient()
    {
        var patients = this["Patient"];
        if (patients.Count == 0) throw new InvalidOperationException("Bundle contains no Patient resource");
        return (Patient)patients[0];
    }
}
