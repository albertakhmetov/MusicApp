/*  Copyright © 2025, Albert Akhmetov <akhmetov@live.com>   
 *
 *  This file is part of MusicApp.
 *
 *  MusicApp is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  MusicApp is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with MusicApp. If not, see <https://www.gnu.org/licenses/>.   
 *
 */
namespace MusicApp.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.Graphics.Gdi;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;

static class Extensions
{
    public static void Minimize(this IAppWindow window)
    {
        PInvoke.ShowWindow((HWND)window.Handle, SHOW_WINDOW_CMD.SW_MINIMIZE);
    }

    public static uint GetDpi(this IAppWindow window)
    {
        var monitor = PInvoke.MonitorFromWindow((HWND)window.Handle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out var _);
        return dpiX;
    }

    public static T? GetProperty<T>(this MediaSource? mediaSource) where T : class
    {
        return mediaSource?.CustomProperties.TryGetValue(nameof(MediaItem), out var value) == true
            ? value as T
            : null;
    }

    public static void SetProperty<T>(this MediaSource mediaSource, T value) where T :class
    {
        ArgumentNullException.ThrowIfNull(mediaSource);

        mediaSource.CustomProperties.Add(typeof(T).Name, value);
    }

    public static async Task<ImageData> LoadCover(this MediaItem? mediaFile)
    {
        if (mediaFile?.IsEmpty == false)
        {
            var file = await StorageFile.GetFileFromPathAsync(mediaFile.FileName);
           
            using var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.MusicView, 300, ThumbnailOptions.UseCurrentScale);

            if (thumbnail != null && thumbnail.Type == ThumbnailType.Image)
            {
                using var stream = thumbnail.AsStreamForRead();
                return new ImageData(stream);
            }
        }

        return ImageData.Empty;
    }

    public static void Bind(this FrameworkElement element, DependencyProperty property, string path, BindingMode mode)
    {
        var binding = new Binding
        {
            Path = new PropertyPath(path),
            Mode = mode
        };

        element.SetBinding(property, binding);
    }

    public static Color ToColor(this string hexValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(hexValue);

        var value = hexValue.StartsWith('#') ? hexValue.AsSpan(1) : hexValue.AsSpan();
        if (value.Length != 6 && value.Length != 8)
        {
            throw new ArgumentOutOfRangeException(nameof(hexValue));
        }

        byte alpha = 0xFF;
        if (value.Length == 8)
        {
            alpha = byte.Parse(value.Slice(0, 2), System.Globalization.NumberStyles.HexNumber);
            value = value.Slice(2);
        }

        var red = byte.Parse(value.Slice(0, 2), System.Globalization.NumberStyles.HexNumber);
        var green = byte.Parse(value.Slice(2, 2), System.Globalization.NumberStyles.HexNumber);
        var blue = byte.Parse(value.Slice(4, 2), System.Globalization.NumberStyles.HexNumber);

        return Color.FromArgb(alpha, red, green, blue);
    }

    public static async void UpdateTheme(this Window window, bool isDarkTheme)
    {
        if (window.Content is FrameworkElement element)
        {
            element.RequestedTheme = isDarkTheme ? ElementTheme.Dark : ElementTheme.Light;
            window.AppWindow.TitleBar.PreferredTheme = isDarkTheme ? TitleBarTheme.Dark : TitleBarTheme.Light;

            await Task.Delay(TimeSpan.FromMilliseconds(100));

            FixButtons(window.AppWindow.TitleBar);
        }
    }

    private static void FixButtons(AppWindowTitleBar titleBar)
    {
        ArgumentNullException.ThrowIfNull(titleBar);

        var isDarkTheme = titleBar.PreferredTheme == TitleBarTheme.Dark;

        if (isDarkTheme)
        {
            titleBar.ButtonForegroundColor = "#FFFFFF".ToColor();
            titleBar.ButtonHoverForegroundColor = "#FFFFFF".ToColor();
            titleBar.ButtonHoverBackgroundColor = "#0FFFFFFF".ToColor();
        }
        else
        {
            titleBar.ButtonForegroundColor = "#191919".ToColor();
            titleBar.ButtonHoverForegroundColor = "#191919".ToColor();
            titleBar.ButtonHoverBackgroundColor = "#09000000".ToColor();
        }
    }

    [DllImport("UxTheme.dll", EntryPoint = "#132", SetLastError = true)]
    private static extern bool ShouldAppsUseDarkMode();
}
