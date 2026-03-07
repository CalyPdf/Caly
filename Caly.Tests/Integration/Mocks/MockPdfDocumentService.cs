using Avalonia.Platform.Storage;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.ViewModels;
using Caly.Pdf.Models;
using SkiaSharp;
using System.Collections.ObjectModel;
using UglyToad.PdfPig.Rendering.Skia;
using Caly.Core.Utilities;

namespace Caly.Tests.Integration.Mocks;

internal sealed class MockPdfDocumentService : IPdfDocumentService
{
    private int _numberOfPages;

    public double PpiScale => 1.0;

    public bool IsActive { get; set; }

    public int NumberOfPages => _numberOfPages;

    public string? FileName => "Test.pdf";

    public long? FileSize => 1024;

    public string? LocalPath => "C:/Test.pdf";

    public bool IsPasswordProtected => false;

    public Task<int> OpenDocument(IStorageFile? storageFile, string? password, CancellationToken token)
    {
        _numberOfPages = 5;
        return Task.FromResult(_numberOfPages);
    }

    public Task<PdfPageSize?> GetPageSizeAsync(int pageNumber, CancellationToken token)
        => Task.FromResult<PdfPageSize?>(null);

    public Task<PdfTextLayer?> GetPageTextLayerAsync(int pageNumber, CancellationToken token)
        => Task.FromResult<PdfTextLayer?>(null);

    public Task<IRef<SKPicture>?> GetRenderPageAsync(int pageNumber, CancellationToken token)
        => Task.FromResult<IRef<SKPicture>?>(null);

    public Task<DocumentPropertiesViewModel?> GetDocumentPropertiesAsync(CancellationToken token)
        => Task.FromResult<DocumentPropertiesViewModel?>(null);

    public Task<ObservableCollection<PdfBookmarkNode>?> GetPdfBookmark(CancellationToken token)
        => Task.FromResult<ObservableCollection<PdfBookmarkNode>?>(null);

    public Task<ObservableCollection<PdfEmbeddedFileViewModel>?> GetEmbeddedFileAsync(CancellationToken token)
        => Task.FromResult<ObservableCollection<PdfEmbeddedFileViewModel>?>(null);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
