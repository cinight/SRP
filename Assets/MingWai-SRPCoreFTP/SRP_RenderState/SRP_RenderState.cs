using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR;

// Very basic scriptable rendering loop example:
// - Use with BasicRenderPipelineShader.shader (the loop expects "BasicPass" pass type to exist)
// - Supports up to 8 enabled lights in the scene (directional, point or spot)
// - Does the same physically based BRDF as the Standard shader
// - No shadows
// - This loop also does not setup lightmaps, light probes, reflection probes or light cookies

[ExecuteInEditMode]
public class SRP_RenderState : RenderPipelineAsset
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP_RenderState", priority = 1)]
    static void CreateSRP_RenderState()
    {
        var instance = ScriptableObject.CreateInstance<SRP_RenderState>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRP_RenderState.asset");
    }

#endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new SRP_RenderStateInstance();
    }
}

public class SRP_RenderStateInstance : RenderPipeline
{
    public SRP_RenderStateInstance()
    {
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //base.Render(renderContext, cameras);
        SRP_RenderStateRendering.Render(renderContext, cameras);
    }
}

public static class SRP_RenderStateRendering
{
    // Main entry point for our scriptable render loop
    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras)
    {
        bool stereoEnabled = XRSettings.isDeviceActive;

        foreach (Camera camera in cameras)
        {
            // Culling
            ScriptableCullingParameters cullingParams;

            // Stereo-aware culling parameters are configured to perform a single cull for both eyes
            if (!CullResults.GetCullingParameters(camera, stereoEnabled, out cullingParams))
                continue;
            CullResults cull = new CullResults();
            CullResults.Cull(ref cullingParams, context, ref cull);

            // Setup camera for rendering (sets render target, view/projection matrices and other
            // per-camera built-in shader variables).
            // If stereo is enabled, we also configure stereo matrices, viewports, and XR device render targets
            context.SetupCameraProperties(camera, stereoEnabled);

            // Draws in-between [Start|Stop]MultiEye are stereo-ized by engine
            if (stereoEnabled)
            {
                context.StartMultiEye(camera);
            }

            // clear depth buffer
            CommandBuffer cmd = new CommandBuffer();
            cmd.ClearRenderTarget(true, false, Color.black);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();

            // Setup global lighting shader variables
            SetupLightShaderVariables(cull.visibleLights, context);

            // Draw skybox
            context.DrawSkybox(camera);

            // Setup DrawSettings and FilterSettings
            ShaderPassName passName = new ShaderPassName("BasicPass");
            DrawRendererSettings drawSettings = new DrawRendererSettings(camera, passName);
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

            //*************************************************************
            // Block
            RenderStateBlock rsb = new RenderStateBlock(RenderStateMask.Depth | RenderStateMask.Blend | RenderStateMask.Raster | RenderStateMask.Stencil );

            DepthState ds = rsb.depthState;//
            ds.writeEnabled = true;
            ds.compareFunction = CompareFunction.LessEqual;
            rsb.depthState = ds;//

            BlendState bs = rsb.blendState;//
            RenderTargetBlendState rs0 = bs.blendState0;//
            bs.alphaToMask = false;
            rs0.sourceColorBlendMode = BlendMode.SrcAlpha;
            rs0.destinationColorBlendMode = BlendMode.One;
            rs0.colorBlendOperation = BlendOp.Add;
            rs0.sourceAlphaBlendMode = BlendMode.Zero;
            rs0.destinationAlphaBlendMode = BlendMode.One;
            rs0.alphaBlendOperation = BlendOp.Add;
            rs0.writeMask = ColorWriteMask.All;
            bs.blendState0 = rs0;//
            rsb.blendState = bs;//

            RasterState rs = rsb.rasterState;//
            rs.cullingMode = CullMode.Off;
            rs.depthClip = false;
            rs.offsetFactor = 0;
            rs.offsetUnits = 0;
            rsb.rasterState = rs;//


            StencilState ss = rsb.stencilState;//
            rsb.stencilReference = 0;
            ss.compareFunction = CompareFunction.Disabled;
            ss.compareFunctionBack = CompareFunction.Disabled;
            ss.compareFunctionFront = CompareFunction.Disabled;
            ss.failOperation = StencilOp.Keep;
            ss.failOperationBack = StencilOp.Keep;
            ss.failOperationFront = StencilOp.Keep;
            ss.passOperation = StencilOp.Keep;
            ss.passOperationBack = StencilOp.Keep;
            ss.passOperationFront = StencilOp.Keep;
            ss.zFailOperation = StencilOp.Keep;
            ss.zFailOperationBack = StencilOp.Keep;
            ss.zFailOperationFront = StencilOp.Keep;
            ss.readMask = 255;
            ss.writeMask = 255;
            ss.enabled = true;
            rsb.stencilState = ss;//

            //**************************************************************
            //mapping

            RenderStateBlock rsb_opaque = new RenderStateBlock( RenderStateMask.Raster | RenderStateMask.Stencil);
            rsb_opaque.rasterState = rsb.rasterState;
            rsb_opaque.stencilState = rsb.stencilState;
            rsb_opaque.stencilReference = rsb.stencilReference;

            RenderStateBlock rsb_trans = new RenderStateBlock( RenderStateMask.Blend );
            rsb_trans.blendState = rsb.blendState;

            RenderStateBlock rsb_over = new RenderStateBlock( RenderStateMask.Raster | RenderStateMask.Depth | RenderStateMask.Stencil);
            rsb_over.depthState = rsb.depthState;
            rsb_over.rasterState = rsb.rasterState;
            rsb_over.stencilState = rsb.stencilState;
            rsb_over.stencilReference = rsb.stencilReference;

            List<RenderStateMapping> rsm = new List<RenderStateMapping>
            {
                new RenderStateMapping("Opaque", rsb_opaque),
                new RenderStateMapping("Transparent", rsb_trans),
                new RenderStateMapping("Overlay", rsb_over)
            };

            //**************************************************************

            // Draw opaque objects using BasicPass shader pass
            filterSettings.layerMask = LayerMask.GetMask("Default");
            drawSettings.sorting.flags = SortFlags.CommonOpaque;
            filterSettings.renderQueueRange = RenderQueueRange.opaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

            // WITH RENDERSTATEBLOCK OPAQUE
            filterSettings.layerMask = LayerMask.GetMask("TransparentFX");
            drawSettings.sorting.flags = SortFlags.CommonOpaque;
            filterSettings.renderQueueRange = RenderQueueRange.opaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings, rsb);

            // WITH RENDERSTATEMAPPING OPAQUE
            filterSettings.layerMask = LayerMask.GetMask("Ignore Raycast");
            drawSettings.sorting.flags = SortFlags.CommonOpaque;
            filterSettings.renderQueueRange = RenderQueueRange.opaque;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings, rsm);

            //**************************************************************

            // Draw transparent objects using BasicPass shader pass
            filterSettings.layerMask = LayerMask.GetMask("Default");
            drawSettings.sorting.flags = SortFlags.CommonTransparent;
            filterSettings.renderQueueRange = RenderQueueRange.transparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

            // WITH RENDERSTATEBLOCK TRANSPARENT
            filterSettings.layerMask = LayerMask.GetMask("TransparentFX");
            drawSettings.sorting.flags = SortFlags.CommonTransparent;
            filterSettings.renderQueueRange = RenderQueueRange.transparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings, rsb);

            // WITH RENDERSTATEMAPPING TRANSPARENT
            filterSettings.layerMask = LayerMask.GetMask("Ignore Raycast");
            drawSettings.sorting.flags = SortFlags.CommonTransparent;
            filterSettings.renderQueueRange = RenderQueueRange.transparent;
            context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings, rsm);

            //**************************************************************

            if (stereoEnabled)
            {
                context.StopMultiEye(camera);
                // StereoEndRender will reset state on the camera to pre-Stereo settings,
                // and invoke XR based events/callbacks.
                context.StereoEndRender(camera);
            }

            context.Submit();
        }
    }

    // Setup lighting variables for shader to use
    private static void SetupLightShaderVariables(List<VisibleLight> lights, ScriptableRenderContext context)
    {
        // We only support up to 8 visible lights here. More complex approaches would
        // be doing some sort of per-object light setups, but here we go for simplest possible
        // approach.
        const int kMaxLights = 8;
        // Just take first 8 lights. Possible improvements: sort lights by intensity or distance
        // to the viewer, so that "most important" lights in the scene are picked, and not the 8
        // that happened to be first.
        int lightCount = Mathf.Min(lights.Count, kMaxLights);

        // Prepare light data
        Vector4[] lightColors = new Vector4[kMaxLights];
        Vector4[] lightPositions = new Vector4[kMaxLights];
        Vector4[] lightSpotDirections = new Vector4[kMaxLights];
        Vector4[] lightAtten = new Vector4[kMaxLights];
        for (int i = 0; i < lightCount; ++i)
        {
            VisibleLight light = lights[i];
            lightColors[i] = light.finalColor;
            if (light.lightType == LightType.Directional)
            {
                // light position for directional lights is: (-direction, 0)
                Vector4 dir = light.localToWorld.GetColumn(2);
                lightPositions[i] = new Vector4(-dir.x, -dir.y, -dir.z, 0);
            }
            else
            {
                // light position for point/spot lights is: (position, 1)
                Vector4 pos = light.localToWorld.GetColumn(3);
                lightPositions[i] = new Vector4(pos.x, pos.y, pos.z, 1);
            }
            // attenuation set in a way where distance attenuation can be computed:
            //  float lengthSq = dot(toLight, toLight);
            //  float atten = 1.0 / (1.0 + lengthSq * LightAtten[i].z);
            // and spot cone attenuation:
            //  float rho = max (0, dot(normalize(toLight), SpotDirection[i].xyz));
            //  float spotAtt = (rho - LightAtten[i].x) * LightAtten[i].y;
            //  spotAtt = saturate(spotAtt);
            // and the above works for all light types, i.e. spot light code works out
            // to correct math for point & directional lights as well.

            float rangeSq = light.range * light.range;

            float quadAtten = (light.lightType == LightType.Directional) ? 0.0f : 25.0f / rangeSq;

            // spot direction & attenuation
            if (light.lightType == LightType.Spot)
            {
                var dir = light.localToWorld.GetColumn(2);
                lightSpotDirections[i] = new Vector4(-dir.x, -dir.y, -dir.z, 0);

                float radAngle = Mathf.Deg2Rad * light.spotAngle;
                float cosTheta = Mathf.Cos(radAngle * 0.25f);
                float cosPhi = Mathf.Cos(radAngle * 0.5f);
                float cosDiff = cosTheta - cosPhi;
                lightAtten[i] = new Vector4(cosPhi, (cosDiff != 0.0f) ? 1.0f / cosDiff : 1.0f, quadAtten, rangeSq);
            }
            else
            {
                // non-spot light
                lightSpotDirections[i] = new Vector4(0, 0, 1, 0);
                lightAtten[i] = new Vector4(-1, 1, quadAtten, rangeSq);
            }
        }

        // ambient lighting spherical harmonics values
        const int kSHCoefficients = 7;
        Vector4[] shConstants = new Vector4[kSHCoefficients];
        SphericalHarmonicsL2 ambientSH = RenderSettings.ambientProbe * RenderSettings.ambientIntensity;
        GetShaderConstantsFromNormalizedSH(ref ambientSH, shConstants);

        // setup global shader variables to contain all the data computed above
        CommandBuffer cmd = new CommandBuffer();
        cmd.SetGlobalVectorArray("globalLightColor", lightColors);
        cmd.SetGlobalVectorArray("globalLightPos", lightPositions);
        cmd.SetGlobalVectorArray("globalLightSpotDir", lightSpotDirections);
        cmd.SetGlobalVectorArray("globalLightAtten", lightAtten);
        cmd.SetGlobalVector("globalLightCount", new Vector4(lightCount, 0, 0, 0));
        cmd.SetGlobalVectorArray("globalSH", shConstants);
        context.ExecuteCommandBuffer(cmd);
        cmd.Release();
    }

    // Prepare L2 spherical harmonics values for efficient evaluation in a shader
    private static void GetShaderConstantsFromNormalizedSH(ref SphericalHarmonicsL2 ambientProbe, Vector4[] outCoefficients)
    {
        for (int channelIdx = 0; channelIdx < 3; ++channelIdx)
        {
            // Constant + Linear
            // In the shader we multiply the normal is not swizzled, so it's normal.xyz.
            // Swizzle the coefficients to be in { x, y, z, DC } order.
            outCoefficients[channelIdx].x = ambientProbe[channelIdx, 3];
            outCoefficients[channelIdx].y = ambientProbe[channelIdx, 1];
            outCoefficients[channelIdx].z = ambientProbe[channelIdx, 2];
            outCoefficients[channelIdx].w = ambientProbe[channelIdx, 0] - ambientProbe[channelIdx, 6];
            // Quadratic polynomials
            outCoefficients[channelIdx + 3].x = ambientProbe[channelIdx, 4];
            outCoefficients[channelIdx + 3].y = ambientProbe[channelIdx, 5];
            outCoefficients[channelIdx + 3].z = ambientProbe[channelIdx, 6] * 3.0f;
            outCoefficients[channelIdx + 3].w = ambientProbe[channelIdx, 7];
        }
        // Final quadratic polynomial
        outCoefficients[6].x = ambientProbe[0, 8];
        outCoefficients[6].y = ambientProbe[1, 8];
        outCoefficients[6].z = ambientProbe[2, 8];
        outCoefficients[6].w = 1.0f;
    }
}
