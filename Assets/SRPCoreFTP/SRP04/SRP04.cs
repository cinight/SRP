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


    public SRP04Instance()
    {

    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //base.Render(renderContext, cameras);
        SRP04Rendering.Render(renderContext, cameras);
    }
}

public static class SRP04Rendering
{
    private static RenderPassAttachment m_Albedo;
    private static RenderPassAttachment m_Emission;
    private static RenderPassAttachment m_Depth;

    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras)
    {
        foreach (Camera camera in cameras)
        {
            CullResults cull = new CullResults();
            CullResults.Cull(camera , context , out cull);

            // Setup camera for rendering (sets render target, view/projection matrices and other
            // per-camera built-in shader variables).
            context.SetupCameraProperties(camera);

            //Attachments
            m_Albedo = new RenderPassAttachment(RenderTextureFormat.ARGB32);
            m_Emission = new RenderPassAttachment(RenderTextureFormat.ARGBHalf);
            m_Depth = new RenderPassAttachment(RenderTextureFormat.Depth);

            m_Albedo.BindSurface(BuiltinRenderTextureType.CameraTarget, false, true);

            m_Albedo.Clear(Color.green);
            m_Emission.Clear(Color.cyan);
            m_Depth.Clear(Color.black, 1f, 0);

            // Setup DrawSettings and FilterSettings
            ShaderPassName passName = new ShaderPassName("BasicPass");
            ShaderPassName passNameadd = new ShaderPassName("AddPass");
            DrawRendererSettings drawSettings = new DrawRendererSettings(camera, passName);
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);
            drawSettings.sorting.flags = SortFlags.CommonOpaque;
            filterSettings.renderQueueRange = RenderQueueRange.opaque;

            //============================================================
            

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