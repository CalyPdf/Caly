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
        private const string SettingsFile = "caly_settings";
        
        private readonly Visual? _target;

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
                w.PropertyChanged += _window_PropertyChanged;
            }
        }

        private void _window_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Window.WindowStateProperty)
            {
                // When it's loaded, we handle the first time the window changes state:
                // If the window goes from Maximised to Normal, we set the width and height
                // because not value has been properly set yet (will use the default xaml values)

                if (_current is not null && sender is Window w)
                {
                    if (!w.IsLoaded)
                    {
                        return;
                    }

                    w.PropertyChanged -= _window_PropertyChanged;

                    var oldState = (WindowState?)e.OldValue;
                    var newState = (WindowState?)e.NewValue;

                    if (!oldState.HasValue || !newState.HasValue)
                    {
                        return;
                    }
                    
                    if (oldState == WindowState.Maximized && newState == WindowState.Normal)
                    {
                        w.Width = _current.Width;
                        w.Height = _current.Height;
                    }
                }
            }
        }

        private void _window_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (_target is Window w)
            {
                w.Opened -= _window_Opened;
                w.Closing -= _window_Closing;
                w.PropertyChanged -= _window_PropertyChanged;

                if (_current is not null)
                {
                    switch (w.WindowState)
                    {
                        case WindowState.Normal:
                            _current.IsMaximised = false;
                            _current.Width = (int)w.Width;
                            _current.Height = (int)w.Height;
                            break;
                        
                        case WindowState.Maximized:
                            _current.IsMaximised = true;
                            break;
                    }
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

                if (_current.IsMaximised)
                {
                    mw.WindowState = WindowState.Maximized;
                    return;
                }

                mw.Width = _current.Width;
                mw.Height = _current.Height;
                
                try
                {
                    if (mw.WindowStartupLocation == WindowStartupLocation.CenterScreen)
                    {
                        // Adjust window position as it looks like the top left corner is at
                        // screen center, not the center of window
                        var screen = mw.Screens.ScreenFromWindow(mw) ?? mw.Screens.Primary;
                        if (screen is null || mw.Width > screen.WorkingArea.Width ||
                            mw.Height > screen.WorkingArea.Height)
                        {
                            // Could not find screen or the window size is bigger than screen size
                            // We set the window in to left corner
                            mw.Position = PixelPoint.FromPoint(new Point(0, 0), screen?.Scaling ?? 1);
                            return;
                        }

                        // Center window
                        double x = screen.WorkingArea.X + (screen.WorkingArea.Width - mw.Width) / 2.0;
                        double y = screen.WorkingArea.Y + (screen.WorkingArea.Height - mw.Height) / 2.0;
                        mw.Position = PixelPoint.FromPoint(new Point(x, y), screen.Scaling);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteExceptionToFile(ex);
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
            if (File.Exists(SettingsFile))
            {
                File.Delete(SettingsFile);
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
                settings.PaneSize = Default.PaneSize;
            }

            if (settings.Width <= 0)
            {
                settings.Width = Default.Width;
            }

            if (settings.Height <= 0)
            {
                settings.Height = Default.Height;
            }
        }

        private void SetDefaultSettings()
        {
            _current ??= Default;
        }

        public void Load()
        {
            if (CalyExtensions.IsMobilePlatform())
            {
                return; // TODO - Create proper mobile class
            }

            try
            {
                if (!File.Exists(SettingsFile))
                {
                    SetDefaultSettings();

                    using (FileStream createStream = File.Create(SettingsFile))
                    {
                        JsonSerializer.Serialize(createStream, _current, SourceGenerationContext.Default.CalySettings);
                    }

                    return;
                }

                using (FileStream createStream = File.OpenRead(SettingsFile))
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
                if (!File.Exists(SettingsFile))
                {
                    SetDefaultSettings();

                    await using (FileStream createStream = File.Create(SettingsFile))
                    {
                        await JsonSerializer.SerializeAsync(createStream, _current, SourceGenerationContext.Default.CalySettings);
                    }
                    return;
                }

                await using (FileStream createStream = File.OpenRead(SettingsFile))
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
                    using (FileStream createStream = File.Create(SettingsFile))
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
                    await using (FileStream createStream = File.Create(SettingsFile))
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
