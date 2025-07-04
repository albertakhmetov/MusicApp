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
namespace MusicApp.Services;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using MusicApp.Core.ViewModels;
using MusicApp.Helpers;
using MusicApp.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

internal class AppService : IAppService
{
    private readonly IServiceProvider serviceProvider;

    private SettingsWindow? settingsWindow;

    public AppService(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        this.serviceProvider = serviceProvider;

        AppInfo = LoadAppInfo();
    }

    public AppInfo AppInfo { get; }

    public IImmutableList<FileType> SupportedFileTypes { get; } = [FileType.Mp3];

    public bool IsFileSupported(string? fileName)
    {
        var ext = Path.GetExtension(fileName);

        return ext is not null && SupportedFileTypes.Any(x => x.Equals(ext));
    }

    public async Task ShowSettings()
    {
        if (settingsWindow is null)
        {
            settingsWindow = serviceProvider.GetRequiredService<SettingsWindow>();
            settingsWindow.AppWindow.Closing += OnSettingsWindowClosing;

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        settingsWindow.AppWindow.Show(true);
    }

    public void Exit()
    {
        App.Current.Exit();
    }

    private AppInfo LoadAppInfo()
    {
        var info = FileVersionInfo.GetVersionInfo(typeof(AppService).Assembly.Location);

        return new AppInfo
        {
            ProductName = info.ProductName ?? "MusicApp",
            ProductVersion = info.ProductVersion,
            ProductDescription = info.Comments,
            LegalCopyright = info.LegalCopyright,
            FileVersion = new Version(
                info.FileMajorPart,
                info.FileMinorPart,
                info.FileBuildPart,
                info.FilePrivatePart),

            IsPreRelease = Regex.IsMatch(info.ProductVersion ?? "", "[a-zA-Z]")
        };
    }

    private void OnSettingsWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;

        sender.Hide();
    }
}
