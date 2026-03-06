using Avalonia;
using Caly.Core.Models;
using Caly.Core.ViewModels;

namespace Caly.Tests
{
    public class PageViewModelTests
    {
        private static TextSelection MakeSelection(int pages = 10) => new TextSelection(pages);

        private static PageViewModel MakePage(int pageNumber = 1) =>
            new PageViewModel(pageNumber, MakeSelection(), 1.0);

        // -----------------------------------------------------------------------
        // Rotation
        // -----------------------------------------------------------------------

        [Fact]
        public void Rotation_Default_IsZero()
        {
            var page = MakePage();
            Assert.Equal(0, page.Rotation);
        }

        [Theory]
        [InlineData(0,   true)]
        [InlineData(90,  false)]
        [InlineData(180, true)]
        [InlineData(270, false)]
        public void IsPortrait_CorrectForEachRotation(int rotation, bool expected)
        {
            var page = MakePage();
            page.Rotation = rotation;
            Assert.Equal(expected, page.IsPortrait);
        }

        [Theory]
        [InlineData(0,   200, 300, 200, 300)] // portrait  → Width=200, Height=300
        [InlineData(90,  200, 300, 300, 200)] // landscape → Width=Height, Height=Width
        [InlineData(180, 200, 300, 200, 300)] // portrait
        [InlineData(270, 200, 300, 300, 200)] // landscape
        public void DisplayDimensions_CorrectForRotation(
            int rotation, double sizeW, double sizeH,
            double expectedDisplayW, double expectedDisplayH)
        {
            var page = MakePage();
            page.Size = new Size(sizeW, sizeH);
            page.Rotation = rotation;

            Assert.Equal(expectedDisplayW, page.DisplayWidth);
            Assert.Equal(expectedDisplayH, page.DisplayHeight);
        }

        [Fact]
        public void Rotation_Wraps360_BackToZero()
        {
            var page = MakePage();
            page.Rotation = 270;
            // Simulates one clockwise step: (270 + 90) % 360 = 0
            page.Rotation = (page.Rotation + 90) % 360;
            Assert.Equal(0, page.Rotation);
        }

        [Fact]
        public void Rotation_CounterclockwiseFrom0_Is270()
        {
            var page = MakePage();
            page.Rotation = 0;
            // Simulates one counter-clockwise step: (0 + 270) % 360 = 270
            page.Rotation = (page.Rotation + 270) % 360;
            Assert.Equal(270, page.Rotation);
        }

        // -----------------------------------------------------------------------
        // SetSize / IsSizeSet
        // -----------------------------------------------------------------------

        [Fact]
        public void IsSizeSet_BeforeSetSize_ReturnsFalse()
        {
            var page = MakePage();
            Assert.False(page.IsSizeSet());
        }

        [Fact]
        public void IsSizeSet_AfterSetSize_ReturnsTrue()
        {
            var page = MakePage();
            page.SetSize(new Size(100, 200));
            Assert.True(page.IsSizeSet());
        }

        [Fact]
        public void SetSize_SetsCorrectValue()
        {
            var page = MakePage();
            var size = new Size(150, 250);
            page.SetSize(size);
            Assert.Equal(size, page.Size);
        }

        [Fact]
        public void SetSize_CalledTwice_OnlyFirstValueApplied()
        {
            var page = MakePage();
            var first  = new Size(100, 200);
            var second = new Size(300, 400);

            page.SetSize(first);
            page.SetSize(second);

            Assert.Equal(first, page.Size);
        }

        [Fact]
        public async Task SetSize_CalledConcurrently_OnlyOneValueApplied()
        {
            // Stress test: whichever thread wins, IsSizeSet must be true
            // and the size must not be the default (0,0).
            var page = MakePage();
            var s1 = new Size(100, 200);
            var s2 = new Size(300, 400);

            await Task.WhenAll(
                Task.Run(() => page.SetSize(s1)),
                Task.Run(() => page.SetSize(s2)));

            Assert.True(page.IsSizeSet());
            Assert.True(page.Size == s1 || page.Size == s2);
        }

        // -----------------------------------------------------------------------
        // ThumbnailSize
        // -----------------------------------------------------------------------

        [Fact]
        public void ThumbnailSize_Height_IsAlways135()
        {
            var page = MakePage();
            page.Size = new Size(100, 200);
            Assert.Equal(135, page.ThumbnailSize.Height);
        }

        [Fact]
        public void ThumbnailSize_Width_RespectsAspectRatio()
        {
            var page = MakePage();
            // aspectRatio = 200/400 = 0.5 → width = (int)(0.5 * 135) = 67
            page.Size = new Size(200, 400);
            Assert.Equal(67, page.ThumbnailSize.Width);
        }

        [Fact]
        public void ThumbnailSize_Width_AtLeastOne_WhenZeroWidth()
        {
            var page = MakePage();
            page.Size = new Size(0, 200); // aspectRatio = 0 → clamped to 1
            Assert.Equal(1, page.ThumbnailSize.Width);
        }

        [Fact]
        public void ThumbnailSize_SquarePage_WidthEqualsHeight()
        {
            var page = MakePage();
            page.Size = new Size(200, 200); // aspectRatio = 1.0
            Assert.Equal(135, page.ThumbnailSize.Width);
            Assert.Equal(135, page.ThumbnailSize.Height);
        }

        // -----------------------------------------------------------------------
        // IsThumbnailRendering
        // -----------------------------------------------------------------------

        [Fact]
        public void IsThumbnailRendering_NoThumbnail_IsTrue()
        {
            var page = MakePage();
            Assert.Null(page.Thumbnail);
            Assert.True(page.IsThumbnailRendering);
        }

        // -----------------------------------------------------------------------
        // IsPageVisible
        // -----------------------------------------------------------------------

        [Fact]
        public void IsPageVisible_NoVisibleArea_IsFalse()
        {
            var page = MakePage();
            Assert.Null(page.VisibleArea);
            Assert.False(page.IsPageVisible);
        }

        [Fact]
        public void IsPageVisible_WithVisibleArea_IsTrue()
        {
            var page = MakePage();
            page.VisibleArea = new Rect(0, 0, 100, 200);
            Assert.True(page.IsPageVisible);
        }

        // -----------------------------------------------------------------------
        // UpdateSearchResultsRanges
        // -----------------------------------------------------------------------

        [Fact]
        public void UpdateSearchResultsRanges_NullToNull_DoesNotChangeSearchResults()
        {
            var page = MakePage();
            page.UpdateSearchResultsRanges(null);

            // SearchResults should remain null; no Dispatcher call because PdfTextLayer is null
            Assert.Null(page.SearchResults);
        }

        [Fact]
        public void UpdateSearchResultsRanges_SameRangesCalledTwice_SecondCallIsNoop()
        {
            var page = MakePage();
            var ranges = new List<Range> { new Range(0, 2) };

            // First call stores the ranges (PdfTextLayer is null, so SearchResults stays null)
            page.UpdateSearchResultsRanges(ranges);
            // Second call with equal sequence is a no-op
            page.UpdateSearchResultsRanges(new List<Range> { new Range(0, 2) });

            Assert.Null(page.SearchResults);
        }

        // -----------------------------------------------------------------------
        // PageNumber
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(100)]
        public void PageNumber_SetInConstructor_ReturnsExpected(int number)
        {
            var page = new PageViewModel(number, MakeSelection(), 1.0);
            Assert.Equal(number, page.PageNumber);
        }

        [Fact]
        public void PpiScale_SetInConstructor_ReturnsExpected()
        {
            var page = new PageViewModel(1, MakeSelection(), 1.5);
            Assert.Equal(1.5, page.PpiScale);
        }
    }
}
