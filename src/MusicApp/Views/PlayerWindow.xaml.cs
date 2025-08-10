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
using System.Windows.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MusicApp.Core;
using MusicApp.Core.Helpers;
using MusicApp.Helpers;
using WinRT.Interop;

public sealed partial class PlayerWindow : Window, IAppWindow
{
    public PlayerWindow()
    {
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

        AppWindow.Resize(AppWindow.Size);

        base.Closed += OnWindowClosed;
    }

    public nint Handle => WindowNative.GetWindowHandle(this);

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
        Closed?.Invoke(this, EventArgs.Empty);
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
