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
    }
}
