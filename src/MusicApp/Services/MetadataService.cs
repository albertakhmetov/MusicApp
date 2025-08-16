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
namespace MusicApp.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using Windows.Storage;
using Windows.Storage.FileProperties;

internal class MetadataService : IMetadataService
{
    public async Task<ImageData> LoadMediaCoverAsync(MediaItem? mediaItem)
    {
        if (mediaItem?.IsEmpty == false)
        {
            var file = await StorageFile.GetFileFromPathAsync(mediaItem.FileName);

            using var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.MusicView, 300, ThumbnailOptions.UseCurrentScale);

            if (thumbnail != null && thumbnail.Type == ThumbnailType.Image)
            {
                using var stream = thumbnail.AsStreamForRead();
                return new ImageData(stream);
            }
        }

        return ImageData.Empty;
    }

    public async Task<MediaItem> LoadMediaItemAsync(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var file = await StorageFile.GetFileFromPathAsync(fileName);
        var musicProperties = await file.Properties.GetMusicPropertiesAsync();

        return new MediaItem(fileName)
        {
            TrackNumber = musicProperties.TrackNumber,
            Title = musicProperties.Title,
            Artist = musicProperties.Artist,
            Album = musicProperties.Album,
            Year = musicProperties.Year,
            Bitrate = musicProperties.Bitrate,
            Duration = musicProperties.Duration
        };
    }

    public async Task<MediaItem[]> LoadMediaItemsAsync(IEnumerable<string> fileNames)
    {
        ArgumentNullException.ThrowIfNull(fileNames);

        var mediaItems = new List<MediaItem>();

        foreach(var fileName in fileNames)
        {
            mediaItems.Add(await LoadMediaItemAsync(fileName));
        }

        return [.. mediaItems];
    }
}
