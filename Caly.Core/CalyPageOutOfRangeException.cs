using System;
using System.Runtime.CompilerServices;

namespace Caly.Core
{
    /// <summary>
    /// The exception that is thrown when the page number is outside the allowable
    /// range of values.
    /// </summary>
    public sealed class CalyPageOutOfRangeException : ArgumentOutOfRangeException
    {
        /// <summary>
        /// The number of pages in the document.
        /// </summary>
        public int? NumberOfPages { get; }

        /// <inheritdoc/>
        public override string Message
        {
            get
            {
                if (NumberOfPages.HasValue)
                {
                    return $"{base.Message} The page number should be greater or equal to '1' and less then the number of pages in the document: '{NumberOfPages}'.";
                }

                return $"{base.Message} The page number should be greater or equal to '1'.";
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CalyPageOutOfRangeException"/> class with
        /// the parameter name and the value of the argument.
        /// </summary>
        /// <param name="paramName">The name of the parameter that caused the exception.</param>
        /// <param name="actualValue">The value of the argument that causes this exception.</param>
        /// <param name="numberOfPages">The number of pages in the document.</param>
        public CalyPageOutOfRangeException(string? paramName, int actualValue, int? numberOfPages)
            : base(paramName, actualValue, "")
        {
            NumberOfPages = numberOfPages;
        }

        /// <summary>
        /// Throws an <see cref="CalyPageOutOfRangeException"/> if <paramref name="value"/> is negative or zero.
        /// </summary>
        /// <param name="value">The page number to validate.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfPageOutOfRange(int value, [CallerArgumentExpression("value")] string? paramName = null)
        {
            if (value <= 0)
            {
                throw new CalyPageOutOfRangeException(paramName, value, null);
            }
        }

        /// <summary>
        /// Throws an <see cref="CalyPageOutOfRangeException"/> if <paramref name="value"/> is negative, zero or greater than the number of pages in the document.
        /// </summary>
        /// <param name="value">The page number to validate.</param>
        /// <param name="numberOfPages">The number of pages in the document.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfPageOutOfRange(int value, int numberOfPages, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value <= 0 || value > numberOfPages)
            {
                throw new CalyPageOutOfRangeException(paramName, value, numberOfPages);
            }
        }
    }
}
