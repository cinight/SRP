using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class SRP04 : RenderPipelineAsset
{
    public SRP04CustomParameter SRP04CP = new SRP04CustomParameter();
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP04", priority = CoreUtils.assetCreateMenuPriority1)]
    static void CreateSRP04()
    {
        var instance = ScriptableObject.CreateInstance<SRP04>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRP04.asset");
    }
#endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        SRP04CP.ClearColor = ClearColor;
        SRP04CP.DrawSkybox = DrawSkybox;
        SRP04CP.DrawOpaque = DrawOpaque;
        SRP04CP.DrawTransparent = DrawTransparent;
        return new SRP04Instance(SRP04CP);
    }
}

public class SRP04Instance : RenderPipeline
{
    public SRP04CustomParameter SRP04CP;

    public SRP04Instance(SRP04CustomParameter SRP04CustomParameter)
    {
        SRP04CP = SRP04CustomParameter;
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //base.Render(renderContext, cameras);
        SRP04Rendering.Render(renderContext, cameras, SRP04CP);
    }
}

public static class SRP04Rendering
{
    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras, SRP04CustomParameter SRP04CP)
    {
        foreach (Camera camera in cameras)
        {
            CullResults cull = new CullResults();
            CullResults.Cull(camera , context , out cull);

            // Setup camera for rendering (sets render target, view/projection matrices and other
            // per-camera built-in shader variables).
            context.SetupCameraProperties(camera);

            // clear depth buffer
            CommandBuffer cmd = CommandBufferPool.Get();
            cmd.ClearRenderTarget(true, !SRP04CP.DrawSkybox, SRP04CP.ClearColor);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            // Setup global lighting shader variables
            //SetupLightShaderVariables(cull.visibleLights, context);

            if (SRP04CP.DrawSkybox)
            {
                // Draw skybox
                context.DrawSkybox(camera);
            }

            // Setup DrawSettings and FilterSettings
            ShaderPassName passName = new ShaderPassName("BasicPass");
            DrawRendererSettings drawSettings = new DrawRendererSettings(camera, passName);
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

            if (SRP04CP.DrawOpaque)
            {
                // Draw opaque objects using BasicPass shader pass
                drawSettings.sorting.flags = SortFlags.CommonOpaque;
                filterSettings.renderQueueRange = RenderQueueRange.opaque;
                context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
            }

            if (SRP04CP.DrawTransparent)
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

public class SRP04CustomParameter
{
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;

    public SRP04CustomParameter()
    {

    }
}