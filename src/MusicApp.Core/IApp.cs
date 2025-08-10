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
namespace MusicApp.Core;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicApp.Core.Models;
using MusicApp.Core.ViewModels;

public interface IApp
{
    static string ApplicationPath
    {
        get
        {
            var processModule = Process.GetCurrentProcess().MainModule;
            return processModule is null
                ? throw new InvalidOperationException("Process.GetCurrentProcess().MainModule is null")
                : processModule.FileName;
        }
    }

#if DEBUG
    static string AppUserModelID => "com.albertakhmetov.MusicApp.Debug";
#else
    static string AppUserModelID => "com.albertakhmetov.MusicApp";
#endif

    AppInfo Info { get; }

    IAppWindow GetWindow<T>() where T : ViewModel;
}