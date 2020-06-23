using System;
using SharpVk.Glfw;

namespace CNMEngine.RenderEngine {
    public class Window {
        private WindowHandle window;
        private WindowSizeDelegate windowSizeCallback;

        public Window(string windowTitle, int initialWidth, int initialHeight, WindowSizeDelegate onReSizeDelegate) {
            Glfw3.Init();
            // GLFW_CLIENT_API | GLFW_NO_API
            Glfw3.WindowHint(0x00022001 | 0, 0);
            this.window = Glfw3.CreateWindow(initialWidth, initialHeight, windowTitle, IntPtr.Zero, IntPtr.Zero);
            this.windowSizeCallback = onReSizeDelegate;

            Glfw3.SetWindowSizeCallback(this.window, this.windowSizeCallback);
        }
    }
}