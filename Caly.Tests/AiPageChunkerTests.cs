using Caly.Core.Models.Ai;
using Caly.Core.Services.Ai;

namespace Caly.Tests
{
    public class AiPageChunkerTests
    {
        private static AiPageContent Page(int number, int chars, bool image = false)
            => new(number, new string('a', chars), image ? new byte[] { 1 } : null);

        [Fact]
        public void Fits_in_one_chunk()
        {
            var chunks = AiPageChunker.Chunk([Page(1, 400), Page(2, 400), Page(3, 400)], 10_000);

            var chunk = Assert.Single(chunks);
            Assert.Equal([1, 2, 3], chunk.Select(p => p.PageNumber));
        }

        [Fact]
        public void Splits_when_over_budget()
        {
            var chunks = AiPageChunker.Chunk([Page(1, 400_000), Page(2, 400_000), Page(3, 400_000)], 150_000);

            Assert.Equal(3, chunks.Count);
            Assert.All(chunks, c => Assert.Single(c));
        }

        [Fact]
        public void Oversized_single_page_gets_own_chunk()
        {
            var chunks = AiPageChunker.Chunk([Page(1, 100), Page(2, 4_000_000), Page(3, 100)], 1_000);

            Assert.Equal(3, chunks.Count);
            Assert.Equal(2, chunks[1][0].PageNumber);
        }

        [Fact]
        public void Empty_input_gives_no_chunks()
        {
            Assert.Empty(AiPageChunker.Chunk([], 1_000));
        }

        [Fact]
        public void Image_cost_counts()
        {
            Assert.True(AiTokenEstimator.Estimate(Page(1, 0, image: true)) >= AiTokenEstimator.TokensPerImage);
            Assert.Equal(0, AiTokenEstimator.Estimate(Page(1, 0)));
        }
    }
}
