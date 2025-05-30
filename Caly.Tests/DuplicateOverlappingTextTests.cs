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

using Caly.Pdf.Layout;
using Caly.Pdf.Models;
using Caly.Pdf.PageFactories;
using UglyToad.PdfPig;

namespace Caly.Tests
{
    public class DuplicateOverlappingTextTests
    {
        [Fact]
        public void Document_PublisherError_1()
        {
            using (var doc = PdfDocument.Open("Document-PublisherError-1.pdf"))
            {
                doc.AddPageFactory<PageTextLayerContent, TextLayerFactory>();

                var layer = doc.GetPage<PageTextLayerContent>(1);
                var expectedLetters = CalyDuplicateOverlappingTextProcessor.Get(layer.Letters, CancellationToken.None);
                var expectedWords = CalyNNWordExtractor.Instance.GetWords(expectedLetters, CancellationToken.None).OrderByReadingOrder().ToArray();
                var expectedParagraphs = CalyDocstrum.Instance.GetBlocks(expectedWords, CancellationToken.None).ToArray();

                layer = doc.GetPage<PageTextLayerContent>(1);
                var actualLetters = CalyDuplicateOverlappingTextProcessor.GetInPlace(layer.Letters.ToList(), CancellationToken.None);
                var actualWords = CalyNNWordExtractor.Instance.GetWords(actualLetters, CancellationToken.None).OrderByReadingOrder().ToArray();
                var actualParagraphs = CalyDocstrum.Instance.GetBlocks(actualWords, CancellationToken.None).ToArray();

                Assert.Equal(expectedParagraphs.Length, actualParagraphs.Length);

                for (int i = 0; i < expectedParagraphs.Length; ++i)
                {
                    var expected = expectedParagraphs[i];
                    var actual = actualParagraphs[i];
                    Assert.Equal(expected.TextLines.Count, actual.TextLines.Count);

                    for (int l = 0; l < expected.TextLines.Count; ++l)
                    {
                        var expectedLine = expected.TextLines[l];
                        var actualLine = actual.TextLines[l];
                        Assert.Equal(expectedLine.Words.Count, actualLine.Words.Count);

                        for (int w = 0; w < expectedLine.Words.Count; ++w)
                        {
                            var expectedWord = expectedLine.Words[w];
                            var actualWord = actualLine.Words[w];
                            Assert.True(actualWord.Value.AsSpan().SequenceEqual(expectedWord.Value.AsSpan()));
                        }
                    }
                }
            }
        }
    }
}