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
        //Set Rendering Features, it makes the Editor shows you those settings on e.g. LightingSettings panel...
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

        //Create Blit Materials
        if ( SRPPlaygroundPipeline.m_CopyDepthMaterial == null ) 
            SRPPlaygroundPipeline.m_CopyDepthMaterial = new Material ( Shader.Find ( "Hidden/MyTestCopyDepth" ) );
        if ( SRPPlaygroundPipeline.m_ScreenSpaceShadowsMaterial == null ) 
            SRPPlaygroundPipeline.m_ScreenSpaceShadowsMaterial = new Material ( Shader.Find ("Hidden/My/ScreenSpaceShadows") );
    }

    //I don't want my UI layer to do useless things e.g. shadows, post-processing, meaningless blits...
    //So if the Camera.RenderingPath is set to legacy ones, I let it to use my default simple pipeline
    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        Camera[] defaultCameras;
        Camera[] customCameras;

        SRPDefault.FilterCameras(cameras, out defaultCameras, out customCameras);

        SRPPlaygroundPipeline.Render(renderContext, customCameras);
        SRPDefault.Render(renderContext, defaultCameras);
    }
}

public static class SRPPlaygroundPipeline
{
    //Pass Names
    private static readonly ShaderPassName m_UnlitPassName = new ShaderPassName("SRPDefaultUnlit");
    private static ShaderPassName passNameDefault = new ShaderPassName("");
    private static ShaderPassName passNameBase = new ShaderPassName("ForwardBase");
    private static ShaderPassName passNameAdd = new ShaderPassName("ForwardAdd");
    private static ShaderPassName passNameShadow = new ShaderPassName("ShadowCaster"); //For depth

    //Shader Properties
    private static int m_ColorRTid = Shader.PropertyToID("_CameraColorRT");
    private static int m_GrabOpaqueRTid = Shader.PropertyToID("_GrabOpaqueTexture"); //Use in shader, for grab pass
    private static int m_DepthRTid = Shader.PropertyToID("_CameraDepthTexture"); //Use in shader, for soft particle
    private static int m_ShadowMapid = Shader.PropertyToID("_ShadowMapTexture"); //Use in shader, for screen-space shadow
    private static int m_ShadowMapLightid = Shader.PropertyToID("_ShadowMap");

    //Render Targets
    private static RenderTargetIdentifier m_ColorRT = new RenderTargetIdentifier(m_ColorRTid);
    private static RenderTargetIdentifier m_DepthRT = new RenderTargetIdentifier(m_DepthRTid);
    private static RenderTargetIdentifier m_ShadowMap = new RenderTargetIdentifier(m_ShadowMapid);
    private static RenderTargetIdentifier m_ShadowMapLight = new RenderTargetIdentifier(m_ShadowMapLightid);

    //Blit Materials
    public static Material m_CopyDepthMaterial; //This shader generates SV_Depth, scene view needs it
    public static Material m_ScreenSpaceShadowsMaterial;

    //Constants
    private static RenderTextureFormat m_ColorFormat = RenderTextureFormat.DefaultHDR;
    private static RenderTextureFormat m_DepthFormat = RenderTextureFormat.Depth;
    private static RenderTextureFormat m_ShadowFormat = RenderTextureFormat.Shadowmap;
    private static RenderTextureFormat m_ShadowMapFormat = RenderTextureFormat.Default;
    private static int depthBufferBits = 32;
    private static int m_ShadowRes = 1024;

    //Misc
    private static PostProcessRenderContext m_PostProcessRenderContext = new PostProcessRenderContext();
    private static PostProcessLayer m_CameraPostProcessLayer;
    private static RendererConfiguration renderConfig = RendererConfiguration.PerObjectReflectionProbes | 
                                                        RendererConfiguration.PerObjectLightmaps;
                                                        //RendererConfiguration.PerObjectLightProbe; //I don't support it for now

    //Easy ClearRenderTarget, do clear according to settings on camera component
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

            //************************** Lighting Variables *****************************
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
                        
                        float lightRangeSqr = light.range * light.range;
                        float fadeStartDistanceSqr = 0.8f * 0.8f * lightRangeSqr;
                        float fadeRangeSqr = (fadeStartDistanceSqr - lightRangeSqr);
                        float oneOverFadeRangeSqr = 1.0f / fadeRangeSqr;
                        float lightRangeSqrOverFadeRangeSqr = -lightRangeSqr / fadeRangeSqr;
                        float quadAtten = 25.0f / lightRangeSqr;
                        lightAttn[0] = new Vector4(quadAtten, oneOverFadeRangeSqr, lightRangeSqrOverFadeRangeSqr, 1.0f);

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

            //************************** Draw Settings ************************************
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

            DrawRendererSettings drawSettingsDefault = new DrawRendererSettings(camera, passNameDefault);
                drawSettingsDefault.rendererConfiguration = renderConfig;
                drawSettingsDefault.flags = DrawRendererFlags.EnableDynamicBatching;
                drawSettingsDefault.SetShaderPassName(5,m_UnlitPassName);

            DrawRendererSettings drawSettingsBase = new DrawRendererSettings(camera, passNameBase);
                drawSettingsBase.flags = DrawRendererFlags.EnableDynamicBatching;
                drawSettingsBase.rendererConfiguration = renderConfig;

            DrawRendererSettings drawSettingsDepth = new DrawRendererSettings(camera, passNameShadow);
                drawSettingsBase.flags = DrawRendererFlags.EnableDynamicBatching;
                drawSettingsBase.rendererConfiguration = renderConfig;

            //************************** Set TempRT ************************************
            CommandBuffer cmdTempId = new CommandBuffer();
            cmdTempId.name = "("+camera.name+")"+ "Setup TempRT";

            //Depth
            RenderTextureDescriptor depthRTDesc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
            depthRTDesc.colorFormat = m_DepthFormat;
            depthRTDesc.depthBufferBits = depthBufferBits;
            cmdTempId.GetTemporaryRT(m_DepthRTid, depthRTDesc,FilterMode.Bilinear);

            //Color
            RenderTextureDescriptor colorRTDesc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
            colorRTDesc.colorFormat = m_ColorFormat;
            colorRTDesc.depthBufferBits = 0;
            colorRTDesc.sRGB = true;
            colorRTDesc.msaaSamples = 1;
            colorRTDesc.enableRandomWrite = false;
            cmdTempId.GetTemporaryRT(m_ColorRTid, colorRTDesc,FilterMode.Bilinear);

            //Shadow
            RenderTextureDescriptor shadowRTDesc = new RenderTextureDescriptor(m_ShadowRes,m_ShadowRes);
            shadowRTDesc.colorFormat = m_ShadowFormat;
            shadowRTDesc.depthBufferBits = depthBufferBits;
            shadowRTDesc.msaaSamples = 1;
            shadowRTDesc.enableRandomWrite = false;
            cmdTempId.GetTemporaryRT(m_ShadowMapLightid, shadowRTDesc,FilterMode.Bilinear);//depth per light

            //ScreenSpaceShadowMap
            RenderTextureDescriptor shadowMapRTDesc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
            shadowMapRTDesc.colorFormat = m_ShadowMapFormat;
            shadowMapRTDesc.depthBufferBits = 0;
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
            bool successShadowMap = false;
            
            if (doShadow)
            {
                DrawShadowsSettings shadowSettings = new DrawShadowsSettings(cull, mainLightIndex);

                successShadowMap = cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(mainLightIndex,
                        0, 1, new Vector3(1,0,0),
                        m_ShadowRes, mainLight.shadowNearPlane, out view, out proj,
                        out shadowSettings.splitData);

                CommandBuffer cmdShadow = new CommandBuffer();
                cmdShadow.name = "(" + camera.name + ")" + "Shadow Mapping";

                cmdShadow.SetRenderTarget(m_ShadowMapLight);
                cmdShadow.ClearRenderTarget(true, true, Color.black);

                cmdShadow.SetViewport(new Rect(0, 0, m_ShadowRes, m_ShadowRes));
                cmdShadow.EnableScissorRect(new Rect(4, 4, m_ShadowRes - 8, m_ShadowRes - 8));
                cmdShadow.SetViewProjectionMatrices(view, proj);

                context.ExecuteCommandBuffer(cmdShadow);
                cmdShadow.Clear();

                //Render Shadow
                context.DrawShadows(ref shadowSettings);

                cmdShadow.DisableScissorRect();
                context.ExecuteCommandBuffer(cmdShadow);
                cmdShadow.Clear();
                cmdShadow.Release();
            }

            //************************** Camera Parameters ************************************
            context.SetupCameraProperties(camera);

            //************************** Depth (for CameraDepthTexture in shader, also shadowmapping) ************************************
            CommandBuffer cmdDepthOpaque = new CommandBuffer();
            cmdDepthOpaque.name = "(" + camera.name + ")" + "Make CameraDepthTexture";

            //if(camera.cameraType == CameraType.SceneView || camera.name == "Preview Camera" || !SystemInfo.graphicsUVStartsAtTop) 
            //    cmdDepthOpaque.EnableShaderKeyword ("_FLIPUV");
             //   else 
             //   cmdDepthOpaque.DisableShaderKeyword ("_FLIPUV");

            cmdDepthOpaque.SetRenderTarget(m_DepthRT);
            context.ExecuteCommandBuffer(cmdDepthOpaque);
            cmdDepthOpaque.Clear();

            filterSettings.renderQueueRange = RenderQueueRange.opaque;
            drawSettingsDepth.sorting.flags = SortFlags.CommonOpaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsDepth, filterSettings);

            cmdDepthOpaque.SetGlobalTexture(m_DepthRTid, m_DepthRT);
                
            context.ExecuteCommandBuffer(cmdDepthOpaque);
            cmdDepthOpaque.Release();

            //************************** Clear ************************************
            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "("+camera.name+")"+ "Clear Flag";

            cmd.SetRenderTarget(m_ColorRT, m_DepthRT);
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

            //************************** Screen Space Shadow ************************************
            if (doShadow)
            {
                CommandBuffer cmdShadow2 = new CommandBuffer();
                cmdShadow2.name = "("+camera.name+")"+ "Screen Space Shadow";

                cmdShadow2.SetGlobalTexture(m_ShadowMapLightid, m_ShadowMapLight);
                cmdShadow2.SetRenderTarget(m_ShadowMap, m_DepthRT);
                cmdShadow2.ClearRenderTarget(false, true, Color.white);

                if(successShadowMap)
                {
                    cmdShadow2.EnableShaderKeyword("SHADOWS_SCREEN");
                    cmdShadow2.EnableShaderKeyword("LIGHTMAP_SHADOW_MIXING");
                    
                    if (SystemInfo.usesReversedZBuffer)
                    {
                        proj.m20 = -proj.m20;
                        proj.m21 = -proj.m21;
                        proj.m22 = -proj.m22;
                        proj.m23 = -proj.m23;
                    }
                    
                    Matrix4x4 WorldToShadow = proj * view;
    
                    float f = 0.5f;
    
                    var textureScaleAndBias = Matrix4x4.identity;
                    textureScaleAndBias.m00 = f;
                    textureScaleAndBias.m11 = f;
                    textureScaleAndBias.m22 = f;
                    textureScaleAndBias.m03 = f;
                    textureScaleAndBias.m23 = f;
                    textureScaleAndBias.m13 = f;

                    WorldToShadow = textureScaleAndBias * WorldToShadow;

                    cmdShadow2.SetGlobalMatrix("_WorldToShadow", WorldToShadow);
                    cmdShadow2.SetGlobalFloat("_ShadowStrength", mainLight.shadowStrength);
                }

                cmdShadow2.Blit(m_ShadowMap, m_ShadowMap, m_ScreenSpaceShadowsMaterial);
                cmdShadow2.SetGlobalTexture(m_ShadowMapid,m_ShadowMap);
                cmdShadow2.SetRenderTarget(m_ColorRT, m_DepthRT);

                context.ExecuteCommandBuffer(cmdShadow2);
                cmdShadow2.Release();
            }

            //************************** Opaque Texture (Grab Pass) ************************************
            CommandBuffer cmdGrab = new CommandBuffer();
            cmdGrab.name = "("+camera.name+")"+ "Grab Opaque";

            cmdGrab.SetGlobalTexture(m_GrabOpaqueRTid, m_ColorRT);

            context.ExecuteCommandBuffer(cmdGrab);
            cmdGrab.Release();

            //************************** Transparent ************************************
            filterSettings.renderQueueRange = RenderQueueRange.transparent;

            // DEFAULT pass
            drawSettingsDefault.sorting.flags = SortFlags.CommonTransparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsDefault, filterSettings);

            // BASE pass
            drawSettingsBase.sorting.flags = SortFlags.CommonTransparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettingsBase, filterSettings);

            //************************** Blit to Camera Target ************************************
            // so that reflection probes will work 
            CommandBuffer cmdColor = new CommandBuffer();
            cmdColor.name = "("+camera.name+")"+ "blit color to cam target";
            cmdColor.Blit(m_ColorRT, BuiltinRenderTextureType.CameraTarget);
            cmdColor.SetRenderTarget(m_ColorRT, m_DepthRT);
            context.ExecuteCommandBuffer(cmdColor);
            cmdColor.Release();

            //************************** Post-processing ************************************
            m_CameraPostProcessLayer = camera.GetComponent<PostProcessLayer>();
            if(m_CameraPostProcessLayer != null && m_CameraPostProcessLayer.enabled)
            {
                //Post-processing
                CommandBuffer cmdpp = new CommandBuffer();
                cmdpp.name = "("+camera.name+")"+ "Post-processing for Opaque";

                m_PostProcessRenderContext.Reset();
                m_PostProcessRenderContext.camera = camera;
                m_PostProcessRenderContext.source = m_ColorRT;
                m_PostProcessRenderContext.sourceFormat = m_ColorFormat;
                m_PostProcessRenderContext.destination = BuiltinRenderTextureType.CameraTarget;
                m_PostProcessRenderContext.command = cmdpp;
                m_PostProcessRenderContext.flip = camera.targetTexture == null;
                m_CameraPostProcessLayer.Render(m_PostProcessRenderContext);

                cmdpp.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,m_DepthRT);
                
                context.ExecuteCommandBuffer(cmdpp);
                cmdpp.Release();
            }

            //************************** Scene View & Preview Camera Fix ************************************
            #if UNITY_EDITOR
                if (camera.cameraType == CameraType.SceneView) //Scene view needs SV_Depth, so that gizmo and grid will appear
                {
                    CommandBuffer cmdSceneDepth = new CommandBuffer();
                    cmdSceneDepth.name = "("+camera.name+")"+ "Copy Depth to SceneViewCamera";
                    cmdSceneDepth.Blit(m_DepthRT, BuiltinRenderTextureType.CameraTarget, m_CopyDepthMaterial);
                    context.ExecuteCommandBuffer(cmdSceneDepth);
                    cmdSceneDepth.Release();
                }
                else
                if (camera.name == "Preview Camera") //Must do this blit so that it shows up things...because post-processing isn't enabled in preview cam?
                {
                    CommandBuffer cmdPreviewCam = new CommandBuffer();
                    cmdPreviewCam.name = "("+camera.name+")"+ "preview camera";
                    cmdPreviewCam.Blit(m_ColorRT, BuiltinRenderTextureType.CameraTarget);
                    context.ExecuteCommandBuffer(cmdPreviewCam);
                    cmdPreviewCam.Release();
                }
            #endif

            //************************** Clean Up ************************************
            CommandBuffer cmdclean = new CommandBuffer();
            cmdclean.name = "("+camera.name+")"+ "Clean Up";
            cmdclean.ReleaseTemporaryRT(m_ColorRTid);
            cmdclean.ReleaseTemporaryRT(m_DepthRTid);
            cmdclean.ReleaseTemporaryRT(m_ShadowMapid);
            cmdclean.ReleaseTemporaryRT(m_ShadowMapLightid);
            context.ExecuteCommandBuffer(cmdclean);
            cmdclean.Release();

            context.Submit();
        }
    }

}