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
using MusicApp.Service;
using MusicApp.Core.Models;

public partial class App : Application
{
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);

            instance = new App(args);
        });

        instance?.host?.StopAsync().Wait();
        instance?.host?.Dispose();
    }

    private static App? instance;

    private readonly ImmutableArray<string> arguments;
    private IHost? host;
    private IAppWindow? mainWindow;

    public App(string[] args)
    {
        host = CreateHost();
        this.arguments = ImmutableArray.Create(args);

        InitializeComponent();

        var theme = host.Services.GetRequiredService<ISettingsService>().WindowTheme.Value;

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

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        mainWindow = host.Services.GetRequiredKeyedService<IAppWindow>("Main");
        mainWindow.Show();

        _ = host.RunAsync();
    }

    private IHost CreateHost()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddHostedService<KeeperService>();

        builder.Services.AddSingleton<IAppService, AppService>();

        builder.Services.AddKeyedSingleton<IAppWindow, MainWindow>("Main");
        builder.Services.AddSingleton<SettingsWindow>();

        builder.Services.AddSingleton<IFileService, FileService>();
        builder.Services.AddSingleton<ISystemEventsService, SystemEventsService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();

        builder.Services.AddSingleton<IPlaybackService, PlaybackService>();

        builder.Services.AddSingleton<IAppCommandManager, AppCommandManager>();
        builder.Services.AddTransient<IAppCommand<MediaItemAddCommand.Parameters>, MediaItemAddCommand>();
        builder.Services.AddTransient<IAppCommand<MediaItemRemoveCommand.Parameters>, MediaItemRemoveCommand>();

        builder.Services.AddSingleton<PlayerViewModel>();
        builder.Services.AddSingleton<PlaylistViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();

        return builder.Build();
    }
}