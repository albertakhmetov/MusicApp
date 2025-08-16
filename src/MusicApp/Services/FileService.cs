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
using MusicApp.Core.Helpers;
using MusicApp.Core.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MusicApp.Core;

class FileService : IFileService
{
    public FileService()
    {
        UserDataPath = Path.GetDirectoryName(IShellService.ApplicationPath)!;
    }

    public string UserDataPath { get; }

    public Stream? ReadUserFile(string fileName)
    {
        var file = new FileInfo(Path.Combine(UserDataPath, fileName));

        return file.Exists ? file.OpenRead() : null;
    }

    public Stream WriteUserFile(string fileName, bool overwrite)
    {
        var file = new FileInfo(Path.Combine(UserDataPath, fileName));
        if (overwrite && file.Exists)
        {
            file.Delete();
        }

        return file.OpenWrite();
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
}
