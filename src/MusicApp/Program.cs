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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
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
        if (IsFirstInstance(args) is false)
        {
            return;
        }

        PInvoke.SetCurrentProcessExplicitAppUserModelID(IAppService.AppUserModelID);

        var appDirectory = Path.GetDirectoryName(IFileService.ApplicationPath);
        if (appDirectory is not null)
        {
            Directory.SetCurrentDirectory(appDirectory);
        }
        else
        {
            return;
        }

        using var host = AppHost.Build(CreateHost);
        host.RunAsync();
    }

    private static bool IsFirstInstance(string[] args)
    {
        appInstance = AppInstance.FindOrRegisterForKey(IAppService.AppUserModelID);

        if (appInstance.IsCurrent is false)
        {
            var data = string.Join(Environment.NewLine, args);

            var process = Process.GetProcessById((int)appInstance.ProcessId);

            while (process.MainWindowHandle == IntPtr.Zero)
            {
                Task.Delay(TimeSpan.FromMilliseconds(1000)).Wait();
            }

            var processWindowHandle = process.MainWindowHandle;

            SingleInstance.Send((HWND)processWindowHandle, data);

            var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            appInstance
                .RedirectActivationToAsync(activatedEventArgs)
                .AsTask()
                .Wait(TimeSpan.FromSeconds(5));

            return false;
        }
        else
        {
            appInstance.Activated += OnAppInstanceActivated;

            return true;
        }
    }

    private static void OnAppInstanceActivated(object? sender, AppActivationArguments e)
    {
        if (appInstance is not null)
        {
            var process = Process.GetProcessById((int)appInstance!.ProcessId);
            PInvoke.SetForegroundWindow((HWND)process.MainWindowHandle);
        }
    }

    private static readonly string InitializationMutexName = $"{IAppService.AppUserModelID}.InitializationMutex";
    private static AppInstance? appInstance;

    private static void CreateHost(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddNLog();
        });

        services.AddSingleton<App>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<SettingsWindow>();

        services.AddSingleton<IAppService, AppService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<ISingleInstanceService, SingleInstanceService>();
        services.AddSingleton<ITaskbarMediaButtonsService, TaskbarMediaButtonsService>();
        services.AddSingleton<ITaskbarMediaCoverService, TaskbarMediaCoverService>();

        services.AddLazySingleton<ISingleInstanceService>();
        services.AddLazySingleton<ITaskbarMediaButtonsService>();
        services.AddLazySingleton<ITaskbarMediaCoverService>();

        services.AddKeyedSingleton<IAppWindow>("Main", (sp, _) => sp.GetRequiredService<MainWindow>());
        services.AddKeyedSingleton<IAppWindow>("Settings", (sp, _) => sp.GetRequiredService<SettingsWindow>());

        services.AddLazyKeyedSingleton<IAppWindow>("Main");
        services.AddLazyKeyedSingleton<IAppWindow>("Settings");

        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<ISystemEventsService, SystemEventsService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        services.AddSingleton<IPlaybackService, PlaybackService>();
        services.AddSingleton<IPlaylistService, PlaylistService>();

        services.AddSingleton<IAppCommandManager, AppCommandManager>();
        services.AddTransient<IAppCommand<MediaItemAddCommand.Parameters>, MediaItemAddCommand>();
        services.AddTransient<IAppCommand<MediaItemRemoveCommand.Parameters>, MediaItemRemoveCommand>();

        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<PlaylistViewModel>();
        services.AddSingleton<SettingsViewModel>();
    }



}
