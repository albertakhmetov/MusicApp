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
namespace MusicApp.Core.Services;

using System.Collections.Immutable;
using System.Diagnostics;
using MusicApp.Core.Models;

public interface IFileService
{
    static string ApplicationPath
    {
        get
        {
            var processModule = Process.GetCurrentProcess().MainModule;
            return processModule is null
                ? throw new InvalidOperationException("Process.GetCurrentProcess().MainModule is null")
                : processModule.FileName;
        }
    }

    string UserDataPath { get; }

    Stream? ReadUserFile(string fileName);

    Stream WriteUserFile(string fileName, bool overwrite);

    Task<IList<string>> PickFilesForOpenAsync(IImmutableList<FileType> fileTypes);

    Task<string?> PickFileForOpenAsync(IImmutableList<FileType> fileTypes);

    Task<string?> PickFileForSaveAsync(IImmutableList<FileType> fileTypes, string? suggestedFileName = null);

    Task<IList<MediaItem>> LoadMediaItems(IEnumerable<string> fileNames);
}
