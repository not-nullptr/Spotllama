using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.Foundation;
using WinRT.Interop;
using System;
using System.Runtime.InteropServices;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using Windows.Foundation;
using Ollama_Spotlight.Utilities;
using System.Linq;
using Windows.Graphics;
using Microsoft.UI.Xaml.Input;
using OllamaSharp;
using System.Diagnostics;
using OllamaSharp.Models;
using OllamaSharp.Streamer;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using System.Net.Http;
using System.Text.Json;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Ollama_Spotlight {
    public sealed partial class MainWindow : Window
    {
        private readonly Easing easing = new(
            new BezierCurve(0.25, 0.8),
            new BezierCurve(0.45, 0.9)
        );
        private const uint SPACE_KEY = 0x22;
        private const uint WM_HOTKEY = 0x0312;
        private readonly WNDPROC origPrc;
        private readonly WNDPROC hotKeyPrc;
        private AppWindow _apw;
        private OverlappedPresenter _presenter;
        private Monitor lastMonitor = null;
        public Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };
        public static Point GetMousePosition()
        {
            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            return new Point(w32Mouse.X, w32Mouse.Y);
        }
        public static Monitor GetMouseMonitor()
        {
            Monitor[] monitors = Monitor.All.ToArray();
            Point point = GetMousePosition();
            Monitor monitor = monitors.Where(m => 
                point.X >= m.WorkingArea.X &&
                point.X <= m.WorkingArea.X + m.WorkingArea.Width &&
                point.Y >= m.WorkingArea.Y &&
                point.Y <= m.WorkingArea.Y + m.WorkingArea.Height)
            .FirstOrDefault();
            return monitor;
        }
        private LRESULT HotKeyPrc(HWND hwnd,
            uint uMsg,
            WPARAM wParam,
            LPARAM lParam)
        {
            if (uMsg == WM_HOTKEY)
            {
                Monitor monitor = GetMouseMonitor();
                int width = monitor.WorkingArea.Width / 2;
                int height = 100;
                int centeredX = monitor.WorkingArea.X + (monitor.WorkingArea.Width - width) / 2;
                int centeredY = monitor.WorkingArea.Y + (monitor.WorkingArea.Height - height) / 2;

                if (!Visible)
                {
                    AppWindow.Move(
                        new PointInt32(centeredX, centeredY)
                    );
                    AppWindow.Resize(new SizeInt32(width, height));
                    PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_SHOW);
                    PInvoke.SetForegroundWindow(hwnd);
                    PInvoke.SetFocus(hwnd);
                    PInvoke.SetActiveWindow(hwnd);
                    LlamaInput.Visibility = Visibility.Visible;
                    LlamaInput.Focus(FocusState.Programmatic);
                    Response.Visibility = Visibility.Collapsed;
                    System.Threading.Thread thread = new(new System.Threading.ThreadStart(async () =>
                    {
                        var client = new HttpClient();
                        await client.PostAsync("http://127.0.0.1:11434/api/generate", new StringContent(
                            JsonSerializer.Serialize(new
                            {
                                model = "llama3:8b"
                            }
                        )));
                    }));
                    thread.Start();
                }
                else
                {
                    if (lastMonitor is not null)
                    {
                        if (lastMonitor.Bounds.X != monitor.Bounds.X ||
                            lastMonitor.Bounds.Y != monitor.Bounds.Y ||
                            lastMonitor.Bounds.Width != monitor.Bounds.Width ||
                            lastMonitor.Bounds.Height != monitor.Bounds.Height)
                        {
                            AppWindow.Move(
                                new PointInt32(centeredX, centeredY)
                            );
                            AppWindow.Resize(new SizeInt32(width, height));

                        }
                        else
                        {
                            PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_HIDE);
                            LlamaInput.Text = "";
                        }
                    }
                    else
                    {
                        PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_HIDE);
                        LlamaInput.Text = "";
                    }

                }
                lastMonitor = monitor;
                return (LRESULT)IntPtr.Zero;
            }
    
            return PInvoke.CallWindowProc(origPrc, hwnd, uMsg, wParam, lParam);
        }

        public MainWindow()
        {
            InitializeComponent();
            HWND hwnd = new HWND(WindowNative.GetWindowHandle(this));
            PInvoke.RegisterHotKey(hwnd, 0, HOT_KEY_MODIFIERS.MOD_WIN | HOT_KEY_MODIFIERS.MOD_CONTROL | HOT_KEY_MODIFIERS.MOD_ALT | HOT_KEY_MODIFIERS.MOD_SHIFT, SPACE_KEY);
    
            hotKeyPrc = HotKeyPrc;
            var hotKeyPrcPointer = Marshal.GetFunctionPointerForDelegate(hotKeyPrc);
            origPrc = Marshal.GetDelegateForFunctionPointer<WNDPROC>((IntPtr)PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, hotKeyPrcPointer));
            ExtendsContentIntoTitleBar = true;
            GetAppWindowAndPresenter();
            _apw.IsShownInSwitchers = false;
            _presenter.SetBorderAndTitleBar(false, false);
            _presenter.IsResizable = false;
            
        }

        private void Current_Activated(object sender, WindowActivatedEventArgs args)
        {
            HWND hwnd = new HWND(WindowNative.GetWindowHandle(this));
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_HIDE);
                LlamaInput.Text = "";
            }
        }

        public void GetAppWindowAndPresenter()
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            WindowId myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            _apw = AppWindow.GetFromWindowId(myWndId);
            _presenter = _apw.Presenter as OverlappedPresenter;
        }

        private void LlamaInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            HWND hwnd = new HWND(WindowNative.GetWindowHandle(this));
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                e.Handled = true;
                LlamaInput.Text = "";
                PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_HIDE);
            }
            else if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ResponseBlock.Text = "";
                e.Handled = true;
                Uri uri = new("http://localhost:11434");
                OllamaApiClient ollama = new(uri);
                ollama.SelectedModel = "llama3:8b";
                string text = LlamaInput.Text;
                LlamaInput.Text = "";
                Monitor monitor = GetMouseMonitor();
                float width = monitor.WorkingArea.Width / 1.25f;
                int height = monitor.WorkingArea.Height / 2;
                float centeredX = monitor.WorkingArea.X + (monitor.WorkingArea.Width - width) / 2;
                int centeredY = monitor.WorkingArea.Y + (monitor.WorkingArea.Height - height) / 2;
                AppWindow.MoveAndResize(new RectInt32
                {
                    Width = (int)width,
                    Height = height,
                    X = (int)centeredX,
                    Y = centeredY,
                });
                LlamaInput.Visibility = Visibility.Collapsed;
                Response.Visibility = Visibility.Visible;
                System.Threading.Tasks.Task.Run(async () =>
                {
                    await ollama.StreamCompletion(text, null, stream =>
                    {
                        dispatcherQueue.TryEnqueue(() =>
                        {
                            ResponseBlock.Text += stream.Response;
                            Scroller.UpdateLayout();
                            Scroller.ScrollTo(0, Scroller.ScrollableHeight);
                        });
                    });
                    await ollama.StreamCompletion(new GenerateCompletionRequest()
                    {
                        System = "You are LLaMA, a helpful AI assistant. The user is unable to provide any follow-up questions, chats or anything else. Your message will end the conversation.",
                        Prompt = text
                    }, new ResponseStreamer(dispatcherQueue, ResponseBlock, Scroller));
                });
            }
        }

        private void Container_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                HWND hwnd = new HWND(WindowNative.GetWindowHandle(this));
                PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_HIDE);
                LlamaInput.Text = "";
            }
        }

        public class ResponseStreamer : IResponseStreamer<GenerateCompletionResponseStream>
        {
            private DispatcherQueue DispatcherQueue;
            private CommunityToolkit.WinUI.UI.Controls.MarkdownTextBlock ResponseBlock;
            private ScrollView Scroller;
            public ResponseStreamer(DispatcherQueue dispatcher, CommunityToolkit.WinUI.UI.Controls.MarkdownTextBlock responseBlock, ScrollView scrollView)
            {
                DispatcherQueue = dispatcher;
                ResponseBlock = responseBlock;
                Scroller = scrollView;
            }
            void IResponseStreamer<GenerateCompletionResponseStream>.Stream(GenerateCompletionResponseStream stream)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ResponseBlock.Text += stream.Response;
                    Scroller.UpdateLayout();
                    Scroller.ScrollTo(0, Scroller.ScrollableHeight);
                });
            }
        }
    }

}
