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
}
