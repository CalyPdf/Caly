using Caly.Core.Services;

namespace Caly.Tests.Mock
{
    internal sealed class InMemoryClipboardService : BaseClipboardService
    {
        private string? _text;

        public override Task SetAsync(string text)
        {
            _text = text;
            return Task.CompletedTask;
        }

        public override Task<string?> GetAsync()
        {
            return Task.FromResult(_text);
        }

        public override Task ClearAsync()
        {
            _text = null;
            return Task.CompletedTask;
        }
    }
}
