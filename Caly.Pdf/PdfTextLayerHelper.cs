﻿// Copyright (c) 2025 BobLd
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
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace Caly.Pdf
{
    public static class PdfTextLayerHelper
    {
        public static bool IsStroke(this TextRenderingMode textRenderingMode)
        {
            switch (textRenderingMode)
            {
                case TextRenderingMode.Stroke:
                case TextRenderingMode.StrokeClip:
                case TextRenderingMode.FillThenStroke:
                case TextRenderingMode.FillThenStrokeClip:
                    return true;

                case TextRenderingMode.Fill:
                case TextRenderingMode.FillClip:
                case TextRenderingMode.NeitherClip:
                case TextRenderingMode.Neither:
                    return false;

                default:
                    return false;
            }
        }

        public static bool IsFill(this TextRenderingMode textRenderingMode)
        {
            switch (textRenderingMode)
            {
                case TextRenderingMode.Fill:
                case TextRenderingMode.FillClip:
                case TextRenderingMode.FillThenStroke:
                case TextRenderingMode.FillThenStrokeClip:
                    return true;

                case TextRenderingMode.Stroke:
                case TextRenderingMode.StrokeClip:
                case TextRenderingMode.NeitherClip:
                case TextRenderingMode.Neither:
                    return false;

                default:
                    return false;
            }
        }

        public static PdfTextLayer GetTextLayer(PageTextLayerContent page, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (page.Letters.Count == 0)
            {
                return PdfTextLayer.Empty;
            }

            var letters = CalyDuplicateOverlappingTextProcessor.GetInPlace(page.Letters, token);
            var words = CalyNNWordExtractor.Instance.GetWords(letters, token);
            var pdfBlocks = CalyDocstrum.Instance.GetBlocks(words, token);

            ushort wordIndex = 0;
            ushort lineIndex = 0;
            ushort blockIndex = 0;

            foreach (PdfTextBlock block in pdfBlocks)
            {
                ushort blockStartIndex = wordIndex;

                foreach (PdfTextLine line in block.TextLines)
                {
                    ushort lineStartIndex = wordIndex;

                    foreach (PdfWord word in line.Words)
                    {
                        // throw if cancelled every now and then
                        if (wordIndex % 100 == 0)
                        {
                            token.ThrowIfCancellationRequested();
                        }

                        word.IndexInPage = wordIndex++;
                        word.TextLineIndex = lineIndex;
                        word.TextBlockIndex = blockIndex;
                    }

                    line.IndexInPage = lineIndex++;
                    line.TextBlockIndex = blockIndex;
                    line.WordStartIndex = lineStartIndex;
                }

                block.IndexInPage = blockIndex++;
                block.WordStartIndex = blockStartIndex;
                block.WordEndIndex = ushort.CreateChecked(wordIndex - 1);
            }

            return new PdfTextLayer(pdfBlocks, page.Annotations);
        }

        private static PdfPoint InverseYAxis(PdfPoint point, double height)
        {
            return new PdfPoint(point.X, height - point.Y);
        }

        private static PdfRectangle InverseYAxis(PdfRectangle rectangle, double height)
        {
            PdfPoint topLeft = InverseYAxis(rectangle.TopLeft, height);
            PdfPoint topRight = InverseYAxis(rectangle.TopRight, height);
            PdfPoint bottomLeft = InverseYAxis(rectangle.BottomLeft, height);
            PdfPoint bottomRight = InverseYAxis(rectangle.BottomRight, height);
            return new PdfRectangle(topLeft, topRight, bottomLeft, bottomRight);
        }

        internal static TextOrientation GetTextOrientation(IReadOnlyList<IPdfTextElement> letters)
        {
            if (letters.Count == 1)
            {
                return letters[0].TextOrientation;
            }

            TextOrientation tempTextOrientation = letters[0].TextOrientation;
            if (tempTextOrientation == TextOrientation.Other)
            {
                return tempTextOrientation;
            }

            foreach (IPdfTextElement letter in letters)
            {
                if (letter.TextOrientation != tempTextOrientation)
                {
                    return TextOrientation.Other;
                }
            }
            return tempTextOrientation;
        }
    }
}
