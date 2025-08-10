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

using System;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MusicApp.Controls;
using MusicApp.Core;
using MusicApp.Core.Services;
using MusicApp.Core.ViewModels;

public sealed partial class PlayerView : UserControl
{
    private readonly ITaskbarMediaButtonsService taskbarMediaButtonsService;
    private readonly ITaskbarMediaCoverService taskbarMediaCoverService;

    public PlayerView(
        IApp app,
        IShellService shellService,
        PlayerViewModel playerViewModel,
        PlaylistViewModel playlistViewModel,
        ITaskbarMediaButtonsService taskbarMediaButtonsService,
        ITaskbarMediaCoverService taskbarMediaCoverService)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(shellService);
        ArgumentNullException.ThrowIfNull(playerViewModel);
        ArgumentNullException.ThrowIfNull(playlistViewModel);
        ArgumentNullException.ThrowIfNull(taskbarMediaCoverService);

        this.taskbarMediaButtonsService = taskbarMediaButtonsService;
        this.taskbarMediaCoverService = taskbarMediaCoverService;

        ShellService = shellService;
        PlayerViewModel = playerViewModel;
        PlaylistViewModel = playlistViewModel;

        SettingsCommand = new RelayCommand(_ => app.GetWindow<SettingsViewModel>().Show());

        InitializeComponent();
    }

    public IAppSliderValueConverter TimeValueConverter { get; } = new SliderTimeValueConverter();

    public IShellService ShellService { get; }

    public PlayerViewModel PlayerViewModel { get; }

    public PlaylistViewModel PlaylistViewModel { get; }

    public ICommand SettingsCommand { get; }

    private void ListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: PlaylistItemViewModel model })
        {
            model.PlayCommand.Execute(model.MediaItem);
        }
    }

    private void ListView_KeyUp(object sender, KeyRoutedEventArgs e)
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


    private sealed class SliderTimeValueConverter : IAppSliderValueConverter
    {
        public string Convert(int value)
        {
            return Helpers.Converters.ToString(TimeSpan.FromSeconds(value));
        }
    }
}
