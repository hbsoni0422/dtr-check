using DtrCheck.Core.Fhir;
using Hl7.Fhir.Model;

namespace DtrCheck.Core.Tests;

public class FieldPathResolverTests
{
    private static Patient TestPatient() => new()
    {
        Name = [new HumanName { Family = "Doe", Given = ["Jane"] }],
    };

    [Fact]
    public void WalksDottedPathsThroughArraysAndObjects()
    {
        var patient = TestPatient();
        Assert.Equal("Doe", FieldPathResolver.Resolve(patient, "name.0.family"));
        Assert.Equal("Jane", FieldPathResolver.Resolve(patient, "name.0.given.0"));
    }

    [Fact]
    public void ReturnsNullForOutOfRangeIndicesAndMissingPaths()
    {
        var patient = TestPatient();
        Assert.Null(FieldPathResolver.Resolve(patient, "name.1.family"));
        Assert.Null(FieldPathResolver.Resolve(patient, "address.0.city"));
    }
}
