﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SharpVk;
using SharpVk.Glfw;
using SharpVk.Khronos;

namespace CNMEngine.RenderEngine {
    class Renderer : VkInstance{
        public RenderPass renderPass;
        public PipelineLayout pipelineLayout;
        public Pipeline pipeline;
        public ShaderModule[] Shaders = new ShaderModule[2];
        public Framebuffer[] frameBuffers;
        public CommandPool commandPool;
        public CommandBuffer[] commandBuffers;

        public Semaphore imageAvailableSemaphore;
        public Semaphore renderFinishedSemaphore;

        public Renderer(WindowHandle window, IVkHelperFunctions helper) : base(window,helper) {
            this.CreateRenderPass();
            this.CreateShaderModules();
            this.CreateGraphicsPipeline();
            this.CreateFrameBuffers();
            this.CreateCommandPool();
            this.CreateCommandBuffers();
            this.CreateSemaphores();
        }

        public void rebindWindow(WindowHandle window) {
            window = base.window;
        }
        ~Renderer() {
            device.WaitIdle();

            this.renderFinishedSemaphore.Dispose();
            this.renderFinishedSemaphore = null;

            this.imageAvailableSemaphore.Dispose();
            this.imageAvailableSemaphore = null;

            this.commandPool.Dispose();
            this.commandPool = null;

            foreach (var frameBuffer in this.frameBuffers) {
                frameBuffer.Dispose();
            }

            this.frameBuffers = null;

            for (int i = Shaders.Length - 1; i >= 0; i--) {
                Shaders[i].Dispose();
                Shaders[i] = null;
            }

            this.pipeline.Dispose();
            this.pipeline = null;

            this.pipelineLayout.Dispose();
            this.pipelineLayout = null;

            foreach (var imageView in swapChainImageViews) {
                imageView.Dispose();
            }

            swapChainImageViews = null;

            this.renderPass.Dispose();
            this.renderPass = null;
        }

        public void CreateRenderPass() {
            this.renderPass = device.CreateRenderPass(
                new AttachmentDescription {
                    Format = swapChainFormat,
                    Samples = SampleCountFlags.SampleCount1,
                    LoadOp = AttachmentLoadOp.Clear,
                    StoreOp = AttachmentStoreOp.Store,
                    StencilLoadOp = AttachmentLoadOp.DontCare,
                    StencilStoreOp = AttachmentStoreOp.DontCare,
                    InitialLayout = ImageLayout.Undefined,
                    FinalLayout = ImageLayout.PresentSource
                },
                new SubpassDescription {
                    DepthStencilAttachment = new AttachmentReference {
                        Attachment = Constants.AttachmentUnused
                    },
                    PipelineBindPoint = PipelineBindPoint.Graphics,
                    ColorAttachments = new[] {
                        new AttachmentReference {
                            Attachment = 0,
                            Layout = ImageLayout.ColorAttachmentOptimal
                        }
                    }
                },
                new[] {
                    new SubpassDependency {
                        SourceSubpass = Constants.SubpassExternal,
                        DestinationSubpass = 0,
                        SourceStageMask = PipelineStageFlags.BottomOfPipe,
                        SourceAccessMask = AccessFlags.MemoryRead,
                        DestinationStageMask = PipelineStageFlags.ColorAttachmentOutput,
                        DestinationAccessMask = AccessFlags.ColorAttachmentRead | AccessFlags.ColorAttachmentWrite
                    },
                    new SubpassDependency {
                        SourceSubpass = 0,
                        DestinationSubpass = Constants.SubpassExternal,
                        SourceStageMask = PipelineStageFlags.ColorAttachmentOutput,
                        SourceAccessMask = AccessFlags.ColorAttachmentRead | AccessFlags.ColorAttachmentWrite,
                        DestinationStageMask = PipelineStageFlags.BottomOfPipe,
                        DestinationAccessMask = AccessFlags.MemoryRead
                    }
                });
        }


        public void CreateShaderModules() {
            ShaderModule CreateShader(string path) {
                var shaderData = helper.LoadShaderData(path, out int codeSize);

                return device.CreateShaderModule(codeSize, shaderData);
            }

            Shaders[0] = CreateShader(@".\Shaders\vert.spv");

            Shaders[1] = CreateShader(@".\Shaders\frag.spv");
        }

        public void CreateGraphicsPipeline() {
            this.pipelineLayout = device.CreatePipelineLayout(null, null);

            this.pipeline = device.CreateGraphicsPipeline(null,
                new[] {
                    new PipelineShaderStageCreateInfo {
                        Stage = ShaderStageFlags.Vertex,
                        Module = Shaders[0],
                        Name = "main"
                    },
                    new PipelineShaderStageCreateInfo {
                        Stage = ShaderStageFlags.Fragment,
                        Module = Shaders[1],
                        Name = "main"
                    }
                },
                new PipelineVertexInputStateCreateInfo(),
                new PipelineInputAssemblyStateCreateInfo {
                    PrimitiveRestartEnable = false,
                    Topology = PrimitiveTopology.TriangleList
                },
                new PipelineRasterizationStateCreateInfo {
                    DepthClampEnable = false,
                    RasterizerDiscardEnable = false,
                    PolygonMode = PolygonMode.Fill,
                    LineWidth = 1,
                    CullMode = CullModeFlags.Back,
                    FrontFace = FrontFace.Clockwise,
                    DepthBiasEnable = false
                },
                this.pipelineLayout,
                this.renderPass,
                0,
                null,
                -1,
                viewportState: new PipelineViewportStateCreateInfo {
                    Viewports = new[] {
                        new Viewport(0f, 0f, swapChainExtent.Width, swapChainExtent.Height, 0, 1)
                    },
                    Scissors = new[] {
                        new Rect2D(swapChainExtent)
                    }
                },
                colorBlendState: new PipelineColorBlendStateCreateInfo {
                    Attachments = new[] {
                        new PipelineColorBlendAttachmentState {
                            ColorWriteMask = ColorComponentFlags.R
                                             | ColorComponentFlags.G
                                             | ColorComponentFlags.B
                                             | ColorComponentFlags.A,
                            BlendEnable = false
                        }
                    },
                    LogicOpEnable = false
                },
                multisampleState: new PipelineMultisampleStateCreateInfo {
                    SampleShadingEnable = false,
                    RasterizationSamples = SampleCountFlags.SampleCount1,
                    MinSampleShading = 1
                });
        }

        public void CreateFrameBuffers() {
            Framebuffer Create(ImageView imageView) => device.CreateFramebuffer(renderPass,
                imageView,
                swapChainExtent.Width,
                swapChainExtent.Height,
                1);

            this.frameBuffers = swapChainImageViews.Select(Create).ToArray();
        }

        public void CreateCommandPool() {
            QueueFamilyIndices queueFamilies = helper.FindQueueFamilies(physicalDevice, surface);

            this.commandPool = device.CreateCommandPool(queueFamilies.GraphicsFamily.Value);
        }

        public void CreateCommandBuffers() {
            this.commandBuffers = device.AllocateCommandBuffers(this.commandPool, CommandBufferLevel.Primary,
                (uint) this.frameBuffers.Length);

            for (int index = 0; index < this.frameBuffers.Length; index++) {
                var commandBuffer = this.commandBuffers[index];

                commandBuffer.Begin(CommandBufferUsageFlags.SimultaneousUse);

                commandBuffer.BeginRenderPass(this.renderPass,
                    this.frameBuffers[index],
                    new Rect2D(swapChainExtent),
                    new ClearValue(),
                    SubpassContents.Inline);

                commandBuffer.BindPipeline(PipelineBindPoint.Graphics, this.pipeline);

                commandBuffer.Draw(3, 1, 0, 0);

                commandBuffer.EndRenderPass();

                commandBuffer.End();
            }
        }

        public void CreateSemaphores() {
            this.imageAvailableSemaphore = device.CreateSemaphore();
            this.renderFinishedSemaphore = device.CreateSemaphore();
        }

        public void DrawFrame() {
            uint nextImage = swapChain.AcquireNextImage(uint.MaxValue, this.imageAvailableSemaphore, null);

            graphicsQueue.Submit(
                new SubmitInfo {
                    CommandBuffers = new[] {this.commandBuffers[nextImage]},
                    SignalSemaphores = new[] {this.renderFinishedSemaphore},
                    WaitDestinationStageMask = new[] {PipelineStageFlags.ColorAttachmentOutput},
                    WaitSemaphores = new[] {this.imageAvailableSemaphore}
                },
                null);

            presentQueue.Present(this.renderFinishedSemaphore, swapChain, nextImage, new Result[1]);
        }
    }
}