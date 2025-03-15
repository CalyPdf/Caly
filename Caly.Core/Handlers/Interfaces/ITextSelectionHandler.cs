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

using System.Collections.Generic;
using Avalonia.Input;
using Avalonia.Media;
using Caly.Core.Controls;
using Caly.Core.Models;
using Caly.Core.ViewModels;

namespace Caly.Core.Handlers.Interfaces
{
    public interface ITextSelectionHandler
    {
        PdfTextSelection Selection { get; }

        void OnPointerMoved(PointerEventArgs e);

        void OnPointerPressed(PointerPressedEventArgs e);

        void OnPointerReleased(PointerReleasedEventArgs e);
        
        /// <summary>
        /// Add results to existing ones.
        /// </summary>
        void AddTextSearchResults(PdfDocumentViewModel documentViewModel, IReadOnlyCollection<TextSearchResultViewModel> searchResults);

        /// <summary>
        /// Clear current results.
        /// </summary>
        void ClearTextSearchResults(PdfDocumentViewModel documentViewModel);

        /// <summary>
        /// TODO - Should not be in selection handler.
        /// </summary>
        void RenderPage(PdfPageTextLayerControl control, DrawingContext context);

        /// <summary>
        /// Clear the current selection.
        /// </summary>
        void ClearSelection(PdfDocumentControl pdfDocumentControl);

        /// <summary>
        /// Clear the current selection.
        /// </summary>
        void ClearSelection(PdfPageTextLayerControl currentTextLayer);
    }
}
