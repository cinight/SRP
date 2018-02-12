using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class SRP07 : RenderPipelineAsset
{
    public SRP07CustomParameter SRP07CP = new SRP07CustomParameter();
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;

    #if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP07", priority = 1)]
    static void CreateSRP07()
    {
        var instance = ScriptableObject.CreateInstance<SRP07>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRP07.asset");
    }
    #endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        SRP07CP.ClearColor = ClearColor;
        SRP07CP.DrawSkybox = DrawSkybox;
        SRP07CP.DrawOpaque = DrawOpaque;
        SRP07CP.DrawTransparent = DrawTransparent;
        return new SRP07Instance(SRP07CP);
    }
}

public class SRP07Instance : RenderPipeline
{
    public SRP07CustomParameter SRP07CP;

    public SRP07Instance(SRP07CustomParameter SRP07CustomParameter)
    {
        SRP07CP = SRP07CustomParameter;
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //base.Render(renderContext, cameras);
        SRP07Rendering.Render(renderContext, cameras,SRP07CP);
    }
}

public static class SRP07Rendering
{
    public static TextMesh textMesh;

    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras, SRP07CustomParameter SRP07CP)
    {
        string tx = "";

        foreach (Camera camera in cameras)
        {
            ScriptableCullingParameters cullingParams;

            // Stereo-aware culling parameters are configured to perform a single cull for both eyes
            if (!CullResults.GetCullingParameters(camera, out cullingParams))
                continue;
            CullResults cull = new CullResults();
            CullResults.Cull(ref cullingParams, context, ref cull);

            // Setup camera for rendering (sets render target, view/projection matrices and other
            // per-camera built-in shader variables).
            context.SetupCameraProperties(camera);

            // clear depth buffer
            CommandBuffer cmd = new CommandBuffer();
            cmd.ClearRenderTarget(true, !SRP07CP.DrawSkybox, SRP07CP.ClearColor);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();

            // Setup global lighting shader variables
            SetupLightShaderVariables(cull.visibleLights, context);

            if(SRP07CP.DrawSkybox)
            {
                // Draw skybox
                context.DrawSkybox(camera);
            }

            // Setup DrawSettings and FilterSettings
            ShaderPassName passName = new ShaderPassName("BasicPass");
            DrawRendererSettings drawSettings = new DrawRendererSettings(camera, passName);
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

            // ////////////////////////////////////////////////////////////
            VisibleLight[] ls = cull.visibleLights.ToArray();
            DrawShadowsSettings[] shadowsSettings = new DrawShadowsSettings[ls.Length];
            
            for (int i=0; i<shadowsSettings.Length; i++)
            {
                shadowsSettings[i] = new DrawShadowsSettings(cull, i);
            }
            
            if(camera == Camera.main) //Only generate result from main cam
            {
                tx += "DrawShadowsSettings" + "\n"+ "\n";

                for (int i=0; i<ls.Length; i++)
                {
                    tx += "lightIndex = " + shadowsSettings[i].lightIndex + " (" + ls[i].light.name + ") " + "\n";
                    tx += "splitData.cullingPlaneCount = " + shadowsSettings[i].splitData.cullingPlaneCount + "\n";
                    tx += "splitData.cullingSphere = " + shadowsSettings[i].splitData.cullingSphere + "\n"+ "\n";
                }
            
                // Output to text
                if (textMesh != null)
                {
                    textMesh.text = tx;
                    Debug.Log("<color=#0F0>TextMesh is updated</color>");
                }
                else
                {
                    tx = "<color=#F00>TextMesh is null</color> Please hit play if you hasn't";
                    Debug.Log(tx);
                }
            }
            // ////////////////////////////////////////////////////////////

            if (SRP07CP.DrawOpaque)
            {
                // Draw opaque objects using BasicPass shader pass
                drawSettings.sorting.flags = SortFlags.CommonOpaque;
                filterSettings.renderQueueRange = RenderQueueRange.opaque;
                context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
                           
                for (int i=0; i<shadowsSettings.Length; i++)
                {
                    if(ls[i].light.shadows != LightShadows.None)
                    context.DrawShadows(ref shadowsSettings[i]);
                }
            }

            if (SRP07CP.DrawTransparent)
            {
                // Draw transparent objects using BasicPass shader pass
                drawSettings.sorting.flags = SortFlags.CommonTransparent;
                filterSettings.renderQueueRange = RenderQueueRange.transparent;
                context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
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

        //Setup shadow variables
        //float4 unity_ShadowSplitSpheres[4];
        //float4 unity_ShadowSplitSqRadii;
        //float4 unity_LightShadowBias;
        //float4 _LightSplitsNear;
        //float4 _LightSplitsFar;
        //float4x4 unity_WorldToShadow[4];
        //half4 _LightShadowData;
        //float4 unity_ShadowFadeCenterAndType;

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

public class SRP07CustomParameter
{
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;

    public SRP07CustomParameter()
    {
        
    }
}