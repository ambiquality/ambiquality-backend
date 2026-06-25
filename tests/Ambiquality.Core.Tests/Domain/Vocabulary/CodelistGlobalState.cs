namespace Ambiquality.Core.Tests.Domain.Vocabulary;

/// <summary>
/// xUnit collection grouping every test that touches the process-wide <c>Codelists</c>
/// static singletons. <c>VocabularyExtensionsLoaderTests</c> mutates them via
/// <c>Codelist.Add</c>, while <c>CodelistsTests</c> enumerates their <c>Concepts</c>;
/// xUnit runs classes in different collections in parallel, so without this shared
/// collection an Add could modify a list mid-enumeration (<c>Collection was modified;
/// enumeration operation may not execute</c>). Members of one collection never run
/// concurrently.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CodelistGlobalState
{
    public const string Name = "Codelist global state";
}
