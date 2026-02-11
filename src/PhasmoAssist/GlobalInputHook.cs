using System.Diagnostics;
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
        private LowLevelInputProc _proc;

        private static bool _lCtrl { get; set; }
        public static bool LCtrl => _lCtrl;
        public static Key ShortcutKey { get; set; } = Key.LeftCtrl;

        private delegate IntPtr LowLevelInputProc(int nCode, IntPtr wParam, IntPtr lParam);

        public event Action<Key> KeyPressed;

        public event Action<MouseButton, bool, POINT>? MouseButtonChanged;
        public event Action<int>? MouseWheel; // delta (+/-120 usually)

        public GlobalInputHook()
        {
            _proc = HookCallback;
            var hooks = SetHook(_proc);
            _hookMId = hooks.Item1;
            _hookKId = hooks.Item2;
        }

        private Tuple<IntPtr, IntPtr> SetHook(LowLevelInputProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                IntPtr hModule = GetModuleHandle(curModule.ModuleName);
                var hookM = SetWindowsHookEx(WH_MOUSE_LL, proc, hModule, 0);
                var hookK = SetWindowsHookEx(WH_KEYBOARD_LL, proc, hModule, 0);
                return new Tuple<nint, nint>(hookM, hookK);
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
                KeyPressed?.Invoke(key);

                if (key == ShortcutKey)
                {
                    _lCtrl = true;
                }
            }
            else if (nCode >= 0 && isUp)
            {
                hook = _hookKId;
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);

                if (key == ShortcutKey)
                {
                    _lCtrl = false;
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
            else return IntPtr.Zero;

            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            UnhookWindowsHookEx(_hookKId);
            UnhookWindowsHookEx(_hookKId);
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
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
