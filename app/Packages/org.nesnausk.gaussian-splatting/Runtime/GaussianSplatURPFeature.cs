// SPDX-License-Identifier: MIT
#if GS_ENABLE_URP

#if !UNITY_6000_0_OR_NEWER
#error Unity Gaussian Splatting URP support only works in Unity 6 or later
#endif

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace GaussianSplatting.Runtime
{
    // ReSharper disable once InconsistentNaming
    class GaussianSplatURPFeature : ScriptableRendererFeature
    {
        class GSRenderPass : ScriptableRenderPass
        {
            const string GaussianSplatRTName = "_GaussianSplatRT";

            const string ProfilerTag = "GaussianSplatRenderGraph";
            static readonly ProfilingSampler s_profilingSampler = new(ProfilerTag);
            static readonly int s_gaussianSplatRT = Shader.PropertyToID(GaussianSplatRTName);
            bool m_LoggedDevicePass;
            int m_DeviceDiagnosticFrames;

            class PassData
            {
                internal UniversalCameraData CameraData;
                internal TextureHandle SourceTexture;
                internal TextureHandle GaussianSplatRT;
                internal bool SampleDevicePixels;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                using var builder = renderGraph.AddUnsafePass(ProfilerTag, out PassData passData);

                var cameraData = frameData.Get<UniversalCameraData>();
                var resourceData = frameData.Get<UniversalResourceData>();
#if UNITY_ANDROID && !UNITY_EDITOR
                if(!m_LoggedDevicePass)
                {
                    m_LoggedDevicePass=true;
                    Debug.Log($"[LobbyGaussian] render xr={cameraData.xr.enabled} views={cameraData.xr.viewCount} target={cameraData.cameraTargetDescriptor.dimension} size={cameraData.cameraTargetDescriptor.width}x{cameraData.cameraTargetDescriptor.height} msaa={cameraData.cameraTargetDescriptor.msaaSamples}");
                }
#endif

                RenderTextureDescriptor rtDesc = cameraData.cameraTargetDescriptor;
                rtDesc.depthBufferBits = 0;
                rtDesc.msaaSamples = 1;
                rtDesc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
                var textureHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, rtDesc, GaussianSplatRTName, true);

                passData.CameraData = cameraData;
                passData.SourceTexture = resourceData.activeColorTexture;
                passData.GaussianSplatRT = textureHandle;
                // Bounded development-player evidence; no per-frame readback or learner UI.
                passData.SampleDevicePixels = Application.platform == RuntimePlatform.Android &&
                    Debug.isDebugBuild && SystemInfo.supportsAsyncGPUReadback && ++m_DeviceDiagnosticFrames == 30;

                builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
                builder.UseTexture(textureHandle, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    var commandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    using var _ = new ProfilingScope(commandBuffer, s_profilingSampler);
                    commandBuffer.SetGlobalTexture(s_gaussianSplatRT, data.GaussianSplatRT);
                    var camera = data.CameraData;
                    bool array = camera.cameraTargetDescriptor.dimension == TextureDimension.Tex2DArray;
                    RTHandle splats = data.GaussianSplatRT;
                    RTHandle target = data.SourceTexture;
                    var size = new Vector2Int(camera.cameraTargetDescriptor.width, camera.cameraTargetDescriptor.height);
                    var xr=camera.xr;
                    bool stereo=xr.enabled;
                    if(stereo)xr.StopSinglePass(commandBuffer);
                    try
                    {
                        int views=stereo?xr.viewCount:1;
                        for(int eye=0;eye<views;eye++)
                        {
                            int slice=array?(stereo?xr.GetTextureArraySlice(eye):0):-1;
                            RenderEye(camera.camera,commandBuffer,splats.nameID,target.nameID,camera.GetViewMatrix(eye),
                                GL.GetGPUProjectionMatrix(camera.GetProjectionMatrix(eye),true),size,slice);
                            if(data.SampleDevicePixels && splats.rt)
                            {
                                int capturedEye=eye;
                                int width=Mathf.Min(64,size.x),height=Mathf.Min(64,size.y);
                                Debug.Log($"[LobbyGaussian] sample eye={eye} slice={slice} camera={camera.camera.transform.position} view={camera.GetViewMatrix(eye)}");
                                commandBuffer.RequestAsyncReadback(splats.rt,0,(size.x-width)/2,width,(size.y-height)/2,height,
                                    Mathf.Max(0,slice),1,TextureFormat.RGBA32,request=>
                                    {
                                        if(request.hasError){Debug.LogWarning($"[LobbyGaussian] centerPatch eye={capturedEye} readbackFailed");return;}
                                        var pixels=request.GetData<Color32>();int visible=0,colored=0;
                                        foreach(var pixel in pixels)
                                        {if(pixel.a>2)visible++;if(pixel.r>2||pixel.g>2||pixel.b>2)colored++;}
                                        Debug.Log($"[LobbyGaussian] centerPatch eye={capturedEye} pixels={pixels.Length} visible={visible} colored={colored}");
                                    });
                            }
                        }
                    }
                    finally
                    {
                        CoreUtils.SetRenderTarget(commandBuffer,target,ClearFlag.None,Color.clear);
                        if(stereo)xr.StartSinglePass(commandBuffer);
                    }
                });
            }
        }

        GSRenderPass m_Pass;
        // Shared by the render-graph pass and off-device array-target regression.
        public static void RenderEye(Camera camera,CommandBuffer cmd,RenderTargetIdentifier splats,
            RenderTargetIdentifier target,Matrix4x4 view,Matrix4x4 projection,Vector2Int size,int slice)
        {
            bool array=slice>=0;
            if(array)cmd.EnableShaderKeyword("GS_TEXTURE_ARRAY");
            cmd.SetGlobalTexture("_GaussianSplatRT",splats);
            CoreUtils.SetRenderTarget(cmd,splats,ClearFlag.Color,Color.clear,depthSlice:slice);
            cmd.SetViewport(new Rect(0,0,size.x,size.y));
            cmd.SetGlobalVector("_ScreenParams",new Vector4(size.x,size.y,1f+1f/size.x,1f+1f/size.y));
            var composite=GaussianSplatRenderSystem.instance.SortAndRenderSplats(camera,cmd,view,projection,size);
            CoreUtils.SetRenderTarget(cmd,target,ClearFlag.None,Color.clear,depthSlice:slice);
            cmd.SetViewport(new Rect(0,0,size.x,size.y));
            cmd.SetGlobalInt("_GaussianEye",array?slice:0);
            if(composite)cmd.DrawProcedural(Matrix4x4.identity,composite,0,MeshTopology.Triangles,3);
            if(array)cmd.DisableShaderKeyword("GS_TEXTURE_ARRAY");
        }
        bool m_HasCamera;

        public override void Create()
        {
            m_Pass = new GSRenderPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };
        }

        public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData)
        {
            m_HasCamera = false;
            var system = GaussianSplatRenderSystem.instance;
            if (system != null && system.GatherSplatsForCamera(cameraData.camera))
                m_HasCamera = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var system = GaussianSplatRenderSystem.instance;
            if (system == null || !system.GatherSplatsForCamera(renderingData.cameraData.camera))
                return;
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass = null;
        }
    }
}

#endif // #if GS_ENABLE_URP
