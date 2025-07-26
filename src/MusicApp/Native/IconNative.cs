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
namespace MusicApp.Native;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Hosting;
using Windows.ApplicationModel.Activation;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

public class IconNative
{
    private const int ICON_DIR_SIZE = 6;
    private const int ICON_DIR_ENTRY_SIZE = 16;
    private readonly byte[] buffer;
    private readonly IconSize[] iconSizes;

    public IconNative(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        buffer = new byte[Convert.ToInt32(stream.Length)];
        stream.ReadExactly(buffer);

        var count = BitConverter.ToUInt16(buffer.AsSpan().Slice(sizeof(ushort) * 2, sizeof(ushort)));
        if (count == 0)
        {
            throw new ArgumentException("Stream doesn't contain icons");
        }

        iconSizes = new IconSize[count];
        for (var frameId = 0; frameId < count; frameId++)
        {
            var entryBuffer = buffer
                .AsSpan()
                .Slice(ICON_DIR_SIZE)
                .Slice(ICON_DIR_ENTRY_SIZE * frameId, ICON_DIR_ENTRY_SIZE);

            iconSizes[frameId] = new IconSize(
                entryBuffer[0] == 0 ? 256 : entryBuffer[0],
                entryBuffer[1] == 0 ? 256 : entryBuffer[1]);
        }
    }

    public int Count => iconSizes.Length;

    public SafeHandle this[int frameId]
    {
        get => GetFrame(frameId);
    }

    public SafeHandle ResolveFrame(int iconWidth, int iconHeight)
    {
        var destSize = new IconSize(iconWidth, iconHeight);

        var bestSize = default(IconSize);
        var bestFrameId = default(int?);

        for (var frameId = 0; frameId < Count; frameId++)
        {
            var size = iconSizes[frameId];
            if (size.CompareTo(destSize) >= 0 && (bestSize is null || size.CompareTo(bestSize) < 0))
            {
                bestSize = size;
                bestFrameId = frameId;
            }
        }

        if (bestFrameId is null)
        {
            var max = iconSizes.Max();
            return GetFrame(Array.IndexOf(iconSizes, max));
        }
        else
        {
            return GetFrame(bestFrameId.Value);
        }
    }

    private SafeHandle GetFrame(int frameId)
    {
        if (frameId < 0 || frameId >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(frameId));
        }

        var entryBuffer = buffer
            .AsSpan()
            .Slice(ICON_DIR_SIZE)
            .Slice(ICON_DIR_ENTRY_SIZE * frameId, ICON_DIR_ENTRY_SIZE);

        int pos = BitConverter.ToInt32(entryBuffer.Slice(ICON_DIR_ENTRY_SIZE - sizeof(uint), sizeof(uint)));
        int length = BitConverter.ToInt32(entryBuffer.Slice(ICON_DIR_ENTRY_SIZE - sizeof(uint) * 2, sizeof(uint)));

        return PInvoke.CreateIconFromResourceEx(
            buffer.AsSpan().Slice(pos, length),
            (BOOL)true,
            0x00030000,
            0,
            0,
            0);
    }

    private record IconSize(int Width, int Height) : IComparable<IconSize>
    {
        public int CompareTo(IconSize? other)
        {
            if (other == null)
            {
                return 1;
            }

            if (Width == other.Width)
            {
                return Height.CompareTo(other.Height);
            }
            else
            {
                return Width.CompareTo(other.Width);
            }
        }
    }
}
