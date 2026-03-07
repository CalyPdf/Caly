using Caly.Core.Models;
using Caly.Core.Services.Interfaces;

namespace Caly.Tests.Integration.Mocks;

internal sealed class MockTextSearchService : ITextSearchService
{
    public Task BuildPdfDocumentIndex(IProgress<int> progress, CancellationToken token)
        => Task.CompletedTask;

    public IEnumerable<TextSearchResult> Search(string text, IReadOnlyCollection<int> pagesToSkip, CancellationToken token)
        => [];

    public void Dispose() { }
}
