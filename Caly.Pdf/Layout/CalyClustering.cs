﻿using UglyToad.PdfPig.Core;

namespace Caly.Pdf.Layout
{
    /*
     * From PdfPig
     */

    /// <summary>
    /// Clustering Algorithms.
    /// </summary>
    public static class CalyClustering
    {
        /// <summary>
        /// Algorithm to group elements using nearest neighbours.
        /// <para>Uses the nearest neighbour as candidate.</para>
        /// </summary>
        /// <typeparam name="T">Letter, Word, TextLine, etc.</typeparam>
        /// <param name="elements">Elements to group.</param>
        /// <param name="distMeasure">The distance measure between two points.</param>
        /// <param name="maxDistanceFunction">The function that determines the maximum distance between two points in the same cluster.</param>
        /// <param name="pivotPoint">The pivot's point to use for pairing, e.g. BottomLeft, TopLeft.</param>
        /// <param name="candidatesPoint">The candidates' point to use for pairing, e.g. BottomLeft, TopLeft.</param>
        /// <param name="filterPivot">Filter to apply to the pivot point. If false, point will not be paired at all, e.g. is white space.</param>
        /// <param name="filterFinal">Filter to apply to both the pivot and the paired point. If false, point will not be paired at all, e.g. pivot and paired point have same font.</param>
        /// <param name="parallelOptions">A <see cref="ParallelOptions">ParallelOptions</see>
        /// instance that configures the behavior of this operation.</param>
        public static IEnumerable<IReadOnlyList<T>> NearestNeighbours<T>(IReadOnlyList<T> elements,
            Func<PdfPoint, PdfPoint, float> distMeasure,
            Func<T, T, float> maxDistanceFunction,
            Func<T, PdfPoint> pivotPoint, Func<T, PdfPoint> candidatesPoint,
            Func<T, bool> filterPivot, Func<T, T, bool> filterFinal,
            ParallelOptions parallelOptions)
        {
            /*************************************************************************************
             * Algorithm steps
             * 1. Find nearest neighbours indexes (done in parallel)
             *  Iterate every point (pivot) and put its nearest neighbour's index in an array
             *  e.g. if nearest neighbour of point i is point j, then indexes[i] = j.
             *  Only conciders a neighbour if it is within the maximum distance. 
             *  If not within the maximum distance, index will be set to -1.
             *  Each element has only one connected neighbour.
             *  NB: Given the possible asymmetry in the relationship, it is possible 
             *  that if indexes[i] = j then indexes[j] != i.
             *  
             * 2. Group indexes
             *  Group indexes if share neighbours in common - Depth-first search
             *  e.g. if we have indexes[i] = j, indexes[j] = k, indexes[m] = n and indexes[n] = -1
             *  (i,j,k) will form a group and (m,n) will form another group.
             *************************************************************************************/

            int[] indexes = Enumerable.Repeat(-1, elements.Count).ToArray();
            CalyKdTree<T> calyKdTree = new CalyKdTree<T>(elements, candidatesPoint);

            // 1. Find nearest neighbours indexes
            Parallel.For(0, elements.Count, parallelOptions, e =>
            {
                var pivot = elements[e];

                if (filterPivot(pivot))
                {
                    var paired = calyKdTree.FindNearestNeighbour(pivot, pivotPoint, distMeasure, out int index, out float dist);

                    if (index != -1 && filterFinal(pivot, paired) && dist < maxDistanceFunction(pivot, paired))
                    {
                        indexes[e] = index;
                    }
                }
            });

            // 2. Group indexes
            foreach (var group in GroupIndexes(indexes, parallelOptions.CancellationToken))
            {
                yield return group.Select(i => elements[i]).ToArray();
            }
        }

        /// <summary>
        /// Algorithm to group elements using nearest neighbours.
        /// <para>Uses the k-nearest neighbours as candidates.</para>
        /// </summary>
        /// <typeparam name="T">Letter, Word, TextLine, etc.</typeparam>
        /// <param name="elements">Elements to group.</param>
        /// <param name="k">The k-nearest neighbours to consider as candidates.</param>
        /// <param name="distMeasure">The distance measure between two points.</param>
        /// <param name="maxDistanceFunction">The function that determines the maximum distance between two points in the same cluster.</param>
        /// <param name="pivotPoint">The pivot's point to use for pairing, e.g. BottomLeft, TopLeft.</param>
        /// <param name="candidatesPoint">The candidates' point to use for pairing, e.g. BottomLeft, TopLeft.</param>
        /// <param name="filterPivot">Filter to apply to the pivot point. If false, point will not be paired at all, e.g. is white space.</param>
        /// <param name="filterFinal">Filter to apply to both the pivot and the paired point. If false, point will not be paired at all, e.g. pivot and paired point have same font.</param>
        /// <param name="parallelOptions">A <see cref="ParallelOptions">ParallelOptions</see>
        /// instance that configures the behavior of this operation.</param>
        public static IEnumerable<IReadOnlyList<T>> NearestNeighbours<T>(IReadOnlyList<T> elements, int k,
            Func<PdfPoint, PdfPoint, float> distMeasure,
            Func<T, T, float> maxDistanceFunction,
            Func<T, PdfPoint> pivotPoint, Func<T, PdfPoint> candidatesPoint,
            Func<T, bool> filterPivot, Func<T, T, bool> filterFinal,
            ParallelOptions parallelOptions)
        {
            /*************************************************************************************
             * Algorithm steps
             * 1. Find nearest neighbours indexes (done in parallel)
             *  Iterate every point (pivot) and put its nearest neighbour's index in an array
             *  e.g. if nearest neighbour of point i is point j, then indexes[i] = j.
             *  Only conciders a neighbour if it is within the maximum distance. 
             *  If not within the maximum distance, index will be set to -1.
             *  Each element has only one connected neighbour.
             *  NB: Given the possible asymmetry in the relationship, it is possible 
             *  that if indexes[i] = j then indexes[j] != i.
             *  
             * 2. Group indexes
             *  Group indexes if share neighbours in common - Depth-first search
             *  e.g. if we have indexes[i] = j, indexes[j] = k, indexes[m] = n and indexes[n] = -1
             *  (i,j,k) will form a group and (m,n) will form another group.
             *************************************************************************************/

            int[] indexes = Enumerable.Repeat(-1, elements.Count).ToArray();
            CalyKdTree<T> calyKdTree = new CalyKdTree<T>(elements, candidatesPoint);

            // 1. Find nearest neighbours indexes
            Parallel.For(0, elements.Count, parallelOptions, e =>
            {
                var pivot = elements[e];

                if (filterPivot(pivot))
                {
                    foreach (var c in calyKdTree.FindNearestNeighbours(pivot, k, pivotPoint, distMeasure))
                    {
                        if (filterFinal(pivot, c.Item1) && c.Item3 < maxDistanceFunction(pivot, c.Item1))
                        {
                            indexes[e] = c.Item2;
                            break;
                        }
                    }
                }
            });

            // 2. Group indexes
            foreach (var group in GroupIndexes(indexes, parallelOptions.CancellationToken))
            {
                yield return group.Select(i => elements[i]).ToArray();
            }
        }

        /// <summary>
        /// Algorithm to group elements using nearest neighbours.
        /// </summary>
        /// <typeparam name="T">Letter, Word, TextLine, etc.</typeparam>
        /// <param name="elements">Array of elements to group.</param>
        /// <param name="distMeasure">The distance measure between two lines.</param>
        /// <param name="maxDistanceFunction">The function that determines the maximum distance between two points in the same cluster.</param>
        /// <param name="pivotLine">The pivot's line to use for pairing.</param>
        /// <param name="candidatesLine">The candidates' line to use for pairing.</param>
        /// <param name="filterPivot">Filter to apply to the pivot point. If false, point will not be paired at all, e.g. is white space.</param>
        /// <param name="filterFinal">Filter to apply to both the pivot and the paired point. If false, point will not be paired at all, e.g. pivot and paired point have same font.</param>
        /// <param name="parallelOptions">A <see cref="ParallelOptions">ParallelOptions</see>
        /// instance that configures the behavior of this operation.</param>
        public static IEnumerable<IReadOnlyList<T>> NearestNeighbours<T>(IReadOnlyList<T> elements,
            Func<PdfLine, PdfLine, float> distMeasure,
            Func<T, T, float> maxDistanceFunction,
            Func<T, PdfLine> pivotLine, Func<T, PdfLine> candidatesLine,
            Func<T, bool> filterPivot, Func<T, T, bool> filterFinal,
            ParallelOptions parallelOptions)
        {
            /*************************************************************************************
             * Algorithm steps
             * 1. Find nearest neighbours indexes (done in parallel)
             *  Iterate every point (pivot) and put its nearest neighbour's index in an array
             *  e.g. if nearest neighbour of point i is point j, then indexes[i] = j.
             *  Only conciders a neighbour if it is within the maximum distance. 
             *  If not within the maximum distance, index will be set to -1.
             *  Each element has only one connected neighbour.
             *  NB: Given the possible asymmetry in the relationship, it is possible 
             *  that if indexes[i] = j then indexes[j] != i.
             *  
             * 2. Group indexes
             *  Group indexes if share neighbours in common - Depth-first search
             *  e.g. if we have indexes[i] = j, indexes[j] = k, indexes[m] = n and indexes[n] = -1
             *  (i,j,k) will form a group and (m,n) will form another group.
             *************************************************************************************/

            int[] indexes = Enumerable.Repeat(-1, elements.Count).ToArray();

            // 1. Find nearest neighbours indexes
            Parallel.For(0, elements.Count, parallelOptions, e =>
            {
                var pivot = elements[e];

                if (filterPivot(pivot))
                {
                    int index = CalyDistances.FindIndexNearest(pivot, elements, pivotLine, candidatesLine, distMeasure, out float dist);

                    if (index != -1)
                    {
                        var paired = elements[index];
                        if (filterFinal(pivot, paired) && dist < maxDistanceFunction(pivot, paired))
                        {
                            indexes[e] = index;
                        }
                    }
                }
            });

            // 2. Group indexes
            foreach (var group in GroupIndexes(indexes, parallelOptions.CancellationToken))
            {
                yield return group.Select(i => elements[i]).ToArray();
            }
        }

        /// <summary>
        /// Group elements using Depth-first search.
        /// <para>https://en.wikipedia.org/wiki/Depth-first_search</para>
        /// </summary>
        /// <param name="edges">The graph. edges[i] = j indicates that there is an edge between i and j.</param>
        /// <returns>A List of HashSets containing the grouped indexes.</returns>
        internal static List<HashSet<int>> GroupIndexes(int[] edges, CancellationToken token)
        {
            int[][] adjacency = new int[edges.Length][];
            for (int i = 0; i < edges.Length; i++)
            {
                if (i % 100 == 0)
                {
                    token.ThrowIfCancellationRequested();
                }

                HashSet<int> matches = new HashSet<int>();
                if (edges[i] != -1) matches.Add(edges[i]);
                for (int j = 0; j < edges.Length; j++)
                {
                    if (edges[j] == i) matches.Add(j);
                }
                adjacency[i] = matches.ToArray();
            }

            List<HashSet<int>> groupedIndexes = new List<HashSet<int>>();
            bool[] isDone = new bool[edges.Length];

            for (int p = 0; p < edges.Length; p++)
            {
                if (isDone[p]) continue;
                groupedIndexes.Add(DfsIterative(p, adjacency, ref isDone));
            }
            return groupedIndexes;
        }

        /// <summary>
        /// Group elements using Depth-first search.
        /// <para>https://en.wikipedia.org/wiki/Depth-first_search</para>
        /// </summary>
        /// <param name="edges">The graph. edges[i] = [j, k, l, ...] indicates that there is an edge between i and each element j, k, l, ...</param>
        /// <returns>A List of HashSets containing the grouped indexes.</returns>
        internal static List<HashSet<int>> GroupIndexes(int[][] edges)
        {
            int[][] adjacency = new int[edges.Length][];
            for (int i = 0; i < edges.Length; i++)
            {
                HashSet<int> matches = new HashSet<int>();
                for (int j = 0; j < edges[i].Length; j++)
                {
                    if (edges[i][j] != -1) matches.Add(edges[i][j]);
                }

                for (int j = 0; j < edges.Length; j++)
                {
                    for (int k = 0; k < edges[j].Length; k++)
                    {
                        if (edges[j][k] == i) matches.Add(j);
                    }
                }
                adjacency[i] = matches.ToArray();
            }

            List<HashSet<int>> groupedIndexes = new List<HashSet<int>>();
            bool[] isDone = new bool[edges.Length];

            for (int p = 0; p < edges.Length; p++)
            {
                if (isDone[p]) continue;
                groupedIndexes.Add(DfsIterative(p, adjacency, ref isDone));
            }
            return groupedIndexes;
        }

        /// <summary>
        /// Depth-first search
        /// <para>https://en.wikipedia.org/wiki/Depth-first_search</para>
        /// </summary>
        private static HashSet<int> DfsIterative(int s, int[][] adj, ref bool[] isDone)
        {
            HashSet<int> group = new HashSet<int>();
            Stack<int> S = new Stack<int>();
            S.Push(s);

            while (S.Count > 0)
            {
                var u = S.Pop();
                if (!isDone[u])
                {
                    group.Add(u);
                    isDone[u] = true;
                    foreach (var v in adj[u])
                    {
                        S.Push(v);
                    }
                }
            }
            return group;
        }
    }
}
