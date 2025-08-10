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

public partial class App : Application, IDisposable, IApp
{
    private readonly CompositeDisposable disposable = [];

    private readonly Dictionary<string, IServiceScope> windowScopes = [];

    private readonly IServiceProvider serviceProvider;
    private readonly ISettingsService settingsService;
    private readonly ISystemEventsService systemEventsService;
    private readonly IHostApplicationLifetime lifetime;
    private readonly ILogger<App> logger;

    private IAppWindow? mainWindow;

    public App(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        this.serviceProvider = serviceProvider;

        settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        systemEventsService = serviceProvider.GetRequiredService<ISystemEventsService>();
        lifetime = serviceProvider.GetRequiredService<IHostApplicationLifetime>();
        logger = serviceProvider.GetRequiredService<ILogger<App>>();

        Info = GetAppInfo();

        InitializeComponent();

        var theme = settingsService.WindowTheme.Value;

        switch (theme)
        {
            case WindowTheme.Dark:
                RequestedTheme = ApplicationTheme.Dark;
                break;

            case WindowTheme.Light:
                RequestedTheme = ApplicationTheme.Light;
                break;
        }

        InitSubscriptions();
    }

    public AppInfo Info { get; }

    public IAppWindow GetWindow<T>() where T : ViewModel
    {
        var viewModelName = typeof(T).Name;

        if (windowScopes.TryGetValue(viewModelName, out var scope) is false)
        {
            scope = serviceProvider.CreateScope();

            var window = scope.ServiceProvider.GetRequiredKeyedService<Window>(viewModelName);
            window.Title = Info.ProductName;
            window.Closed += OnWindowClosed;

            var scopeData = scope.ServiceProvider.GetRequiredService<ScopeDataService>();
            scopeData.Init((IAppWindow)window);

            windowScopes.Add(viewModelName, scope);

            var view = scope.ServiceProvider.GetRequiredKeyedService<UserControl>(viewModelName);

            if (window.Content is Grid grid)
            {
                grid.Children.Insert(0, view);

                if (window.AppWindow.Presenter is OverlappedPresenter presenter && presenter.HasTitleBar)
                {
                    Grid.SetRow(view, 1);
                }
            }
            else
            {
                window.Content = view;
            }
        }

        return scope.ServiceProvider.GetRequiredService<ScopeDataService>().Window;
    }

    public void Dispose()
    {
        if (disposable.IsDisposed is false)
        {
            disposable.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        const string windowSuffix = "Window";
        var name = sender?.GetType().Name;

        if (sender is Window window && name?.EndsWith(windowSuffix) is true)
        {
            var viewModelName = $"{name.Substring(0, name.Length - windowSuffix.Length)}ViewModel";

            if (windowScopes.TryGetValue(viewModelName, out var scope))
            {
                window.Closed -= OnWindowClosed;
                scope.Dispose();

                windowScopes.Remove(viewModelName);
            }
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs _)
    {
        base.OnLaunched(_);

        mainWindow = GetWindow<PlayerViewModel>();
        mainWindow.Show();
        mainWindow.Closed += (_, _) => lifetime.StopApplication();

        //try
        //{
        //    var windowDisposable = new CompositeDisposable {
        //        singleInstanceService.Resolve(),
        //        taskbarMediaButtons.Resolve(),
        //        taskbarMediaCover.Resolve()
        //    };

        //    var window = appWindow.Resolve();
        //    window.Closed += (_, _) =>
        //    {
        //        windowDisposable.Dispose();
        //    };

        //    window.Show();

        //    var args = Environment.GetCommandLineArgs();
        //    logger.LogInformation("Args count: {length}", args.Length);
        //    foreach (var arg in args)
        //    {
        //        logger.LogInformation("\t{arg}", arg);
        //    }

        //    if (args.Length > 1)
        //    {
        //        LoadMediaItems(args[1]);
        //    }
        //    else
        //    {
        //        playlistService.Load();
        //    }
        //}
        //catch (Exception ex)
        //{
        //    logger.LogCritical(ex, "OnLaunched exception");
        //}
    }

    //private async void LoadMediaItems(string path)
    //{
    //    var items = await fileService.LoadMediaItems([path]);

    //    await appCommandManager.ExecuteAsync(new MediaItemAddCommand.Parameters
    //    {
    //        Overwrite = true,
    //        Items = items.ToImmutableArray()
    //    });
    //}

    private void InitSubscriptions()
    {
        if (SynchronizationContext.Current == null)
        {
            throw new InvalidOperationException("SynchronizationContext.Current can't be null");
        }

        Observable
            .CombineLatest(
                settingsService.WindowTheme,
                systemEventsService.AppDarkTheme,
                (theme, isSystemDark) => theme == WindowTheme.Dark || theme == WindowTheme.System && isSystemDark)
            .DistinctUntilChanged()
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(isDarkTheme => UpdateTheme(isDarkTheme))
            .DisposeWith(disposable);
    }

    private void UpdateTheme(bool isDarkTheme)
    {
        foreach (var scope in windowScopes.Values)
        {
            if (scope.ServiceProvider.GetRequiredService<IAppWindow>() is Window window)
            {
                window.UpdateTheme(isDarkTheme);
            }
        }
    }

    private static AppInfo GetAppInfo()
    {
        var info = FileVersionInfo.GetVersionInfo(typeof(App).Assembly.Location);
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

    public sealed class WindowData
    {
        public required Window Window { get; init; }

        public required IServiceScope Scope { get; init; }

        public IServiceProvider ServiceProvider => Scope.ServiceProvider;
    }
}