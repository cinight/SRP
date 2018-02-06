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
public class SRP03 : RenderPipelineAsset
{
    public SRP03CustomParameter SRP03CP = new SRP03CustomParameter();
    public SRP03CustomParameter.CullingCamera culling = SRP03CustomParameter.CullingCamera.MainCamera;


#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP03", priority = 1)]
    static void CreateSRP03()
    {
        var instance = ScriptableObject.CreateInstance<SRP03>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRP03.asset");
    }

#endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        SRP03CP.DoCulling = culling;
        return new SRP03Instance(SRP03CP);
    }
}

public class SRP03Instance : RenderPipeline
{
    public SRP03CustomParameter SRP03CP;

    public SRP03Instance(SRP03CustomParameter SRP03CustomParameter)
    {
        SRP03CP = SRP03CustomParameter;
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //base.Render(renderContext, cameras);
        SRP03Rendering.Render(renderContext, cameras, SRP03CP);
    }
}

public static class SRP03Rendering
{
    public static TextMesh textMesh;
    public static Renderer[] rens;
    public static Light[] lights;
    public static ReflectionProbe[] reflprobes;
    public static Camera MainCam;
    public static Camera AllCam;
    public static Camera NoneCam;

    // Main entry point for our scriptable render loop
    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras, SRP03CustomParameter SRP03CP)
    {
        Camera cullingCam;
        switch (SRP03CP.DoCulling)
        {
            case SRP03CustomParameter.CullingCamera.MainCamera:
                {
                    cullingCam = MainCam;
                    break;
                }
            case SRP03CustomParameter.CullingCamera.AllCam:
                {
                    cullingCam = AllCam;
                    break;
                }
            case SRP03CustomParameter.CullingCamera.NoneCam:
                {
                    cullingCam = NoneCam;
                    break;
                }
            default:
                {
                    cullingCam = MainCam;
                    break;
                }
        }
        //bool stereoEnabled = XRSettings.isDeviceActive;

        foreach (Camera camera in cameras)
        {
            bool RenderThisCam = true;

            switch (SRP03CP.DoCulling)
            {
                case SRP03CustomParameter.CullingCamera.MainCamera:
                    {
                        if(camera == AllCam || camera == NoneCam)
                            RenderThisCam = false;
                        break;
                    }
                case SRP03CustomParameter.CullingCamera.AllCam:
                    {
                        if(camera == MainCam || camera == NoneCam)
                            RenderThisCam = false;
                        break;
                    }
                case SRP03CustomParameter.CullingCamera.NoneCam:
                    {
                        if(camera == MainCam || camera == AllCam)
                            RenderThisCam = false;
                        break;
                    }
                default:
                    {
                        break;
                    }
            }

            if(RenderThisCam)
            {
                // Culling
                ScriptableCullingParameters cullingParams;

                if (!CullResults.GetCullingParameters(camera, out cullingParams))
                    continue;
                CullResults cull = new CullResults();
                CullResults.Cull(ref cullingParams, context, ref cull);

                //==============================================
                if(camera == cullingCam) //only show result of that camera
                {
                    string tx = "";
                    tx += "Culling Result of : " + cullingCam.name + " \n";
                    tx += "\n";
                    //-------------------------------
                    VisibleLight[] ls = cull.visibleLights.ToArray();
                    tx += "Lights : Visible : "+ls.Length+"\n";
                    
                    if (lights != null)
                    {
                        for (int i = 0; i < lights.Length; i++)
                        {
                            int existed = 0;
                            for (int j = 0; j < ls.Length; j++)
                            {
                                if (lights[i] == ls[j].light)
                                {
                                    existed++;
                                }
                            }
                            if (existed > 0)
                            {
                                tx += lights[i].gameObject.name + " : <color=#0F0>Visible</color>" + "\n";
                            }
                            else
                            {
                                tx += lights[i].gameObject.name + " : <color=#F00>Not Visible</color>" + "\n";
                            }
                        }
                    }
                    else
                    {
                        tx += "Light list is null \n";
                    }
                    tx += "\n";
                    //-------------------------------
                    VisibleReflectionProbe[] rs = cull.visibleReflectionProbes.ToArray();
                    tx += "Reflection Probes : Visible : "+rs.Length+"\n";
                    
                    if (reflprobes != null)
                    {
                        for (int i = 0; i < reflprobes.Length; i++)
                        {
                            int existed = 0;
                            for (int j = 0; j < rs.Length; j++)
                            {
                                if (reflprobes[i] == rs[j].probe)
                                {
                                    existed++;
                                }
                            }
                            if (existed > 0)
                            {
                                tx += reflprobes[i].gameObject.name + " : <color=#0F0>Visible</color>" + "\n";
                            }
                            else
                            {
                                tx += reflprobes[i].gameObject.name + " : <color=#F00>Not Visible</color>" + "\n";
                            }
                        }
                    }
                    else
                    {
                        tx += "reflection probe list is null \n";
                    }
                    tx += "\n";
                    //-------------------------------
                    
                    tx += "Renderers : \n";
                    /* 
                    VisibleRenderers[] vrens = cullingCamResult.visibleRenderers.ToArray();
                    if (rens != null)
                    {
                        for (int i = 0; i < rens.Length; i++)
                        {
                            int existed = 0;
                            for (int j = 0; j < vrens.Length; j++)
                            {
                                if (rens[i] == vrens[j])
                                {
                                    existed++;
                                }
                            }
                            if (existed > 0)
                            {
                                tx += rens[i].gameObject.name + " : <color=#0F0>Visible</color>" + "\n";
                            }
                            else
                            {
                                tx += rens[i].gameObject.name + " : <color=#F00>Not Visible</color>" + "\n";
                            }
                        }
                    }
                    else
                    {
                        tx += "renderer list is null \n";
                    }
                    */

                    for (int i =0;i<rens.Length;i++)
                    {
                        if (rens[i].isVisible)
                        {

                            tx += rens[i].gameObject.name + " <color=#0F0>Yes</color> \n";
                        }
                        else
                        {
                            tx += rens[i].gameObject.name + " <color=#F00>No</color> \n";
                        }
                    }

                    tx += "\n";
                    
                    
                    //-------------------------------
                    //Show debug msg on TextMesh
                    //Debug.Log(tx);
                    
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
                //==============================================

                // Setup camera for rendering (sets render target, view/projection matrices and other
                // per-camera built-in shader variables).
                // If stereo is enabled, we also configure stereo matrices, viewports, and XR device render targets
                context.SetupCameraProperties(camera);

                // Draws in-between [Start|Stop]MultiEye are stereo-ized by engine
                //if (stereoEnabled)
                //{
                //   context.StartMultiEye(camera);
                //}

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

                // Draw opaque objects using BasicPass shader pass
                drawSettings.sorting.flags = SortFlags.CommonOpaque;
                filterSettings.renderQueueRange = RenderQueueRange.opaque;
                context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

                // Draw transparent objects using BasicPass shader pass
                drawSettings.sorting.flags = SortFlags.CommonTransparent;
                filterSettings.renderQueueRange = RenderQueueRange.transparent;
                context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
    /* 
                if (stereoEnabled)
                {
                    context.StopMultiEye(camera);
                    // StereoEndRender will reset state on the camera to pre-Stereo settings,
                    // and invoke XR based events/callbacks.
                    context.StereoEndRender(camera);
                }
    */
                context.Submit();
            }


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


public class SRP03CustomParameter
{
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;
    public enum CullingCamera
    {
        MainCamera,
        AllCam,
        NoneCam
    };
    public CullingCamera DoCulling = CullingCamera.MainCamera;

    public SRP03CustomParameter()
    {

    }
}