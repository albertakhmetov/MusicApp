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

internal class AppService : IAppService
{
    private readonly IServiceProvider serviceProvider;

    private readonly BehaviorSubject<bool> isAppRegistedSubject;
    private SettingsWindow? settingsWindow;

    public AppService(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        this.serviceProvider = serviceProvider;

        AppInfo = LoadAppInfo();
        SupportedFileTypes = [
            new FileType { Description = "MP3 Music File", Extension = ".mp3" }
        ];

        isAppRegistedSubject = new BehaviorSubject<bool>(GetAppRegistrationState());
        IsAppRegisted = isAppRegistedSubject.AsObservable();
    }

    public AppInfo AppInfo { get; }

    public IImmutableList<FileType> SupportedFileTypes { get; }

    public IObservable<bool> IsAppRegisted { get; }

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
        serviceProvider.GetRequiredService<IHostApplicationLifetime>().StopApplication();
    }

    public void SetAppRegistrationState(bool isAppRegisted)
    {
        if(isAppRegisted)
        {
            RegisterApp();
        }
        else
        {
            UnregisterApp();
        }
    }

    private void RegisterApp()
    {
        var appPath = serviceProvider.GetRequiredService<IFileService>().ApplicationPath;
        var resourcePath = Path.Combine(Path.GetDirectoryName(appPath)!, $"{AppInfo.ProductName}.Resources.dll");
        var appFileName = Path.GetFileName(appPath);

        using var capabilitiesKey = Registry.CurrentUser.CreateSubKey($@"Software\{AppInfo.ProductName}\Capabilities");
        capabilitiesKey.SetValue("ApplicationName", AppInfo.ProductName);
        capabilitiesKey.SetValue("ApplicationDescription", AppInfo.ProductDescription ?? "");
        capabilitiesKey.SetValue("ApplicationIcon", $"{resourcePath},0");

        foreach (var type in SupportedFileTypes)
        {
            var fileExtension = $"{AppInfo.ProductName}{type.Extension}";

            using var fileKey = capabilitiesKey.CreateSubKey("FileAssociations");
            fileKey.SetValue(type.Extension, fileExtension);

            using var fileExtensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{fileExtension}");
            {
                fileExtensionKey.SetValue("", type.Description);

                using var fileExtensionIconKey = fileExtensionKey.CreateSubKey("DefaultIcon");
                fileExtensionIconKey.SetValue("", $"{resourcePath},1");

                using var fileExtensionCommandKey = fileExtensionKey.CreateSubKey("shell\\open\\command");
                fileExtensionCommandKey.SetValue("", $"\"{appPath}\" \"%1\"");
            }
        }

        using var registeredAppsKey = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications");
        registeredAppsKey.SetValue(AppInfo.ProductName, $@"Software\{AppInfo.ProductName}\Capabilities");

        using var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{appFileName}");
        appKey.SetValue("", appPath);

        unsafe
        {
            PInvoke.SHChangeNotify(SHCNE_ID.SHCNE_ASSOCCHANGED, 0x0000, null, null);
        }

        isAppRegistedSubject.OnNext(GetAppRegistrationState());
    }

    private void UnregisterApp()
    {
        var appPath = serviceProvider.GetRequiredService<IFileService>().ApplicationPath;
        var appFileName = Path.GetFileName(appPath);

        foreach (var type in SupportedFileTypes)
        {
            var fileExtension = $"{AppInfo.ProductName}{type.Extension}";
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{fileExtension}", false);
        }

        using (var registeredAppsKey = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
        {
            registeredAppsKey?.DeleteValue(AppInfo.ProductName, false);
        }

        Registry.CurrentUser.DeleteSubKeyTree($@"Software\{AppInfo.ProductName}", false);

        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{appFileName}", false);

        unsafe
        {
            PInvoke.SHChangeNotify(SHCNE_ID.SHCNE_ASSOCCHANGED, 0x0000, null, null);
        }

        isAppRegistedSubject.OnNext(GetAppRegistrationState());
    }

    private bool GetAppRegistrationState()
    {
        using (var registeredAppsKey = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications"))
        {
            return registeredAppsKey?.GetValue(AppInfo.ProductName) is not null;
        }
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
