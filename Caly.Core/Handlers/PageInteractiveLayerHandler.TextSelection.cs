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

using Avalonia.VisualTree;
using Caly.Core.Controls;
using Caly.Core.ViewModels;
using System;
using Avalonia.Media;

namespace Caly.Core.Handlers
{
    public sealed partial class PageInteractiveLayerHandler
    {
        private static readonly Color _selectionColor = Color.FromArgb(0xa9, 0x33, 0x99, 0xFF);

        public void ClearSelection(PdfDocumentControl pdfDocumentControl)
        {
            Debug.ThrowNotOnUiThread();
            int start = Selection.GetStartPageIndex();
            int end = Selection.GetEndPageIndex();

            System.Diagnostics.Debug.Assert(start <= end);

            Selection.ResetSelection();

            if (start == -1 || end == -1 ||
                pdfDocumentControl.DataContext is not PdfDocumentViewModel docVm)
            {
                return;
            }

            for (int pageNumber = start; pageNumber <= end; ++pageNumber)
            {
                docVm.Pages[pageNumber - 1].FlagInteractiveLayerChanged();
            }
        }

        public void ClearSelection(PdfPageTextLayerControl currentTextLayer)
        {
            Debug.ThrowNotOnUiThread();

            PdfDocumentControl pdfDocumentControl = currentTextLayer.FindAncestorOfType<PdfDocumentControl>() ??
                                                    throw new NullReferenceException($"{typeof(PdfDocumentControl)} not found.");
            ClearSelection(pdfDocumentControl);
        }
    }
}
