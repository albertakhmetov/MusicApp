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
namespace MusicApp;

using System;
using System.Collections.Immutable;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using MusicApp.Views;
using MusicApp.Core.Commands;
using MusicApp.Core.Services;
using MusicApp.Core.ViewModels;
using MusicApp.Services;
using WinRT.Interop;
using MusicApp.Core.Models;
using System.Runtime.InteropServices;
using MusicApp.Helpers;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System.Diagnostics;
using Windows.Win32;
using System.Web;
using System.Xml.Schema;
using MusicApp.Core;
using System.Reactive.Disposables;

public partial class App : Application
{
    private readonly CompositeDisposable disposable = [];

    private readonly ISettingsService settingsService;
    private readonly IFileService fileService;
    private readonly IPlaylistService playlistService;
    private readonly IAppCommandManager appCommandManager;

    private readonly ILazyDependency<ISingleInstanceService> singleInstanceService;
    private readonly ILazyDependency<ITaskbarMediaButtonsService> taskbarMediaButtons;
    private readonly ILazyDependency<ITaskbarMediaCoverService> taskbarMediaCover;
    private readonly ILazyDependency<IAppWindow> appWindow;

    private readonly ILogger<App> logger;

    public App(
        ISettingsService settingsService,
        IFileService fileService,
        IPlaylistService playlistService,
        IAppCommandManager appCommandManager,
        ILogger<App> logger,
        ILazyDependency<ISingleInstanceService> singleInstanceService,
        ILazyDependency<ITaskbarMediaButtonsService> taskbarMediaButtons,
        ILazyDependency<ITaskbarMediaCoverService> taskbarMediaCover,
        [FromKeyedServices("Main")] ILazyDependency<IAppWindow> appWindow)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(fileService);
        ArgumentNullException.ThrowIfNull(playlistService);
        ArgumentNullException.ThrowIfNull(appCommandManager);
        ArgumentNullException.ThrowIfNull(singleInstanceService);
        ArgumentNullException.ThrowIfNull(taskbarMediaButtons);
        ArgumentNullException.ThrowIfNull(taskbarMediaCover);
        ArgumentNullException.ThrowIfNull(appWindow);

        this.settingsService = settingsService;
        this.fileService = fileService;
        this.playlistService = playlistService;
        this.appCommandManager = appCommandManager;
        this.logger = logger;

        this.singleInstanceService = singleInstanceService;
        this.taskbarMediaButtons = taskbarMediaButtons;
        this.taskbarMediaCover = taskbarMediaCover;
        this.appWindow = appWindow;

        InitializeComponent();

        var theme = this.settingsService.WindowTheme.Value;

        switch (theme)
        {
            case WindowTheme.Dark:
                RequestedTheme = ApplicationTheme.Dark;
                break;

            case WindowTheme.Light:
                RequestedTheme = ApplicationTheme.Light;
                break;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs _)
    {
        base.OnLaunched(_);

        try
        {
            var windowDisposable = new CompositeDisposable {
                singleInstanceService.Resolve(),
                taskbarMediaButtons.Resolve(),
                taskbarMediaCover.Resolve()
            };          
            
            var window = appWindow.Resolve();
            window.Closed += (_, _) =>
            {
                windowDisposable.Dispose();
            };

            window.Show();

            var args = Environment.GetCommandLineArgs();
            logger.LogInformation("Args count: {length}", args.Length);
            foreach (var arg in args)
            {
                logger.LogInformation("\t{arg}", arg);
            }

            if (args.Length > 1)
            {
                LoadMediaItems(args[1]);
            }
            else
            {
                playlistService.Load();
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "OnLaunched exception");
        }
    }

    private async void LoadMediaItems(string path)
    {
        var items = await fileService.LoadMediaItems([path]);

        await appCommandManager.ExecuteAsync(new MediaItemAddCommand.Parameters
        {
            Overwrite = true,
            Items = items.ToImmutableArray()
        });
    }
}