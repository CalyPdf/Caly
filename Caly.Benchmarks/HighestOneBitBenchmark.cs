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

using BenchmarkDotNet.Attributes;

namespace Caly.Benchmarks
{
    [MemoryDiagnoser]
    public class HighestOneBitBenchmark
    {
        // JBIG2 filter
        private const int end = 65_536;

        [Benchmark(Baseline = true)]
        public long HighestOneBit()
        {
            long sum = 0;
            for (int i = -end; i <= end; ++i)
            {
                sum += HighestOneBitLocal(i);
            }
            return sum;
        }

        [Benchmark]
        public long HighestOneBitNoString()
        {
            long sum = 0;
            for (int i = -end; i <= end; ++i)
            {
                sum += HighestOneBit(i);
            }
            return sum;
        }

        public static int HighestOneBit(int number)
        {
            if (number == 0) return 1;
            return (int)Math.Pow(2, Math.Floor(Math.Log2(number)));
        }

        public static int HighestOneBitLocal(int number)
        {
            return (int)Math.Pow(2, Convert.ToString(number, 2).Length - 1);
        }
    }
}
