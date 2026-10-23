using Caly.Core.Services.Ai;
using Caly.Tests.Fakes;
using SkiaSharp;

namespace Caly.Tests
{
    public class AiPageContentProviderTests
    {
        [Fact]
        public async Task Text_is_joined_in_reading_order()
        {
            var service = new AiTestDocumentService();
            service.SetText(1, "Hello World", "Second line");

            var content = await new AiPageContentProvider(service).GetAsync(1, false, CancellationToken.None);

            Assert.Equal(1, content.PageNumber);
            int first = content.Text.IndexOf("Hello World", StringComparison.Ordinal);
            int second = content.Text.IndexOf("Second line", StringComparison.Ordinal);
            Assert.True(first >= 0, content.Text);
            Assert.True(second > first, content.Text);
        }

        [Fact]
        public async Task Image_is_null_when_not_requested()
        {
            var service = new AiTestDocumentService();
            service.SetText(1, "Hello");

            var content = await new AiPageContentProvider(service).GetAsync(1, false, CancellationToken.None);

            Assert.Null(content.Png);
            Assert.Null(service.LastPicture);
        }

        [Fact]
        public async Task Image_long_side_is_1024()
        {
            var service = new AiTestDocumentService { PictureSize = new SKSize(300, 200) };
            service.SetText(1, "Hello");

            var content = await new AiPageContentProvider(service).GetAsync(1, true, CancellationToken.None);

            Assert.NotNull(content.Png);
            using var bitmap = SKBitmap.Decode(content.Png);
            Assert.Equal(1024, bitmap.Width);
            Assert.Equal(683, bitmap.Height);
        }

        [Fact]
        public async Task Missing_text_layer_gives_empty_text_not_exception()
        {
            var service = new AiTestDocumentService();
            service.SetNoTextLayer(1);

            var content = await new AiPageContentProvider(service).GetAsync(1, false, CancellationToken.None);

            Assert.Equal(string.Empty, content.Text);
        }

        [Fact]
        public async Task Picture_ref_is_disposed()
        {
            var service = new AiTestDocumentService();
            service.SetText(1, "Hello");

            await new AiPageContentProvider(service).GetAsync(1, true, CancellationToken.None);

            Assert.NotNull(service.LastPicture);
            Assert.False(service.LastPicture.IsAlive);
        }
    }
}
