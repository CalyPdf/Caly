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
