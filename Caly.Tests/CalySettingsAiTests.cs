using System.Text.Json;
using Caly.Core.Models;
using Caly.Core.Services;

namespace Caly.Tests
{
    public class CalySettingsAiTests
    {
        private static CalySettings? Load(string json)
            => JsonSerializer.Deserialize(json, SourceGenerationContext.Default.CalySettings);

        [Fact]
        public void Default_has_model_id()
        {
            Assert.Equal("qwen3.5-0.8b-generic-gpu:4", CalySettings.Default.Ai.ModelId);
            Assert.True(CalySettings.Default.Ai.Enabled);
        }

        [Fact]
        public void Missing_ai_section_loads_defaults()
        {
            var settings = Load("{}");

            Assert.NotNull(settings);
            Assert.NotNull(settings.Ai);
            Assert.Equal("qwen3.5-0.8b-generic-gpu:4", settings.Ai.ModelId);
            Assert.True(settings.Ai.Enabled);
        }

        [Fact]
        public void Ai_section_roundtrips()
        {
            var settings = new CalySettings { Ai = new CalySettings.CalySettingsAi { ModelId = "other:1", Enabled = false } };

            string json = JsonSerializer.Serialize(settings, SourceGenerationContext.Default.CalySettings);
            var loaded = Load(json);

            Assert.NotNull(loaded);
            Assert.Equal("other:1", loaded.Ai.ModelId);
            Assert.False(loaded.Ai.Enabled);
        }
    }
}
