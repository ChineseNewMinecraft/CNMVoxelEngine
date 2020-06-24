﻿using SharpVk.Khronos;
using SharpVk.Multivendor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SharpVk;
using SharpVk.Glfw;
using SharpVk.Multivendor;
using Image = SharpVk.Image;

namespace CNMEngine.RenderEngine {
    public static class GLFW3Extend {
        [DllImport("glfw3", EntryPoint = "glfwGetWindowSize", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GetWindowSize(WindowHandle window, out uint width, out uint height);
    }
    public struct SwapChainSupportDetails {
        public SurfaceCapabilities Capabilities;
        public SurfaceFormat[] Formats;
        public PresentMode[] PresentModes;
    }

    public struct QueueFamilyIndices {
        public uint? GraphicsFamily;
        public uint? PresentFamily;

        public IEnumerable<uint> Indices {
            get {
                if (this.GraphicsFamily.HasValue) {
                    yield return this.GraphicsFamily.Value;
                }

                if (this.PresentFamily.HasValue && this.PresentFamily != this.GraphicsFamily) {
                    yield return this.PresentFamily.Value;
                }
            }
        }

        public bool IsComplete {
            get {
                return this.GraphicsFamily.HasValue
                       && this.PresentFamily.HasValue;
            }
        }
    }

    public interface IVkHelperFunctions {
        public Bool32 DebugReport(DebugReportFlags flags, DebugReportObjectType objectType, ulong @object,
            HostSize location, int messageCode, string layerPrefix, string message, IntPtr userData);

        public QueueFamilyIndices FindQueueFamilies(PhysicalDevice device, Surface surface);
        public uint[] LoadShaderData(string filePath, out int codeSize);
        public long GetPhysicalDeviceScore(PhysicalDevice device);
    }

    public class VkHelperFunctions : IVkHelperFunctions {
        public static Bool32 _DebugReport(DebugReportFlags flags, DebugReportObjectType objectType, ulong @object,
            HostSize location, int messageCode, string layerPrefix, string message, IntPtr userData) {
            Console.WriteLine(message);

            return false;
        }

        public static QueueFamilyIndices _FindQueueFamilies(PhysicalDevice device, Surface surface) {
            QueueFamilyIndices indices = new QueueFamilyIndices();

            var queueFamilies = device.GetQueueFamilyProperties();

            for (uint index = 0; index < queueFamilies.Length && !indices.IsComplete; index++) {
                if (queueFamilies[index].QueueFlags.HasFlag(QueueFlags.Graphics)) {
                    indices.GraphicsFamily = index;
                }

                if (device.GetSurfaceSupport(index, surface)) {
                    indices.PresentFamily = index;
                }
            }

            return indices;
        }

        public static uint[] _LoadShaderData(string filePath, out int codeSize) {
            var fileBytes = File.ReadAllBytes(filePath);
            var shaderData = new uint[(int) Math.Ceiling(fileBytes.Length / 4f)];

            System.Buffer.BlockCopy(fileBytes, 0, shaderData, 0, fileBytes.Length);

            codeSize = fileBytes.Length;

            return shaderData;
        }

        public Bool32 DebugReport(DebugReportFlags flags, DebugReportObjectType objectType, ulong @object,
            HostSize location, int messageCode, string layerPrefix, string message, IntPtr userData) {
            return VkHelperFunctions._DebugReport(flags, objectType, @object, location, messageCode, layerPrefix, message, userData);
        }

        public QueueFamilyIndices FindQueueFamilies(PhysicalDevice device, Surface surface) {
            return VkHelperFunctions._FindQueueFamilies(device, surface);
        }

        public uint[] LoadShaderData(string filePath, out int codeSize) {
            return VkHelperFunctions._LoadShaderData(filePath, out codeSize);
        }

        public long GetPhysicalDeviceScore(PhysicalDevice device) {
            long score = 0;
            var prop = device.GetProperties();
            score += prop.Limits.MaxImageDimension2D;
            return score;
        }
    }

    public class VkInstance {
        public uint SurfaceWidth;
        public uint SurfaceHeight;
        public IVkHelperFunctions helper;

        public WindowHandle window;

        public Instance instance;
        public Surface surface;
        public PhysicalDevice physicalDevice;
        public Device device;
        public Queue graphicsQueue;
        public Queue presentQueue;

        public Swapchain swapChain;
        public Image[] swapChainImages;
        public ImageView[] swapChainImageViews;
        public Format swapChainFormat;
        public Extent2D swapChainExtent;

        public VkInstance(WindowHandle window, IVkHelperFunctions helper) {
            this.window = window;
            GLFW3Extend.GetWindowSize(window,out uint x,out uint y);

            this.SurfaceWidth = x;
            this.SurfaceHeight = y;

            this.helper = helper;

            this.CreateInstance();
            this.CreateSurface();
            this.PickPhysicalDevice();
            this.CreateLogicalDevice();
            this.CreateSwapChain();
            this.CreateImageViews();
        }

        ~VkInstance() {
            this.swapChain.Dispose();
            this.swapChain = null;

            this.device.Dispose();
            this.device = null;

            this.surface.Dispose();
            this.surface = null;

            this.instance.Dispose();
            this.instance = null;
        }

        public void CreateInstance() {
            var enabledLayers = new List<string>();

            //VK_LAYER_LUNARG_api_dump
            //VK_LAYER_LUNARG_standard_validation
            var prop = Instance.EnumerateLayerProperties();
            void AddAvailableLayer(string layerName, LayerProperties[] prop) {
                if (prop.Any(x => x.LayerName == layerName)) {
                    enabledLayers.Add(layerName);
                }
            }
#if DEBUG
            AddAvailableLayer("VK_LAYER_LUNARG_standard_validation", prop);
#if APIDUMP
            AddAvailableLayer("VK_LAYER_LUNARG_api_dump",prop);
#endif
#endif

            this.instance = Instance.Create(
                enabledLayers.ToArray(),
                Glfw3.GetRequiredInstanceExtensions().Append(ExtExtensions.DebugReport).ToArray(),
                applicationInfo: new ApplicationInfo {
                    ApplicationName = "Hello Triangle",
                    ApplicationVersion = new SharpVk.Version(1, 0, 0),
                    EngineName = "SharpVk",
                    EngineVersion = new SharpVk.Version(0, 4, 1),
                    ApiVersion = new SharpVk.Version(1, 0, 0)
                });

            instance.CreateDebugReportCallback(helper.DebugReport, DebugReportFlags.Error | DebugReportFlags.Warning);
        }

        public void CreateSurface() {
            this.surface = this.instance.CreateGlfw3Surface(this.window);
        }

        public void PickPhysicalDevice() {
            var availableDevices = this.instance.EnumeratePhysicalDevices();
            var ranked = availableDevices.Select(device => { return helper.GetPhysicalDeviceScore(device); }).ToList();
            this.physicalDevice = availableDevices[ranked.IndexOf(ranked.Max())];
        }

        public void CreateLogicalDevice() {
            QueueFamilyIndices queueFamilies = helper.FindQueueFamilies(this.physicalDevice, surface);

            this.device = physicalDevice.CreateDevice(queueFamilies.Indices
                    .Select(index => new DeviceQueueCreateInfo {
                        QueueFamilyIndex = index,
                        QueuePriorities = new[] {1f}
                    }).ToArray(),
                null,
                KhrExtensions.Swapchain);

            this.graphicsQueue = this.device.GetQueue(queueFamilies.GraphicsFamily.Value, 0);
            this.presentQueue = this.device.GetQueue(queueFamilies.PresentFamily.Value, 0);
        }

        public void CreateSwapChain() {
            SwapChainSupportDetails swapChainSupport = this.QuerySwapChainSupport(this.physicalDevice);

            uint imageCount = swapChainSupport.Capabilities.MinImageCount + 1;
            if (swapChainSupport.Capabilities.MaxImageCount > 0 &&
                imageCount > swapChainSupport.Capabilities.MaxImageCount) {
                imageCount = swapChainSupport.Capabilities.MaxImageCount;
            }

            SurfaceFormat surfaceFormat = this.ChooseSwapSurfaceFormat(swapChainSupport.Formats);

            QueueFamilyIndices queueFamilies = helper.FindQueueFamilies(this.physicalDevice, surface);

            var indices = queueFamilies.Indices.ToArray();

            Extent2D extent = this.ChooseSwapExtent(swapChainSupport.Capabilities);

            this.swapChain = device.CreateSwapchain(surface,
                imageCount,
                surfaceFormat.Format,
                surfaceFormat.ColorSpace,
                extent,
                1,
                ImageUsageFlags.ColorAttachment,
                indices.Length == 1
                    ? SharingMode.Exclusive
                    : SharingMode.Concurrent,
                indices,
                swapChainSupport.Capabilities.CurrentTransform,
                CompositeAlphaFlags.Opaque,
                this.ChooseSwapPresentMode(swapChainSupport.PresentModes),
                true,
                this.swapChain);

            this.swapChainImages = this.swapChain.GetImages();
            this.swapChainFormat = surfaceFormat.Format;
            this.swapChainExtent = extent;
        }

        public void CreateImageViews() {
            this.swapChainImageViews = this.swapChainImages
                .Select(image => device.CreateImageView(image,
                    ImageViewType.ImageView2d,
                    this.swapChainFormat,
                    ComponentMapping.Identity,
                    new ImageSubresourceRange(ImageAspectFlags.Color, 0, 1, 0, 1)))
                .ToArray();
        }

        public SurfaceFormat ChooseSwapSurfaceFormat(SurfaceFormat[] availableFormats) {
            if (availableFormats.Length == 1 && availableFormats[0].Format == Format.Undefined) {
                return new SurfaceFormat {
                    Format = Format.B8G8R8A8UNorm,
                    ColorSpace = ColorSpace.SrgbNonlinear
                };
            }

            foreach (var format in availableFormats) {
                if (format.Format == Format.B8G8R8A8UNorm && format.ColorSpace == ColorSpace.SrgbNonlinear) {
                    return format;
                }
            }

            return availableFormats[0];
        }

        public PresentMode ChooseSwapPresentMode(PresentMode[] availablePresentModes) {
            return availablePresentModes.Contains(PresentMode.Mailbox)
                ? PresentMode.Mailbox
                : PresentMode.Fifo;
        }

        public Extent2D ChooseSwapExtent(SurfaceCapabilities capabilities) {
            if (capabilities.CurrentExtent.Width != uint.MaxValue) {
                return capabilities.CurrentExtent;
            }
            else {
                return new Extent2D {
                    Width = Math.Max(capabilities.MinImageExtent.Width,
                        Math.Min(capabilities.MaxImageExtent.Width, (uint)SurfaceWidth)),
                    Height = Math.Max(capabilities.MinImageExtent.Height,
                        Math.Min(capabilities.MaxImageExtent.Height, (uint)SurfaceHeight))
                };
            }
        }

        SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice device) {
            return new SwapChainSupportDetails {
                Capabilities = device.GetSurfaceCapabilities(this.surface),
                Formats = device.GetSurfaceFormats(this.surface),
                PresentModes = device.GetSurfacePresentModes(this.surface)
            };
        }

        public bool IsSuitableDevice(PhysicalDevice device) {
            return device.EnumerateDeviceExtensionProperties(null)
                       .Any(extension => extension.ExtensionName == KhrExtensions.Swapchain)
                   && helper.FindQueueFamilies(device, surface).IsComplete;
        }
    }
}