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
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using MusicApp.Core;
using MusicApp.Core.Helpers;
using MusicApp.Core.Services;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

internal sealed class Taskbar : IDisposable
{
    private const int WM_COMMAND = 0x0111;

    private readonly CompositeDisposable disposable = [];

    private readonly IAppWindow appWindow;
    private readonly List<TaskbarButton> buttons = [];

    private readonly ComObjectSafeHandle<ITaskbarList3> taskbarList;
    private readonly Receiver receiver;

    public Taskbar(IAppWindow appWindow, params TaskbarButton[] buttons)
    {
        ArgumentNullException.ThrowIfNull(appWindow);
        ArgumentNullException.ThrowIfNull(appWindow.Procedure);
        ArgumentNullException.ThrowIfNull(buttons);
        ArgumentOutOfRangeException.ThrowIfZero(buttons.Length);

        this.appWindow = appWindow;

        taskbarList = new ComObjectSafeHandle<ITaskbarList3>((ITaskbarList3)new CTaskbarList());
        taskbarList.Value.HrInit();

        RegisterButtons(buttons);

        receiver = new Receiver();
        receiver.ButtonClicked
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(buttonId => ExecuteCommand(buttonId))
            .DisposeWith(disposable);

        this.appWindow.Procedure
            .Subscribe(WM_COMMAND, receiver)
            .DisposeWith(disposable);
    }

    private void RegisterButtons(TaskbarButton[] buttons)
    {
        foreach (var button in buttons)
        {
            button.EnabledChanged += OnButtonChanged;
            button.VisibleChanged += OnButtonChanged;

            this.buttons.Add(button);
        }

        taskbarList.Value.ThumbBarAddButtons(
            (HWND)appWindow.Handle,
            buttons.Select(CreateThumbButton).ToArray());
    }

    private THUMBBUTTON CreateThumbButton(TaskbarButton button, int arg2)
    {
        var flags = THUMBBUTTONFLAGS.THBF_ENABLED | THUMBBUTTONFLAGS.THBF_DISMISSONCLICK; ;

        if (button.IsEnabled is false)
        {
            flags |= THUMBBUTTONFLAGS.THBF_DISABLED;
        }

        if (button.IsVisible is false)
        {
            flags |= THUMBBUTTONFLAGS.THBF_HIDDEN;
        }

        return new THUMBBUTTON
        {
            dwMask = THUMBBUTTONMASK.THB_FLAGS | THUMBBUTTONMASK.THB_ICON | THUMBBUTTONMASK.THB_TOOLTIP,
            iId = button.Id,
            dwFlags = flags,
            hIcon = (HICON)button.Icon.DangerousGetHandle(),
            szTip = button.ToolTip
        };
    }

    private void OnButtonChanged(object? sender, EventArgs _)
    {
        if (sender is TaskbarButton button)
        {
            var flags = THUMBBUTTONFLAGS.THBF_ENABLED | THUMBBUTTONFLAGS.THBF_DISMISSONCLICK;

            if (button.IsEnabled is false)
            {
                flags |= THUMBBUTTONFLAGS.THBF_DISABLED;
            }

            if (button.IsVisible is false)
            {
                flags |= THUMBBUTTONFLAGS.THBF_HIDDEN;
            }

            var thumbButton = new THUMBBUTTON
            {
                dwMask = THUMBBUTTONMASK.THB_FLAGS,
                iId = button.Id,
                dwFlags = flags,
            };

            taskbarList.Value.ThumbBarUpdateButtons((HWND)appWindow.Handle, [thumbButton]);
        }
    }

    public void Dispose()
    {
        if (disposable.IsDisposed is false)
        {
            disposable.Dispose();
        }

        taskbarList?.Dispose();

        foreach (var b in buttons)
        {
            b.EnabledChanged -= OnButtonChanged;
            b.VisibleChanged -= OnButtonChanged;
            b.Dispose();
        }
    }

    private void ExecuteCommand(int buttonId)
    {
        var button = buttons.FirstOrDefault(button => button.Id == buttonId);

        button?.Command?.Execute(button.CommandParameter);
    }

    private sealed class Receiver : IAppWindowProcedure.IReceiver
    {
        private readonly Subject<int> buttonClickedSubject;

        public Receiver()
        {
            buttonClickedSubject = new Subject<int>();
            ButtonClicked = buttonClickedSubject.AsObservable();
        }

        public IObservable<int> ButtonClicked { get; }

        public bool Process(uint message, nuint wParam, nint lParam)
        {
            if (message != WM_COMMAND)
            {
                return false;
            }

            var buttonId = (int)(wParam & 0xFFFF);
            buttonClickedSubject.OnNext(buttonId);

            return true;
        }
    }

    private class ComObjectSafeHandle<T> : SafeHandle where T : class
    {
        public ComObjectSafeHandle(T value) : base(nint.Zero, true)
        {
            ArgumentNullException.ThrowIfNull(value);

            Value = value;
            SetHandle(Marshal.GetIUnknownForObject(value));
        }

        public T Value { get; private set; }

        public override bool IsInvalid => handle == nint.Zero;

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                Marshal.ReleaseComObject(Value);
            }

            return true;
        }
    }

    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    internal class CTaskbarList
    {
    }
}
