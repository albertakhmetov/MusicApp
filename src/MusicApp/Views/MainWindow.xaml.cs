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
namespace MusicApp.Views;

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MusicApp.Controls;
using MusicApp.Core;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using MusicApp.Core.ViewModels;
using MusicApp.Helpers;
using Windows.ApplicationModel.Chat;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;
using Microsoft.UI;
using MusicApp.Native;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Windows.Graphics.Imaging;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

public partial class MainWindow : Window, IAppWindow
{
    private readonly CompositeDisposable disposable = [];
    private readonly ISettingsService settingsService;
    private readonly IFileService fileService;
    private readonly ISystemEventsService systemEventsService;
    private readonly IPlaybackService playbackService;

    private readonly WindowProc windowProc;

    public MainWindow(
        IAppService appService,
        ISettingsService settingsService,
        IFileService fileService,
        ISystemEventsService systemEventsService,
        IPlaybackService playbackService,
        PlayerViewModel playerViewModel,
        PlaylistViewModel playlistViewModel)
    {
        ArgumentNullException.ThrowIfNull(appService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(fileService);
        ArgumentNullException.ThrowIfNull(systemEventsService);
        ArgumentNullException.ThrowIfNull(playbackService);
        ArgumentNullException.ThrowIfNull(playerViewModel);
        ArgumentNullException.ThrowIfNull(playlistViewModel);

        this.settingsService = settingsService;
        this.fileService = fileService;
        this.systemEventsService = systemEventsService;
        this.playbackService = playbackService;

        AppService = appService;
        PlayerViewModel = playerViewModel;
        PlaylistViewModel = playlistViewModel;

        windowProc = new WindowProc(this);
        new TaskbarMediaButtons(this).DisposeWith(disposable);
        new TaskbarMediaCover(this).DisposeWith(disposable);

        MinimizeCommand = new RelayCommand(_ => this.Minimize());
        CloseCommand = new RelayCommand(_ => this.Close());
        SettingsCommand = new RelayCommand(_ => AppService.ShowSettings());

        this.InitializeComponent();

        ExtendsContentIntoTitleBar = true;

        var presenter = OverlappedPresenter.Create();
        presenter.PreferredMinimumWidth = 600;
        presenter.PreferredMaximumWidth = 800;
        presenter.PreferredMinimumHeight = 600;
        presenter.SetBorderAndTitleBar(true, false);

        AppWindow.SetPresenter(presenter);

        var icon = System.Drawing.Icon
            .ExtractAssociatedIcon(fileService.ApplicationPath)!
            .DisposeWith(disposable);
        AppWindow.SetIcon(Win32Interop.GetIconIdFromIcon(icon.Handle));
        AppWindow.Title = appService.AppInfo.ProductName;

        //  Closed += (_, _) => AppService.Exit();

        AppWindow.Resize(AppWindow.Size);

        Closed += OnClosed;
        InitSubscriptions();
    }

    public nint Handle => WindowNative.GetWindowHandle(this);

    public IAppSliderValueConverter TimeValueConverter { get; } = new SliderTimeValueConverter();

    public PlayerViewModel PlayerViewModel { get; }

    public PlaylistViewModel PlaylistViewModel { get; }

    public IAppService AppService { get; }

    public ICommand MinimizeCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand SettingsCommand { get; }

    public void Show()
    {
        AppWindow.Show(true);
    }

    private void InitSubscriptions()
    {
        if (SynchronizationContext.Current == null)
        {
            throw new InvalidOperationException("SynchronizationContext.Current can't be null");
        }

        Observable
            .CombineLatest(
                settingsService.WindowTheme,
                systemEventsService.AppDarkTheme,
                (theme, isSystemDark) => theme == WindowTheme.Dark || theme == WindowTheme.System && isSystemDark)
            .DistinctUntilChanged()
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(isDarkTheme => this.UpdateTheme(isDarkTheme))
            .DisposeWith(disposable);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        if (!disposable.IsDisposed)
        {
            disposable.Dispose();
        }

        AppService.Exit();
    }

    private void UpdateDragRectangles()
    {
        var scale = this.GetDpi() / 96d;

        AppWindow.TitleBar.SetDragRectangles([
            new Windows.Graphics.RectInt32(
                0,
                0,
                ((HeaderGrid.ActualWidth - WindowControlsPanel.ActualWidth) * scale).ToInt32(),
                (WindowControlsPanel.ActualHeight * scale).ToInt32()),
            new Windows.Graphics.RectInt32(
                0,
                (WindowControlsPanel.ActualHeight * scale).ToInt32(),
                (HeaderGrid.ActualWidth * scale).ToInt32(),
                ((HeaderGrid.ActualHeight - WindowControlsPanel.ActualHeight) * scale).ToInt32())
            ]);
    }

    private void OnGridLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDragRectangles();
    }

    private sealed class SliderTimeValueConverter : IAppSliderValueConverter
    {
        public string Convert(int value)
        {
            return Helpers.Converters.ToString(TimeSpan.FromSeconds(value));
        }
    }

    private void HeaderGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDragRectangles();
    }

    private void ListView_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: PlaylistItemViewModel model })
        {
            model.PlayCommand.Execute(model.MediaItem);
        }
    }

    private void ListView_KeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.OriginalSource is ListViewItem { Content: PlaylistItemViewModel model })
        {
            switch (e.OriginalKey)
            {
                case Windows.System.VirtualKey.Space:
                    model.PlayCommand.Execute(model.MediaItem);
                    break;

                case Windows.System.VirtualKey.Delete:
                    model.RemoveCommand.Execute(model.MediaItem);
                    break;
            }
        }
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        DragTarget.Visibility = Visibility.Visible;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        DragTarget.Visibility = Visibility.Collapsed;
    }

    private void OnDropped(object sender, EventArgs e)
    {
        DragTarget.Visibility = Visibility.Collapsed;
    }

    private sealed class TaskbarMediaButtons : IDisposable
    {
        private readonly CompositeDisposable disposable = [];

        private readonly MainWindow window;
        private readonly Taskbar taskbar;

        private readonly TaskbarButton previousButton, nextButton, togglePlayButton;
        private IconNative? previousIcon, nextIcon, playIcon, pauseIcon;

        public TaskbarMediaButtons(MainWindow window)
        {
            ArgumentNullException.ThrowIfNull(window);
            this.window = window;

            taskbar = new Taskbar(window.windowProc).DisposeWith(disposable);

            previousButton = taskbar.AddButton(nameof(previousButton));
            previousButton.ToolTip = "Previous Track";
            previousButton.Command = new RelayCommand(_ => window.playbackService.GoPrevious());

            togglePlayButton = taskbar.AddButton(nameof(togglePlayButton));
            togglePlayButton.Command = new RelayCommand(_ => window.playbackService.TogglePlayback());

            nextButton = taskbar.AddButton(nameof(nextButton));
            nextButton.ToolTip = "Next Track";
            nextButton.Command = new RelayCommand(_ => window.playbackService.GoNext());

            InitSubscriptions();
        }

        public void Dispose()
        {
            if (disposable.IsDisposed is false)
            {
                disposable.Dispose();
            }
        }

        private void InitSubscriptions()
        {
            if (SynchronizationContext.Current == null)
            {
                throw new InvalidOperationException("SynchronizationContext.Current can't be null");
            }

            Observable
                .CombineLatest(
                    window.systemEventsService.SystemDarkTheme,
                    window.systemEventsService.IconWidth,
                    window.systemEventsService.IconHeight,
                    (IsDarkTheme, IconWidth, IconHeight) => new { IsDarkTheme, IconWidth, IconHeight })
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(x => LoadIcons(x.IsDarkTheme, x.IconWidth, x.IconHeight))
                .DisposeWith(disposable);

            window.playbackService
                .State
                .Select(x => x == PlaybackState.Paused)
                .Throttle(TimeSpan.FromMilliseconds(150))
                .DistinctUntilChanged()
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(isPaused =>
                {
                    togglePlayButton.ToolTip = isPaused ? "Play" : "Pause";
                    togglePlayButton.Icon = isPaused ? playIcon?[7] : pauseIcon?[7];
                })
                .DisposeWith(disposable);

            window.playbackService
                .State
                .Select(x => x == PlaybackState.Paused || x == PlaybackState.Playing)
                .Throttle(TimeSpan.FromMilliseconds(150))
                .DistinctUntilChanged()
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(isActivePlayback => togglePlayButton.IsEnabled = isActivePlayback)
                .DisposeWith(disposable);

            window.playbackService
                .CanGoPrevious
                .Throttle(TimeSpan.FromMilliseconds(150))
                .DistinctUntilChanged()
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(canGoPrevious => previousButton.IsEnabled = canGoPrevious)
                .DisposeWith(disposable);

            window.playbackService
                .CanGoNext
                .Throttle(TimeSpan.FromMilliseconds(150))
                .DistinctUntilChanged()
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(canGoNext => nextButton.IsEnabled = canGoNext)
                .DisposeWith(disposable);
        }

        private async void LoadIcons(bool isDarkTheme, int iconWidth, int iconHeight)
        {
            previousIcon = Load(isDarkTheme ? "Dark.Previous" : "Light.Previous");
            nextIcon = Load(isDarkTheme ? "Dark.Next" : "Light.Next");
            playIcon = Load(isDarkTheme ? "Dark.Play" : "Light.Play");
            pauseIcon = Load(isDarkTheme ? "Dark.Pause" : "Light.Pause");

            var isPaused = await window.playbackService.State.FirstAsync() == PlaybackState.Paused;

            previousButton.Icon = previousIcon?.ResolveFrame(iconWidth, iconHeight);
            nextButton.Icon = nextIcon?.ResolveFrame(iconWidth, iconHeight);
            togglePlayButton.Icon = (isPaused ? playIcon : pauseIcon)?.ResolveFrame(iconWidth, iconHeight);
        }

        private IconNative Load(string name)
        {
            using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream($"MusicApp.Assets.{name}.ico");

            if (stream == null)
            {
                throw new InvalidOperationException($"Can't load {name} icon");
            }

            return new IconNative(stream);
        }
    }

    private sealed class TaskbarMediaCover : IDisposable
    {
        private readonly CompositeDisposable disposable = [];

        private readonly MainWindow window;
        private readonly Thumbnail thumbnail;

        private ImageData? imageData;

        public TaskbarMediaCover(MainWindow mainWindow)
        {
            ArgumentNullException.ThrowIfNull(mainWindow);

            this.window = mainWindow;

            thumbnail = new Thumbnail(window.windowProc);
            thumbnail.Preview += OnPreview;
            thumbnail.LivePreview += OnLivePreview;

            InitSubscriptions();
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
            window.LiveBorder.Visibility = Visibility.Visible;

            var renderTargetBitmap = new RenderTargetBitmap();
            await renderTargetBitmap.RenderAsync(window.Content);

            window.LiveBorder.Visibility = Visibility.Collapsed;

            var width = renderTargetBitmap.PixelWidth;
            var height = renderTargetBitmap.PixelHeight;

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb
            );

            var pixelData = (await renderTargetBitmap.GetPixelsAsync()).ToArray();

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

        public void Dispose()
        {
            if (disposable.IsDisposed is false)
            {
                disposable.Dispose();
            }
        }

        private ImageData? ImageData
        {
            get => imageData;
            set
            {
                imageData = value;
                thumbnail.Invalidate();
            }
        }

        private void InitSubscriptions()
        {
            if (SynchronizationContext.Current == null)
            {
                throw new InvalidOperationException("SynchronizationContext.Current can't be null");
            }

            window.playbackService
                .MediaItemCover
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(cover => ImageData = cover)
                .DisposeWith(disposable);

            Observable
                .CombineLatest(
                    window.playbackService.MediaItem,
                    window.playbackService.MediaItemCover,
                    window.playbackService.Position,
                    (x, y, z) => true)
                .Throttle(TimeSpan.FromMicroseconds(150))
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(_ => thumbnail.Invalidate())
                .DisposeWith(disposable);
        }
    }
}
