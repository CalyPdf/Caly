namespace Caly.Pdf.Layout
{
    /*
     * From PdfPig
     */

    using UglyToad.PdfPig.Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Contains helpful tools for distance measures.
    /// </summary>
    public static class CalyDistances
    {
        /// <summary>
        /// The Euclidean distance is the "ordinary" straight-line distance between two points.
        /// </summary>
        /// <param name="point1">The first point.</param>
        /// <param name="point2">The second point.</param>
        public static float Euclidean(PdfPoint point1, PdfPoint point2)
        {
            float dx = (float)(point1.X - point2.X);
            float dy = (float)(point1.Y - point2.Y);
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// The weighted Euclidean distance.
        /// </summary>
        /// <param name="point1">The first point.</param>
        /// <param name="point2">The second point.</param>
        /// <param name="wX">The weight of the X coordinates. Default is 1.</param>
        /// <param name="wY">The weight of the Y coordinates. Default is 1.</param>
        public static float WeightedEuclidean(PdfPoint point1, PdfPoint point2, float wX = 1.0f, float wY = 1.0f)
        {
            float dx = (float)(point1.X - point2.X);
            float dy = (float)(point1.Y - point2.Y);
            return MathF.Sqrt(wX * dx * dx + wY * dy * dy);
        }

        /// <summary>
        /// The Manhattan distance between two points is the sum of the absolute differences of their Cartesian coordinates.
        /// <para>Also known as rectilinear distance, L1 distance, L1 norm, snake distance, city block distance, taxicab metric.</para>
        /// </summary>
        /// <param name="point1">The first point.</param>
        /// <param name="point2">The second point.</param>
        public static float Manhattan(PdfPoint point1, PdfPoint point2)
        {
            return MathF.Abs((float)(point1.X - point2.X)) + MathF.Abs((float)(point1.Y - point2.Y));
        }

        /// <summary>
        /// The angle in degrees between the horizontal axis and the line between two points.
        /// <para>-180 ≤ θ ≤ 180</para>
        /// </summary>
        /// <param name="startPoint">The first point.</param>
        /// <param name="endPoint">The second point.</param>
        public static float Angle(in PdfPoint startPoint, in PdfPoint endPoint)
        {
            return MathF.Atan2((float)(endPoint.Y - startPoint.Y), (float)(endPoint.X - startPoint.X)) * 180 / MathF.PI;
        }

        /// <summary>
        /// The absolute distance between the Y coordinates of two points.
        /// </summary>
        /// <param name="point1">The first point.</param>
        /// <param name="point2">The second point.</param>
        public static float Vertical(PdfPoint point1, PdfPoint point2)
        {
            return MathF.Abs((float)(point2.Y - point1.Y));
        }

        /// <summary>
        /// The absolute distance between the X coordinates of two points.
        /// </summary>
        /// <param name="point1">The first point.</param>
        /// <param name="point2">The second point.</param>
        public static float Horizontal(PdfPoint point1, PdfPoint point2)
        {
            return MathF.Abs((float)(point2.X - point1.X));
        }

        /// <summary>
        /// Bound angle so that -180 ≤ θ ≤ 180.
        /// </summary>
        /// <param name="angle">The angle to bound.</param>
        public static float BoundAngle180(float angle)
        {
            angle = (angle + 180) % 360;
            if (angle < 0) angle += 360;
            return angle - 180;
        }

        /// <summary>
        /// Bound angle so that 0 ≤ θ ≤ 360.
        /// </summary>
        /// <param name="angle">The angle to bound.</param>
        public static float BoundAngle0to360(float angle)
        {
            angle %= 360;
            if (angle < 0) angle += 360;
            return angle;
        }

        /// <summary>
        /// Get the minimum edit distance between two strings.
        /// </summary>
        /// <param name="string1">The first string.</param>
        /// <param name="string2">The second string.</param>
        public static int MinimumEditDistance(string string1, string string2)
        {
            ushort[,] d = new ushort[string1.Length + 1, string2.Length + 1];

            for (int i = 1; i <= string1.Length; i++)
            {
                d[i, 0] = (ushort)i;
            }

            for (int j = 1; j <= string2.Length; j++)
            {
                d[0, j] = (ushort)j;
            }

            for (int j = 1; j <= string2.Length; j++)
            {
                for (int i = 1; i <= string1.Length; i++)
                {
                    d[i, j] = Math.Min(Math.Min(
                        (ushort)(d[i - 1, j] + 1),
                        (ushort)(d[i, j - 1] + 1)),
                        (ushort)(d[i - 1, j - 1] + (string1[i - 1] == string2[j - 1] ? 0 : 1))); // substitution, set cost to 1
                }
            }
            return d[string1.Length, string2.Length];
        }

        /// <summary>
        /// Get the minimum edit distance between two strings.
        /// <para>Returned values are between 0 and 1 included. A value of 0 means that the two strings are indentical.</para>
        /// </summary>
        /// <param name="string1">The first string.</param>
        /// <param name="string2">The second string.</param>
        public static float MinimumEditDistanceNormalised(string string1, string string2)
        {
            return MinimumEditDistance(string1, string2) / (float)Math.Max(string1.Length, string2.Length);
        }

        /// <summary>
        /// Find the index of the nearest point, excluding itself.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="element">The reference point, for which to find the nearest neighbour.</param>
        /// <param name="candidates">The list of neighbours candidates.</param>
        /// <param name="pivotPoint"></param>
        /// <param name="candidatePoint"></param>
        /// <param name="distanceMeasure">The distance measure to use.</param>
        /// <param name="distance">The distance between the reference element and its nearest neighbour.</param>
        public static int FindIndexNearest<T>(T element, IReadOnlyList<T> candidates,
            Func<T, PdfPoint> pivotPoint, Func<T, PdfPoint> candidatePoint,
            Func<PdfPoint, PdfPoint, float> distanceMeasure, out float distance)
        {
            if (candidates == null || candidates.Count == 0)
            {
                throw new ArgumentException("CalyDistances.FindIndexNearest(): The list of neighbours candidates is either null or empty.", nameof(candidates));
            }

            if (distanceMeasure == null)
            {
                throw new ArgumentException("CalyDistances.FindIndexNearest(): The distance measure must not be null.", nameof(distanceMeasure));
            }

            distance = float.MaxValue;
            int closestPointIndex = -1;
            var pivot = pivotPoint(element);

            for (var i = 0; i < candidates.Count; i++)
            {
                float currentDistance = distanceMeasure(pivot, candidatePoint(candidates[i]));
                if (currentDistance < distance && !candidates[i].Equals(element))
                {
                    distance = currentDistance;
                    closestPointIndex = i;
                }
            }

            return closestPointIndex;
        }

        /// <summary>
        /// Find the index of the nearest line, excluding itself.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="element">The reference line, for which to find the nearest neighbour.</param>
        /// <param name="candidates">The list of neighbours candidates.</param>
        /// <param name="pivotLine"></param>
        /// <param name="candidateLine"></param>
        /// <param name="distanceMeasure">The distance measure between two lines to use.</param>
        /// <param name="distance">The distance between the reference element and its nearest neighbour.</param>
        public static int FindIndexNearest<T>(T element, IReadOnlyList<T> candidates,
            Func<T, PdfLine> pivotLine, Func<T, PdfLine> candidateLine,
            Func<PdfLine, PdfLine, float> distanceMeasure, out float distance)
        {
            if (candidates == null || candidates.Count == 0)
            {
                throw new ArgumentException("CalyDistances.FindIndexNearest(): The list of neighbours candidates is either null or empty.", nameof(candidates));
            }

            if (distanceMeasure == null)
            {
                throw new ArgumentException("CalyDistances.FindIndexNearest(): The distance measure must not be null.", nameof(distanceMeasure));
            }

            distance = float.MaxValue;
            int closestLineIndex = -1;
            var pivot = pivotLine(element);

            for (var i = 0; i < candidates.Count; i++)
            {
                float currentDistance = distanceMeasure(pivot, candidateLine(candidates[i]));
                if (currentDistance < distance && !candidates[i].Equals(element))
                {
                    distance = currentDistance;
                    closestLineIndex = i;
                }
            }

            return closestLineIndex;
        }
    }
}
