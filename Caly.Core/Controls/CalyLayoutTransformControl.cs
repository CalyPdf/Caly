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
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Caly.Core.Controls
{
    // Temporary fix for https://github.com/AvaloniaUI/Avalonia/issues/17867

    internal sealed class CalyLayoutTransformControl : LayoutTransformControl, IScrollSnapPointsInfo
    {
        private ItemsPresenter? _itemsPresenter;
        private IScrollSnapPointsInfo? _scrollSnapPointsInfoImplementation;

        public override void ApplyTemplate()
        {
            base.ApplyTemplate();

            if (_itemsPresenter is null)
            {
                _itemsPresenter = this.FindDescendantOfType<ItemsPresenter>();
                if (_itemsPresenter is null)
                {
                    throw new NullReferenceException("this.FindDescendantOfType<ItemsPresenter>()");
                }
            }
        }

        public IReadOnlyList<double> GetIrregularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment)
        {
            if (_scrollSnapPointsInfoImplementation is null)
            {
                _scrollSnapPointsInfoImplementation = _itemsPresenter!.Panel as IScrollSnapPointsInfo;
                if (_scrollSnapPointsInfoImplementation is null)
                {
                    throw new NullReferenceException("_itemsPresenter!.Panel as IScrollSnapPointsInfo");
                }
            }
            return _scrollSnapPointsInfoImplementation.GetIrregularSnapPoints(orientation, snapPointsAlignment);
        }

        public double GetRegularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment, out double offset)
        {
            if (_scrollSnapPointsInfoImplementation is null)
            {
                _scrollSnapPointsInfoImplementation = _itemsPresenter!.Panel as IScrollSnapPointsInfo;
                if (_scrollSnapPointsInfoImplementation is null)
                {
                    throw new NullReferenceException("_itemsPresenter!.Panel as IScrollSnapPointsInfo");
                }
            }
            return _scrollSnapPointsInfoImplementation.GetRegularSnapPoints(orientation, snapPointsAlignment, out offset);
        }

        public bool AreHorizontalSnapPointsRegular
        {
            get
            {
                if (_scrollSnapPointsInfoImplementation is null)
                {
                    _scrollSnapPointsInfoImplementation = _itemsPresenter!.Panel as IScrollSnapPointsInfo;
                    if (_scrollSnapPointsInfoImplementation is null)
                    {
                        throw new NullReferenceException("_itemsPresenter!.Panel as IScrollSnapPointsInfo");
                    }
                }

                return _scrollSnapPointsInfoImplementation.AreHorizontalSnapPointsRegular;
            }
            set
            {
                if (_scrollSnapPointsInfoImplementation is null)
                {
                    _scrollSnapPointsInfoImplementation = _itemsPresenter!.Panel as IScrollSnapPointsInfo;
                    if (_scrollSnapPointsInfoImplementation is null)
                    {
                        throw new NullReferenceException("_itemsPresenter!.Panel as IScrollSnapPointsInfo");
                    }
                }
                _scrollSnapPointsInfoImplementation.AreHorizontalSnapPointsRegular = value;
            }
        }

        public bool AreVerticalSnapPointsRegular
        {
            get
            {
                if (_scrollSnapPointsInfoImplementation is null)
                {
                    _scrollSnapPointsInfoImplementation = _itemsPresenter!.Panel as IScrollSnapPointsInfo;
                    if (_scrollSnapPointsInfoImplementation is null)
                    {
                        throw new NullReferenceException("_itemsPresenter!.Panel as IScrollSnapPointsInfo");
                    }
                }

                return _scrollSnapPointsInfoImplementation.AreVerticalSnapPointsRegular;
            }
            set
            {
                if (_scrollSnapPointsInfoImplementation is null)
                {
                    _scrollSnapPointsInfoImplementation = _itemsPresenter!.Panel as IScrollSnapPointsInfo;
                    if (_scrollSnapPointsInfoImplementation is null)
                    {
                        throw new NullReferenceException("_itemsPresenter!.Panel as IScrollSnapPointsInfo");
                    }
                }

                _scrollSnapPointsInfoImplementation.AreVerticalSnapPointsRegular = value;
            }
        }

        public event EventHandler<RoutedEventArgs>? HorizontalSnapPointsChanged
        {
            add
            {
                if (_scrollSnapPointsInfoImplementation is null)
                {
                    _scrollSnapPointsInfoImplementation = _itemsPresenter!.Panel as IScrollSnapPointsInfo;
                    if (_scrollSnapPointsInfoImplementation is null)
                    {
                        throw new NullReferenceException("_itemsPresenter!.Panel as IScrollSnapPointsInfo");
                    }
                }
                _scrollSnapPointsInfoImplementation.HorizontalSnapPointsChanged += value;
            }
            remove => _scrollSnapPointsInfoImplementation.HorizontalSnapPointsChanged -= value;
        }

        public event EventHandler<RoutedEventArgs>? VerticalSnapPointsChanged
        {
            add
            {
                if (_scrollSnapPointsInfoImplementation is null)
                {
                    _scrollSnapPointsInfoImplementation = _itemsPresenter!.Panel as IScrollSnapPointsInfo;
                    if (_scrollSnapPointsInfoImplementation is null)
                    {
                        throw new NullReferenceException("_itemsPresenter!.Panel as IScrollSnapPointsInfo");
                    }
                }
                _scrollSnapPointsInfoImplementation.VerticalSnapPointsChanged += value;
            }
            remove => _scrollSnapPointsInfoImplementation.VerticalSnapPointsChanged -= value;
        }
    }
}
