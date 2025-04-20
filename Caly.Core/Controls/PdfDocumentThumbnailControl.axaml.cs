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

using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls
{
    [TemplatePart("PART_ListBox", typeof(ListBox))]
    public sealed class PdfDocumentThumbnailControl : TemplatedControl
    {
        private ListBox? _listBox;
        private bool _isScrollingToPage = false;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            
            _listBox = e.NameScope.FindFromNameScope<ListBox>("PART_ListBox");
            _listBox.PreparingContainer += ListBoxOnPreparingContainer;
            _listBox.ContainerPrepared += _listBox_ContainerPrepared;
            _listBox.ContainerClearing += _listBox_ContainerClearing;
            _listBox.PropertyChanged += ListBoxOnPropertyChanged;
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);

            if (_listBox is not null)
            {
                _listBox.PreparingContainer -= ListBoxOnPreparingContainer;
                _listBox.ContainerPrepared -= _listBox_ContainerPrepared;
                _listBox.ContainerClearing -= _listBox_ContainerClearing;
                _listBox.PropertyChanged -= ListBoxOnPropertyChanged;
            }
        }

        private void ListBoxOnPreparingContainer(object? sender, ContainerPreparedEventArgs e)
        {
            if (_isScrollingToPage || e.Container is not ListBoxItem container)
            {
                return;
            }
            
            container.PropertyChanged += _onContainerPropertyChanged;
        }

        private void ListBoxOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == DataContextProperty && e.OldValue is PdfDocumentViewModel oldVm)
            {
                oldVm.ClearAllThumbnails();
            }
            else if (e.Property == SelectingItemsControl.SelectedIndexProperty && DataContext is PdfDocumentViewModel vm)
            {
                // Looks like there a bug where the binding does not work when new page = old page + 1
                vm.SelectedPageIndex = (int?)e.NewValue + 1;
            }
        }
        
        private void _listBox_ContainerPrepared(object? sender, ContainerPreparedEventArgs e)
        {
            if (_isScrollingToPage || e.Container.DataContext is not PdfPageViewModel vm)
            {
                return;
            }
            
            vm.LoadThumbnail();
        }

        private void _onContainerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ContentPresenter.ContentProperty && e.OldValue is PdfPageViewModel vm)
            {
                vm.UnloadThumbnail();
            }
        }

        private void _listBox_ContainerClearing(object? sender, ContainerClearingEventArgs e)
        {
            if (e.Container is not ListBoxItem container)
            {
                return;
            }

            container.PropertyChanged -= _onContainerPropertyChanged;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsVisibleProperty && _listBox is not null)
            {
                if (change is { OldValue: false, NewValue: true })
                {
                    // Thumbnails control becomes visible
                    try
                    {
                        _isScrollingToPage = true;
                        _listBox.ScrollIntoView(_listBox.SelectedIndex);
                    }
                    finally
                    {
                        _isScrollingToPage = false;
                    }

                    // Check thumbnails visibility
                    if (_listBox.Parent is not ScrollViewer sv)
                    {
                        return;
                    }

                    Rect viewPort = new Rect((Point)sv.Offset, sv.Viewport);
                    foreach (ListBoxItem listBoxItem in _listBox.GetRealizedContainers().OfType<ListBoxItem>())
                    {
                        if (listBoxItem.DataContext is PdfPageViewModel vm && viewPort.Intersects(listBoxItem.Bounds))
                        {
                            vm.LoadThumbnail(); // Load image
                        }
                    }
                }
                else if (change is { OldValue: true, NewValue: false })
                {
                    // Thumbnails control is hidden
                    if (DataContext is PdfDocumentViewModel vm)
                    {
                        vm.ClearAllThumbnails();
                    }
                }
            }
        }
    }
}
