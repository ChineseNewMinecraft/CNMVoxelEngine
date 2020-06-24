﻿using SharpVk.Glfw;
using SharpVk.Khronos;
using SharpVk.Multivendor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
 using System.Reflection;
 using CNMEngine.RenderEngine;
using SharpVk;
using Version = System.Version;

namespace CNMEngine {

    class Programme {
        public void Run() {
            var helper = new VkHelperFunctions();
            Glfw3.Init();

            Glfw3.WindowHint(WindowAttribute.ClientApi, 0); 
            var window = Glfw3.CreateWindow(1024, 768, "shit", default, default);
            var renderer = new Renderer(window,helper);
            // 这个必须在主函数
            Glfw3.SetWindowSizeCallback(window,((window, width, height) => {
                renderer.device.WaitIdle();

                renderer.commandPool.FreeCommandBuffers(renderer.commandBuffers);

                foreach (var frameBuffer in renderer.frameBuffers) {
                    frameBuffer.Dispose();
                }

                renderer.frameBuffers = null;

                renderer.pipeline.Dispose();
                renderer.pipeline = null;

                renderer.pipelineLayout.Dispose();
                renderer.pipelineLayout = null;

                foreach (var imageView in renderer.swapChainImageViews) {
                    imageView.Dispose();
                }

                renderer.swapChainImageViews = null;

                renderer.renderPass.Dispose();
                renderer.renderPass = null;

                renderer.swapChain.Dispose();
                renderer.swapChain = null;

                renderer.CreateSwapChain();
                renderer.CreateImageViews();
                renderer.CreateRenderPass();
                renderer.CreateGraphicsPipeline();
                renderer.CreateFrameBuffers();
                renderer.CreateCommandBuffers();
            }));
            while (!Glfw3.WindowShouldClose(window)) {
                renderer.DrawFrame();
                Glfw3.PollEvents();
            }
        }

        public static void Main() {
            new Programme().Run();
        }
    }
}