using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class SRP02 : RenderPipelineAsset
{
    public SRP02CustomParameter SRP02CP = new SRP02CustomParameter();
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;

    #if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP02", priority = 1)]
    static void CreateSRP02()
    {
        var instance = ScriptableObject.CreateInstance<SRP02>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRP02.asset");
    }
    #endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        SRP02CP.ClearColor = ClearColor;
        SRP02CP.DrawSkybox = DrawSkybox;
        SRP02CP.DrawOpaque = DrawOpaque;
        SRP02CP.DrawTransparent = DrawTransparent;
        return new SRP02Instance(SRP02CP);
    }
}

public class SRP02Instance : RenderPipeline
{
    public SRP02CustomParameter SRP02CP;

    public SRP02Instance(SRP02CustomParameter SRP02CustomParameter)
    {
        SRP02CP = SRP02CustomParameter;
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //base.Render(renderContext, cameras);
        SRP02Rendering.Render(renderContext, cameras,SRP02CP);
    }
}

public static class SRP02Rendering
{
    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras, SRP02CustomParameter SRP02CP)
    {
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
            cmd.ClearRenderTarget(true, !SRP02CP.DrawSkybox, SRP02CP.ClearColor);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();

            // Setup global lighting shader variables
            //SetupLightShaderVariables(cull.visibleLights, context);

            if(SRP02CP.DrawSkybox)
            {
                // Draw skybox
                context.DrawSkybox(camera);
            }

            // Setup DrawSettings and FilterSettings
            ShaderPassName passName = new ShaderPassName("BasicPass");
            DrawRendererSettings drawSettings = new DrawRendererSettings(camera, passName);
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

            if (SRP02CP.DrawOpaque)
            {
                // Draw opaque objects using BasicPass shader pass
                drawSettings.sorting.flags = SortFlags.CommonOpaque;
                filterSettings.renderQueueRange = RenderQueueRange.opaque;
                context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
            }

            if (SRP02CP.DrawTransparent)
            {
                // Draw transparent objects using BasicPass shader pass
                drawSettings.sorting.flags = SortFlags.CommonTransparent;
                filterSettings.renderQueueRange = RenderQueueRange.transparent;
                context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
            }

            context.Submit();
        }
    }
}

public class SRP02CustomParameter
{
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;

    public SRP02CustomParameter()
    {

    }
}