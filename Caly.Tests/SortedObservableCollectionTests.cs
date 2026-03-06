using Caly.Core.Utilities;

namespace Caly.Tests
{
    public class SortedObservableCollectionTests
    {
        public class ItemToSort
        {
            public int Number { get; init; }

            public int Check { get; init; }
        }

        [Fact]
        public void Add()
        {
            var collection = new SortedObservableCollection<ItemToSort>(item => item.Number);

            for (int i = 0; i < 10; ++i)
            {
                collection.AddSorted(new ItemToSort { Number = Random.Shared.Next(), Check = i });
            }

            for (int i = 1; i < collection.Count; ++i)
            {
                Assert.True(collection[i - 1].Number <= collection[i].Number);
            }
        }

        [Fact]
        public void AddDup()
        {
            var collection = new SortedObservableCollection<ItemToSort>(item => item.Number);

            int i = 0;
            collection.AddSorted(new ItemToSort { Number = -1, Check = i++ });
            collection.AddSorted(new ItemToSort { Number = 0, Check = i++ });
            collection.AddSorted(new ItemToSort { Number = 0, Check = i++ });
            collection.AddSorted(new ItemToSort { Number = 0, Check = i++ });
            collection.AddSorted(new ItemToSort { Number = 5, Check = i++ });

            for (i = 1; i < collection.Count; ++i)
            {
                Assert.True(collection[i - 1].Number <= collection[i].Number);
            }
        }

        [Fact]
        public void AddClearAdd()
        {
            var collection = new SortedObservableCollection<ItemToSort>(item => item.Number);

            for (int i = 0; i < 10; ++i)
            {
                collection.AddSorted(new ItemToSort { Number = Random.Shared.Next(), Check = i });
            }

            for (int i = 1; i < collection.Count; ++i)
            {
                Assert.True(collection[i - 1].Number <= collection[i].Number);
            }

            collection.Clear();

            for (int i = 0; i < 10; ++i)
            {
                collection.AddSorted(new ItemToSort { Number = Random.Shared.Next(), Check = i });
            }

            for (int i = 1; i < collection.Count; ++i)
            {
                Assert.True(collection[i - 1].Number <= collection[i].Number);
            }
        }

        [Fact]
        public void Constructor_NullGetOrderFunc_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SortedObservableCollection<ItemToSort>(null!));
        }

        [Fact]
        public void AddSorted_SingleItem_IsAtIndex0()
        {
            var collection = new SortedObservableCollection<ItemToSort>(item => item.Number);
            var item = new ItemToSort { Number = 42, Check = 1 };

            collection.AddSorted(item);

            Assert.Single(collection);
            Assert.Same(item, collection[0]);
        }

        [Fact]
        public void AddSorted_NegativeKeys_SortsCorrectly()
        {
            var collection = new SortedObservableCollection<ItemToSort>(item => item.Number);

            collection.AddSorted(new ItemToSort { Number = 5 });
            collection.AddSorted(new ItemToSort { Number = -10 });
            collection.AddSorted(new ItemToSort { Number = -3 });
            collection.AddSorted(new ItemToSort { Number = 0 });

            for (int i = 1; i < collection.Count; ++i)
            {
                Assert.True(collection[i - 1].Number <= collection[i].Number);
            }

            Assert.Equal(-10, collection[0].Number);
            Assert.Equal(5, collection[^1].Number);
        }

        [Fact]
        public void RemoveItem_MaintainsOrder()
        {
            var collection = new SortedObservableCollection<ItemToSort>(item => item.Number);

            var item1 = new ItemToSort { Number = 1 };
            var item2 = new ItemToSort { Number = 2 };
            var item3 = new ItemToSort { Number = 3 };

            collection.AddSorted(item3);
            collection.AddSorted(item1);
            collection.AddSorted(item2);

            collection.Remove(item2);

            Assert.Equal(2, collection.Count);
            Assert.Equal(1, collection[0].Number);
            Assert.Equal(3, collection[1].Number);
        }

        [Fact]
        public void RemoveItem_AfterClear_CollectionIsEmpty()
        {
            var collection = new SortedObservableCollection<ItemToSort>(item => item.Number);

            collection.AddSorted(new ItemToSort { Number = 1 });
            collection.AddSorted(new ItemToSort { Number = 2 });
            collection.Clear();

            Assert.Empty(collection);
        }

        [Fact]
        public void SetItem_ThrowsInvalidOperationException()
        {
            var collection = new SortedObservableCollection<ItemToSort>(item => item.Number);
            collection.AddSorted(new ItemToSort { Number = 1 });

            Assert.Throws<InvalidOperationException>(() =>
                collection[0] = new ItemToSort { Number = 99 });
        }

        [Fact]
        public void MoveItem_ThrowsInvalidOperationException()
        {
            var collection = new SortedObservableCollection<ItemToSort>(item => item.Number);
            collection.AddSorted(new ItemToSort { Number = 1 });
            collection.AddSorted(new ItemToSort { Number = 2 });

            Assert.Throws<InvalidOperationException>(() =>
                collection.Move(0, 1));
        }

        [Fact]
        public void AddSorted_ManyItems_AlwaysSorted()
        {
            var collection = new SortedObservableCollection<ItemToSort>(item => item.Number);
            var numbers = new[] { 50, 10, 80, 30, 20, 70, 40, 60, 90, 5 };

            foreach (var n in numbers)
            {
                collection.AddSorted(new ItemToSort { Number = n });
            }

            Assert.Equal(numbers.Length, collection.Count);
            for (int i = 1; i < collection.Count; ++i)
            {
                Assert.True(collection[i - 1].Number <= collection[i].Number);
            }
        }
    }
}
