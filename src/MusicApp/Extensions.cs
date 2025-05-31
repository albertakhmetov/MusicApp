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
namespace MusicApp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicApp.Core.Models;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;

static class Extensions
{
    public static T? GetProperty<T>(this MediaSource? mediaSource) where T : class
    {
        return mediaSource?.CustomProperties.TryGetValue(nameof(MediaItem), out var value) == true
            ? value as T
            : null;
    }

    public static void SetProperty<T>(this MediaSource mediaSource, T value) where T :class
    {
        ArgumentNullException.ThrowIfNull(mediaSource);

        mediaSource.CustomProperties.Add(nameof(T), value);
    }

    public static async Task<ImageData> LoadCover(this MediaItem? mediaFile)
    {
        if (mediaFile?.IsEmpty == false)
        {
            var file = await StorageFile.GetFileFromPathAsync(mediaFile.FileName);

            var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.MusicView, 300, ThumbnailOptions.UseCurrentScale);

            if (thumbnail != null && thumbnail.Type == ThumbnailType.Image)
            {
                using var stream = thumbnail.AsStreamForRead();
                return new ImageData(stream);
            }
        }

        return ImageData.Empty;
    }
}
