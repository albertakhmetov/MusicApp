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
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

class FileService : IFileService
{
    private readonly IApp app;

    public FileService(IApp app)
    {
        ArgumentNullException.ThrowIfNull(app);

        this.app = app;
    }

    public async Task<IList<MediaItem>> LoadMediaItems(IEnumerable<string> fileNames)
    {
        var mediaItems = new List<MediaItem>();

        foreach (var fileName in fileNames ?? [])
        {
            var file = await StorageFile.GetFileFromPathAsync(fileName);
            var musicProperties = await file.Properties.GetMusicPropertiesAsync();

            mediaItems.Add(new MediaItem(fileName)
            {
                TrackNumber = musicProperties.TrackNumber,
                Title = musicProperties.Title,
                Artist = musicProperties.Artist,
                Album = musicProperties.Album,
                Year = musicProperties.Year,
                Bitrate = musicProperties.Bitrate,
                Duration = musicProperties.Duration,
            });
        }

        return mediaItems;
    }

    public async Task<IList<string>> PickMultipleFilesAsync()
    {
        var openPicker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, app.Handle);

        openPicker.ViewMode = PickerViewMode.Thumbnail;
        openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        openPicker.FileTypeFilter.Add(".mp3");

        var files = await openPicker.PickMultipleFilesAsync();
        return files?.Select(x => x.Path).ToArray() ?? [];
    }
}
