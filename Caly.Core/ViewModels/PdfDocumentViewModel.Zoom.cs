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

using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Caly.Core.ViewModels
{
    public partial class PdfDocumentViewModel
    {
        private static readonly double[] _zoomLevelsDiscrete =
        [
            0.08, 0.125, 0.25, 0.33, 0.5, 0.67, 0.75, 1,
            1.25, 1.5, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64
        ];

        /*
         * See PDF Reference 1.7 - C.2 Architectural limits
         * The magnification factor of a view should be constrained to be between approximately 8 percent and 6400 percent.
         */
#pragma warning disable CA1822
        public double MinZoomLevel => 0.08;
        public double MaxZoomLevel => 64;
#pragma warning restore CA1822

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ZoomInCommand))]
        [NotifyCanExecuteChangedFor(nameof(ZoomOutCommand))]
        private double _zoomLevel = 1;
        
        [RelayCommand(CanExecute = nameof(CanZoomIn))]
        private void ZoomIn()
        {
            var index = Array.BinarySearch(_zoomLevelsDiscrete, ZoomLevel);
            if (index < -1)
            {
                ZoomLevel = Math.Min(MaxZoomLevel, _zoomLevelsDiscrete[~index]);
            }
            else
            {
                if (index >= _zoomLevelsDiscrete.Length - 1)
                {
                    return;
                }

                ZoomLevel = Math.Min(MaxZoomLevel, _zoomLevelsDiscrete[index + 1]);
            }
        }

        private bool CanZoomIn()
        {
            return ZoomLevel < MaxZoomLevel;
        }

        [RelayCommand(CanExecute = nameof(CanZoomOut))]
        private void ZoomOut()
        {
            var index = Array.BinarySearch(_zoomLevelsDiscrete, ZoomLevel);
            if (index < -1)
            {
                ZoomLevel = Math.Max(MinZoomLevel, _zoomLevelsDiscrete[~index - 1]);
            }
            else
            {
                if (index == 0)
                {
                    return;
                }

                ZoomLevel = Math.Max(MinZoomLevel, _zoomLevelsDiscrete[index - 1]);
            }
        }

        private bool CanZoomOut()
        {
            return ZoomLevel > MinZoomLevel;
        }

        [RelayCommand]
        private void RotateAllPagesClockwise()
        {
            ExecutePreservePage(() =>
            {
                foreach (PdfPageViewModel page in Pages)
                {
                    page.RotateClockwise();
                }
            });
        }

        [RelayCommand]
        private void RotateAllPagesCounterclockwise()
        {
            ExecutePreservePage(() =>
            {
                foreach (PdfPageViewModel page in Pages)
                {
                    page.RotateCounterclockwise();
                }
            });
        }

        [RelayCommand]
        private void RotatePageClockwise(int pageNumber)
        {
            if (pageNumber <= 0 || pageNumber > Pages.Count)
            {
                return;
            }

            ExecutePreservePage(() => Pages[pageNumber - 1].RotateClockwise());
        }

        [RelayCommand]
        private void RotatePageCounterclockwise(int pageNumber)
        {
            if (pageNumber <= 0 || pageNumber > Pages.Count)
            {
                return;
            }

            ExecutePreservePage(() => Pages[pageNumber - 1].RotateCounterclockwise());
        }

        private void ExecutePreservePage(Action action)
        {
            // TODO - Investigate why page selection changes

            int? current = SelectedPageIndex;

            action();

            Dispatcher.UIThread.Post(() =>
            {
                // We makes sure selected page did not change
                SelectedPageIndex = current;
            }, DispatcherPriority.Loaded);
        }
    }
}
