using System.Globalization;
using System.Text;
using Avalonia.Platform.Storage;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Caly.Pdf;
using Caly.Pdf.Models;
using Caly.Pdf.PageFactories;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;

namespace Caly.Tests.Fakes;

/// <summary>
/// A document service that serves, per page, a text layer read from a tiny generated PDF
/// and a picture of a given size. Only the two calls the AI page provider makes are implemented.
/// </summary>
internal sealed class AiTestDocumentService : IPdfDocumentService
{
    private readonly Dictionary<int, PdfTextLayer?> _layers = new();

    public SKSize PictureSize { get; set; } = new(300, 200);

    /// <summary>The reference handed out by the last <see cref="GetRenderPageAsync"/>.</summary>
    public IRef<SKPicture>? LastPicture { get; private set; }

    public void SetText(int pageNumber, params string[] lines) => _layers[pageNumber] = BuildTextLayer(lines);

    public void SetNoTextLayer(int pageNumber) => _layers[pageNumber] = null;

    public Task<PdfTextLayer?> GetPageTextLayerAsync(int pageNumber, CancellationToken token)
        => Task.FromResult(_layers.GetValueOrDefault(pageNumber));

    public Task<IRef<SKPicture>?> GetRenderPageAsync(int pageNumber, CancellationToken token)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, PictureSize.Width, PictureSize.Height));
        using (var paint = new SKPaint { Color = SKColors.Red })
        {
            canvas.DrawRect(new SKRect(10, 10, 50, 50), paint);
        }

        LastPicture = RefCountable.Create(recorder.EndRecording());
        return Task.FromResult<IRef<SKPicture>?>(LastPicture);
    }

    private static PdfTextLayer BuildTextLayer(string[] lines)
    {
        var content = new StringBuilder("BT\n/F1 12 Tf\n");
        double y = 150;
        foreach (string line in lines)
        {
            content.Append(CultureInfo.InvariantCulture, $"1 0 0 1 10 {y} Tm\n({line}) Tj\n");
            y -= 40;
        }

        content.Append("ET\n");

        using var document = PdfDocument.Open(MiniPdf(Encoding.Latin1.GetBytes(content.ToString())));
        document.Advanced.ReplaceIndirectObject(CalyPdfHelper.FakePpiReference, new NumericToken(1));
        document.AddPageFactory<PageTextLayerContent, TextLayerFactory>();
        return PdfTextLayerHelper.GetTextLayer(document.GetPageTextLayerContent(1, CancellationToken.None), CancellationToken.None);
    }

    private static byte[] MiniPdf(byte[] contentStream)
    {
        var objects = new List<byte[]>
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Encoding.ASCII.GetBytes("<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /MediaBox [0 0 300 200] /Contents 4 0 R >>"),
            StreamObject(contentStream)
        };

        using var ms = new MemoryStream();
        void Write(byte[] b) => ms.Write(b, 0, b.Length);
        void WriteText(string s) => Write(Encoding.ASCII.GetBytes(s));

        WriteText("%PDF-1.7\n");
        var offsets = new long[objects.Count + 1];
        for (int i = 0; i < objects.Count; i++)
        {
            offsets[i + 1] = ms.Position;
            WriteText($"{i + 1} 0 obj\n");
            Write(objects[i]);
            WriteText("\nendobj\n");
        }

        long startXref = ms.Position;
        WriteText($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (int i = 1; i <= objects.Count; i++)
        {
            WriteText($"{offsets[i].ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");
        }

        WriteText($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{startXref}\n%%EOF\n");
        return ms.ToArray();
    }

    private static byte[] StreamObject(byte[] data)
    {
        byte[] head = Encoding.ASCII.GetBytes($"<< /Length {data.Length} >>\nstream\n");
        byte[] tail = Encoding.ASCII.GetBytes("\nendstream");
        return [.. head, .. data, .. tail];
    }

    public double PpiScale => 1.0;
    public bool IsActive { get; set; }
    public int NumberOfPages => 1;
    public string? FileName => "ai.pdf";
    public string? Title => null;
    public long? FileSize => null;
    public string? LocalPath => null;
    public bool IsPasswordProtected => false;
    public PdfPreferences? Preferences => null;
    public Func<CancellationToken, Task<string?>>? PasswordPrompt { get; set; }

    public Task<DocumentOpeningState> OpenDocument(IStorageFile? storageFile, string? password, CancellationToken token)
        => throw new NotImplementedException();

    public Task<DocumentPropertiesViewModel?> GetDocumentPropertiesAsync(CancellationToken token)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<PdfBookmarkNode>?> GetPdfBookmark(CancellationToken token)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<PdfEmbeddedFileViewModel>?> GetEmbeddedFileAsync(CancellationToken token)
        => throw new NotImplementedException();

    public Task<UglyToad.PdfPig.Rendering.Skia.PdfPageSize?> GetPageSizeAsync(int pageNumber, CancellationToken token)
        => throw new NotImplementedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
