using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Interop;


namespace VongageVideoWinTest
{
    /// <summary>
    /// User32
    /// </summary>
    static class User32
    {
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hParent, IntPtr hChildAfter, string pClassName, string pWindowName);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr PostMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);
    }


    /// <summary>
    /// UnityHost
    /// </summary>
    public class UnityHost : HwndHost
    {
    #region << Field >>

        private Process _childProcess;
        private HandleRef _childHandleRef;

        private const int WM_ACTIVATE = 0x0006;
        private const int WM_CLOSE = 0x0010;
        private readonly IntPtr WA_ACTIVE = new IntPtr(1);
        private readonly IntPtr WA_INACTIVE = new IntPtr(0);

    #endregion << Field >>

        /// <summary>
        /// AppPath
        /// </summary>
        public string AppPath
        {
            get; 
            set;
        }


        /// <summary>
        /// UnityHost
        /// </summary>
        public UnityHost()
        {
        }


        /// <summary>
        /// BuildWindowCore
        /// </summary>
        /// <param name="hwndParent"></param>
        /// <returns></returns>
        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            var cmdline = $"-parentHWND {hwndParent.Handle}";
            _childProcess = Process.Start(AppPath, cmdline);

            while (true)
            {
                var hwndChild = User32.FindWindowEx(hwndParent.Handle, IntPtr.Zero, null, null);
                if (hwndChild != IntPtr.Zero)
                {
                    User32.SendMessage(hwndChild, WM_ACTIVATE, WA_ACTIVE, IntPtr.Zero);

                    return _childHandleRef = new HandleRef(this, hwndChild);
                }
                Thread.Sleep(100);
            }
        }


        /// <summary>
        /// DestroyWindowCore
        /// </summary>
        /// <param name="hwnd"></param>
        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            User32.PostMessage(_childHandleRef.Handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

            var counter = 30;
            while (!_childProcess.HasExited)
            {
                if (--counter < 0)
                {
                    Debug.WriteLine("Process not dead yet, killing...");
                    _childProcess.Kill();
                }
                Thread.Sleep(100);
            }
            _childProcess.Dispose();
        }
    }
}
