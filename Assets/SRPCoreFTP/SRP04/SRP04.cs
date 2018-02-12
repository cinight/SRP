using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class SRP04 : RenderPipelineAsset
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP04", priority = 1)]
    static void CreateSRP04()
    {
        var instance = ScriptableObject.CreateInstance<SRP04>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRP04.asset");
    }
#endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new SRP04Instance();
    }
}

public class SRP04Instance : RenderPipeline
{
    private RenderPassAttachment m_Albedo;
    private RenderPassAttachment m_Emission;
    private RenderPassAttachment m_Depth;

    public SRP04Instance()
    {
        m_Albedo = new RenderPassAttachment(RenderTextureFormat.ARGB32);
        m_Emission = new RenderPassAttachment(RenderTextureFormat.ARGBHalf);
        m_Depth = new RenderPassAttachment(RenderTextureFormat.Depth);

        m_Albedo.Clear(new Color(0.0f, 0.0f, 0.0f, 0.0f));
        m_Emission.Clear(new Color(0.0f, 0.0f, 0.0f, 0.0f));
        m_Depth.Clear(new Color(0.5f, 0.5f, 0.5f, 0.5f));
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //base.Render(renderContext, cameras);
        SRP04Rendering.Render(renderContext, cameras, m_Albedo, m_Emission, m_Depth);
    }
}

public static class SRP04Rendering
{
    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras, RenderPassAttachment m_Albedo, RenderPassAttachment m_Emission, RenderPassAttachment m_Depth)
    {
        foreach (Camera camera in cameras)
        {
            CullResults cull = new CullResults();
            CullResults.Cull(camera , context , out cull);

            // Setup camera for rendering (sets render target, view/projection matrices and other
            // per-camera built-in shader variables).
            context.SetupCameraProperties(camera);

            // Setup DrawSettings and FilterSettings
            ShaderPassName passName = new ShaderPassName("BasicPass");
            ShaderPassName passNameadd = new ShaderPassName("AddPass");
            DrawRendererSettings drawSettings = new DrawRendererSettings(camera, passName);
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);
            drawSettings.sorting.flags = SortFlags.CommonOpaque;
            filterSettings.renderQueueRange = RenderQueueRange.opaque;

            //============================================================
            m_Albedo.BindSurface(BuiltinRenderTextureType.CameraTarget, false, true);

            using (RenderPass rp = new RenderPass(context, camera.pixelWidth, camera.pixelHeight, 1, new[] { m_Albedo, m_Emission }, m_Depth))
            {
                using (new RenderPass.SubPass(rp, new[] { m_Albedo, m_Emission }, null))
                {
                    context.DrawSkybox(camera);
                    
                    drawSettings.SetShaderPassName (0,passName);
                    context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
                }
                
                using (new RenderPass.SubPass(rp, new[] { m_Albedo }, new[] { m_Albedo, m_Emission }, false))
                {
                    drawSettings.SetShaderPassName (1,passNameadd);
                    context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
                }
                //rp.Dispose();
            }
            //============================================================
            context.Submit();
        }
    }
}