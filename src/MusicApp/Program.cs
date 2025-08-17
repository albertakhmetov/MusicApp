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
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using MusicApp.Core;
using MusicApp.Core.Commands;
using MusicApp.Core.Services;
using MusicApp.Core.ViewModels;
using MusicApp.Native;
using MusicApp.Services;
using MusicApp.Views;
using NLog.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (InstanceService.IsFirstInstance)
        {
            PInvoke.SetCurrentProcessExplicitAppUserModelID(IShellService.AppUserModelID);

            var environment = new AppEnvironment();
            Directory.SetCurrentDirectory(environment.ApplicationDirectoryInfo.FullName);

            using var host = AppHost.Build(environment, CreateHost);
            host.RunAsync();
        }
        else
        {
            InstanceService.ActivateFirstInstance(args).GetAwaiter().GetResult();
        }
    }

    private static void CreateHost(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddNLog();
        });

        services.AddSingleton<App>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IInstanceService, InstanceService>();

        services.AddSingleton<ISystemEventsService, SystemEventsService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        services.AddSingleton<IPlaybackService, PlaybackService>();
        services.AddSingleton<IPlaylistStorageService, PlaylistStorageService>();
        services.AddSingleton<IMetadataService, MetadataService>();

        services.AddScoped<ScopeDataService>();
        services.AddScoped<IAppService, AppService>();

        services.AddKeyedScoped<Window, PlayerWindow>(nameof(PlayerViewModel));
        services.AddKeyedScoped<Window, SettingsWindow>(nameof(SettingsViewModel));

        services.AddScoped(sp => sp.GetRequiredService<ScopeDataService>().AppWindow);

        services.AddKeyedScoped<IAppWindowService, TaskbarMediaButtonsService>(nameof(PlayerViewModel));
        services.AddKeyedScoped<IAppWindowService, TaskbarMediaCoverService>(nameof(PlayerViewModel));

        services.AddKeyedScoped<UserControl, PlayerView>(nameof(PlayerViewModel));
        services.AddKeyedScoped<UserControl, SettingsView>(nameof(SettingsViewModel));

        services.AddScoped<PlayerViewModel>();
        services.AddScoped<PlaylistViewModel>();
        services.AddScoped<SettingsViewModel>();

        services.AddScoped<IAppCommandManager, AppCommandManager>();
        services.AddTransient<IAppCommand<MediaItemAddCommand.Parameters>, MediaItemAddCommand>();
        services.AddTransient<IAppCommand<MediaItemRemoveCommand.Parameters>, MediaItemRemoveCommand>();
    }
}
