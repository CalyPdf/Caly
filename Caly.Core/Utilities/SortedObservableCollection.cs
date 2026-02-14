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
using System.Collections.ObjectModel;
using System.Threading;

namespace Caly.Core.Utilities;

public sealed class SortedObservableCollection<T> : ObservableCollection<T>
{
    private readonly Lock _lock = new();
    private readonly List<int> _orders = new();
    private readonly Func<T, int> _getOrderFunc;

    public SortedObservableCollection(Func<T, int> getOrderFunc)
    {
        _getOrderFunc = getOrderFunc ?? throw new ArgumentNullException(nameof(getOrderFunc));
    }

    public void AddSorted(T item)
    {
        int order = _getOrderFunc(item);
        lock (_lock)
        {
            int index = _orders.BinarySearch(order);
            if (index < -1)
            {
                index = ~index;
            }
            else
            {
                index++;
            }

            _orders.Insert(index, order);
            InsertItem(index, item);
        }
    }

    protected override void ClearItems()
    {
        lock (_lock)
        {
            _orders.Clear();
            base.ClearItems();
        }
    }

    protected override void RemoveItem(int index)
    {
        lock (_lock)
        {
            var item = this[index];
            _orders.Remove(_getOrderFunc(item));
            base.RemoveItem(index);
        }
    }

    protected override void SetItem(int index, T item)
    {
        throw new InvalidOperationException("Cannot set item in SortedObservableCollection.");
    }

    protected override void MoveItem(int oldIndex, int newIndex)
    {
        throw new InvalidOperationException("Cannot move item in SortedObservableCollection.");
    }
}
