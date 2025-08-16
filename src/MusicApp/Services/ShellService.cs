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
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using MusicApp.Core;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using Windows.Win32;
using Windows.Win32.UI.Shell;

internal class ShellService : IShellService
{
    private readonly BehaviorSubject<bool> isAppRegistedSubject;

    private readonly string productName, productDescription;
    private readonly string appPath, appFileName, resourcePath;

    public ShellService()
    {
        Info = GetAppInfo();

        productName = Info.ProductName;
        productDescription = Info.ProductDescription ?? "";

        appPath = IShellService.ApplicationPath;
        appFileName = Path.GetFileName(appPath);
        resourcePath = Path.Combine(
            $"{Path.GetDirectoryName(appPath)}",
            $"{Path.GetFileNameWithoutExtension(appPath)}.Resources.dll");

        SupportedFileTypes = [
            new FileType { Description = "MP3 Music File", Extension = ".mp3" }
        ];

        isAppRegistedSubject = new BehaviorSubject<bool>(IsAppRegisted());
        IsRegistred = isAppRegistedSubject.AsObservable();
    }

    public AppInfo Info { get; }

    public IImmutableList<FileType> SupportedFileTypes { get; }

    public IObservable<bool> IsRegistred { get; }

    public bool IsFileSupported(string? fileName)
    {
        var ext = Path.GetExtension(fileName);

        return ext is not null && SupportedFileTypes.Any(x => x.Equals(ext));
    }

    public void Register()
    {
        using var capabilitiesKey = Registry.CurrentUser.CreateSubKey($@"Software\{productName}\Capabilities");
        capabilitiesKey.SetValue("ApplicationName", productName);
        capabilitiesKey.SetValue("ApplicationDescription", productDescription);
        capabilitiesKey.SetValue("ApplicationIcon", $"{resourcePath},0");

        foreach (var type in SupportedFileTypes)
        {
            var fileExtension = $"{productName}{type.Extension}";

            using var fileKey = capabilitiesKey.CreateSubKey("FileAssociations");
            fileKey.SetValue(type.Extension, fileExtension);

            using var fileExtensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{fileExtension}");
            {
                fileExtensionKey.SetValue("", type.Description);

                using var fileExtensionIconKey = fileExtensionKey.CreateSubKey("DefaultIcon");
                fileExtensionIconKey.SetValue("", $"{resourcePath},1");

                using var fileExtensionVerbKey = fileExtensionKey.CreateSubKey("shell\\Open");
                fileExtensionVerbKey.SetValue("MultiSelectModel", "Player");

                using var fileExtensionCommandKey = fileExtensionKey.CreateSubKey("shell\\open\\command");
                fileExtensionCommandKey.SetValue("", $"\"{appPath}\" \"%1\"");
            }
        }

        using var registeredAppsKey = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications");
        registeredAppsKey.SetValue(productName, $@"Software\{productName}\Capabilities");

        using var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{appFileName}");
        appKey.SetValue("", appPath);

        unsafe
        {
            PInvoke.SHChangeNotify(SHCNE_ID.SHCNE_ASSOCCHANGED, SHCNF_FLAGS.SHCNF_IDLIST, null, null);
        }

        isAppRegistedSubject.OnNext(IsAppRegisted());
    }

    public void Unregister()
    {
        foreach (var type in SupportedFileTypes)
        {
            var fileExtension = $"{productName}{type.Extension}";
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{fileExtension}", false);
        }

        using var registeredAppsKey = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true);
        registeredAppsKey?.DeleteValue(productName, false);

        Registry.CurrentUser.DeleteSubKeyTree($@"Software\{productName}", false);

        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{appFileName}", false);

        unsafe
        {
            PInvoke.SHChangeNotify(SHCNE_ID.SHCNE_ASSOCCHANGED, SHCNF_FLAGS.SHCNF_IDLIST, null, null);
        }

        isAppRegistedSubject.OnNext(IsAppRegisted());
    }

    private bool IsAppRegisted()
    {
        using var registeredAppsKey = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications");

        return registeredAppsKey?.GetValue(productName) is not null;
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
}
