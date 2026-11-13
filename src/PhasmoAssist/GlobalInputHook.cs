using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace PhasmoAssist
{
    public class GlobalInputHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int WH_MOUSE_LL = 14;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;
        private const int WM_MOUSEWHEEL = 0x020A;

        private IntPtr _hookMId = IntPtr.Zero;
        private IntPtr _hookKId = IntPtr.Zero;
        private readonly LowLevelInputProc _proc;
        private readonly Thread _hookThread;
        private readonly ManualResetEventSlim _hookReady = new(false);
        private readonly HashSet<Key> _pressedKeys = new();
        private uint _hookThreadId;
        private Exception? _initializationError;
        private int _disposeRequested;
        private const uint WM_QUIT = 0x0012;
        private const uint PM_NOREMOVE = 0;

        private static volatile bool _lShift;
        private static volatile bool _lAlt;
        public static bool LShift => _lShift;
        public static bool LAlt => _lAlt;
        public static Key SpecialKey { get; set; } = Key.LeftShift;
        public static Key ShortcutKey { get; set; } = Key.LeftAlt;

        private delegate IntPtr LowLevelInputProc(int nCode, IntPtr wParam, IntPtr lParam);

        public event Action<Key> KeyPressed;

        public event Action<MouseButton, bool, POINT>? MouseButtonChanged;
        public event Action<int>? MouseWheel; // delta (+/-120 usually)

        public GlobalInputHook()
        {
            _proc = HookCallback;
            _hookThread = new Thread(RunHookMessageLoop)
            {
                IsBackground = true,
                Name = "PhasmoAssist global input hook"
            };
            _hookThread.Start();

            if (!_hookReady.Wait(TimeSpan.FromSeconds(5)))
            {
                Dispose();
                throw new TimeoutException("Global input hooks did not initialize in time.");
            }

            if (_initializationError != null)
            {
                Dispose();
                throw new InvalidOperationException("Could not install global input hooks.", _initializationError);
            }
        }

        private void RunHookMessageLoop()
        {
            try
            {
                _hookThreadId = GetCurrentThreadId();
                PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);

                var module = GetModuleHandle(null);
                _hookKId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, module, 0);
                if (_hookKId == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install keyboard hook.");
                }

                _hookMId = SetWindowsHookEx(WH_MOUSE_LL, _proc, module, 0);
                if (_hookMId == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install mouse hook.");
                }
            }
            catch (Exception ex)
            {
                _initializationError = ex;
            }
            finally
            {
                _hookReady.Set();
            }

            try
            {
                if (_initializationError == null && Volatile.Read(ref _disposeRequested) == 0)
                {
                    while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0)
                    {
                    }
                }
            }
            finally
            {
                if (_hookKId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookKId);
                    _hookKId = IntPtr.Zero;
                }
                if (_hookMId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookMId);
                    _hookMId = IntPtr.Zero;
                }

                _pressedKeys.Clear();
                _lShift = false;
                _lAlt = false;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            var hook = IntPtr.Zero;
            var isDown = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;
            var isUp = wParam == WM_KEYUP || wParam == WM_SYSKEYUP;

            if (nCode >= 0 && isDown)
            {
                hook = _hookKId;
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);
                if (_pressedKeys.Add(key))
                {
                    KeyPressed?.Invoke(key);
                }

                if (key == SpecialKey)
                {
                    _lShift = true;
                }
                else if (key == ShortcutKey)
                {
                    _lAlt = true;
                }
            }
            else if (nCode >= 0 && isUp)
            {
                hook = _hookKId;
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);
                _pressedKeys.Remove(key);

                if (key == SpecialKey)
                {
                    _lShift = false;
                }
                else if (key == ShortcutKey)
                {
                    _lAlt = false;
                }
            }
            else if (nCode >= 0)
            {
                hook = _hookMId;
                int msg = wParam.ToInt32();
                var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                switch (msg)
                {
                    case WM_LBUTTONDOWN: MouseButtonChanged?.Invoke(MouseButton.Left, true, info.pt); break;
                    case WM_LBUTTONUP: MouseButtonChanged?.Invoke(MouseButton.Left, false, info.pt); break;

                    case WM_RBUTTONDOWN: MouseButtonChanged?.Invoke(MouseButton.Right, true, info.pt); break;
                    case WM_RBUTTONUP: MouseButtonChanged?.Invoke(MouseButton.Right, false, info.pt); break;

                    case WM_MBUTTONDOWN: MouseButtonChanged?.Invoke(MouseButton.Middle, true, info.pt); break;
                    case WM_MBUTTONUP: MouseButtonChanged?.Invoke(MouseButton.Middle, false, info.pt); break;

                    case WM_XBUTTONDOWN:
                    case WM_XBUTTONUP:
                        {
                            // High word of mouseData indicates XBUTTON1 or XBUTTON2
                            int highWord = (short)((info.mouseData >> 16) & 0xFFFF);
                            var btn = (highWord == 1) ? MouseButton.XButton1 : MouseButton.XButton2;
                            isDown = msg == WM_XBUTTONDOWN;
                            MouseButtonChanged?.Invoke(btn, isDown, info.pt);
                            break;
                        }

                    case WM_MOUSEWHEEL:
                        {
                            // High word contains wheel delta
                            int delta = (short)((info.mouseData >> 16) & 0xFFFF);
                            MouseWheel?.Invoke(delta);
                            break;
                        }
                }
            }
            else return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeRequested, 1) != 0)
            {
                return;
            }

            if (_hookThreadId != 0)
            {
                PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }

            if (Thread.CurrentThread != _hookThread && _hookThread.IsAlive)
            {
                _hookThread.Join(TimeSpan.FromSeconds(2));
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
            public override string ToString() => $"({x}, {y})";
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelInputProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
            public uint lPrivate;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint minFilter, uint maxFilter, uint removeMsg);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint minFilter, uint maxFilter);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
