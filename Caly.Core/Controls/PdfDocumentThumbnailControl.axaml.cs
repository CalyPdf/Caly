// Copyright (C) 2024 BobLd
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY - without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

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
            _listBox.ContainerPrepared += _listBox_ContainerPrepared;
            _listBox.ContainerClearing += _listBox_ContainerClearing;
            _listBox.PropertyChanged += _listBox_PropertyChanged;
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);

            if (_listBox is not null)
            {
                _listBox.ContainerPrepared -= _listBox_ContainerPrepared;
                _listBox.ContainerClearing -= _listBox_ContainerClearing;
                _listBox.PropertyChanged -= _listBox_PropertyChanged;
            }
        }
        
        private void _listBox_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == DataContextProperty && e.OldValue is PdfDocumentViewModel oldVm)
            {
                oldVm.ClearAllThumbnails();
            }
        }
        
        private void _listBox_ContainerPrepared(object? sender, ContainerPreparedEventArgs e)
        {
            if (_isScrollingToPage)
            {
                return;
            }

            if (e.Container is not ListBoxItem container || e.Container.DataContext is not PdfPageViewModel vm)
            {
                return;
            }
            
            container.PropertyChanged += _onContainerPropertyChanged;
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
