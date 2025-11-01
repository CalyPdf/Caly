using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace Caly.Core.Utilities
{
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
}
