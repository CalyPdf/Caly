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

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Caly.Pdf.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Caly.Core.ViewModels
{
    public partial class PdfDocumentViewModel
    {
        private readonly Lazy<Task> _loadLayersTask;
        public Task LoadLayersTask => _loadLayersTask.Value;

        private readonly Lazy<Task> _loadPropertiesTask;
        public Task LoadPropertiesTask => _loadPropertiesTask.Value;

        [ObservableProperty] private PdfDocumentProperties _properties;

        [ObservableProperty] private ObservableCollection<PdfDocumentLayerViewModel>? _layers;

        [ObservableProperty] private HierarchicalTreeDataGridSource<PdfDocumentLayerViewModel> _layersSource;

        [ObservableProperty] private PdfDocumentLayerViewModel? _selectedLayer;


        private Task LoadProperties()
        {
            _cts.Token.ThrowIfCancellationRequested();
            return Task.Run(() => _pdfService.SetDocumentPropertiesAsync(this, _cts.Token));
        }

        private async Task LoadLayers()
        {
            _cts.Token.ThrowIfCancellationRequested();
            await Task.Run(() => _pdfService.SetDocumentLayersAsync(this, _cts.Token));

            if (Layers?.Count > 0)
            {
                LayersSource = new HierarchicalTreeDataGridSource<PdfDocumentLayerViewModel>(Layers)
                {
                    Columns =
                    {
                        new HierarchicalExpanderColumn<PdfDocumentLayerViewModel>(
                            new TextColumn<PdfDocumentLayerViewModel, string>(null,
                                x => x.Title,
                                options: new TextColumnOptions<PdfDocumentLayerViewModel>()
                                {
                                    CanUserSortColumn = false,
                                    IsTextSearchEnabled = false,
                                    TextWrapping = TextWrapping.WrapWithOverflow,
                                    TextAlignment = TextAlignment.Left,
                                    MaxWidth = new GridLength(400)
                                }), x => x.Nodes)
                    }
                };

                Dispatcher.UIThread.Post(() =>
                {
                    LayersSource.RowSelection!.SingleSelect = true;
                    //LayersSource.RowSelection.SelectionChanged += BookmarksSelectionChanged;
                    LayersSource.ExpandAll();
                });
            }
        }
    }
}
