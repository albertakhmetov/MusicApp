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
namespace MusicApp.Native;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MusicApp.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

internal sealed class AppWindowProcedure : IAppWindowProcedure
{
    private readonly Dictionary<uint, List<IAppWindowProcedure.IReceiver>> receivers = [];
    private readonly WNDPROC wndProc, nativeWndProc;

    public AppWindowProcedure(IAppWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        wndProc = new WNDPROC(WndProc);
        var p = PInvoke.SetWindowLongPtr(
            hWnd: (HWND)window.Handle,
            nIndex: WINDOW_LONG_PTR_INDEX.GWL_WNDPROC,
            dwNewLong: Marshal.GetFunctionPointerForDelegate(wndProc));
        nativeWndProc = Marshal.GetDelegateForFunctionPointer<WNDPROC>(p);
    }

    public IDisposable Subscribe(uint msg, IAppWindowProcedure.IReceiver receiver)
    {
        if (receivers.ContainsKey(msg) is false)
        {
            receivers[msg] = new List<IAppWindowProcedure.IReceiver>();
        }

        receivers[msg].Add(receiver);

        return Disposable.Create(() =>
        {
            if (receivers.TryGetValue(msg, out var list))
            {
                list.Remove(receiver);
            }
        });
    }

    private LRESULT WndProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (receivers.TryGetValue(msg, out var receiverList))
        {
            var isProcessed = false;

            foreach (var receiver in receiverList)
            {
                isProcessed |= receiver.Process(msg, wParam.Value, lParam.Value);
            }

            if (isProcessed)
            {
                return (LRESULT)0;
            }
        }

        return PInvoke.CallWindowProc(nativeWndProc, hWnd, msg, wParam, lParam);
    }
}
