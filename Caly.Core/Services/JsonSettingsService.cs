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
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.Views;
using static Caly.Core.Models.CalySettings;

namespace Caly.Core.Services
{
    [JsonSerializable(typeof(CalySettings), GenerationMode = JsonSourceGenerationMode.Metadata)]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
    
    internal sealed class JsonSettingsService : ISettingsService
    {
        private const string _settingsFile = "caly_settings";
        
        private readonly Visual _target;

        private CalySettings? _current;

        public JsonSettingsService(Visual target)
        {
            if (CalyExtensions.IsMobilePlatform())
            {
                SetDefaultSettings(); // TODO - Create proper mobile class
                return;
            }

            _target = target;
            if (_target is Window w)
            {
                w.Opened += _window_Opened;
                w.Closing += _window_Closing;
            }
        }

        private void _window_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (_target is Window w)
            {
                w.Closing -= _window_Closing;

                if (_current is not null && w.WindowState == WindowState.Normal)
                {
                    _current.Width = (int)w.Width;
                    _current.Height = (int)w.Height;
                }
            }

            Save();
        }

        private void _window_Opened(object? sender, EventArgs e)
        {
            if (_target is Window w)
            {
                w.Opened -= _window_Opened;
            }

            if (sender is MainWindow mw)
            {
                if (_current is null)
                {
                    return;
                }
                
                mw.Width = _current.Width;
                mw.Height = _current.Height;

                if (mw.WindowStartupLocation == WindowStartupLocation.CenterScreen)
                {
                    // Adjust window position as it looks like the top left corner is at screen center, not the center of window
                    mw.Position -= PixelPoint.FromPoint(new Point(mw.Width / 2.0, mw.Height / 2.0), mw.Screens.ScreenFromWindow(mw)?.Scaling ?? 1);
                }
            }
            else
            {
                throw new InvalidOperationException($"Expecting '{typeof(MainWindow)}' but got '{sender?.GetType()}'.");
            }
        }

        public void SetProperty(CalySettingsProperty property, object value)
        {
            try
            {
                if (_current is null)
                {
                    return;
                }

                switch (property)
                {
                    case CalySettingsProperty.PaneSize:
                        _current.PaneSize = (int)(double)value;
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteExceptionToFile(ex);
            }
        }

        public CalySettings GetSettings()
        {
            if (_current is null)
            {
                Load();
            }

            return _current!;
        }

        public async ValueTask<CalySettings> GetSettingsAsync()
        {
            if (_current is null)
            {
                await LoadAsync();
            }

            return _current!;
        }

        private void HandleCorruptedFile()
        {
            if (File.Exists(_settingsFile))
            {
                File.Delete(_settingsFile);
            }

            SetDefaultSettings();
        }

        private static void ValidateSetting(CalySettings? settings)
        {
            if (settings is null)
            {
                return;
            }

            if (settings.PaneSize <= 0)
            {
                settings.PaneSize = CalySettings.Default.PaneSize;
            }

            if (settings.Width <= 0)
            {
                settings.Width = CalySettings.Default.Width;
            }

            if (settings.Height <= 0)
            {
                settings.Height = CalySettings.Default.Height;
            }
        }

        private void SetDefaultSettings()
        {
            _current ??= CalySettings.Default;
        }

        public void Load()
        {
            if (CalyExtensions.IsMobilePlatform())
            {
                return; // TODO - Create proper mobile class
            }

            try
            {
                if (!File.Exists(_settingsFile))
                {
                    SetDefaultSettings();

                    using (FileStream createStream = File.Create(_settingsFile))
                    {
                        JsonSerializer.Serialize(createStream, _current, SourceGenerationContext.Default.CalySettings);
                    }

                    return;
                }

                using (FileStream createStream = File.OpenRead(_settingsFile))
                {
                    _current = JsonSerializer.Deserialize(createStream, SourceGenerationContext.Default.CalySettings);
                    ValidateSetting(_current);
                }
            }
            catch (JsonException jsonEx)
            {
                HandleCorruptedFile();
                Debug.WriteExceptionToFile(jsonEx);
            }
            catch (Exception ex)
            {
                Debug.WriteExceptionToFile(ex);
            }
        }

        public async Task LoadAsync()
        {
            Debug.ThrowOnUiThread();

            if (CalyExtensions.IsMobilePlatform())
            {
                return; // TODO - Create proper mobile class
            }

            try
            {
                if (!File.Exists(_settingsFile))
                {
                    SetDefaultSettings();

                    await using (FileStream createStream = File.Create(_settingsFile))
                    {
                        await JsonSerializer.SerializeAsync(createStream, _current, SourceGenerationContext.Default.CalySettings);
                    }
                    return;
                }

                await using (FileStream createStream = File.OpenRead(_settingsFile))
                {
                    _current = await JsonSerializer.DeserializeAsync(createStream, SourceGenerationContext.Default.CalySettings);
                    ValidateSetting(_current);
                }
            }
            catch (JsonException jsonEx)
            {
                HandleCorruptedFile();
                Debug.WriteExceptionToFile(jsonEx);
            }
            catch (Exception ex)
            {
                Debug.WriteExceptionToFile(ex);
            }
        }

        public void Save()
        {
            if (CalyExtensions.IsMobilePlatform())
            {
                return; // TODO - Create proper mobile class
            }

            if (_current is not null)
            {
                try
                {
                    using (FileStream createStream = File.Create(_settingsFile))
                    {
                        JsonSerializer.Serialize(createStream, _current, SourceGenerationContext.Default.CalySettings);
                    }
                }
                catch (JsonException jsonEx)
                {
                    HandleCorruptedFile();
                    Debug.WriteExceptionToFile(jsonEx);
                }
                catch (Exception ex)
                {
                    Debug.WriteExceptionToFile(ex);
                }
            }
        }

        public async Task SaveAsync()
        {
            Debug.ThrowOnUiThread();

            if (CalyExtensions.IsMobilePlatform())
            {
                return; // TODO - Create proper mobile class
            }

            if (_current is not null)
            {
                try
                {
                    await using (FileStream createStream = File.Create(_settingsFile))
                    {
                        await JsonSerializer.SerializeAsync(createStream, _current, SourceGenerationContext.Default.CalySettings);
                    }
                }
                catch (JsonException jsonEx)
                {
                    HandleCorruptedFile();
                    Debug.WriteExceptionToFile(jsonEx);
                }
                catch (Exception ex)
                {
                    Debug.WriteExceptionToFile(ex);
                }
            }
        }
    }
}
