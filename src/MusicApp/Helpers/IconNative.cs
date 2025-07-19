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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

public class IconNative
{
    private const int ICON_DIR_SIZE = 6;
    private const int ICON_DIR_ENTRY_SIZE = 16;
    private readonly byte[] buffer;
    private readonly int[] widths;

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

        widths = new int[count];
        for (var frameId = 0; frameId < count; frameId++)
        {
            var entryBuffer = buffer
                .AsSpan()
                .Slice(ICON_DIR_SIZE)
                .Slice(ICON_DIR_ENTRY_SIZE * frameId, ICON_DIR_ENTRY_SIZE);

            widths[frameId] = entryBuffer[0] == 0 ? 256 : entryBuffer[0];
        }
    }

    public int Count => widths.Length;

    public SafeHandle this[int frameId]
    {
        get => GetFrame(frameId);
    }

    public SafeHandle ResolveFrame(int destWidth)
    {
        var bestWidth = default(int?);
        var bestFrameId = default(int?);

        for (var frameId = 0; frameId < Count; frameId++)
        {
            if (widths[frameId] >= destWidth && (bestWidth.HasValue is false || widths[frameId] < bestWidth.Value))
            {
                bestWidth = widths[frameId];
                bestFrameId = frameId;
            }
        }

        if (bestFrameId is null)
        {
            var max = widths.Max();
            return GetFrame(Array.IndexOf(widths, max));
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
}
