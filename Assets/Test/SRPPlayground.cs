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
    //Pass Names
    private static readonly ShaderPassName m_UnlitPassName = new ShaderPassName("SRPDefaultUnlit");
    private static ShaderPassName passNameDefault = new ShaderPassName("");
    private static ShaderPassName passNameBase = new ShaderPassName("ForwardBase");
    private static ShaderPassName passNameAdd = new ShaderPassName("ForwardAdd");
    private static ShaderPassName passNameShadow = new ShaderPassName("ShadowCaster");

    //Shader Properties
    private static int m_ColorRTid = Shader.PropertyToID("_CameraColorRT");
    private static int m_GrabOpaqueRTid = Shader.PropertyToID("_GrabOpaqueTexture"); //Use in shader, for grab pass
    private static int m_DepthRTid = Shader.PropertyToID("_CameraDepthTexture"); //Use in shader, for soft particle
    private static int m_CopyDepthRTid = Shader.PropertyToID("_CameraCopyDepthTexture");
    private static int m_ShadowMapid = Shader.PropertyToID("_ShadowMap"); //Use in shader, for screen-space shadow
    private static int m_ShadowMapLightid = Shader.PropertyToID("_ShadowMapTexture");

    //Render Targets
    private static RenderTargetIdentifier m_ColorRT = new RenderTargetIdentifier(m_ColorRTid);
    private static RenderTargetIdentifier m_DepthRT = new RenderTargetIdentifier(m_DepthRTid);
    private static RenderTargetIdentifier m_CopyDepthRT = new RenderTargetIdentifier(m_CopyDepthRTid);
    private static RenderTargetIdentifier m_ShadowMap = new RenderTargetIdentifier(m_ShadowMapid);
    private static RenderTargetIdentifier m_ShadowMapLight = new RenderTargetIdentifier(m_ShadowMapLightid);

    //Blit Materials
    private static Material m_CopyDepthMaterial;
    private static Material m_ScreenSpaceShadowsMaterial;

    //Constants
    private static RenderTextureFormat m_ColorFormat = RenderTextureFormat.DefaultHDR;
    private static RenderTextureFormat m_DepthFormat = RenderTextureFormat.Depth;
    private static RenderTextureFormat m_ShadowFormat = RenderTextureFormat.Shadowmap;
    private static RenderTextureFormat m_ShadowMapFormat = RenderTextureFormat.ARGB32;
    private static int depthBufferBits = 32;
    private static int m_ShadowRes = 1024;

    //Misc
    private static PostProcessRenderContext m_PostProcessRenderContext = new PostProcessRenderContext();
    private static PostProcessLayer m_CameraPostProcessLayer;
    private static RendererConfiguration renderConfig = RendererConfiguration.PerObjectReflectionProbes | 
                                                        RendererConfiguration.PerObjectLightmaps;

    //Easy ClearRenderTarget
    private static void ClearFlag(CommandBuffer cmd, Camera cam, Color color)
    {
        bool clearcolor = true;
        bool cleardepth = true;
        if( cam.clearFlags == CameraClearFlags.Skybox || cam.clearFlags == CameraClearFlags.Depth ) {clearcolor = false;}
        cmd.ClearRenderTarget(cleardepth, clearcolor, color);
    }

    //Starts Rendering Part
    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras)
    {
        //************************** Create Blit Materials ****************************************
        if ( m_CopyDepthMaterial == null ) m_CopyDepthMaterial = new Material ( Shader.Find ( "Hidden/MyTestCopyDepth" ) );
        if ( m_ScreenSpaceShadowsMaterial == null ) m_ScreenSpaceShadowsMaterial = new Material ( Shader.Find ( "Hidden/MyInternal-ScreenSpaceShadows" ) );

        //************************** Set Rendering Features ****************************************
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

        // //////////////////////////////////// START EACH CAMERA RENDERING //////////////////////////////////////////////
        foreach (Camera camera in cameras)
        {
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

            //************************** Lighting Variables  *****************************
            CommandBuffer cmdLighting = new CommandBuffer();
            cmdLighting.name = "("+camera.name+")"+ "Lighting variable";
            int additionalLightSet = 0;
            int mainLightIndex = -1;
            Light mainLight = null;

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

                        mainLight = light.light;
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
                //drawSettingsAdd.rendererConfiguration = renderConfig;

            //************************** Set TempRT ************************************
            CommandBuffer cmdTempId = new CommandBuffer();
            cmdTempId.name = "("+camera.name+")"+ "Setup TempRT";

            //Depth
            RenderTextureDescriptor depthRTDesc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
            depthRTDesc.colorFormat = m_DepthFormat;
            depthRTDesc.depthBufferBits = depthBufferBits;
            cmdTempId.GetTemporaryRT(m_DepthRTid, depthRTDesc,FilterMode.Bilinear);

            //Copy depth for _CameraDepthTexture
            cmdTempId.GetTemporaryRT(m_CopyDepthRTid, depthRTDesc, FilterMode.Bilinear);

            //Color
            RenderTextureDescriptor colorRTDesc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
            colorRTDesc.colorFormat = m_ColorFormat;
            colorRTDesc.depthBufferBits = depthBufferBits;
            colorRTDesc.sRGB = true;
            colorRTDesc.msaaSamples = 1;
            colorRTDesc.enableRandomWrite = false;
            cmdTempId.GetTemporaryRT(m_ColorRTid, colorRTDesc,FilterMode.Bilinear);

            //Shadow
            RenderTextureDescriptor shadowRTDesc = new RenderTextureDescriptor(m_ShadowRes,m_ShadowRes);
            shadowRTDesc.colorFormat = m_ShadowFormat;
            shadowRTDesc.depthBufferBits = depthBufferBits;
            shadowRTDesc.sRGB = true;
            shadowRTDesc.msaaSamples = 1;
            shadowRTDesc.enableRandomWrite = false;
            cmdTempId.GetTemporaryRT(m_ShadowMapLightid, shadowRTDesc,FilterMode.Bilinear);//depth per light

            //ScreenSpaceShadowMap
            RenderTextureDescriptor shadowMapRTDesc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
            shadowMapRTDesc.colorFormat = m_ShadowMapFormat;
            shadowMapRTDesc.depthBufferBits = depthBufferBits;
            shadowMapRTDesc.sRGB = true;
            shadowMapRTDesc.msaaSamples = 1;
            shadowMapRTDesc.enableRandomWrite = false;
            cmdTempId.GetTemporaryRT(m_ShadowMapid, shadowMapRTDesc, FilterMode.Bilinear);//screen space shadow

            context.ExecuteCommandBuffer(cmdTempId);
            cmdTempId.Release();
            
            //************************** Do shadow? ************************************
            Bounds bounds;
            bool doShadow = cull.GetShadowCasterBounds(mainLightIndex, out bounds);
            Matrix4x4 view = Matrix4x4.identity;
            Matrix4x4 proj = Matrix4x4.identity;

            //************************** Shadow Mapping ************************************
            if (doShadow)
            {
                DrawShadowsSettings shadowSettings = new DrawShadowsSettings(cull, mainLightIndex);

                bool success = cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(mainLightIndex,
                        0, 1, new Vector3(0.05f, 0.2f, 0.3f),
                        m_ShadowRes, mainLight.shadowNearPlane, out view, out proj,
                        out shadowSettings.splitData);

                CommandBuffer cmdShadow = new CommandBuffer();
                cmdShadow.name = "(" + camera.name + ")" + "Shadow Mapping";

                cmdShadow.SetRenderTarget(m_ShadowMapLight);
                cmdShadow.ClearRenderTarget(true, true, Color.black);
                cmdShadow.SetViewProjectionMatrices(view, proj);

                context.ExecuteCommandBuffer(cmdShadow);
                cmdShadow.Release();

                //Render Shadow
                context.DrawShadows(ref shadowSettings);
                //Reset matrices
                context.SetupCameraProperties(camera);
            }

            //************************** Depth (for CameraDepthTexture in shader, also shadowmapping) ************************************
            CommandBuffer cmdDepthOpaque = new CommandBuffer();
            cmdDepthOpaque.name = "(" + camera.name + ")" + "Before Depth";
                cmdDepthOpaque.SetRenderTarget(m_DepthRT);
                ClearFlag(cmdDepthOpaque, camera, Color.black);
            context.ExecuteCommandBuffer(cmdDepthOpaque);
            cmdDepthOpaque.Release();

            // Opaque
            filterSettings.renderQueueRange = RenderQueueRange.opaque;
            drawSettingsShadow.sorting.flags = SortFlags.CommonOpaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsShadow, filterSettings);

            CommandBuffer cmdDepthOpaque2 = new CommandBuffer();
            cmdDepthOpaque2.name = "(" + camera.name + ")" + "After Depth";
                cmdDepthOpaque2.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, m_DepthRT);
            context.ExecuteCommandBuffer(cmdDepthOpaque2);
            cmdDepthOpaque2.Release();

            //************************** Collect Shadow ************************************
            if (doShadow)
            {
                CommandBuffer cmdShadow2 = new CommandBuffer();
                cmdShadow2.name = "("+camera.name+")"+ "Screen Space Shadow";

                cmdShadow2.SetGlobalTexture(m_ShadowMapLightid, m_ShadowMapLight); //internal one gets _ShadowMapTexture, i mess up naming
                cmdShadow2.SetRenderTarget(m_ShadowMap);
                cmdShadow2.ClearRenderTarget(true, true, Color.white);
                
                //cmdShadow2.EnableShaderKeyword("SHADOWS_SINGLE_CASCADE");

                //Setup shadow variables

                //_LightShadowData.x - shadow strength
                //_LightShadowData.y - Appears to be unused
                //_LightShadowData.z - 1.0 / shadow far distance
                //_LightShadowData.w - shadow near distance
                Vector4 LightShadowData = new Vector4(1, 0, 0.05f, -10f);
                        //mainLight.shadowStrength,
                        //0, 1f / (QualitySettings.shadowDistance),
                       // QualitySettings.shadowNearPlaneOffset);
                    cmdShadow2.SetGlobalVector("_LightShadowData", LightShadowData);
                    Matrix4x4 WorldToShadow = view * proj;
                    cmdShadow2.SetGlobalMatrix("unity_WorldToShadow0", WorldToShadow);
                cmdShadow2.SetGlobalFloat("_ShadowStrength", mainLight.shadowStrength);

                cmdShadow2.Blit(m_ShadowMap, m_ShadowMap, m_ScreenSpaceShadowsMaterial);

                cmdShadow2.SetGlobalTexture(m_ShadowMapid,m_ShadowMap);
                cmdShadow2.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, m_DepthRT);

                context.ExecuteCommandBuffer(cmdShadow2);
                cmdShadow2.Release();
            }

            //************************** Preview Cam Fix ************************************
            if (camera.name == "Preview Camera") //So that opaque can render on it
            {
                CommandBuffer cmdPreviewCam = new CommandBuffer();
                cmdPreviewCam.name = "("+camera.name+")"+ "preview camera";
                    ClearFlag(cmdPreviewCam,camera, camera.backgroundColor);
                    cmdPreviewCam.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                context.ExecuteCommandBuffer(cmdPreviewCam);
                cmdPreviewCam.Release();
            }

            //************************** Clear ************************************
            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "("+camera.name+")"+ "Clear Flag";
                ClearFlag(cmd, camera, camera.backgroundColor);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();

            //************************** Skybox ************************************
            if( camera.clearFlags == CameraClearFlags.Skybox) context.DrawSkybox(camera);

            //************************** Opaque ************************************
            filterSettings.renderQueueRange = RenderQueueRange.opaque;
            // DEFAULT pass, draw shaders without a pass name
            drawSettingsDefault.sorting.flags = SortFlags.CommonOpaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsDefault, filterSettings);
            // BASE pass
            drawSettingsBase.sorting.flags = SortFlags.CommonOpaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsBase, filterSettings);
            // ADD pass
            drawSettingsAdd.sorting.flags = SortFlags.CommonOpaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsAdd, filterSettings);

            //************************** Opaque Texture (Grab Pass) ************************************
            CommandBuffer cmdGrab = new CommandBuffer();
            cmdGrab.name = "("+camera.name+")"+ "Grab Opaque";

            cmdGrab.Blit(BuiltinRenderTextureType.CameraTarget, m_ColorRT);
            cmdGrab.SetGlobalTexture(m_GrabOpaqueRTid, m_ColorRT);

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

            //************************** CameraDepthTexture ************************************
            CommandBuffer cmdDepth = new CommandBuffer();
            cmdDepth.name = "("+camera.name+")"+ "Depth";

            if(camera.cameraType == CameraType.SceneView) m_CopyDepthMaterial.EnableKeyword("_FLIPUV");
                else m_CopyDepthMaterial.DisableKeyword("_FLIPUV");
            cmdDepth.Blit(m_DepthRT, m_CopyDepthRT, m_CopyDepthMaterial);
            cmdDepth.SetGlobalTexture(m_DepthRTid, m_CopyDepthRT);
            cmdDepth.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,m_DepthRT);

            context.ExecuteCommandBuffer(cmdDepth);
            cmdDepth.Release();

            //************************** Scene View & Preview Cam Fix ************************************
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

            // DEFAULT pass
            drawSettingsDefault.sorting.flags = SortFlags.CommonTransparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsDefault, filterSettings);

            // BASE pass
            drawSettingsBase.sorting.flags = SortFlags.CommonTransparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsBase, filterSettings);

            // ADD pass
            drawSettingsAdd.sorting.flags = SortFlags.CommonTransparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsAdd, filterSettings);


            //************************** Clean Up ************************************
            CommandBuffer cmdclean = new CommandBuffer();
            cmdclean.name = "("+camera.name+")"+ "Clean Up";
            cmdclean.ReleaseTemporaryRT(m_ColorRTid);
            cmdclean.ReleaseTemporaryRT(m_DepthRTid);
            cmdclean.ReleaseTemporaryRT(m_CopyDepthRTid);
            cmdclean.ReleaseTemporaryRT(m_ShadowMapid);
            cmdclean.ReleaseTemporaryRT(m_ShadowMapLightid);
            context.ExecuteCommandBuffer(cmdclean);
            cmdclean.Release();

            context.Submit();
        }
    }

}