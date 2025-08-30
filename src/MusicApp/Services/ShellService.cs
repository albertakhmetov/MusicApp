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
using MusicApp.Core.Helpers;
using MusicApp.Core.Models;
using MusicApp.Core.Services;
using Windows.Win32;
using Windows.Win32.UI.Shell;
using Windows.Win32.System.Com;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

internal class ShellService : IShellService
{
    private readonly ILogger logger;
    private readonly BehaviorSubject<bool> isAppRegistedSubject;

    private readonly string productName, productDescription;
    private readonly FileInfo appFileInfo, resourceFileInfo, shortcutFileInfo;

    public ShellService(IAppEnvironment appEnvironment, ILogger<ShellService> logger)
    {
        ArgumentNullException.ThrowIfNull(appEnvironment);
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;

        productName = appEnvironment.ProductName;
        productDescription = appEnvironment.ProductDescription ?? "";

        appFileInfo = appEnvironment.ApplicationFileInfo;
        resourceFileInfo = appEnvironment.ApplicationDirectoryInfo
            .GetFileInfo($"{appFileInfo.GetFileNameWithoutExtension()}.Resources.dll");

        shortcutFileInfo = new FileInfo(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            $"{productName}.lnk"));

        SupportedFileTypes = [
            new FileType { Description = "MP3 Music File", Extension = ".mp3" },
            new FileType { Description = "MKA Music File", Extension = ".mka" }
        ];

        isAppRegistedSubject = new BehaviorSubject<bool>(IsAppRegisted());
        IsRegistred = isAppRegistedSubject.AsObservable();
    }

    public IImmutableList<FileType> SupportedFileTypes { get; }

    public IObservable<bool> IsRegistred { get; }

    public bool IsFileSupported(string? fileName)
    {
        var ext = Path.GetExtension(fileName);

        return ext is not null && SupportedFileTypes.Any(x => x.Equals(ext));
    }

    public void Register()
    {
        CreateShortcut(
            shortcutFileInfo.FullName,
            appFileInfo.FullName,
            workingDirectory: appFileInfo.Directory!.FullName,
            iconPath: resourceFileInfo.FullName);

        try
        {
            using var capabilitiesKey = Registry.CurrentUser.CreateSubKey($@"Software\{productName}\Capabilities");
            capabilitiesKey.SetValue("ApplicationName", productName);
            capabilitiesKey.SetValue("ApplicationDescription", productDescription);
            capabilitiesKey.SetValue("ApplicationIcon", $"{resourceFileInfo.FullName},0");

            foreach (var type in SupportedFileTypes)
            {
                var fileExtension = $"{productName}{type.Extension}";

                using var fileKey = capabilitiesKey.CreateSubKey("FileAssociations");
                fileKey.SetValue(type.Extension, fileExtension);

                using var fileExtensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{fileExtension}");
                {
                    fileExtensionKey.SetValue("", type.Description);

                    using var fileExtensionIconKey = fileExtensionKey.CreateSubKey("DefaultIcon");
                    fileExtensionIconKey.SetValue("", $"{resourceFileInfo.FullName},1");

                    using var fileExtensionVerbKey = fileExtensionKey.CreateSubKey("shell\\Open");
                    fileExtensionVerbKey.SetValue("MultiSelectModel", "Player");

                    using var fileExtensionCommandKey = fileExtensionKey.CreateSubKey("shell\\open\\command");
                    fileExtensionCommandKey.SetValue("", $"\"{appFileInfo.FullName}\" \"%1\"");
                }
            }

            using var registeredAppsKey = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications");
            registeredAppsKey.SetValue(productName, $@"Software\{productName}\Capabilities");

            using var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{appFileInfo.Name}");
            appKey.SetValue("", appFileInfo.FullName);

            unsafe
            {
                PInvoke.SHChangeNotify(SHCNE_ID.SHCNE_ASSOCCHANGED, SHCNF_FLAGS.SHCNF_IDLIST, null, null);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error during app registering");
            throw;
        }

        isAppRegistedSubject.OnNext(IsAppRegisted());
    }

    public void Unregister()
    {
        if (shortcutFileInfo.Exists)
        {
            shortcutFileInfo.Delete();
        }
        try
        {
            foreach (var type in SupportedFileTypes)
            {
                var fileExtension = $"{productName}{type.Extension}";
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{fileExtension}", false);
            }

            using var registeredAppsKey = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true);
            registeredAppsKey?.DeleteValue(productName, false);

            Registry.CurrentUser.DeleteSubKeyTree($@"Software\{productName}", false);

            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{appFileInfo.Name}", false);

            unsafe
            {
                PInvoke.SHChangeNotify(SHCNE_ID.SHCNE_ASSOCCHANGED, SHCNF_FLAGS.SHCNF_IDLIST, null, null);
            }

            isAppRegistedSubject.OnNext(IsAppRegisted());
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error during app unregistering");
            throw;
        }
    }

    private bool IsAppRegisted()
    {
        using var registeredAppsKey = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications");

        return registeredAppsKey?.GetValue(productName) is not null;
    }

    private bool CreateShortcut(
        string shortcutPath,
        string targetPath,
        string arguments = "",
        string workingDirectory = "",
        string description = "",
        string iconPath = "")
    {
        try
        {
            var shellLink = (IShellLinkW)new ShellLink();
            var persistFile = (IPersistFile)shellLink;

            try
            {
                shellLink.SetPath(targetPath);

                if (!string.IsNullOrEmpty(arguments))
                {
                    shellLink.SetArguments(arguments);
                }

                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    shellLink.SetWorkingDirectory(workingDirectory);
                }

                if (!string.IsNullOrEmpty(description))
                {
                    shellLink.SetDescription(description);
                }

                if (!string.IsNullOrEmpty(iconPath))
                {
                    shellLink.SetIconLocation(iconPath, 0);
                }

                persistFile.Save(shortcutPath, true);
            }
            finally
            {
                Marshal.ReleaseComObject(persistFile);
                Marshal.ReleaseComObject(shellLink);
            }

            return File.Exists(shortcutPath);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error during shortcut creation");
            return false;
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }
}
