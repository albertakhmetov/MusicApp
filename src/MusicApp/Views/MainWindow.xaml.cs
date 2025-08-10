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

using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Microsoft.Extensions.Hosting;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MusicApp.Controls;
using MusicApp.Core;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using MusicApp.Core.ViewModels;
using MusicApp.Helpers;
using MusicApp.Native;
using WinRT.Interop;

public partial class MainWindow : Window, IAppWindow
{
    private readonly CompositeDisposable disposable = [];
    private readonly ISettingsService settingsService;
    private readonly ISystemEventsService systemEventsService;
    private readonly IHostApplicationLifetime lifetime;

    public MainWindow(
        IHostApplicationLifetime lifetime,
        IAppService appService,
        IShellService shellService,
        ISettingsService settingsService,
        ISystemEventsService systemEventsService,
        PlayerViewModel playerViewModel,
        PlaylistViewModel playlistViewModel)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(appService);
        ArgumentNullException.ThrowIfNull(shellService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(systemEventsService);
        ArgumentNullException.ThrowIfNull(playerViewModel);
        ArgumentNullException.ThrowIfNull(playlistViewModel);

        this.lifetime = lifetime;
        this.settingsService = settingsService;
        this.systemEventsService = systemEventsService;

        AppService = appService;
        ShellService = shellService;
        PlayerViewModel = playerViewModel;
        PlaylistViewModel = playlistViewModel;

        Procedure = new AppWindowProcedure(this);

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
            .ExtractAssociatedIcon(IFileService.ApplicationPath)!
            .DisposeWith(disposable);
        AppWindow.SetIcon(Win32Interop.GetIconIdFromIcon(icon.Handle));
        AppWindow.Title = appService.AppInfo.ProductName;

        //  Closed += (_, _) => AppService.Exit();

        AppWindow.Resize(AppWindow.Size);
        AppWindow.Closing += OnAppWindowClosing;
        base.Closed += OnClosed;

        InitSubscriptions();
    }

    public nint Handle => WindowNative.GetWindowHandle(this);

    public IAppWindowProcedure Procedure { get; }

    public IAppSliderValueConverter TimeValueConverter { get; } = new SliderTimeValueConverter();

    public PlayerViewModel PlayerViewModel { get; }

    public PlaylistViewModel PlaylistViewModel { get; }

    public IAppService AppService { get; }

    public IShellService ShellService { get; }

    public ICommand MinimizeCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand SettingsCommand { get; }

    public event CancelEventHandler? Closing;

    public new event EventHandler? Closed;

    public void Show(bool activateWindow = true)
    {
        AppWindow.Show(activateWindow);
    }

    public void Hide() => AppWindow.Hide();

    public async Task<WindowCaptureData?> Capture()
    {
        LiveBorder.Visibility = Visibility.Visible;

        try
        {
            var renderTargetBitmap = new RenderTargetBitmap();
            await renderTargetBitmap.RenderAsync(Content);

            var pixels = await renderTargetBitmap.GetPixelsAsync();

            return new WindowCaptureData
            {
                Width = renderTargetBitmap.PixelWidth,
                Height = renderTargetBitmap.PixelHeight,
                Pixels = pixels.ToArray()
            };
        }
        finally
        {
            LiveBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        var e = new CancelEventArgs();
        Closing?.Invoke(this, e);

        args.Cancel = e.Cancel;
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
        Closed?.Invoke(this, EventArgs.Empty);

        if (!disposable.IsDisposed)
        {
            disposable.Dispose();
        }

        lifetime.StopApplication();
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
}
