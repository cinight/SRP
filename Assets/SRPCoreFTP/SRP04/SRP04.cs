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

public class SRP04Instance : RenderPipeline
{
    SRP04 m_Parent;

    RenderPassAttachment m_Albedo;
    RenderPassAttachment m_Emission;
    RenderPassAttachment m_Output;
    RenderPassAttachment m_Depth;

    public SRP04Instance(SRP04 parent)
    {
        m_Parent = parent;

        //Attachments
        m_Albedo = new RenderPassAttachment(RenderTextureFormat.ARGB32);
        m_Emission = new RenderPassAttachment(RenderTextureFormat.ARGB32);
        m_Output = new RenderPassAttachment(RenderTextureFormat.ARGB32);
        m_Depth = new RenderPassAttachment(RenderTextureFormat.Depth);

        m_Albedo.Clear(Color.green, 1f , 0);
        m_Emission.Clear(Color.green, 1f , 0);
        //m_Output.Clear(Color.cyan);
        m_Depth.Clear(Color.black, 1f, 0);

        m_Output.BindSurface(BuiltinRenderTextureType.CameraTarget, false, true);
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        base.Render(renderContext, cameras);
        SRP04Rendering(renderContext, cameras);
    }

    public void SRP04Rendering(ScriptableRenderContext context, IEnumerable<Camera> cameras)
    {
        foreach (Camera camera in cameras)
        {
            // Culling
            ScriptableCullingParameters cullingParams;

            if (!CullResults.GetCullingParameters(camera, out cullingParams))
                continue;
            CullResults cull = CullResults.Cull(ref cullingParams, context);

            // Setup camera for rendering (sets render target, view/projection matrices and other
            // per-camera built-in shader variables).
            context.SetupCameraProperties(camera);

            // Setup DrawSettings and FilterSettings
            //FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);
            //filterSettings.renderQueueRange = RenderQueueRange.opaque;

            using (RenderPass rp = new RenderPass(context, camera.pixelWidth, camera.pixelHeight, 1, new[] { m_Albedo, m_Emission, m_Output }, m_Depth))
            {
                using (new RenderPass.SubPass(rp, new[] { m_Albedo, m_Emission }, null))
                {
                    var settings = new DrawRendererSettings(camera, new ShaderPassName("BasicPass"))
                    {
                        sorting = { flags = SortFlags.CommonOpaque }
                    };

                   // DrawRendererSettings drawSettings = new DrawRendererSettings(camera, new ShaderPassName("BasicPass"));
                    var fs = new FilterRenderersSettings(true);
                    fs.renderQueueRange = RenderQueueRange.opaque;
                    context.DrawRenderers(cull.visibleRenderers, ref settings, fs);
                }
                
                using (new RenderPass.SubPass(rp, new[] { m_Output }, new[] { m_Albedo, m_Emission }, false))
                {
                    context.DrawSkybox(camera);
                    var settings = new DrawRendererSettings(camera, new ShaderPassName("AddPass"))
                    {
                        sorting = { flags = SortFlags.CommonOpaque }
                    };
                    //DrawRendererSettings drawSettings = new DrawRendererSettings(camera, new ShaderPassName("AddPass"));
                    var fs = new FilterRenderersSettings(true);
                    fs.renderQueueRange = RenderQueueRange.opaque;
                    context.DrawRenderers(cull.visibleRenderers, ref settings, fs);
                }
            }

            context.Submit();
        }
    }
}