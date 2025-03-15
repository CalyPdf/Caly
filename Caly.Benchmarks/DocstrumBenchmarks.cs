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
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Caly.Benchmarks
{
    [MemoryDiagnoser]
    public class DocstrumBenchmarks
    {
        //private const string _path = "fseprd1102849.pdf";
        private const string _path = "2559 words.pdf";

        private readonly Word[] _words;
        private readonly PdfWord[] _calyWords;

        public DocstrumBenchmarks()
        {
            using (var doc = PdfDocument.Open(_path))
            {
                doc.AddPageFactory<PageTextLayerContent, TextLayerFactory>();

                var page = doc.GetPage(1);
                _words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToArray();

                var layer = doc.GetPage<PageTextLayerContent>(1);
                _calyWords = CalyNNWordExtractor.Instance.GetWords(layer.Letters, CancellationToken.None).ToArray();
            }
        }

        [Benchmark(Baseline = true)]
        public IReadOnlyList<TextBlock> PdfPig()
        {
            return DocstrumBoundingBoxes.Instance.GetBlocks(_words);
        }

        [Benchmark]
        public IReadOnlyList<PdfTextBlock> Caly()
        {
            return CalyDocstrum.Instance.GetBlocks(_calyWords, CancellationToken.None);
        }


        //[Benchmark]
        //public IReadOnlyList<PdfTextBlock> Caly2()
        //{
        //    return CalyDocstrum2.Instance.GetBlocks(_calyWords); //, CancellationToken.None);
        //}
    }
}