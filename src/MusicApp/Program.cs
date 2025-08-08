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

        XamlCheckProcessRequirements();
        WinRT.ComWrappersSupport.InitializeComWrappers();

        using var host = CreateHost();
        var logger = host.Services.GetRequiredService<ILogger<App>>();

        try
        {
            using var sm = new SemaphoreSlim(0);

            _ = host.RunAsync();

            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStarted.Register(() => sm.Release());

            if (sm.Wait(TimeSpan.FromSeconds(5)))
            {
                Application.Start(_ =>
                {
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);

                    var instance = host.Services.GetRequiredService<App>();
                    instance.UnhandledException += (_, e) => logger.LogCritical(e.Exception, "Unhandled Exception");
                });

                lifetime.StopApplication();
            }
            else
            {
                logger.LogCritical("Startup timeout");
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Initialization Exception");
        }
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

    private static IHost CreateHost()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddNLog();
        });

        builder.Services.AddSingleton<App>();
        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddSingleton<SettingsWindow>();

        builder.Services.AddSingleton<IAppService, AppService>();
        builder.Services.AddSingleton<IShellService, ShellService>();
        builder.Services.AddSingleton<ISingleInstanceService, SingleInstanceService>();
        builder.Services.AddSingleton<ITaskbarMediaButtonsService, TaskbarMediaButtonsService>();
        builder.Services.AddSingleton<ITaskbarMediaCoverService, TaskbarMediaCoverService>();

        builder.Services.AddLazySingleton<ISingleInstanceService>();
        builder.Services.AddLazySingleton<ITaskbarMediaButtonsService>();
        builder.Services.AddLazySingleton<ITaskbarMediaCoverService>();

        builder.Services.AddKeyedSingleton<IAppWindow>("Main", (sp, _) => sp.GetRequiredService<MainWindow>());
        builder.Services.AddKeyedSingleton<IAppWindow>("Settings", (sp, _) => sp.GetRequiredService<SettingsWindow>());

        builder.Services.AddLazyKeyedSingleton<IAppWindow>("Main");
        builder.Services.AddLazyKeyedSingleton<IAppWindow>("Settings");

        builder.Services.AddSingleton<IFileService, FileService>();
        builder.Services.AddSingleton<ISystemEventsService, SystemEventsService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();

        builder.Services.AddSingleton<IPlaybackService, PlaybackService>();
        builder.Services.AddSingleton<IPlaylistService, PlaylistService>();

        builder.Services.AddSingleton<IAppCommandManager, AppCommandManager>();
        builder.Services.AddTransient<IAppCommand<MediaItemAddCommand.Parameters>, MediaItemAddCommand>();
        builder.Services.AddTransient<IAppCommand<MediaItemRemoveCommand.Parameters>, MediaItemRemoveCommand>();

        builder.Services.AddSingleton<PlayerViewModel>();
        builder.Services.AddSingleton<PlaylistViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();

        return builder.Build();
    }


    [DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();
}
