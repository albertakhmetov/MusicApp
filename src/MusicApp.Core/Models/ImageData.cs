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
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ImageData : IDisposable
{
    public readonly static ImageData Empty = new(null);

    private readonly byte[] buffer;

    public ImageData(Stream? sourceStream)
    {
        if (sourceStream == null)
        {
            Size = 0;
            buffer = [];
        }
        else
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(sourceStream.Length, int.MaxValue);

            Size = (int)sourceStream.Length;

            buffer = ArrayPool<byte>.Shared.Rent(Size);
            sourceStream.ReadExactly(buffer, 0, Size);
        }
    }

    public bool IsEmpty => Size == 0;

    public int Size { get; }

    public void Dispose()
    {
        if (!IsEmpty)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public Stream GetStream()
    {
        return new MemoryStream(buffer, 0, Size, false, false);
    }
}
