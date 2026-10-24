using Avalonia.Headless.XUnit;
using Avalonia.Platform.Storage;
using Caly.Core.Models;
using Caly.Core.Services;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using SkiaSharp;
using UglyToad.PdfPig.Fonts.SystemFonts;

namespace Caly.Tests;

/// <summary>
/// Caly opens documents with its own <c>ParsingOptions</c> and page factories, so the PdfPig.Rendering.Skia
/// registration of the bundled Standard 14 substitutes never runs: <see cref="PdfPigDocumentService"/> must do it.
/// </summary>
public class StandardFontSubstituteTests
{
    private const float Scale = 2;

    private sealed class FakeSettingsService : ISettingsService
    {
        public void SetProperty(CalySettings.CalySettingsProperty property, object value)
        {
        }

        public CalySettings GetSettings() => CalySettings.Default;

        public ValueTask<CalySettings> GetSettingsAsync() => ValueTask.FromResult(CalySettings.Default);

        public void Load()
        {
        }

        public Task LoadAsync() => Task.CompletedTask;

        public void Save()
        {
        }

        public Task SaveAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// Avalonia's file-backed <see cref="IStorageFile"/> is internal, and the interface is not client-implementable.
    /// </summary>
    private static IStorageFile OpenStorageFile(string path)
    {
        Type type = typeof(IStorageFile).Assembly.GetType("Avalonia.Platform.Storage.FileIO.BclStorageFile", throwOnError: true)!;
        return (IStorageFile)Activator.CreateInstance(type, new FileInfo(path))!;
    }

    /// <summary>
    /// PdfPig.Rendering.Skia#163: the ZapfDingbats ✔ of a check box widget (font not embedded) was drawn with the
    /// installed font, Segoe UI Emoji on Windows: purple. With the substitute it is the black Foxit Dingbats glyph.
    /// </summary>
    [AvaloniaFact]
    public async Task NonEmbeddedZapfDingbatsCheckMarkUsesSubstitute()
    {
        // The open/dispose paths assert they do not run on the UI thread.
        await Task.Run(async () =>
        {
            await using var service = new PdfPigDocumentService(new FakeSettingsService());

            var state = await service.OpenDocument(OpenStorageFile(Path.Combine("Documents", "checkbox-widget.pdf")), null, CancellationToken.None);
            Assert.Equal(DocumentOpeningState.Success, state);

            Assert.True(SystemFontFinder.TryGetSubstituteFont("ZapfDingbats", out _), "Opening a document did not register the substitutes.");

            using IRef<SKPicture>? picture = await service.GetRenderPageAsync(1, CancellationToken.None);
            Assert.NotNull(picture);

            // The picture is in page points (612 x 792), origin top-left.
            using var bitmap = new SKBitmap((int)(612 * Scale), (int)(792 * Scale));
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                canvas.Scale(Scale);
                canvas.DrawPicture(picture.Item);
            }

            // Widget /Rect [430 708 443.4934 719.6907] on a 792 pt high page. Stay inside its 1 pt border.
            var inner = new SKRectI((int)(431.5 * Scale), (int)((792 - 718.2) * Scale), (int)(442 * Scale), (int)((792 - 709.5) * Scale));

            int dark = 0, coloured = 0;
            for (int y = inner.Top; y < inner.Bottom; y++)
            {
                for (int x = inner.Left; x < inner.Right; x++)
                {
                    SKColor c = bitmap.GetPixel(x, y);
                    int max = Math.Max(c.Red, Math.Max(c.Green, c.Blue));
                    int min = Math.Min(c.Red, Math.Min(c.Green, c.Blue));

                    if (max - min > 40)
                    {
                        coloured++;
                    }
                    else if (max < 96)
                    {
                        dark++;
                    }
                }
            }

            Assert.Equal(0, coloured);
            // The ✔ covers roughly a fifth of the box; a missing or tiny glyph would not.
            Assert.True(dark > inner.Width * inner.Height / 10, $"Only {dark} dark pixels in the check box.");
        });
    }
}
