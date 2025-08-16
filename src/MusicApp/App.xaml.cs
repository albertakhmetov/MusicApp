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
using System.Reactive.Linq;
using MusicApp.Core.Helpers;
using System.Windows.Navigation;
using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.ApplicationModel.Chat;

public partial class App : Application
{
    private readonly IHostApplicationLifetime lifetime;
    private readonly ISettingsService settingsService;
    private readonly IInstanceService instanceService;
    private readonly IWindowService windowService;
    private readonly ILogger<App> logger;

    private IAppWindow? mainWindow;

    public App(
        IHostApplicationLifetime lifetime,
        ISettingsService settingsService,
        IInstanceService instanceService,
        IWindowService windowService,
        ILogger<App> logger)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(instanceService);
        ArgumentNullException.ThrowIfNull(windowService);
        ArgumentNullException.ThrowIfNull(logger);

        this.lifetime = lifetime;
        this.settingsService = settingsService;
        this.instanceService = instanceService;
        this.windowService = windowService;
        this.logger = logger;

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

    protected override async void OnLaunched(LaunchActivatedEventArgs _)
    {
        try
        {
            base.OnLaunched(_);

            logger.LogInformation("Creating main window");

            mainWindow = await windowService.GetWindowAsync<PlayerViewModel>();
            mainWindow.Show();
            mainWindow.Closed += (_, _) => lifetime.StopApplication();

            var args = Environment.GetCommandLineArgs();

            logger.LogInformation("Arguments count (with the app path): {Count}", args.Length);

            await instanceService.StartAsync(args.Skip(1));
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "OnLaunched exception");
        }
    }
}