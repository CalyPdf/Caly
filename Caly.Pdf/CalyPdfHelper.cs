namespace Caly.Pdf
{
    internal static class CalyPdfHelper
    {
        public static bool IsPunctuation(this ReadOnlySpan<char> text)
        {
            for (int i = 0; i < text.Length; ++i)
            {
                if (char.IsPunctuation(text[i]))
                {
                    return true;
                }    
            }

            return false;
        }

        public static bool IsSeparator(this ReadOnlySpan<char> text)
        {
            for (int i = 0; i < text.Length; ++i)
            {
                if (char.IsSeparator(text[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
