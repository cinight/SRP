using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class SRP06 : RenderPipelineAsset
{
    #if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP06", priority = CoreUtils.assetCreateMenuPriority1)]
    static void CreateSRP06()
    {
        var instance = ScriptableObject.CreateInstance<SRP06>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRP06.asset");
    }
    #endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new SRP06Instance();
    }
}

public class SRP06Instance : RenderPipeline
{
    public SRP06Instance()
    {
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //base.Render(renderContext, cameras);
        SRP06Rendering.Render(renderContext, cameras);
    }
}

public static class SRP06Rendering
{
    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras)
    {
        int i = 0;
        foreach(Camera cam in cameras)
        {
            Debug.Log(i+" "+cam.name);
            i++;
        }

        Camera camera = Camera.main;

        CullResults cull = new CullResults();
        CullResults.Cull(camera,context,out cull);

        context.SetupCameraProperties(camera);

        // clear depth buffer
        CommandBuffer cmd = CommandBufferPool.Get();
        cmd.ClearRenderTarget(true, false, Color.black);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);

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

        context.Submit();
    }
}