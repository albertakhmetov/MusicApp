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
using System.Text;
using System.Threading.Tasks;
using MusicApp.Core;


internal class AppEnvironment : IAppEnvironment
{
    public AppEnvironment()
    {
        var info = FileVersionInfo.GetVersionInfo(typeof(App).Assembly.Location);
        ProductName = info.ProductName ?? throw new InvalidOperationException("Product Name can't be null");
        ProductDescription = info.Comments ?? throw new InvalidOperationException("Comments can't be null");
        ProductVersion = info.ProductVersion ?? throw new InvalidOperationException("Product Version can't be null");

        var processFileName = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(processFileName))
        {
            throw new InvalidOperationException("Process MainModule can't be null");
        }

        ApplicationFileInfo = new FileInfo(processFileName);
        ApplicationDirectoryInfo = ApplicationFileInfo.Directory ?? throw new InvalidOperationException("Application Directory can't be null");
        UserDataDirectoryInfo = ApplicationDirectoryInfo;
    }

    public string ProductName { get; }

    public string ProductVersion { get; }

    public string ProductDescription { get; }

    public FileInfo ApplicationFileInfo { get; }

    public DirectoryInfo ApplicationDirectoryInfo { get; }

    public DirectoryInfo UserDataDirectoryInfo { get; }
}
