namespace Caly.Printing.Tests;

public class PrintTestDocumentTests
{
    [Fact]
    public async Task Create_BuildsOnePagePerArgumentWithTheRequestedOrientation()
    {
        await using var document = PrintTestDocument.Create("sample.pdf", true, false);

        Assert.Equal("sample.pdf", document.FileName);
        Assert.Equal(2, document.NumberOfPages);
        Assert.Equal(new[] { 1, 2 }, document.AllPages.Select(p => p.PageNumber));

        var portrait = await document.GetPageSizeAsync(1, CancellationToken.None);
        var landscape = await document.GetPageSizeAsync(2, CancellationToken.None);

        Assert.NotNull(portrait);
        Assert.NotNull(landscape);
        Assert.True(portrait.Value.Height > portrait.Value.Width);
        Assert.True(landscape.Value.Width > landscape.Value.Height);
    }

    [Fact]
    public async Task GetRenderPageAsync_ReturnsAPictureTheSizeOfThePage()
    {
        await using var document = PrintTestDocument.Create("sample.pdf", true);

        using var picture = await document.GetRenderPageAsync(1, CancellationToken.None);

        Assert.NotNull(picture);
        Assert.True(picture.Item.CullRect.Width > 0);
        Assert.True(picture.Item.CullRect.Height > picture.Item.CullRect.Width);
    }

    [Fact]
    public void RecordingProgress_RecordsReportsInOrderAndInvokesTheCallback()
    {
        var seen = new List<int>();
        var progress = new RecordingProgress(seen.Add);

        progress.Report(1);
        progress.Report(2);

        Assert.Equal(new[] { 1, 2 }, progress.Values);
        Assert.Equal(new[] { 1, 2 }, seen);
    }
}
