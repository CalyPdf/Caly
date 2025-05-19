namespace Caly.Pdf.Layout
{
    /*
     * From PdfPig
     */
    
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UglyToad.PdfPig.Core;

    // for kd-tree with line segments, see https://stackoverflow.com/questions/14376679/how-to-represent-line-segments-in-kd-tree 

    /// <summary>
    /// K-D tree data structure of <see cref="PdfPoint"/>.
    /// </summary>
    public sealed class CalyKdTree : CalyKdTree<PdfPoint>
    {
        /// <summary>
        /// K-D tree data structure of <see cref="PdfPoint"/>.
        /// </summary>
        /// <param name="points">The points used to build the tree.</param>
        public CalyKdTree(IReadOnlyList<PdfPoint> points) : base(points, p => p)
        { }

        /// <summary>
        /// Get the nearest neighbour to the pivot point.
        /// Only returns 1 neighbour, even if equidistant points are found.
        /// </summary>
        /// <param name="pivot">The point for which to find the nearest neighbour.</param>
        /// <param name="distanceMeasure">The distance measure used, e.g. the Euclidian distance.</param>
        /// <param name="index">The nearest neighbour's index (returns -1 if not found).</param>
        /// <param name="distance">The distance between the pivot and the nearest neighbour (returns <see cref="float.NaN"/> if not found).</param>
        /// <returns>The nearest neighbour's point.</returns>
        public PdfPoint FindNearestNeighbour(PdfPoint pivot, Func<PdfPoint, PdfPoint, float> distanceMeasure, out int index, out float distance)
        {
            return FindNearestNeighbour(pivot, p => p, distanceMeasure, out index, out distance);
        }

        /// <summary>
        /// Get the k nearest neighbours to the pivot point.
        /// Might return more than k neighbours if points are equidistant.
        /// <para>Use <see cref="FindNearestNeighbour(PdfPoint, Func{PdfPoint, PdfPoint, float}, out int, out float)"/> if only looking for the (single) closest point.</para>
        /// </summary>
        /// <param name="pivot">The point for which to find the nearest neighbour.</param>
        /// <param name="k">The number of neighbours to return. Might return more than k neighbours if points are equidistant.</param>
        /// <param name="distanceMeasure">The distance measure used, e.g. the Euclidian distance.</param>
        /// <returns>Returns a list of tuples of the k nearest neighbours. Tuples are (element, index, distance).</returns>
        public IReadOnlyList<(PdfPoint, int, float)> FindNearestNeighbours(PdfPoint pivot, int k, Func<PdfPoint, PdfPoint, float> distanceMeasure)
        {
            return FindNearestNeighbours(pivot, k, p => p, distanceMeasure);
        }
    }

    /// <summary>
    /// K-D tree data structure.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CalyKdTree<T>
    {
        private readonly KdTreeComparerY kdTreeComparerY = new KdTreeComparerY();
        private readonly KdTreeComparerX kdTreeComparerX = new KdTreeComparerX();

        /// <summary>
        /// The root of the tree.
        /// </summary>
        public readonly CalyKdTreeNode<T> Root;

        /// <summary>
        /// Number of elements in the tree.
        /// </summary>
        public readonly int Count;

        /// <summary>
        /// K-D tree data structure.
        /// </summary>
        /// <param name="elements">The elements used to build the tree.</param>
        /// <param name="elementsPointFunc">The function that converts the candidate elements into a <see cref="PdfPoint"/>.</param>
        public CalyKdTree(IReadOnlyList<T> elements, Func<T, PdfPoint> elementsPointFunc)
        {
            if (elements == null || elements.Count == 0)
            {
                throw new ArgumentException("CalyKdTree(): candidates cannot be null or empty.", nameof(elements));
            }

            Count = elements.Count;

            KdTreeElement<T>[] array = new KdTreeElement<T>[Count];

            for (int i = 0; i < Count; i++)
            {
                var el = elements[i];
                array[i] = new KdTreeElement<T>(i, elementsPointFunc(el), el);
            }

#if NET6_0_OR_GREATER
            Root = BuildTree(new Span<KdTreeElement<T>>(array));
#else
            Root = BuildTree(new ArraySegment<KdTreeElement<T>>(array));
#endif
        }

#if NET6_0_OR_GREATER
        private CalyKdTreeNode<T> BuildTree(Span<KdTreeElement<T>> P, int depth = 0)
        {
            if (P.Length == 0)
            {
                return null;
            }

            if (P.Length == 1)
            {
                return new CalyKdTreeLeaf<T>(P[0], depth);
            }

            if (depth % 2 == 0)
            {
                P.Sort(kdTreeComparerX);
            }
            else
            {
                P.Sort(kdTreeComparerY);
            }

            if (P.Length == 2)
            {
                return new CalyKdTreeNode<T>(new CalyKdTreeLeaf<T>(P[0], depth + 1), null, P[1], depth);
            }

            int median = P.Length / 2;

            CalyKdTreeNode<T> vLeft = BuildTree(P.Slice(0, median), depth + 1);
            CalyKdTreeNode<T> vRight = BuildTree(P.Slice(median + 1), depth + 1);

            return new CalyKdTreeNode<T>(vLeft, vRight, P[median], depth);
        }
#else
        private CalyKdTreeNode<T> BuildTree(ArraySegment<KdTreeElement<T>> P, int depth = 0)
        {
            if (P.Count == 0)
            {
                return null;
            }

            if (P.Count == 1)
            {
                return new CalyKdTreeLeaf<T>(P.GetAt(0), depth);
            }

            if (depth % 2 == 0)
            {
                P.Sort(kdTreeComparerX);
            }
            else
            {
                P.Sort(kdTreeComparerY);
            }

            if (P.Count == 2)
            {
                return new CalyKdTreeNode<T>(new CalyKdTreeLeaf<T>(P.GetAt(0), depth + 1), null, P.GetAt(1), depth);
            }

            int median = P.Count / 2;

            CalyKdTreeNode<T> vLeft = BuildTree(P.Take(median), depth + 1);
            CalyKdTreeNode<T> vRight = BuildTree(P.Skip(median + 1), depth + 1);

            return new CalyKdTreeNode<T>(vLeft, vRight, P.GetAt(median), depth);
        }
#endif

        #region NN
        /// <summary>
        /// Get the nearest neighbour to the pivot element.
        /// Only returns 1 neighbour, even if equidistant points are found.
        /// </summary>
        /// <param name="pivot">The element for which to find the nearest neighbour.</param>
        /// <param name="pivotPointFunc">The function that converts the pivot element into a <see cref="PdfPoint"/>.</param>
        /// <param name="distanceMeasure">The distance measure used, e.g. the Euclidian distance.</param>
        /// <param name="index">The nearest neighbour's index (returns -1 if not found).</param>
        /// <param name="distance">The distance between the pivot and the nearest neighbour (returns <see cref="float.NaN"/> if not found).</param>
        /// <returns>The nearest neighbour's element.</returns>
        public T FindNearestNeighbour(T pivot, Func<T, PdfPoint> pivotPointFunc, Func<PdfPoint, PdfPoint, float> distanceMeasure, out int index, out float distance)
        {
            var result = FindNearestNeighbour(Root, pivot, pivotPointFunc, distanceMeasure);
            index = result.Item1 != null ? result.Item1.Index : -1;
            distance = result.Item2 ?? float.NaN;
            return result.Item1 != null ? result.Item1.Element : default;
        }

        private static (CalyKdTreeNode<T>, float?) FindNearestNeighbour(CalyKdTreeNode<T> node, T pivot, Func<T, PdfPoint> pivotPointFunc, Func<PdfPoint, PdfPoint, float> distance)
        {
            if (node == null)
            {
                return (null, null);
            }
            else if (node.IsLeaf)
            {
                if (node.Element.Equals(pivot))
                {
                    return (null, null);
                }
                return (node, distance(node.Value, pivotPointFunc(pivot)));
            }
            else
            {
                var point = pivotPointFunc(pivot);
                var currentNearestNode = node;
                var currentDistance = distance(node.Value, point);

                CalyKdTreeNode<T> newNode = null;
                float? newDist = null;

                var pointValue = node.IsAxisCutX ? point.X : point.Y;

                if (pointValue < node.L)
                {
                    // start left
                    (newNode, newDist) = FindNearestNeighbour(node.LeftChild, pivot, pivotPointFunc, distance);

                    if (newDist.HasValue && newDist <= currentDistance && !newNode.Element.Equals(pivot))
                    {
                        currentDistance = newDist.Value;
                        currentNearestNode = newNode;
                    }

                    if (node.RightChild != null && pointValue + currentDistance >= node.L)
                    {
                        (newNode, newDist) = FindNearestNeighbour(node.RightChild, pivot, pivotPointFunc, distance);
                    }
                }
                else
                {
                    // start right
                    (newNode, newDist) = FindNearestNeighbour(node.RightChild, pivot, pivotPointFunc, distance);

                    if (newDist.HasValue && newDist <= currentDistance && !newNode.Element.Equals(pivot))
                    {
                        currentDistance = newDist.Value;
                        currentNearestNode = newNode;
                    }

                    if (node.LeftChild != null && pointValue - currentDistance <= node.L)
                    {
                        (newNode, newDist) = FindNearestNeighbour(node.LeftChild, pivot, pivotPointFunc, distance);
                    }
                }

                if (newDist.HasValue && newDist <= currentDistance && !newNode.Element.Equals(pivot))
                {
                    currentDistance = newDist.Value;
                    currentNearestNode = newNode;
                }

                return (currentNearestNode, currentDistance);
            }
        }
        #endregion

        #region k-NN
        /// <summary>
        /// Get the k nearest neighbours to the pivot element.
        /// Might return more than k neighbours if points are equidistant.
        /// <para>Use <see cref="FindNearestNeighbour(CalyKdTreeNode{Q}, T, Func{T, PdfPoint}, Func{PdfPoint, PdfPoint, float})"/> if only looking for the (single) closest point.</para>
        /// </summary>
        /// <param name="pivot">The element for which to find the k nearest neighbours.</param>
        /// <param name="k">The number of neighbours to return. Might return more than k neighbours if points are equidistant.</param>
        /// <param name="pivotPointFunc">The function that converts the pivot element into a <see cref="PdfPoint"/>.</param>
        /// <param name="distanceMeasure">The distance measure used, e.g. the Euclidian distance.</param>
        /// <returns>Returns a list of tuples of the k nearest neighbours. Tuples are (element, index, distance).</returns>
        public IReadOnlyList<(T, int, float)> FindNearestNeighbours(T pivot, int k, Func<T, PdfPoint> pivotPointFunc, Func<PdfPoint, PdfPoint, float> distanceMeasure)
        {
            var kdTreeNodes = new KNearestNeighboursQueue(k);
            FindNearestNeighbours(Root, pivot, k, pivotPointFunc, distanceMeasure, kdTreeNodes);
            return kdTreeNodes.SelectMany(n => n.Value.Select(e => (e.Element, e.Index, n.Key))).ToArray();
        }

        private static (CalyKdTreeNode<T>, float) FindNearestNeighbours(CalyKdTreeNode<T> node, T pivot, int k,
            Func<T, PdfPoint> pivotPointFunc, Func<PdfPoint, PdfPoint, float> distance, KNearestNeighboursQueue queue)
        {
            if (node == null)
            {
                return (null, float.NaN);
            }
            else if (node.IsLeaf)
            {
                if (node.Element.Equals(pivot))
                {
                    return (null, float.NaN);
                }

                var currentDistance = distance(node.Value, pivotPointFunc(pivot));
                var currentNearestNode = node;

                if (!queue.IsFull || currentDistance <= queue.LastDistance)
                {
                    queue.Add(currentDistance, currentNearestNode);
                    currentDistance = queue.LastDistance;
                    currentNearestNode = queue.LastElement;
                }

                return (currentNearestNode, currentDistance);
            }
            else
            {
                var point = pivotPointFunc(pivot);
                var currentNearestNode = node;
                var currentDistance = distance(node.Value, point);
                if ((!queue.IsFull || currentDistance <= queue.LastDistance) && !node.Element.Equals(pivot))
                {
                    queue.Add(currentDistance, currentNearestNode);
                    currentDistance = queue.LastDistance;
                    currentNearestNode = queue.LastElement;
                }

                CalyKdTreeNode<T> newNode = null;
                float newDist = float.NaN;

                var pointValue = node.IsAxisCutX ? point.X : point.Y;

                if (pointValue < node.L)
                {
                    // start left
                    (newNode, newDist) = FindNearestNeighbours(node.LeftChild, pivot, k, pivotPointFunc, distance, queue);

                    if (!float.IsNaN(newDist) && newDist <= currentDistance && !newNode.Element.Equals(pivot))
                    {
                        queue.Add(newDist, newNode);
                        currentDistance = queue.LastDistance;
                        currentNearestNode = queue.LastElement;
                    }

                    if (node.RightChild != null && pointValue + currentDistance >= node.L)
                    {
                        (newNode, newDist) = FindNearestNeighbours(node.RightChild, pivot, k, pivotPointFunc, distance, queue);
                    }
                }
                else
                {
                    // start right
                    (newNode, newDist) = FindNearestNeighbours(node.RightChild, pivot, k, pivotPointFunc, distance, queue);

                    if (!float.IsNaN(newDist) && newDist <= currentDistance && !newNode.Element.Equals(pivot))
                    {
                        queue.Add(newDist, newNode);
                        currentDistance = queue.LastDistance;
                        currentNearestNode = queue.LastElement;
                    }

                    if (node.LeftChild != null && pointValue - currentDistance <= node.L)
                    {
                        (newNode, newDist) = FindNearestNeighbours(node.LeftChild, pivot, k, pivotPointFunc, distance, queue);
                    }
                }

                if (!float.IsNaN(newDist) && newDist <= currentDistance && !newNode.Element.Equals(pivot))
                {
                    queue.Add(newDist, newNode);
                    currentDistance = queue.LastDistance;
                    currentNearestNode = queue.LastElement;
                }

                return (currentNearestNode, currentDistance);
            }
        }

        private sealed class KNearestNeighboursQueue : SortedList<float, HashSet<CalyKdTreeNode<T>>>
        {
            public readonly int K;

            public CalyKdTreeNode<T> LastElement { get; private set; }

            public float LastDistance { get; private set; }

            public bool IsFull => Count >= K;

            public KNearestNeighboursQueue(int k) : base(k)
            {
                K = k;
                LastDistance = float.PositiveInfinity;
            }

            public void Add(float key, CalyKdTreeNode<T> value)
            {
                if (key > LastDistance && IsFull)
                {
                    return;
                }

                if (!ContainsKey(key))
                {
                    base.Add(key, new HashSet<CalyKdTreeNode<T>>());
                    if (Count > K)
                    {
                        RemoveAt(Count - 1);
                    }
                }

                if (this[key].Add(value))
                {
                    var last = this.Last();
                    LastElement = last.Value.Last();
                    LastDistance = last.Key;
                }
            }
        }
        #endregion

        internal readonly struct KdTreeElement<R>
        {
            internal KdTreeElement(int index, PdfPoint point, R value)
            {
                Index = index;
                Value = point;
                Element = value;
            }

            public int Index { get; }

            public PdfPoint Value { get; }

            public R Element { get; }
        }

        private sealed class KdTreeComparerY : IComparer<KdTreeElement<T>>
        {
            public int Compare(KdTreeElement<T> p0, KdTreeElement<T> p1)
            {
                return p0.Value.Y.CompareTo(p1.Value.Y);
            }
        }

        private sealed class KdTreeComparerX : IComparer<KdTreeElement<T>>
        {
            public int Compare(KdTreeElement<T> p0, KdTreeElement<T> p1)
            {
                return p0.Value.X.CompareTo(p1.Value.X);
            }
        }

        /// <summary>
        /// K-D tree leaf.
        /// </summary>
        /// <typeparam name="Q"></typeparam>
        public sealed class CalyKdTreeLeaf<Q> : CalyKdTreeNode<Q>
        {
            /// <summary>
            /// Return true if leaf.
            /// </summary>
            public override bool IsLeaf => true;

            internal CalyKdTreeLeaf(KdTreeElement<Q> point, int depth)
                : base(null, null, point, depth)
            { }

            /// <inheritdoc />
            public override string ToString()
            {
                return "Leaf->" + Value.ToString();
            }
        }

        /// <summary>
        /// K-D tree node.
        /// </summary>
        /// <typeparam name="Q"></typeparam>
        public class CalyKdTreeNode<Q>
        {
            /// <summary>
            /// Split value (X or Y axis).
            /// </summary>
            public float L => IsAxisCutX ? (float)Value.X : (float)Value.Y;

            /// <summary>
            /// Split point.
            /// </summary>
            public PdfPoint Value { get; }

            /// <summary>
            /// Left child.
            /// </summary>
            public CalyKdTreeNode<Q> LeftChild { get; internal set; }

            /// <summary>
            /// Right child.
            /// </summary>
            public CalyKdTreeNode<Q> RightChild { get; internal set; }

            /// <summary>
            /// The node's element.
            /// </summary>
            public Q Element { get; }

            /// <summary>
            /// True if this cuts with X axis, false if cuts with Y axis.
            /// </summary>
            public bool IsAxisCutX { get; }

            /// <summary>
            /// The element's depth in the tree.
            /// </summary>
            public int Depth { get; }

            /// <summary>
            /// Return true if leaf.
            /// </summary>
            public virtual bool IsLeaf => false;

            /// <summary>
            /// The index of the element in the original array.
            /// </summary>
            public int Index { get; }

            internal CalyKdTreeNode(CalyKdTreeNode<Q> leftChild, CalyKdTreeNode<Q> rightChild, KdTreeElement<Q> point, int depth)
            {
                LeftChild = leftChild;
                RightChild = rightChild;
                Value = point.Value;
                Element = point.Element;
                Depth = depth;
                IsAxisCutX = depth % 2 == 0;
                Index = point.Index;
            }

            /// <summary>
            /// Get the leaves.
            /// </summary>
            public IEnumerable<CalyKdTreeLeaf<Q>> GetLeaves()
            {
                var leaves = new List<CalyKdTreeLeaf<Q>>();
                RecursiveGetLeaves(LeftChild, ref leaves);
                RecursiveGetLeaves(RightChild, ref leaves);
                return leaves;
            }

            private void RecursiveGetLeaves(CalyKdTreeNode<Q> leaf, ref List<CalyKdTreeLeaf<Q>> leaves)
            {
                if (leaf == null)
                {
                    return;
                }

                if (leaf is CalyKdTreeLeaf<Q> lLeaf)
                {
                    leaves.Add(lLeaf);
                }
                else
                {
                    RecursiveGetLeaves(leaf.LeftChild, ref leaves);
                    RecursiveGetLeaves(leaf.RightChild, ref leaves);
                }
            }

            /// <inheritdoc />
            public override string ToString()
            {
                return "Node->" + Value.ToString();
            }
        }
    }
}
