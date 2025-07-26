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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;

internal sealed class Thumbnail : WindowProc.IReceiver, IDisposable
{
    private readonly CompositeDisposable disposable = [];

    private readonly WindowProc windowProc;
    private ImageData? imageData;

    public Thumbnail(WindowProc windowProc)
    {
        ArgumentNullException.ThrowIfNull(windowProc);

        this.windowProc = windowProc;

        SetStaticPreview();

        this.windowProc
            .Register(this, PInvoke.WM_DWMSENDICONICTHUMBNAIL)
            .DisposeWith(disposable);

        this.windowProc
            .Register(this, PInvoke.WM_DWMSENDICONICLIVEPREVIEWBITMAP)
            .DisposeWith(disposable);
    }

    public ImageData? ImageData
    {
        get => imageData;
        set
        {
            this.imageData = value;

        }
    }

    public event PreviewEventHandler? Preview;

    public event PreviewEventHandler? LivePreview;

    public void Invalidate()
    {
        PInvoke.DwmInvalidateIconicBitmaps(windowProc.HWND);
    }

    public void Dispose()
    {
        if (disposable.IsDisposed is false)
        {
            disposable.Dispose();
        }
    }

    LRESULT WindowProc.IReceiver.Process(uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == PInvoke.WM_DWMSENDICONICTHUMBNAIL)
        {
            var args = new PreviewEventArgs
            {
                Width = (int)(lParam.Value >> 16),
                Height = (int)(lParam.Value & 0xFFFF)
            };

            Preview?.Invoke(this, args).Wait();

            if (args.Bitmap != null)
            {
                using var bitmap = args.Bitmap;
                using var hBitmap = new HBitmapSafeHandle(bitmap);

                PInvoke.DwmSetIconicThumbnail(windowProc.HWND, hBitmap, 0).ThrowOnFailure();
            }
        }

        if (msg == PInvoke.WM_DWMSENDICONICLIVEPREVIEWBITMAP)
        {
            SetLivePreview();
        }

        return (LRESULT)0;
    }

    private async void SetLivePreview()
    {
        var args = new PreviewEventArgs
        {
            Width = 0,
            Height = 0
        };

        var callback = LivePreview;

        if (callback != null)
        {
            await callback.Invoke(this, args);

            if (args.Bitmap != null)
            {
                using var bitmap = args.Bitmap;
                using var hBitmap = new HBitmapSafeHandle(bitmap);

                PInvoke.DwmSetIconicLivePreviewBitmap(windowProc.HWND, hBitmap, null, 0).ThrowOnFailure();
            }
        }
    }

    private void SetStaticPreview()
    {
        BOOL forceIconic = true;
        BOOL hasIconicBitmap = true;

        unsafe
        {
            PInvoke.DwmSetWindowAttribute(
                windowProc.HWND,
                DWMWINDOWATTRIBUTE.DWMWA_FORCE_ICONIC_REPRESENTATION,
                &forceIconic,
                (uint)Marshal.SizeOf<BOOL>()).ThrowOnFailure();

            PInvoke.DwmSetWindowAttribute(
                windowProc.HWND,
                DWMWINDOWATTRIBUTE.DWMWA_HAS_ICONIC_BITMAP,
                &hasIconicBitmap,
                (uint)Marshal.SizeOf<BOOL>()).ThrowOnFailure();
        }
    }

    public delegate Task PreviewEventHandler(Thumbnail sender, PreviewEventArgs e);

    public sealed class PreviewEventArgs : EventArgs
    {
        public int Width { get; init; }

        public int Height { get; init; }

        public Bitmap? Bitmap { get; set; }
    }

    private class HBitmapSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public HBitmapSafeHandle(Bitmap bitmap) : base(true)
        {
            ArgumentNullException.ThrowIfNull(bitmap);

            SetHandle(bitmap.GetHbitmap(Color.FromArgb(0)));
        }

        protected override bool ReleaseHandle()
        {
            return PInvoke.DeleteObject((HGDIOBJ)handle);
        }
    }
}
