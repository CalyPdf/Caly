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

using BenchmarkDotNet.Attributes;
using Caly.Pdf.Layout;
using Caly.Pdf.Models;
using Caly.Pdf.PageFactories;
using UglyToad.PdfPig;

namespace Caly.Benchmarks
{
    [MemoryDiagnoser]
    public class DuplicateOverlappingTextBenchmarks
    {
        private const string _pathNoDup = "2559 words.pdf";
        private const string _pathDup = "Document-PublisherError-1.pdf";

        private readonly IReadOnlyList<PdfLetter> _calyWordsNoDup;
        private readonly IReadOnlyList<PdfLetter> _calyWordsDup;

        public DuplicateOverlappingTextBenchmarks()
        {
            using (var doc = PdfDocument.Open(_pathNoDup))
            {
                doc.AddPageFactory<PageTextLayerContent, TextLayerFactory>();

                var layer = doc.GetPage<PageTextLayerContent>(1);
                _calyWordsNoDup = layer.Letters;
            }

            using (var doc = PdfDocument.Open(_pathDup))
            {
                doc.AddPageFactory<PageTextLayerContent, TextLayerFactory>();

                var layer = doc.GetPage<PageTextLayerContent>(1);
                _calyWordsDup = layer.Letters;
            }
        }

        [Benchmark]
        public IReadOnlyList<PdfLetter> BaseNoDup()
        {
            return CalyDuplicateOverlappingTextProcessor.Get(_calyWordsNoDup, CancellationToken.None);
        }

        [Benchmark]
        public IReadOnlyList<PdfLetter> BaseDup()
        {
            return CalyDuplicateOverlappingTextProcessor.Get(_calyWordsDup, CancellationToken.None);
        }
    }
}
