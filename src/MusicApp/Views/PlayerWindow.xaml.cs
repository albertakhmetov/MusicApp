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

using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using MusicApp.Core;
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using MusicApp.Helpers;
using MusicApp.Native;
using Windows.Foundation;
using WinRT.Interop;

public sealed partial class PlayerWindow : Window, IAppWindow
{
    private IDisposable themeSubscription;
    private System.Drawing.Icon icon;

    public PlayerWindow(
        IAppEnvironment appEnvironment,
        ISettingsService settingsService,
        ISystemEventsService systemEventsService)
    {
        if (SynchronizationContext.Current == null)
        {
            throw new InvalidOperationException("SynchronizationContext.Current can't be null");
        }

        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(systemEventsService);

        Procedure = new AppWindowProcedure(this);

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;

        var presenter = OverlappedPresenter.Create();
        presenter.PreferredMinimumWidth = 600;
        presenter.PreferredMaximumWidth = 800;
        presenter.PreferredMinimumHeight = 600;
        presenter.SetBorderAndTitleBar(true, false);

        AppWindow.SetPresenter(presenter);

        MinimizeCommand = new RelayCommand(_ => presenter.Minimize());
        CloseCommand = new RelayCommand(_ => Close());

        icon = System.Drawing.Icon.ExtractAssociatedIcon(appEnvironment.ApplicationFileInfo.FullName)!;
        AppWindow.SetIcon(Win32Interop.GetIconIdFromIcon(icon.Handle));

        AppWindow.Resize(AppWindow.Size);

        base.Closed += OnWindowClosed;

        themeSubscription = Observable
            .CombineLatest(
                settingsService.WindowTheme,
                systemEventsService.AppDarkTheme,
                (theme, isSystemDark) => theme == WindowTheme.Dark || theme == WindowTheme.System && isSystemDark)
            .DistinctUntilChanged()
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(isDarkTheme => this.UpdateTheme(isDarkTheme));
    }

    public nint Handle => WindowNative.GetWindowHandle(this);

    public IAppWindowProcedure Procedure { get; }

    public ICommand MinimizeCommand { get; }

    public ICommand CloseCommand { get; }

    public new event EventHandler? Closed;

    public void Hide()
    {
        AppWindow.Hide();
    }

    public void Show()
    {
        AppWindow.Show();
    }

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

    private void OnContentGridLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDragRectangles();
    }

    private void OnContentGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDragRectangles();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        themeSubscription.Dispose();

        Closed?.Invoke(this, EventArgs.Empty);

        icon.Dispose();
    }

    private void UpdateDragRectangles()
    {
        var scale = this.GetDpi() / 96d;

        AppWindow.TitleBar.SetDragRectangles([
            new Windows.Graphics.RectInt32(
                0,
                0,
                ((ContentGrid.ActualWidth - WindowControlsPanel.ActualWidth) * scale).ToInt32(),
                (WindowControlsPanel.ActualHeight * scale).ToInt32()),
            //new Windows.Graphics.RectInt32(
            //    0,
            //    (WindowControlsPanel.ActualHeight * scale).ToInt32(),
            //    (HeaderGrid.ActualWidth * scale).ToInt32(),
            //    ((HeaderGrid.ActualHeight - WindowControlsPanel.ActualHeight) * scale).ToInt32())
            ]);
    }
}
