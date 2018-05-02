using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.PostProcessing;

[ExecuteInEditMode]
public class SRPPlayground : RenderPipelineAsset
{
    #if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRPPlayground", priority = 1)]
    static void CreateSRPPlayground()
    {
        var instance = ScriptableObject.CreateInstance<SRPPlayground>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRPPlayground.asset");
    }
    #endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new SRPPlaygroundInstance();
    }
}

public class SRPPlaygroundInstance : RenderPipeline
{
    public SRPPlaygroundInstance()
    {
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //base.Render(renderContext, cameras);
        SRPPlaygroundPipeline.Render(renderContext, cameras);
    }
}

public static class SRPPlaygroundPipeline
{
    private static readonly ShaderPassName m_UnlitPassName = new ShaderPassName("SRPDefaultUnlit");
    private static ShaderPassName passNameDefault = new ShaderPassName("");
    private static ShaderPassName passNameBase = new ShaderPassName("ForwardBase");
    private static ShaderPassName passNameAdd = new ShaderPassName("ForwardAdd");
    private static ShaderPassName passNameShadow = new ShaderPassName("ShadowCaster");

    private static int m_ColorRTid = Shader.PropertyToID("_CameraColorRT");
    private static int m_DepthRTid = Shader.PropertyToID("_CameraDepthTexture");
    private static int m_CopyDepthRTid = Shader.PropertyToID("_CameraCopyDepthTexture");
    private static int m_ShadowMapid = Shader.PropertyToID("_ShadowMapTexture");
    private static RenderTargetIdentifier m_ColorRT = new RenderTargetIdentifier(m_ColorRTid);
    private static RenderTargetIdentifier m_DepthRT = new RenderTargetIdentifier(m_DepthRTid);
    private static RenderTargetIdentifier m_CopyDepthRT = new RenderTargetIdentifier(m_CopyDepthRTid);
    private static RenderTargetIdentifier m_ShadowMap = new RenderTargetIdentifier(m_ShadowMapid);
    private static Material m_CopyDepthMaterial;

    private static RenderTextureFormat m_ColorFormat = RenderTextureFormat.DefaultHDR;
    private static PostProcessRenderContext m_PostProcessRenderContext = new PostProcessRenderContext();
    private static PostProcessLayer m_CameraPostProcessLayer;

    private static RendererConfiguration renderConfig = RendererConfiguration.PerObjectReflectionProbes | 
                                                        RendererConfiguration.PerObjectLightmaps;

    private static void ClearFlag(CommandBuffer cmd, Camera cam)
    {
        bool clearcolor = true;
        bool cleardepth = true;
        if( cam.clearFlags == CameraClearFlags.Skybox || cam.clearFlags == CameraClearFlags.Depth ) {clearcolor = false;}
        cmd.ClearRenderTarget(cleardepth, clearcolor, cam.backgroundColor);
    }

    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras)
    {
        //For scene view
        if(m_CopyDepthMaterial == null) m_CopyDepthMaterial = new Material(Shader.Find("Hidden/MyTestCopyDepth"));

        //************************** SetRenderingFeatures ****************************************
        #if UNITY_EDITOR
            SupportedRenderingFeatures.active = new SupportedRenderingFeatures()
            {
                reflectionProbeSupportFlags = SupportedRenderingFeatures.ReflectionProbeSupportFlags.None,
                defaultMixedLightingMode = SupportedRenderingFeatures.LightmapMixedBakeMode.Subtractive,
                supportedMixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeMode.Subtractive,
                supportedLightmapBakeTypes = LightmapBakeType.Baked | LightmapBakeType.Mixed | LightmapBakeType.Realtime,
                supportedLightmapsModes = LightmapsMode.CombinedDirectional | LightmapsMode.NonDirectional,
                rendererSupportsLightProbeProxyVolumes = false,
                rendererSupportsMotionVectors = false,
                rendererSupportsReceiveShadows = true,
                rendererSupportsReflectionProbes = true
            };
            SceneViewDrawMode.SetupDrawMode();
        #endif

        // //////////////////////////////////////////////////////////////////////////////////
        foreach (Camera camera in cameras)
        {
            camera.depthTextureMode = DepthTextureMode.Depth;

            //************************** UGUI Geometry on scene view *************************
            #if UNITY_EDITOR
                 if (camera.cameraType == CameraType.SceneView)
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            #endif

            //************************** Culling ****************************************
            ScriptableCullingParameters cullingParams;
            if (!CullResults.GetCullingParameters(camera, out cullingParams))
                continue;
            CullResults cull = new CullResults();
            CullResults.Cull(ref cullingParams, context, ref cull);

            //************************** Cam Properties **********************************
            context.SetupCameraProperties(camera);

            //************************** Lighting Variables  *****************************
            CommandBuffer cmdLighting = new CommandBuffer();
            cmdLighting.name = "("+camera.name+")"+ "Lighting variable";
            int additionalLightSet = 0;
            int mainLightIndex = -1;

            Vector4[] lightPositions = new Vector4[8];
            Vector4[] lightColors = new Vector4[8];
            Vector4[] lightAttn = new Vector4[8];
            Vector4[] lightSpotDir = new Vector4[8];

            //Initialise values
            for(int i=0; i <8; i++)
            {
                lightPositions[i] = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);
                lightColors[i] = Color.black;
                lightAttn[i] = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
                lightSpotDir[i] = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);
            }

            for (int i=0; i< cull.visibleLights.Count; i++)
            {
                VisibleLight light = cull.visibleLights[i];

                if(mainLightIndex == -1) //Directional light
                {
                    if (light.lightType == LightType.Directional)
                    {
                        Vector4 dir = light.localToWorld.GetColumn(2);
                        lightPositions[0] = new Vector4(-dir.x, -dir.y, -dir.z, 0);
                        lightColors[0] = light.light.color;

                        /*
                        float lightRangeSqr = light.range * light.range;
                        float fadeStartDistanceSqr = 0.8f * 0.8f * lightRangeSqr;
                        float fadeRangeSqr = (fadeStartDistanceSqr - lightRangeSqr);
                        float oneOverFadeRangeSqr = 1.0f / fadeRangeSqr;
                        float lightRangeSqrOverFadeRangeSqr = -lightRangeSqr / fadeRangeSqr;
                        float quadAtten = 25.0f / lightRangeSqr;
                        lightAttn[0] = new Vector4(quadAtten, oneOverFadeRangeSqr, lightRangeSqrOverFadeRangeSqr, 1.0f);

                        /*
                        SphericalHarmonicsL2 ambientSH = RenderSettings.ambientProbe;
                        Color linearGlossyEnvColor = new Color(ambientSH[0, 0], ambientSH[1, 0], ambientSH[2, 0]) * RenderSettings.reflectionIntensity;
                        Color glossyEnvColor = CoreUtils.ConvertLinearToActiveColorSpace(linearGlossyEnvColor);
                        Shader.SetGlobalVector("_GlossyEnvironmentColor", glossyEnvColor);
                        // Used when subtractive mode is selected
                        Shader.SetGlobalVector("_SubtractiveShadowColor", CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.subtractiveShadowColor));
                        */

                        cmdLighting.SetGlobalVector("_LightColor0", lightColors[0]);
                        cmdLighting.SetGlobalVector("_WorldSpaceLightPos0", lightPositions[0] );
                        mainLightIndex = i;
                    }
                }
                else
                {
                    additionalLightSet++;
                    continue;//so far just do only 1 directional light
                }
            }

            cmdLighting.SetGlobalVectorArray("unity_LightPosition", lightPositions);
            cmdLighting.SetGlobalVectorArray("unity_LightColor", lightColors);
            cmdLighting.SetGlobalVectorArray("unity_LightAtten", lightAttn);
            cmdLighting.SetGlobalVectorArray("unity_SpotDirection", lightSpotDir);

            context.ExecuteCommandBuffer(cmdLighting);
            cmdLighting.Release();

            //************************** Draw Settings  ************************************
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

            DrawRendererSettings drawSettingsDefault = new DrawRendererSettings(camera, passNameDefault);
                drawSettingsDefault.rendererConfiguration = renderConfig;
                drawSettingsDefault.SetShaderPassName(5,m_UnlitPassName);

            DrawRendererSettings drawSettingsBase = new DrawRendererSettings(camera, passNameBase);
                drawSettingsBase.rendererConfiguration = renderConfig;
            DrawRendererSettings drawSettingsAdd = new DrawRendererSettings(camera, passNameAdd);
                drawSettingsAdd.rendererConfiguration = renderConfig;
            DrawRendererSettings drawSettingsShadow = new DrawRendererSettings(camera, passNameShadow);
            //DrawRendererSettings drawSettingsMeta = new DrawRendererSettings(camera, passNameMeta);

            //************************** Set TempRT ************************************
            CommandBuffer cmdTempId = new CommandBuffer();
            cmdTempId.name = "("+camera.name+")"+ "Setup TempRT";

            //Depth
            RenderTextureDescriptor depthRTDesc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
            depthRTDesc.colorFormat = RenderTextureFormat.Depth;
            depthRTDesc.depthBufferBits = 32;
            cmdTempId.GetTemporaryRT(m_DepthRTid, depthRTDesc,FilterMode.Bilinear);

            //Color
            RenderTextureDescriptor colorRTDesc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
            colorRTDesc.colorFormat = m_ColorFormat;
            colorRTDesc.depthBufferBits = 32; // TODO: does the color RT always need depth?
            colorRTDesc.sRGB = true;
            colorRTDesc.msaaSamples = 1;
            colorRTDesc.enableRandomWrite = false;
            cmdTempId.GetTemporaryRT(m_ColorRTid, colorRTDesc,FilterMode.Bilinear);

            //Copy depth for _CameraDepthTexture
            cmdTempId.GetTemporaryRT(m_CopyDepthRTid, depthRTDesc,FilterMode.Bilinear);

            //ShadowMap
            cmdTempId.GetTemporaryRT(m_ShadowMapid, colorRTDesc,FilterMode.Bilinear);

            context.ExecuteCommandBuffer(cmdTempId);
            cmdTempId.Release();

            //************************** Depth (for CameraDepthTexture in shader) ************************************
            //In this pipeline we don't want to make a new depth only shader so we just use shadow caster pass to generate depth
            CommandBuffer cmdDepthOpaque = new CommandBuffer();
            cmdDepthOpaque.name = "("+camera.name+")"+ "Depth for opaque";
            
            cmdDepthOpaque.SetRenderTarget(m_DepthRT);

            // Opaque
            filterSettings.renderQueueRange = RenderQueueRange.opaque;
            drawSettingsShadow.sorting.flags = SortFlags.CommonOpaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsShadow, filterSettings);
           
            cmdDepthOpaque.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, m_DepthRT);

            context.ExecuteCommandBuffer(cmdDepthOpaque);
            cmdDepthOpaque.Release();

            //************************** Preview Cam ************************************
            if (camera.name == "Preview Camera") //So that opaque can render on it
            {
                CommandBuffer cmdPreviewCam = new CommandBuffer();
                cmdPreviewCam.name = "("+camera.name+")"+ "preview camera";
                ClearFlag(cmdPreviewCam,camera);
                cmdPreviewCam.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                context.ExecuteCommandBuffer(cmdPreviewCam);
                cmdPreviewCam.Release();
            }

            //************************** Clear Flags  ************************************
            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "("+camera.name+")"+ "Clear Flag";
            ClearFlag(cmd, camera);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();

            //************************** Skybox ************************************
            if( camera.clearFlags == CameraClearFlags.Skybox) context.DrawSkybox(camera);

            //************************** Opaque ************************************
            filterSettings.renderQueueRange = RenderQueueRange.opaque;
            // Draw OPAQUE objects using DEFAULT pass
            drawSettingsDefault.sorting.flags = SortFlags.CommonOpaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsDefault, filterSettings);
            // Draw OPAQUE objects using BASE pass
            drawSettingsBase.sorting.flags = SortFlags.CommonOpaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsBase, filterSettings);
            // Draw OPAQUE objects using ADD pass
            drawSettingsAdd.sorting.flags = SortFlags.CommonOpaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsAdd, filterSettings);

            //************************** Opaque Texture (Grab Pass) ************************************
            CommandBuffer cmdGrab = new CommandBuffer();
            cmdGrab.name = "("+camera.name+")"+ "Grab Opaque";

            cmdGrab.Blit(BuiltinRenderTextureType.CameraTarget, m_ColorRT);
            cmdGrab.SetGlobalTexture("_GrabOpaqueTexture", m_ColorRT);

            context.ExecuteCommandBuffer(cmdGrab);
            cmdGrab.Release();

            //************************** Post-processing for opaque ************************************
            m_CameraPostProcessLayer = camera.GetComponent<PostProcessLayer>();
            if(m_CameraPostProcessLayer != null && m_CameraPostProcessLayer.enabled)
            {
                //Post-processing
                CommandBuffer cmdpp = new CommandBuffer();
                cmdpp.name = "("+camera.name+")"+ "Post-processing for Opaque";

                cmdpp.Blit( BuiltinRenderTextureType.CameraTarget, m_ColorRT);

                m_PostProcessRenderContext.Reset();
                m_PostProcessRenderContext.camera = camera;
                m_PostProcessRenderContext.source = m_ColorRT;
                m_PostProcessRenderContext.sourceFormat = m_ColorFormat;
                m_PostProcessRenderContext.destination = m_ColorRT;
                m_PostProcessRenderContext.command = cmdpp;
                m_PostProcessRenderContext.flip = camera.targetTexture == null;
                m_CameraPostProcessLayer.Render(m_PostProcessRenderContext);

                cmdpp.Blit(m_ColorRT,BuiltinRenderTextureType.CameraTarget);
                
                context.ExecuteCommandBuffer(cmdpp);
                cmdpp.Release();
            }

            //************************** Depth texture ************************************
            CommandBuffer cmdDepth = new CommandBuffer();
            cmdDepth.name = "("+camera.name+")"+ "Depth";

            if(camera.cameraType == CameraType.SceneView) m_CopyDepthMaterial.EnableKeyword("_FLIPUV");
            else m_CopyDepthMaterial.DisableKeyword("_FLIPUV");
            cmdDepth.Blit(m_DepthRT, m_CopyDepthRT, m_CopyDepthMaterial);
            cmdDepth.SetGlobalTexture(m_DepthRTid, m_CopyDepthRT);
            cmdDepth.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,m_DepthRT);

            context.ExecuteCommandBuffer(cmdDepth);
            cmdDepth.Release();

            //************************** Scene View & Preview Cam ************************************
            #if UNITY_EDITOR
                if (camera.cameraType == CameraType.SceneView) //Copy depth to scene view camera, so that gizmo and grid will appear
                {
                    CommandBuffer cmdSceneDepth = new CommandBuffer();
                    cmdSceneDepth.name = "("+camera.name+")"+ "Copy Depth to SceneViewCamera";
                    cmdSceneDepth.Blit(m_DepthRT, BuiltinRenderTextureType.CameraTarget, m_CopyDepthMaterial);
                    context.ExecuteCommandBuffer(cmdSceneDepth);
                    cmdSceneDepth.Release();
                }
                else if (camera.name == "Preview Camera") //So that transparent can render on it
                {
                    CommandBuffer cmdPreviewCam = new CommandBuffer();
                    cmdPreviewCam.name = "("+camera.name+")"+ "preview camera";
                    cmdPreviewCam.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                    context.ExecuteCommandBuffer(cmdPreviewCam);
                    cmdPreviewCam.Release();
                }
            #endif

            //************************** Transparent ************************************
            filterSettings.renderQueueRange = RenderQueueRange.transparent;

            // Draw TRANSPARENT objects using DEFAULT pass
            drawSettingsDefault.sorting.flags = SortFlags.CommonTransparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsDefault, filterSettings);

            // Draw TRANSPARENT objects using BASE pass
            drawSettingsBase.sorting.flags = SortFlags.CommonTransparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsBase, filterSettings);

            // Draw TRANSPARENT objects using ADD pass
            drawSettingsAdd.sorting.flags = SortFlags.CommonTransparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsAdd, filterSettings);


            //************************** Clean Up ************************************
            CommandBuffer cmdclean = new CommandBuffer();
            cmdclean.name = "("+camera.name+")"+ "Clean Up";
            cmdclean.ReleaseTemporaryRT(m_ColorRTid);
            cmdclean.ReleaseTemporaryRT(m_DepthRTid);
            cmdclean.ReleaseTemporaryRT(m_CopyDepthRTid);
            cmdclean.ReleaseTemporaryRT(m_ShadowMapid);
            context.ExecuteCommandBuffer(cmdclean);
            cmdclean.Release();

            context.Submit();
        }
    }

}