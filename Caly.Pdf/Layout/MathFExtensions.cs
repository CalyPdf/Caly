namespace Caly.Pdf.Layout
{
    /*
     * From PdfPig - adapted
     */

    /// <summary>
    /// Useful math extensions.
    /// </summary>
    public static class MathFExtensions
    {
        /// <summary>
        /// Computes the mode of a sequence of <see cref="float"/> values.
        /// </summary>
        /// <param name="array">The sequence of floats.</param>
        /// <returns>The mode of the sequence. Returns <see cref="float.NaN"/> if the sequence has no mode or if it is not unique.</returns>
        public static float Mode(this IEnumerable<float> array)
        {
            if (array?.Any() != true) return float.NaN;
            var sorted = array.GroupBy(v => v).Select(v => (v.Count(), v.Key)).OrderByDescending(g => g.Item1);
            var mode = sorted.First();
            if (sorted.Count() > 1 && mode.Item1 == sorted.ElementAt(1).Item1) return float.NaN;
            return mode.Key;
        }
        
        /// <summary>
        /// Test for almost equality to 0.
        /// </summary>
        /// <param name="number"></param>
        /// <param name="epsilon"></param>
        public static bool AlmostEqualsToZero(this float number, float epsilon = 1e-5f)
        {
            return (number > -epsilon) && (number < epsilon);
        }

        /// <summary>
        /// Test for almost equality.
        /// </summary>
        /// <param name="number"></param>
        /// <param name="other"></param>
        /// <param name="epsilon"></param>
        public static bool AlmostEquals(this float number, float other, float epsilon = 1e-5f)
        {
            return AlmostEqualsToZero(number - other, epsilon);
        }
    }
}
