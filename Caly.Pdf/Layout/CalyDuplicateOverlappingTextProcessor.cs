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

            // Use a dictionary keyed by Value to look up candidate duplicates in O(1)
            var duplicateIndex = new Dictionary<string, List<int>>();
            var cleanLetters = new List<PdfLetter>();

            for (int i = 0; i < letters.Count; ++i)
            {
                if (i % 1000 == 0)
                {
                    token.ThrowIfCancellationRequested();
                }

                var letter = letters[i];
                bool addLetter = true;

                var key = letter.Value;
                if (duplicateIndex.TryGetValue(key, out var candidateIndices))
                {
                    double tolerance = letter.BoundingBox.Width / (key.Length == 0 ? 1 : key.Length) / 3.0;
                    double minX = letter.BoundingBox.BottomLeft.X - tolerance;
                    double maxX = letter.BoundingBox.BottomLeft.X + tolerance;
                    double minY = letter.BoundingBox.BottomLeft.Y - tolerance;
                    double maxY = letter.BoundingBox.BottomLeft.Y + tolerance;

                    for (int ci = 0; ci < candidateIndices.Count; ci++)
                    {
                        int idx = candidateIndices[ci];
                        var l = cleanLetters[idx];
                        if (minX <= l.BoundingBox.BottomLeft.X &&
                            maxX >= l.BoundingBox.BottomLeft.X &&
                            minY <= l.BoundingBox.BottomLeft.Y &&
                            maxY >= l.BoundingBox.BottomLeft.Y)
                        {
                            addLetter = false;
                            break;
                        }
                    }
                }

                if (addLetter)
                {
                    int newIndex = cleanLetters.Count;
                    cleanLetters.Add(letter);

                    if (!duplicateIndex.TryGetValue(key, out var list))
                    {
                        list = new List<int>();
                        duplicateIndex[key] = list;
                    }
                    list.Add(newIndex);
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

            // Use a dictionary keyed by Value to look up candidate duplicates in O(1).
            // When a later letter matches an earlier one, mark the earlier one for removal
            // (keeping the last occurrence, matching original semantics).
            var duplicateIndex = new Dictionary<string, List<int>>();
            var toRemove = new bool[cleanLetters.Count];
            int removeCount = 0;

            for (int i = 0; i < cleanLetters.Count; ++i)
            {
                if (i % 1000 == 0)
                {
                    token.ThrowIfCancellationRequested();
                }

                var letter = cleanLetters[i];
                var key = letter.Value;

                if (duplicateIndex.TryGetValue(key, out var candidateIndices))
                {
                    double tolerance = letter.BoundingBox.Width / (key.Length == 0 ? 1 : key.Length) / 3.0;
                    double minX = letter.BoundingBox.BottomLeft.X - tolerance;
                    double maxX = letter.BoundingBox.BottomLeft.X + tolerance;
                    double minY = letter.BoundingBox.BottomLeft.Y - tolerance;
                    double maxY = letter.BoundingBox.BottomLeft.Y + tolerance;

                    bool replaced = false;
                    for (int ci = 0; ci < candidateIndices.Count; ci++)
                    {
                        int idx = candidateIndices[ci];
                        var l = cleanLetters[idx];
                        if (minX <= l.BoundingBox.BottomLeft.X &&
                            maxX >= l.BoundingBox.BottomLeft.X &&
                            minY <= l.BoundingBox.BottomLeft.Y &&
                            maxY >= l.BoundingBox.BottomLeft.Y)
                        {
                            // Mark the earlier letter for removal, keep the later one
                            toRemove[idx] = true;
                            removeCount++;
                            candidateIndices[ci] = i;
                            replaced = true;
                            break;
                        }
                    }

                    if (!replaced)
                    {
                        candidateIndices.Add(i);
                    }
                }
                else
                {
                    duplicateIndex[key] = new List<int> { i };
                }
            }

            // Compact the list in-place in O(n)
            if (removeCount > 0)
            {
                int writeIdx = 0;
                for (int readIdx = 0; readIdx < cleanLetters.Count; readIdx++)
                {
                    if (!toRemove[readIdx])
                    {
                        cleanLetters[writeIdx++] = cleanLetters[readIdx];
                    }
                }

                cleanLetters.RemoveRange(writeIdx, cleanLetters.Count - writeIdx);
            }

            return cleanLetters;
        }
    }
}
