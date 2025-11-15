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
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Caly.Core.Controls;
using Caly.Core.Models;
using Caly.Core.ViewModels;

namespace Caly.Core.Handlers.Interfaces
{
    public interface IPageInteractiveLayerHandler
    {
        TextSelection Selection { get; }

        void OnPointerMoved(PointerEventArgs e);

        void OnPointerPressed(PointerPressedEventArgs e);

        void OnPointerReleased(PointerReleasedEventArgs e);

        void OnPointerExitedEvent(PointerEventArgs e);

        /// <summary>
        /// Add results to existing ones.
        /// </summary>
        void AddTextSearchResults(DocumentViewModel documentViewModel, IReadOnlyCollection<TextSearchResultViewModel> searchResults);

        /// <summary>
        /// Clear current results.
        /// </summary>
        void ClearTextSearchResults(DocumentViewModel documentViewModel);

        /// <summary>
        /// TODO - Should not be in selection handler.
        /// </summary>
        void RenderPage(PageInteractiveLayerControl control, DrawingContext context, Rect visibleArea);

        /// <summary>
        /// Clear the current selection.
        /// </summary>
        void ClearSelection(DocumentControl documentControl);

        /// <summary>
        /// Clear the current selection.
        /// </summary>
        void ClearSelection(PageInteractiveLayerControl currentTextLayer);

        void UpdateInteractiveLayer(PageViewModel page);
    }
}
