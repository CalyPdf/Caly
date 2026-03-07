using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using static Caly.Core.Models.CalySettings;

namespace Caly.Tests.Integration.Mocks;

internal sealed class MockSettingsService : ISettingsService
{
    private CalySettings _settings = CalySettings.Default;

    public void SetProperty(CalySettingsProperty property, object value) { }

    public CalySettings GetSettings() => _settings;

    public ValueTask<CalySettings> GetSettingsAsync() => ValueTask.FromResult(_settings);

    public void Load() { }

    public Task LoadAsync() => Task.CompletedTask;

    public void Save() { }

    public Task SaveAsync() => Task.CompletedTask;
}
