// Copyright (c) 2025 BobLd
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Caly.Core;
using Caly.Core.Models;
using Caly.Pdf;
using Caly.Pdf.Models;
using Caly.Pdf.PageFactories;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;

namespace Caly.Tests
{
    public class TextSelectionTests
    {
        [Fact]
        public void ExtendSelectionNoStart()
        {
            var selection = new PdfTextSelection(100);
            var ex = Assert.Throws<Exception>(() => selection.Extend(50, null));
            Assert.Equal("Cannot extend text selection if it has started.", ex.Message);
        }

        [Fact]
        public void PageOutOfRangeCtor()
        {
            var ex = Assert.Throws<CalyPageOutOfRangeException>(() => new PdfTextSelection(-88));
            Assert.Contains("(Parameter 'numberOfPages')", ex.Message);
            Assert.Contains("Actual value was -88. The page number should be greater or equal to '1'.", ex.Message);
        }

        [Fact]
        public void PageOutOfRangeStart()
        {
            var selection = new PdfTextSelection(15);
            
            var ex = Assert.Throws<CalyPageOutOfRangeException>(() => selection.Start(-1, null));
            Assert.Contains("(Parameter 'pageNumber')", ex.Message);
            Assert.Contains("Actual value was -1. The page number should be greater or equal to '1' and less then the number of pages in the document: '15'.", ex.Message);

            ex = Assert.Throws<CalyPageOutOfRangeException>(() => selection.Start(100, null));
            Assert.Contains("(Parameter 'pageNumber')", ex.Message);
            Assert.Contains("Actual value was 100. The page number should be greater or equal to '1' and less then the number of pages in the document: '15'.", ex.Message);
        }

        [Fact]
        public void PageOutOfRangeExtend()
        {
            var selection = new PdfTextSelection(15);

            selection.Start(1, new PdfWord([new PdfLetter("a", new PdfRectangle(), 1, 1)]));

            var ex = Assert.Throws<CalyPageOutOfRangeException>(() => selection.Extend(-1, null));
            Assert.Contains("(Parameter 'pageNumber')", ex.Message);
            Assert.Contains("Actual value was -1. The page number should be greater or equal to '1' and less then the number of pages in the document: '15'.", ex.Message);

            ex = Assert.Throws<CalyPageOutOfRangeException>(() => selection.Extend(100, null));
            Assert.Contains("(Parameter 'pageNumber')", ex.Message);
            Assert.Contains("Actual value was 100. The page number should be greater or equal to '1' and less then the number of pages in the document: '15'.", ex.Message);
        }

        [Fact]
        public void SelectForward()
        {
            using (var doc = PdfDocument.Open("TIKA-584-0.pdf"))
            {
                doc.AddPageFactory<PageTextLayerContent, TextLayerFactory>();
                
                var selection = new PdfTextSelection(doc.NumberOfPages); // 588

                // Start selection
                var page5 = PdfTextLayerHelper.GetTextLayer(doc.GetPage<PageTextLayerContent>(5), CancellationToken.None);
                var firstWord = page5.TextBlocks[0].TextLines[0].Words[0];
                selection.Start(5, firstWord);

                Assert.True(selection.HasStarted);
                Assert.False(selection.IsValid);

                Assert.Equal(5, selection.AnchorPageIndex);
                Assert.Equal(-1, selection.AnchorOffset);
                Assert.Equal(-1, selection.AnchorOffsetDistance);
                Assert.Equal(firstWord, selection.AnchorWord);
                Assert.Null(selection.AnchorPoint);

                // Extend selection
                var page278 = PdfTextLayerHelper.GetTextLayer(doc.GetPage<PageTextLayerContent>(278), CancellationToken.None);
                var lastWord = page278.TextBlocks[0].TextLines[0].Words[0];
                selection.Extend(278, lastWord);

                Assert.True(selection.IsForward);
                Assert.False(selection.IsBackward);
                Assert.True(selection.HasStarted);
                Assert.True(selection.IsValid);

                Index startIndex = selection.GetFirstWordIndex();
                Assert.Equal(0, startIndex.Value);
                Assert.False(startIndex.IsFromEnd);

                Index endIndex = selection.GetLastWordIndex();
                Assert.Equal(1, endIndex.Value);
                Assert.True(endIndex.IsFromEnd);

                Assert.Equal(5, selection.AnchorPageIndex);
                Assert.Equal(-1, selection.AnchorOffset);
                Assert.Equal(-1, selection.AnchorOffsetDistance);
                Assert.Equal(firstWord, selection.AnchorWord);
                Assert.Null(selection.AnchorPoint);

                Assert.Equal(278, selection.FocusPageIndex);
                Assert.Equal(-1, selection.FocusOffset);
                Assert.Equal(-1, selection.FocusOffsetDistance);
                Assert.Equal(lastWord, selection.FocusWord);

                Assert.False(selection.IsPageInSelection(1));
                Assert.False(selection.IsPageInSelection(4));
                Assert.False(selection.IsPageInSelection(279));
                Assert.False(selection.IsPageInSelection(588));

                Assert.True(selection.IsPageInSelection(5));
                Assert.True(selection.IsPageInSelection(100));
                Assert.True(selection.IsPageInSelection(278));
            }
        }

        [Fact]
        public void SelectBackward()
        {
            using (var doc = PdfDocument.Open("TIKA-584-0.pdf"))
            {
                doc.AddPageFactory<PageTextLayerContent, TextLayerFactory>();
                var selection = new PdfTextSelection(doc.NumberOfPages); // 588

                // Start selection
                var page369 = PdfTextLayerHelper.GetTextLayer(doc.GetPage<PageTextLayerContent>(369), CancellationToken.None);
                var firstWord = page369.TextBlocks[0].TextLines[0].Words[0];
                selection.Start(369, firstWord);

                Assert.True(selection.HasStarted);
                Assert.False(selection.IsValid);

                Assert.Equal(369, selection.AnchorPageIndex);
                Assert.Equal(-1, selection.AnchorOffset);
                Assert.Equal(-1, selection.AnchorOffsetDistance);
                Assert.Equal(firstWord, selection.AnchorWord);
                Assert.Null(selection.AnchorPoint);

                // Extend selection
                var page278 = PdfTextLayerHelper.GetTextLayer(doc.GetPage<PageTextLayerContent>(278), CancellationToken.None);
                var lastWord = page278.TextBlocks[0].TextLines[0].Words[0];
                selection.Extend(278, lastWord);

                Assert.False(selection.IsForward);
                Assert.True(selection.IsBackward);
                Assert.True(selection.HasStarted);
                Assert.True(selection.IsValid);

                Index startIndex = selection.GetFirstWordIndex();
                Assert.Equal(1, startIndex.Value);
                Assert.True(startIndex.IsFromEnd);

                Index endIndex = selection.GetLastWordIndex();
                Assert.Equal(0, endIndex.Value);
                Assert.False(endIndex.IsFromEnd);

                Assert.Equal(369, selection.AnchorPageIndex);
                Assert.Equal(-1, selection.AnchorOffset);
                Assert.Equal(-1, selection.AnchorOffsetDistance);
                Assert.Equal(firstWord, selection.AnchorWord);
                Assert.Null(selection.AnchorPoint);

                Assert.Equal(278, selection.FocusPageIndex);
                Assert.Equal(-1, selection.FocusOffset);
                Assert.Equal(-1, selection.FocusOffsetDistance);
                Assert.Equal(lastWord, selection.FocusWord);

                Assert.False(selection.IsPageInSelection(1));
                Assert.False(selection.IsPageInSelection(277));
                Assert.False(selection.IsPageInSelection(370));
                Assert.False(selection.IsPageInSelection(588));

                Assert.True(selection.IsPageInSelection(278));
                Assert.True(selection.IsPageInSelection(300));
                Assert.True(selection.IsPageInSelection(369));
            }
        }

        [Fact]
        public void SelectAll()
        {
            const int numberOfPages = 15;
            
            var selection = new PdfTextSelection(numberOfPages);
            
            // Select all
            selection.SetAllSelected();

            Assert.Equal(numberOfPages, selection.NumberOfPages);
            Assert.True(selection.HasStarted);
            Assert.True(selection.IsValid);
            Assert.True(selection.IsForward);
            
            Assert.Equal(1, selection.AnchorPageIndex);
            Assert.Equal(1, selection.GetStartPageIndex());

            Assert.Equal(numberOfPages, selection.FocusPageIndex);
            Assert.Equal(numberOfPages, selection.GetEndPageIndex());

            Index startIndex = selection.GetFirstWordIndex();
            Assert.Equal(0, startIndex.Value);
            Assert.False(startIndex.IsFromEnd);

            Index endIndex = selection.GetLastWordIndex();
            Assert.Equal(1, endIndex.Value);
            Assert.True(endIndex.IsFromEnd);

            Assert.Equal(-1, selection.AnchorOffset);
            Assert.Equal(-1, selection.FocusOffset);

            Assert.Equal(-1, selection.AnchorOffsetDistance);
            Assert.Equal(-1, selection.FocusOffsetDistance);

            Assert.Null(selection.AnchorWord);
            Assert.Null(selection.FocusWord);
            Assert.Null(selection.AnchorPoint);

            Assert.True(selection.IsPageInSelection(1));
            Assert.True(selection.IsPageInSelection(7));
            Assert.True(selection.IsPageInSelection(15));

            // Reset selection
            selection.ResetSelection();

            Assert.Equal(numberOfPages, selection.NumberOfPages);
            Assert.False(selection.HasStarted);
            Assert.False(selection.IsValid);
            Assert.True(selection.IsForward);

            Assert.Equal(-1, selection.AnchorPageIndex);
            Assert.Equal(-1, selection.GetStartPageIndex());

            Assert.Equal(-1, selection.FocusPageIndex);
            Assert.Equal(-1, selection.GetEndPageIndex());

            startIndex = selection.GetFirstWordIndex();
            Assert.Equal(0, startIndex.Value);
            Assert.False(startIndex.IsFromEnd);

            endIndex = selection.GetLastWordIndex();
            Assert.Equal(1, endIndex.Value);
            Assert.True(endIndex.IsFromEnd);

            Assert.Equal(-1, selection.AnchorOffset);
            Assert.Equal(-1, selection.FocusOffset);

            Assert.Equal(-1, selection.AnchorOffsetDistance);
            Assert.Equal(-1, selection.FocusOffsetDistance);

            Assert.Null(selection.AnchorWord);
            Assert.Null(selection.FocusWord);
            Assert.Null(selection.AnchorPoint);

            Assert.False(selection.IsPageInSelection(1));
            Assert.False(selection.IsPageInSelection(7));
            Assert.False(selection.IsPageInSelection(15));
        }
    }
}
