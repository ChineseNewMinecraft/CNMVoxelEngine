using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SharpVk;
using SharpVk.Glfw;
using SharpVk.Interop.Khronos;
using SharpVk.Multivendor;

namespace CNMEngine.RenderEngine {
    struct InstanceCreateHelper {
        public Instance Instance { get; set; }
        public Surface Surface { get; set; }
        public PhysicalDevice PhysicalDevice { get; set; }
        public Device Device { get; set; }
        public Queue GraphicsQueue { get; set; }
        public Queue PresentQueue { get; set; }

        public Swapchain SwapChain { get; set; }
        public Image[] SwapChainImages { get; set; }
        public ImageView[] SwapChainImageViews { get; set; }

        public static Bool32 DebugMessage(DebugReportFlags flags, DebugReportObjectType objecttype, ulong o,
            HostSize location, int messagecode,
            string playerprefix, string pmessage, IntPtr puserdata) {
            Debug.WriteLine("printing debug callback");
            Debug.WriteLine("location: {0}, messagecode: {1}, message: {2}", location, messagecode, pmessage);
            return false;
        }

        public void CreateInstance(string applicationName) {
            var enabledLayers = new HashSet<string>();

            enabledLayers.Add("VK_LAYER_LUNARG_api_dump");
            enabledLayers.Add("VK_LAYER_LUNARG_standard_validation");


            this.Instance = Instance.Create(
                enabledLayers.ToArray(),
                Glfw3.GetRequiredInstanceExtensions().Append(ExtExtensions.DebugReport).ToArray(),
                applicationInfo: new ApplicationInfo {
                    ApplicationName = applicationName,
                    ApplicationVersion = new SharpVk.Version(1, 0, 0),
                    EngineName = "CNM Voxel Engine",
                    EngineVersion = new SharpVk.Version(1, 0, 0),
                    ApiVersion = new SharpVk.Version(1, 0, 0)
                });

            Instance.CreateDebugReportCallback(DebugMessage,
#if DEBUG
                DebugReportFlags.Error | DebugReportFlags.Warning
#else
                    DebugReportFlags.Error
#endif
            );
        }

        public void CreateSurface() { }

        public void SelectPhysicalDevice() { }

        public void CreateLogicalDevice() { }

        public void CreateSwapChain() { }
    }
}