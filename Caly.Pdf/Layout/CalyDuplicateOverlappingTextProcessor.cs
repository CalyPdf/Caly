using Caly.Pdf.Models;

namespace Caly.Pdf.Layout
{
    /*
     * From PdfPig - optimised
     */

    /// <summary>
    /// Checks if each letter is a duplicate and overlaps any other letter and remove the duplicate, and flag the remaining as bold.
    /// <para>Logic inspired from PdfBox's PDFTextStripper class.</para>
    /// </summary>
    public static class CalyDuplicateOverlappingTextProcessor
    {
        /// <summary>
        /// Checks if each letter is a duplicate and overlaps any other letter and remove the duplicate, and flag the remaining as bold.
        /// <para>Logic inspired from PdfBox's PDFTextStripper class.</para>
        /// </summary>
        /// <param name="letters">Letters to be processed.</param>
        /// <param name="token"/>
        /// <returns>Letters with no duplicate overlapping.</returns>
        public static IReadOnlyList<PdfLetter> Get(IReadOnlyList<PdfLetter> letters, CancellationToken token)
        {
            if (letters is null || letters.Count == 0)
            {
                return letters;
            }

            var cleanLetters = new List<PdfLetter>() { letters[0] };

            for (int i = 1; i < letters.Count; ++i)
            {
                if (i % 1000 == 0)
                {
                    token.ThrowIfCancellationRequested();
                }

                var letter = letters[i];
                double tolerance = letter.BoundingBox.Width / (letter.Value.Length == 0 ? 1 : letter.Value.Length) / 3.0;
                double minX = letter.BoundingBox.BottomLeft.X - tolerance;
                double maxX = letter.BoundingBox.BottomLeft.X + tolerance;
                double minY = letter.BoundingBox.BottomLeft.Y - tolerance;
                double maxY = letter.BoundingBox.BottomLeft.Y + tolerance;

                var duplicates = cleanLetters
                    .Where(l => minX <= l.BoundingBox.BottomLeft.X &&
                                maxX >= l.BoundingBox.BottomLeft.X &&
                                minY <= l.BoundingBox.BottomLeft.Y &&
                                maxY >= l.BoundingBox.BottomLeft.Y); // do other checks?

                var duplicatesOverlapping = duplicates.Any(l => l.Value.Equals(letter.Value));

                if (!duplicatesOverlapping)
                {
                    cleanLetters.Add(letter);
                }
            }

            return cleanLetters;
        }

        /// <summary>
        /// Checks if each letter is a duplicate and overlaps any other letter and remove the duplicate, and flag the remaining as bold.
        /// <para>Logic inspired from PdfBox's PDFTextStripper class.</para>
        /// </summary>
        /// <param name="letters">Letters to be processed.</param>
        /// <param name="token"/>
        /// <returns>Letters with no duplicate overlapping.</returns>
        public static IReadOnlyList<PdfLetter> GetInPlace(IReadOnlyList<PdfLetter> letters, CancellationToken token)
        {
            if (letters is null || letters.Count == 0)
            {
                return letters;
            }

            if (letters is not List<PdfLetter> cleanLetters)
            {
                cleanLetters = letters.ToList();
            }

            // Mark duplicates for removal instead of using RemoveAt (which is O(n) per call)
            bool[] remove = new bool[cleanLetters.Count];

            for (int i = 0; i < cleanLetters.Count; ++i)
            {
                if (remove[i])
                {
                    continue;
                }

                if (i % 1000 == 0)
                {
                    token.ThrowIfCancellationRequested();
                }

                var letter = cleanLetters[i];
                double tolerance = letter.BoundingBox.Width / (letter.Value.Length == 0 ? 1 : letter.Value.Length) / 3.0;
                double minX = letter.BoundingBox.BottomLeft.X - tolerance;
                double maxX = letter.BoundingBox.BottomLeft.X + tolerance;
                double minY = letter.BoundingBox.BottomLeft.Y - tolerance;
                double maxY = letter.BoundingBox.BottomLeft.Y + tolerance;

                for (int j = i + 1; j < cleanLetters.Count; ++j)
                {
                    if (remove[j])
                    {
                        continue;
                    }

                    var l = cleanLetters[j];
                    if (minX <= l.BoundingBox.BottomLeft.X &&
                        maxX >= l.BoundingBox.BottomLeft.X &&
                        minY <= l.BoundingBox.BottomLeft.Y &&
                        maxY >= l.BoundingBox.BottomLeft.Y &&
                        l.Value.Equals(letter.Value))
                    {
                        remove[i] = true;
                        break;
                    }
                }
            }

            // Single compaction pass — write position tracks where to place kept items
            int write = 0;
            for (int i = 0; i < cleanLetters.Count; i++)
            {
                if (!remove[i])
                {
                    cleanLetters[write++] = cleanLetters[i];
                }
            }
            cleanLetters.RemoveRange(write, cleanLetters.Count - write);

            return cleanLetters;
        }
    }
}
