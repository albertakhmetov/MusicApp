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
using System.Configuration;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MusicApp.Core.Helpers;
using MusicApp.Core.Services;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.DataExchange;

class SingleInstance : IDisposable
{
    private readonly CompositeDisposable disposable = [];

    private readonly IAppWindow appWindow;
    private readonly Receiver receiver;

    public SingleInstance(IAppWindow appWindow)
    {
        ArgumentNullException.ThrowIfNull(appWindow);
        ArgumentNullException.ThrowIfNull(appWindow.Procedure);

        this.appWindow = appWindow;

        receiver = new Receiver();
        DataReceived = receiver.DataReceived;

        this.appWindow.Procedure
            .Subscribe(PInvoke.WM_COPYDATA, receiver)
            .DisposeWith(disposable);
    }

    public IObservable<string> DataReceived { get; }

    public void Dispose()
    {
        if (disposable.IsDisposed is false)
        {
            disposable.Dispose();
        }
    }

    public unsafe static void Send(nint handle, string message)
    {
        byte[] bytes = Encoding.Unicode.GetBytes(message);

        fixed (byte* pBytes = bytes)
        {
            var cds = new COPYDATASTRUCT
            {
                dwData = 0,
                cbData = (uint)bytes.Length,
                lpData = pBytes
            };

            nint pCds = Marshal.AllocCoTaskMem(Marshal.SizeOf(cds));
            try
            {
                Marshal.StructureToPtr(cds, pCds, false);

                PInvoke.SendMessage((HWND)handle, PInvoke.WM_COPYDATA, nuint.Zero, (LPARAM)pCds);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pCds);
            }
        }
    }

    private sealed class Receiver : IAppWindowProcedure.IReceiver
    {
        private readonly Subject<string> dataReceivedSubject;

        public Receiver()
        {
            dataReceivedSubject = new Subject<string>();

            DataReceived = dataReceivedSubject.AsObservable();
        }

        public IObservable<string> DataReceived { get; }

        public unsafe bool Process(uint message, nuint wParam, nint lParam)
        {
            if (message != PInvoke.WM_COPYDATA)
            {
                return false;
            }

            var cds = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);

            if (cds.cbData > 0)
            {
                var data = Encoding.Unicode.GetString((byte*)cds.lpData, (int)cds.cbData);

                if (string.IsNullOrEmpty(data) is false)
                {
                    dataReceivedSubject.OnNext(data);
                }
            }

            return true;
        }
    }
}
