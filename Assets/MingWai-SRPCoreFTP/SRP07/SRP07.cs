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
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP07", priority = CoreUtils.assetCreateMenuPriority1)]
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
    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras, SRP07CustomParameter SRP07CP)
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
            CommandBuffer cmd = CommandBufferPool.Get();
            cmd.ClearRenderTarget(true, !SRP07CP.DrawSkybox, SRP07CP.ClearColor);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            // Setup global lighting shader variables
            //SetupLightShaderVariables(cull.visibleLights, context);

            if(SRP07CP.DrawSkybox)
            {
                // Draw skybox
                context.DrawSkybox(camera);
            }

            // Setup DrawSettings and FilterSettings
            ShaderPassName passName = new ShaderPassName("BasicPass");
            DrawRendererSettings drawSettings = new DrawRendererSettings(camera, passName);
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

            if (SRP07CP.DrawOpaque)
            {
                // Draw opaque objects using BasicPass shader pass
                drawSettings.sorting.flags = SortFlags.CommonOpaque;
                filterSettings.renderQueueRange = RenderQueueRange.opaque;
                context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
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