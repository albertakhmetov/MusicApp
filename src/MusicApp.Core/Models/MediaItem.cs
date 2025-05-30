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
namespace MusicApp.Core.Models;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class MediaItem
{
    public static readonly MediaItem Empty = new(null);

    public MediaItem(string? fileName)
    {
        FileName = fileName ?? string.Empty;
    }

    public bool IsEmpty => string.IsNullOrEmpty(FileName);

    public string FileName { get; }

    public uint? TrackNumber { get; init; }

    public string? Title { get; init; }

    public string? Album { get; init; }

    public string? Artist { get; init; }

    public uint? Year { get; init; }

    public uint? Bitrate { get; init; }

    public TimeSpan? Duration { get; init; }

    public ImmutableArray<string> Genre { get; init; }

    public bool Equals(string? other)
    {
        return other == null ? false : FileName.Equals(other, StringComparison.CurrentCultureIgnoreCase);
    }

    public bool Equals(MediaItem? other)
    {
        return other == null ? false : Equals(other.FileName);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as MediaItem);
    }

    public override int GetHashCode()
    {
        return FileName.GetHashCode(StringComparison.CurrentCultureIgnoreCase);
    }
}
