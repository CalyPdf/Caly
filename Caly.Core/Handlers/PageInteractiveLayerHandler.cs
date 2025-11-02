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

using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Caly.Core.Controls;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Models;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Caly.Pdf;
using Caly.Pdf.Models;
using System;
using System.Collections.Generic;
using UglyToad.PdfPig.Actions;
using UglyToad.PdfPig.Core;

namespace Caly.Core.Handlers
{
    // See https://github.com/AvaloniaUI/Avalonia.HtmlRenderer/blob/master/Source/HtmlRenderer/Core/Handlers/SelectionHandler.cs
    //
    // See https://github.com/AvaloniaUI/Avalonia/pull/13107/files#diff-f183b476e3366d748fd935e515bf1c8d8845525dcb130aae00ebd70422cd453e
    // See https://github.com/AvaloniaUI/AvaloniaEdit/blob/master/src/AvaloniaEdit/Editing/SelectionLayer.cs

    public sealed partial class PageInteractiveLayerHandler : IPageInteractiveLayerHandler
    {
        /// <summary>
        /// <c>true</c> if we are currently selecting text. <c>false</c> otherwise.
        /// </summary>
        private bool _isSelecting;

        /// <summary>
        /// <c>true</c> if we are selecting text though multiple click (full word selection).
        /// </summary>
        private bool _isMultipleClickSelection;

        private Point? _startPointerPressed;

        public PdfTextSelection Selection { get; }

        public PageInteractiveLayerHandler(int numberOfPages)
        {
            Selection = new PdfTextSelection(numberOfPages);
            _searchWordsResults = new IReadOnlyList<PdfWord>[numberOfPages];
            _searchIndexResults = new IReadOnlyList<Range>[numberOfPages];
        }

        public void UpdateInteractiveLayer(PdfPageViewModel page)
        {
            if (page.PdfTextLayer is null || page.PdfTextLayer.Count == 0)
            {
                return;
            }

            // Update selection
            Selection.SelectWordsInRange(page);

            // Update search results
            var searchRange = _searchIndexResults[page.PageNumber - 1];

            if (searchRange is not null && searchRange.Count > 0)
            {
                var results = new List<PdfWord>(searchRange.Count);
                
                for (var i = 0; i < searchRange.Count; i++)
                {
                    var range = searchRange[i];
                    var start = page.PdfTextLayer[range.Start];
                    var end = page.PdfTextLayer[range.End];
                    results.AddRange(page.PdfTextLayer.GetWords(start, end));
                }

                _searchWordsResults[page.PageNumber - 1] = results;
            }

            Dispatcher.UIThread.Invoke(page.FlagInteractiveLayerChanged);
        }

        private static bool TrySwitchCapture(PdfPageTextLayerControl currentTextLayer, PointerEventArgs e)
        {
            PdfPageItem? endPdfPage = currentTextLayer.FindAncestorOfType<PdfDocumentControl>()?.GetPdfPageItemOver(e);
            if (endPdfPage is null)
            {
                // Cursor is not over any page, do nothing
                return false;
            }

            PdfPageTextLayerControl endTextLayer = endPdfPage.TextLayer ??
                                                   throw new NullReferenceException($"{typeof(PdfPageTextLayerControl)} not found.");

            e.Pointer.Capture(endTextLayer); // Switch capture to new page
            return true;
        }

        private PdfWord? FindNearestWordWhileSelecting(Point loc, PdfTextLayer textLayer)
        {
            if (textLayer.TextBlocks is null || textLayer.TextBlocks.Count == 0)
            {
                return null;
            }

#if DEBUG
            p1 = loc;
#endif

            // Try finding the closest line as we are already selecting something

            // TODO - To finish, improve performance
            var point = new PdfPoint(loc.X, loc.Y);

            double dist = double.MaxValue;
            double projectionOnLine = 0;
            PdfTextLine? l = null;

            foreach (var block in textLayer.TextBlocks)
            {
                foreach (var line in block.TextLines)
                {
                    PdfPoint? projection = PdfPointExtensions.ProjectPointOnLine(in point,
                        line.BoundingBox.BottomLeft,
                        line.BoundingBox.BottomRight,
                        out double s);

                    if (!projection.HasValue || s < 0)
                    {
                        // If s < 0, the cursor is before the line (to the left), we ignore
                        continue;
                    }

                    // If s > 1, the cursor is after the line (to the right), we measure distance from bottom right corner
                    PdfPoint referencePoint = s > 1 ? line.BoundingBox.BottomRight : projection.Value;

                    double localDist = SquaredWeightedEuclidean(in point, in referencePoint, wY: 4); // Make y direction farther
                    
                    // TODO - Prevent selection line 'below' cursor

                    if (localDist < dist)
                    {
                        dist = localDist;
                        l = line;
                        projectionOnLine = s;
#if DEBUG
                        p2 = new Point(projection.Value.X, projection.Value.Y);
                        focusLine = l;
#endif
                    }
                }
            }

            if (l is null)
            {
#if DEBUG
                p2 = null;
                focusLine = null;
#endif
                return null;
            }

            if (projectionOnLine >= 1)
            {
                // Cursor after line, return last word
                return l.Words[^1];
            }

            // TODO - to improve, we already know where on the line is the point thanks to 'projectionOnLine'
            return l.FindNearestWord(loc.X, loc.Y);

            static double SquaredWeightedEuclidean(in PdfPoint point1, in PdfPoint point2, double wX = 1.0, double wY = 1.0)
            {
                double dx = point1.X - point2.X;
                double dy = point1.Y - point2.Y;
                return wX * dx * dx + wY * dy * dy;
            }
        }

        public void OnPointerExitedEvent(PointerEventArgs e)
        {
            Debug.ThrowNotOnUiThread();
            
            if (e.Source is not PdfPageTextLayerControl control)
            {
                return;
            }
            
            HideAnnotation(control);
        }

#if DEBUG
        Point? p1;
        Point? p2;
        PdfTextLine? focusLine;
#endif
        public void OnPointerMoved(PointerEventArgs e)
        {
#if DEBUG
            p1 = null;
            p2 = null;
            focusLine = null;
#endif
            if (e.IsPanningOrZooming())
            {
                // Panning pages is not handled here
                return;
            }

            // Needs to be on UI thread to access
            if (e.Source is not PdfPageTextLayerControl control || control.PdfTextLayer is null)
            {
                return;
            }

            var pointerPoint = e.GetCurrentPoint(control);
            var loc = pointerPoint.Position;

            if (e is PointerWheelEventArgs we)
            {
                // TODO - Looks like there's a bug in Avalonia (TBC) where the position of the pointer
                // is 1 step behind the actual position.
                // We need to add back this step (1 scroll step is 50, see link below)
                // https://github.com/AvaloniaUI/Avalonia/blob/dadc9ab69284bb228ad460f36d5442b4eee4a82a/src/Avalonia.Controls/Presenters/ScrollContentPresenter.cs#L684

                // TODO - The hack does not work when zoomed

                double x = Math.Max(loc.X - we.Delta.X * 50.0, 0);
                double y = Math.Max(loc.Y - we.Delta.Y * 50.0, 0);

                loc = new Point(x, y);

                // TODO - We have an issue when scrolling and changing page here, similar the TrySwitchCapture
                // not sure how we should address it
            }

            if (pointerPoint.Properties.IsLeftButtonPressed && _startPointerPressed.HasValue && _startPointerPressed.Value.Euclidean(loc) > 1.0)
            {
                // Text selection
                HandleMouseMoveSelection(control, e, loc);
            }
            else
            {
                HandleMouseMoveOver(control, loc);
            }
        }

        private void HandleMouseMoveSelection(PdfPageTextLayerControl control, PointerEventArgs e, Point loc)
        {
            if (_isMultipleClickSelection || control.DataContext is not PdfPageViewModel cvm)
            {
                return;
            }

            if (!control.Bounds.Contains(loc))
            {
                if (TrySwitchCapture(control, e))
                {
                    // Update all pages
                    return;
                }

                return;
            }

            // Get the line under the cursor or nearest from the top
            PdfTextLine? lineBox = control.PdfTextLayer!.FindLineOver(loc.X, loc.Y);

            PdfWord? word = null;
            if (Selection.HasStarted && lineBox is null)
            {
                // Try to find the closest line as we are already selecting something
                word = FindNearestWordWhileSelecting(loc, control.PdfTextLayer);
            }

            if (lineBox is null && word is null)
            {
                return;
            }
            
            if (lineBox is not null && word is null)
            {
                // Get the word under the cursor
                word = lineBox.FindWordOver(loc.X, loc.Y);

                // If no word found under the cursor use the last or the first word in the line
                if (word is null)
                {
                    word = lineBox.FindNearestWord(loc.X, loc.Y);
                }
            }

            if (word is null)
            {
                return;
            }

            // If there is matching word
            bool allowPartialSelect = !_isMultipleClickSelection;

            int focusPageIndex = Selection.FocusPageIndex;
            Point? partialSelectLoc = allowPartialSelect ? loc : null;
            if (!Selection.HasStarted)
            {
                Selection.Start(control.PageNumber!.Value, word, partialSelectLoc);
            }

            // Always set the focus word
            Selection.Extend(control.PageNumber!.Value, word, partialSelectLoc);
            Selection.SelectWordsInRange(cvm);

            // Check for change of focus page
            if (focusPageIndex != -1 && focusPageIndex != Selection.FocusPageIndex)
            {
                PdfDocumentControl pdfDocumentControl = control.FindAncestorOfType<PdfDocumentControl>() ??
                                                        throw new ArgumentNullException($"{typeof(PdfDocumentControl)} not found.");

                if (pdfDocumentControl.DataContext is not PdfDocumentViewModel docVm)
                {
                    throw new ArgumentNullException($"DataContext {typeof(PdfDocumentViewModel)} not set.");
                }

                // Focus page has changed
                int start = Math.Min(focusPageIndex, Selection.FocusPageIndex);
                int end = Math.Max(focusPageIndex, Selection.FocusPageIndex);
                for (int i = start; i <= end; ++i) // TODO - do not always do end page, only if deselecting
                {
                    Selection.SelectWordsInRange(docVm.Pages[i - 1]);
                }
            }

            control.SetIbeamCursor();

            _isSelecting = Selection.IsValid &&
                           (Selection.AnchorWord != Selection.FocusWord || // Multiple words selected
                            (Selection.AnchorOffset != -1 && Selection.FocusOffset != -1)); // Selection within same word
        }
        
        /// <summary>
        /// Handle mouse hover over words, links or others
        /// </summary>
        private static void HandleMouseMoveOver(PdfPageTextLayerControl control, Point loc)
        {
            PdfAnnotation? annotation = control.PdfTextLayer!.FindAnnotationOver(loc.X, loc.Y);

            if (annotation is not null)
            {
                if (!string.IsNullOrEmpty(annotation.Content))
                {
                    ShowAnnotation(control, annotation);
                }

                if (annotation.IsInteractive)
                {
                    control.SetHandCursor();
                    return;
                }
            }
            else
            {
                HideAnnotation(control);
            }

            PdfWord? word = control.PdfTextLayer!.FindWordOver(loc.X, loc.Y);
            if (word is not null)
            {
                if (control.PdfTextLayer.GetLine(word)?.IsInteractive == true)
                {
                    control.SetHandCursor();
                }
                else
                {
                    control.SetIbeamCursor();
                }
            }
            else
            {
                control.SetDefaultCursor();
            }
        }

        private void HandleMultipleClick(PdfPageTextLayerControl control, PointerPressedEventArgs e, PdfWord word)
        {
            if (control.PdfTextLayer is null || control.DataContext is not PdfPageViewModel vm)
            {
                return;
            }

            PdfWord? startWord;
            PdfWord? endWord;

            if (e.ClickCount == 2)
            {
                // Select whole word
                startWord = word;
                endWord = word;
            }
            else if (e.ClickCount == 3)
            {
                // Select whole line
                var block = control.PdfTextLayer.TextBlocks![word.TextBlockIndex];
                var line = block.TextLines![word.TextLineIndex - block.TextLines[0].IndexInPage];

                startWord = line.Words![0];
                endWord = line.Words[^1];
            }
            else if (e.ClickCount == 4)
            {
                // Select whole paragraph
                var block = control.PdfTextLayer.TextBlocks![word.TextBlockIndex];

                startWord = block.TextLines![0].Words![0];
                endWord = block.TextLines![^1].Words![^1];
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"HandleMultipleClick: Not handled, got {e.ClickCount} click(s).");
                return;
            }

            ClearSelection(control);

            int pageNumber = control.PageNumber!.Value;
            Selection.Start(pageNumber, startWord);
            Selection.Extend(pageNumber, endWord);
            Selection.SelectWordsInRange(vm);

            System.Diagnostics.Debug.WriteLine($"HandleMultipleClick: {startWord} -> {endWord}.");
        }

        public void OnPointerPressed(PointerPressedEventArgs e)
        {
            Debug.ThrowNotOnUiThread();

            if (e.Source is not PdfPageTextLayerControl control || control.PdfTextLayer is null)
            {
                return;
            }

            if (e.IsPanningOrZooming())
            {
                // Panning pages is not handled here
                HideAnnotation(control);
                return;
            }

            bool clearSelection = false;

            _isMultipleClickSelection = e.ClickCount > 1;

            var pointerPoint = e.GetCurrentPoint(control);
            var point = pointerPoint.Position;

            if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                _startPointerPressed = point;

                // Text selection
                PdfWord? word = control.PdfTextLayer.FindWordOver(point.X, point.Y);

                if (word is not null && Selection.IsWordSelected(control.PageNumber!.Value, word))
                {
                    clearSelection = e.ClickCount == 1; // Clear selection if single click
                    HandleMultipleClick(control, e, word); // TODO - we pass 1 click here too
                }
                else if (word is not null && e.ClickCount == 2)
                {
                    // TODO - do better multiple click selection
                    HandleMultipleClick(control, e, word);
                }
                else
                {
                    clearSelection = true;
                }
            }
            else if (pointerPoint.Properties.IsRightButtonPressed)
            {
                // TODO
            }

            if (clearSelection)
            {
                ClearSelection(control);
            }
        }

        public void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (e.IsPanningOrZooming())
            {
                // Panning pages is not handled here
                return;
            }

            if (e.Source is not PdfPageTextLayerControl control || control.PdfTextLayer is null)
            {
                return;
            }

            _startPointerPressed = null;

            var pointerPoint = e.GetCurrentPoint(control);

            bool ignore = _isSelecting || _isMultipleClickSelection;
            if (!ignore && pointerPoint.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                ClearSelection(control);

                // Check link
                if (!_isSelecting)
                {
                    var point = pointerPoint.Position;

                    // Annotation
                    PdfAnnotation? annotation = control.PdfTextLayer.FindAnnotationOver(point.X, point.Y);

                    if (annotation?.Action is not null)
                    {
                        switch (annotation.Action.Type)
                        {
                            case ActionType.URI:
                                string? uri = ((UriAction)annotation.Action)?.Uri;
                                if (!string.IsNullOrEmpty(uri))
                                {
                                    CalyExtensions.OpenBrowser(uri);
                                    return;
                                }
                                break;

                            case ActionType.GoTo:
                            case ActionType.GoToE:
                            case ActionType.GoToR:
                                var goToAction = (AbstractGoToAction)annotation.Action;
                                var dest = goToAction?.Destination;
                                if (dest is not null)
                                {
                                    var documentControl = control.FindAncestorOfType<PdfDocumentControl>();
                                    documentControl?.GoToPage(dest.PageNumber);
                                    return;
                                }
                                else
                                {
                                    // Log error
                                }
                                break;
                        }
                    }

                    // Words
                    PdfWord? word = control.PdfTextLayer.FindWordOver(point.X, point.Y);
                    if (word is not null && control.PdfTextLayer.GetLine(word) is { IsInteractive: true } line)
                    {
                        /*
                         * TODO - Use TopLevel.GetTopLevel(source)?.Launcher
                         *  if (e.Source is Control source && TopLevel.GetTopLevel(source)?.Launcher is {}
                         *  launcher && word is not null && control.PdfTextLayer.GetLine(word) is { IsInteractive: true } line)
                         *  ...
                         *  launcher.LaunchUriAsync(new Uri(match.ToString()))
                         */

                        var match = PdfTextLayerHelper.GetInteractiveMatch(line);
                        if (!match.IsEmpty)
                        {
                            CalyExtensions.OpenBrowser(match);
                        }
                    }
                }
            }

            _isSelecting = false;
        }
    }
}
