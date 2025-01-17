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
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
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

        /*
        private readonly FuncDataTemplate<PdfDocumentLayerViewModel> CheckBoxColumnTemplate = new FuncDataTemplate<PdfDocumentLayerViewModel>((value, namescope) =>
            new StackPanel()
            {
                Margin = new Thickness(),
                VerticalAlignment = VerticalAlignment.Center,
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new CheckBox()
                    {
                        MinHeight = 10,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        RenderTransform = new ScaleTransform(0.7, 0.7),
                        [!CheckBox.IsCheckedProperty] = new Binding("IsVisible")
                    },
                    new TextBlock
                    {

                        [!TextBlock.TextProperty] = new Binding("Title"),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Left
                    }
                }
            });
        */

        private async Task LoadLayers()
        {
            _cts.Token.ThrowIfCancellationRequested();
            await Task.Run(() => _pdfService.SetDocumentLayersAsync(this, _cts.Token));

            if (Layers?.Count > 0)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var checkBoxColumnTemplate = new FuncDataTemplate<PdfDocumentLayerViewModel>((_, _) =>
                    {
                        // We need to use the GetObservable() approach to bind to make it work in AOT - thx Steve Monaco

                        // CheckBox
                        var checkBox = new CheckBox()
                        {
                            Margin = new Thickness(),
                            Padding = new Thickness(),
                            MinHeight = 10,
                            Height = 26,
                            Width = 26,
                            VerticalAlignment = VerticalAlignment.Center,
                            RenderTransform = new ScaleTransform(0.7, 0.7),
                        };
                        var isVisibleProperty = checkBox.GetObservable(CheckBox.DataContextProperty)
                            .OfType<PdfDocumentLayerViewModel>()
                            .Select(x => (bool?)x?.IsVisible);
                        checkBox.Bind(CheckBox.IsCheckedProperty, isVisibleProperty);

                        // TextBlock
                        var textBlock = new TextBlock()
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            TextAlignment = TextAlignment.Left
                        };
                        var titleProperty = textBlock.GetObservable(TextBlock.DataContextProperty)
                            .OfType<PdfDocumentLayerViewModel>()
                            .Select(x => x?.Title);
                        textBlock.Bind(TextBlock.TextProperty, titleProperty);

                        // StackPanel
                        return new StackPanel()
                        {
                            Margin = new Thickness(),
                            VerticalAlignment = VerticalAlignment.Center,
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                checkBox,
                                textBlock
                            }
                        };
                    });

                    LayersSource = new HierarchicalTreeDataGridSource<PdfDocumentLayerViewModel>(Layers)
                    {
                        Columns =
                        {
                            new HierarchicalExpanderColumn<PdfDocumentLayerViewModel>(
                                new TemplateColumn<PdfDocumentLayerViewModel>(null, checkBoxColumnTemplate,
                                    options: new TemplateColumnOptions<PdfDocumentLayerViewModel>()
                                    {
                                        CanUserSortColumn = false, IsTextSearchEnabled = false,
                                    }),
                                x => x.Nodes)
                        }
                    };

                    LayersSource.RowSelection!.SingleSelect = true;
                    //LayersSource.RowSelection.SelectionChanged += BookmarksSelectionChanged;
                    LayersSource.ExpandAll();
                });
            }
        }
    }
}
