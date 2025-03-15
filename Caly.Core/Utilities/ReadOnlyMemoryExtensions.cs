// Copyright (c) 2025 BobLd
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Buffers;
using System.Text;

namespace Caly.Core.Utilities
{
    internal static class ReadOnlyMemoryExtensions
    {
        private const char Padding = '\0';
        private const char Space = ' ';

        public static void AppendClean(this StringBuilder sb, ReadOnlyMemory<char> memory)
        {
            Span<char> output = memory.Length < 512 ?
                stackalloc char[(int)memory.Length] :
                new char[memory.Length];

            memory.Span.CopyTo(output);

            // Padding chars are problematic in string builder, we remove them
            for (int i = 0; i < output.Length; ++i)
            {
                if (output[i] == Padding)
                {
                    output[i] = Space;
                }
            }

            if (!output.IsEmpty && !MemoryExtensions.IsWhiteSpace(output))
            {
                sb.Append(output);
            }
        }

        public static string GetString(this ReadOnlySequence<char> sequence)
        {
            return string.Create((int)sequence.Length, sequence, (chars, state) =>
            {
                // https://www.stevejgordon.co.uk/creating-strings-with-no-allocation-overhead-using-string-create-csharp
                state.CopyTo(chars);
            });
        }

        public static string GetString(this ReadOnlyMemory<char> memory)
        {
            /*
            if (MemoryMarshal.TryGetString(memory, out string? str, out _, out _))
            {
                return str;
            }
            */

            return string.Create(memory.Length, memory, (chars, state) =>
            {
                // https://www.stevejgordon.co.uk/creating-strings-with-no-allocation-overhead-using-string-create-csharp
                state.Span.CopyTo(chars);
            });
        }
    }
}
