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
using Microsoft.Extensions.Hosting;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.UI.Shell;
using Microsoft.Windows.AppLifecycle;
using MusicApp.Core;

internal class AppService : IAppService
{
    private readonly ILazyDependency<IAppWindow> appWindow;
    private IAppWindow? settingsWindow;

    public AppService([FromKeyedServices("Settings")] ILazyDependency<IAppWindow> appWindow)
    {
        ArgumentNullException.ThrowIfNull(appWindow);

        this.appWindow = appWindow;

        AppInfo = LoadAppInfo();
    }

    public AppInfo AppInfo { get; }

    public async Task ShowSettings()
    {
        if (settingsWindow is null)
        {
            settingsWindow = appWindow.Resolve();
            settingsWindow.Closing += OnSettingsWindowClosing;

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        settingsWindow.Show(true);
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

    private void OnSettingsWindowClosing(object? sender, CancelEventArgs args)
    {
        args.Cancel = true;

        (sender as IAppWindow)?.Hide();
    }
}
