using Hl7.Cql.CqlToElm.Toolkit;
using Hl7.Cql.CqlToElm.Toolkit.Extensions;
using Hl7.Cql.Fhir;
using Hl7.Cql.Invocation.Toolkit;
using Hl7.Cql.Invocation.Toolkit.Extensions;
using Hl7.Cql.Runtime;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;

namespace DtrCheck.Core.Cql;

/// <summary>
/// Compiles the CQL libraries in a directory once (compilation takes real
/// wall-clock time -- CQL to ELM to Roslyn-compiled C#) and reuses the
/// resulting invoker for every subsequent expression evaluation. Register
/// as a singleton; do not construct one per request.
/// </summary>
public sealed class CqlEngine : IDisposable
{
    private readonly LibrarySetInvoker _invoker;

    public CqlEngine(DirectoryInfo cqlDirectory, ILoggerFactory loggerFactory)
    {
        var toolkit = new CqlToolkit(loggerFactory);
        toolkit.AddCqlLibrariesFromDirectory(cqlDirectory);
        _invoker = toolkit.CreateLibrarySetInvoker();
    }

    /// <summary>Evaluates a single named define from a compiled CQL library against a patient's FHIR bundle.</summary>
    public object? EvaluateExpression(
        Bundle bundle,
        string library,
        string version,
        string expression,
        IEnumerable<ValueSet>? valueSets = null)
    {
        var valueSetList = valueSets?.ToList();
        var context = valueSetList is { Count: > 0 }
            ? FhirCqlContext.ForBundle(bundle, valueSets: valueSetList.ToValueSetDictionary())
            : FhirCqlContext.ForBundle(bundle);

        var libraryId = CqlVersionedLibraryIdentifier.ParseFromIdentifierAndVersion(library, version);
        return _invoker.InvokeLibraryDefinition(context, libraryId, expression);
    }

    public void Dispose() => _invoker.Dispose();
}
