namespace MusicApp.Services;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Media.Imaging;
using MusicApp.Core;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using MusicApp.Native;
using MusicApp.Views;

internal class TaskbarMediaCoverService : ITaskbarMediaCoverService, IDisposable
{
    private readonly CompositeDisposable disposable = [];

    private readonly IPlaybackService playbackService;

    private readonly Thumbnail thumbnail;
    private readonly IAppWindow window;
    private ImageData? imageData;

    public TaskbarMediaCoverService(IPlaybackService playbackService, IAppWindow window)
    {
        ArgumentNullException.ThrowIfNull(playbackService);
        ArgumentNullException.ThrowIfNull(window);

        this.playbackService = playbackService;
        this.window = window;

        thumbnail = new Thumbnail(this.window);
        thumbnail.Preview += OnPreview;
        thumbnail.LivePreview += OnLivePreview;

        InitSubscriptions();
    }

    private ImageData? ImageData
    {
        get => imageData;
        set
        {
            imageData = value;
            thumbnail?.Invalidate();
        }
    }

    public void Dispose()
    {
        if (disposable?.IsDisposed is false)
        {
            disposable.Dispose();
        }

        thumbnail.Dispose();
    }

    private void InitSubscriptions()
    {
        if (SynchronizationContext.Current is null)
        {
            throw new InvalidOperationException("SynchronizationContext.Current can't be null");
        }

        playbackService
            .MediaItemCover
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(cover => ImageData = cover)
            .DisposeWith(disposable);

        Observable
            .CombineLatest(
                playbackService.MediaItem,
                playbackService.MediaItemCover,
                playbackService.Position,
                (x, y, z) => true)
            .Throttle(TimeSpan.FromMicroseconds(150))
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(_ => thumbnail.Invalidate())
            .DisposeWith(disposable);
    }

    private Task OnPreview(Thumbnail sender, Thumbnail.PreviewEventArgs e)
    {
        var minSideSize = Math.Min(e.Width, e.Height);

        var bitmap = new Bitmap(minSideSize, minSideSize, PixelFormat.Format32bppArgb);

        using var stream = imageData?.IsEmpty == false
            ? imageData.GetStream()
            : typeof(Taskbar).Assembly.GetManifestResourceStream($"MusicApp.Assets.app.png")!;

        using var image = System.Drawing.Image.FromStream(stream);

        var padding = imageData?.IsEmpty == false ? 0 : minSideSize / 3;

        using var g = Graphics.FromImage(bitmap);
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        g.Clear(Color.Transparent);

        g.DrawImage(image, new Rectangle(padding, padding, minSideSize - padding * 2, minSideSize - padding * 2));

        e.Bitmap = bitmap;

        return Task.CompletedTask;
    }

    private async Task OnLivePreview(Thumbnail sender, Thumbnail.PreviewEventArgs e)
    {
        var captureData = default(WindowCaptureData);

        if (window is null || (captureData = await window.Capture()) is null)
        {
            return;
        }

        var bitmap = new Bitmap(captureData.Width, captureData.Height, PixelFormat.Format32bppArgb);

        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb
        );

        var pixelData = captureData.Pixels;

        unsafe
        {
            byte* destPtr = (byte*)bitmapData.Scan0;
            fixed (byte* srcPtr = pixelData)
            {
                for (int i = 0; i < pixelData.Length; i++)
                {
                    destPtr[i] = srcPtr[i];
                }
            }
        }

        bitmap.UnlockBits(bitmapData);

        e.Bitmap = bitmap;
    }
}
