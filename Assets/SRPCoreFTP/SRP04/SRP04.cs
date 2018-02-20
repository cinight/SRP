using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class SRP04 : RenderPipelineAsset
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP04")]
    static void CreateSRP04()
    {
        var instance = ScriptableObject.CreateInstance<SRP04>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRP04.asset");
    }
#endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new SRP04Instance(this);
    }
}


public abstract class RenderPipeline : IRenderPipeline
{
    public virtual void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        if(disposed)
            throw new System.ObjectDisposedException(string.Format("{0} has been disposed. Do not call Render on disposed RenderLoops.",this));
    }

    public bool disposed { get; private set;}

    public virtual void Dispose()
    {
        disposed = true;
    }
}

public class SRP04Instance : RenderPipeline
{
    SRP04 m_Parent;

    public SRP04Instance(SRP04 parent)
    {
        m_Parent = parent;
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        base.Render(renderContext, cameras);
        SRP04Rendering.Render(renderContext, cameras);
    }
}

public static class SRP04Rendering
{
    private static RenderPassAttachment m_Albedo;
    private static RenderPassAttachment m_Emission;
    private static RenderPassAttachment m_Output;
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
            m_Emission = new RenderPassAttachment(RenderTextureFormat.ARGB32);
            m_Output = new RenderPassAttachment(RenderTextureFormat.ARGB32);
            m_Depth = new RenderPassAttachment(RenderTextureFormat.Depth);

            m_Output.BindSurface(BuiltinRenderTextureType.CameraTarget, false, true);

            m_Albedo.Clear(Color.green);
            m_Emission.Clear(Color.cyan);
            m_Output.Clear(Color.cyan);
            m_Depth.Clear(Color.black, 1f, 0);

            // Setup DrawSettings and FilterSettings
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);
            filterSettings.renderQueueRange = RenderQueueRange.opaque;

            //============================================================
            

            using (RenderPass rp = new RenderPass(context, camera.pixelWidth, camera.pixelHeight, 1, new[] { m_Albedo, m_Emission, m_Output }, m_Depth))
            {
                using (new RenderPass.SubPass(rp, new[] { m_Albedo, m_Emission }, null))
                {
                    DrawRendererSettings drawSettings = new DrawRendererSettings(camera, new ShaderPassName("BasicPass"));
                    context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
                }
                
                using (new RenderPass.SubPass(rp, new[] { m_Output }, new[] { m_Albedo, m_Emission }, false))
                {
                    context.DrawSkybox(camera);
                    DrawRendererSettings drawSettings = new DrawRendererSettings(camera, new ShaderPassName("AddPass"));
                    context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
                }
            }
            //============================================================
            context.Submit();
        }
    }
}