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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using UglyToad.PdfPig.Content;

namespace Caly.Core.ViewModels
{
    public sealed partial class PdfDocumentLayerViewModel : ViewModelBase
    {
        public required string Title { get; init; }

        [ObservableProperty] private bool _isVisible;

        public ObservableCollection<PdfDocumentLayerViewModel>? Nodes { get; internal set; }

        public static PdfDocumentLayerViewModel BuildRecursively(OptionalContentGroupElement ocge)
        {
            var root = new PdfDocumentLayerViewModel() { Title = ocge.Name, IsVisible = true };
            BuildChildren(ocge, root);
            return root;
        }

        private static void BuildChildren(OptionalContentGroupElement o, PdfDocumentLayerViewModel vm)
        {
            if (o.Nested is not null && o.Nested.Count > 0)
            {
                vm.Nodes = new ObservableCollection<PdfDocumentLayerViewModel>();

                foreach (var nested in o.Nested)
                {
                    var n = new PdfDocumentLayerViewModel()
                    {
                        Title = nested.Name,
                        IsVisible = true
                    };
                    BuildChildren(nested, n);
                    vm.Nodes.Add(n);
                }
            }
        }
    }
}
