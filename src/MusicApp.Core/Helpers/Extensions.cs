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
namespace MusicApp.Core.Helpers;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

public static class Extensions
{
    public static T DisposeWith<T>(this T obj, CompositeDisposable compositeDisposable) where T : IDisposable
    {
        compositeDisposable.Add(obj);

        return obj;
    }

    public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        ArgumentNullException.ThrowIfNull(action);

        foreach (var i in enumerable)
        {
            action(i);
        }
    }

    public static int ToInt32(this double value)
    {
        return Convert.ToInt32(value);
    }

    public static FileInfo GetFileInfo(this DirectoryInfo directoryInfo, string fileName) => new(Path.Combine(directoryInfo.FullName, fileName));

    public static FileStream OpenWrite(this FileInfo fileInfo, bool overwrite)
    {
        if (overwrite && fileInfo.Exists)
        {
            fileInfo.Delete();
        }

        return fileInfo.OpenWrite();
    }

    public static string GetFileNameWithoutExtension(this FileInfo fileInfo)
    {
        return Path.GetFileNameWithoutExtension(fileInfo.FullName);
    }
}
