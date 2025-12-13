using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace GraphSimulator.Execution.Controller
{
    public static class WpfInputHelper
    {
        // Virtual Key Codes
        public const byte VK_RETURN = 0x0D;
        public const byte VK_SHIFT = 0x10;
        public const byte VK_CONTROL = 0x11;
        public const byte VK_MENU = 0x12;
        public const byte VK_ESCAPE = 0x1B;
        public const byte VK_SPACE = 0x20;
        public const byte VK_LEFT = 0x25;
        public const byte VK_UP = 0x26;
        public const byte VK_RIGHT = 0x27;
        public const byte VK_DOWN = 0x28;
        public const byte VK_DELETE = 0x2E;
        public const byte VK_TAB = 0x09;

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;
        private const int MOUSEEVENTF_WHEEL = 0x0800;
        private const int MOUSEEVENTF_HWHEEL = 0x01000; // Horizontal wheel

        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public static void ClickAt(int x, int y)
        {
            SetCursorPos(x, y);
            Thread.Sleep(10);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }

        public static void RightClickAt(int x, int y)
        {
            SetCursorPos(x, y);
            Thread.Sleep(10);
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
        }

        public static void MoveTo(int x, int y)
        {
            SetCursorPos(x, y);
        }

        public static void TypeText(string text)
        {
            foreach (char c in text)
            {
                short vk = VkKeyScan(c);
                byte virtualKey = (byte)(vk & 0xFF);
                byte shiftState = (byte)(vk >> 8);

                if ((shiftState & 1) != 0)
                {
                    keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                }

                keybd_event(virtualKey, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                Thread.Sleep(10);
                keybd_event(virtualKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                if ((shiftState & 1) != 0)
                {
                    keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }

                Thread.Sleep(10);
            }
        }

        public static void PressKey(byte virtualKeyCode)
        {
            keybd_event(virtualKeyCode, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(10);
            keybd_event(virtualKeyCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void Scroll(int delta)
        {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, delta, 0);
        }

        public static void ScrollHorizontal(int delta)
        {
            mouse_event(MOUSEEVENTF_HWHEEL, 0, 0, delta, 0);
        }

        public static void KeyDown(byte virtualKeyCode)
        {
            keybd_event(virtualKeyCode, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        }

        public static void KeyUp(byte virtualKeyCode)
        {
            keybd_event(virtualKeyCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}