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
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP04", priority = 1)]
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

    private static RenderPassAttachment m_Albedo;
    private static RenderPassAttachment m_Emission;
    private static RenderPassAttachment m_Depth;

    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras, SRP04CustomParameter SRP04CP)
    {
        if(m_Albedo == null) m_Albedo = new RenderPassAttachment(RenderTextureFormat.ARGB32);
        if(m_Emission == null) m_Emission = new RenderPassAttachment(RenderTextureFormat.ARGBHalf);
        if(m_Depth == null) m_Depth = new RenderPassAttachment(RenderTextureFormat.Depth);

        m_Albedo.Clear(new Color(0.0f, 0.0f, 0.0f, 0.0f), 1.0f, 0);
        m_Emission.Clear(new Color(0.0f, 0.0f, 0.0f, 0.0f), 1.0f, 0);
        m_Depth.Clear(new Color(), 1.0f, 0);

        foreach (Camera camera in cameras)
        {
            CullResults cull = new CullResults();
            CullResults.Cull(camera , context , out cull);

            // Setup camera for rendering (sets render target, view/projection matrices and other
            // per-camera built-in shader variables).
            context.SetupCameraProperties(camera);

            // clear depth buffer
            CommandBuffer cmd = new CommandBuffer();
            cmd.ClearRenderTarget(true, !SRP04CP.DrawSkybox, SRP04CP.ClearColor);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();

            // Setup DrawSettings and FilterSettings
            ShaderPassName passName = new ShaderPassName("BasicPass");
            ShaderPassName passNameadd = new ShaderPassName("AddPass");
            DrawRendererSettings drawSettings = new DrawRendererSettings(camera, passName);
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

            //============================================================
            m_Albedo.BindSurface(BuiltinRenderTextureType.CameraTarget, false, true);

            using (RenderPass rp = new RenderPass(context, camera.pixelWidth, camera.pixelHeight, 1, new[] { m_Albedo, m_Emission }, m_Depth))
            {
                // Start the first subpass, GBuffer creation: render to albedo, specRough, normal and emission, no need to read any input attachments
                using (new RenderPass.SubPass(rp, new[] { m_Albedo, m_Emission }, null))
                {
                    context.DrawSkybox(camera);
                    
                    drawSettings.SetShaderPassName (0,passName);

                    // Draw opaque objects using BasicPass shader pass
                    drawSettings.sorting.flags = SortFlags.CommonOpaque;
                    filterSettings.renderQueueRange = RenderQueueRange.opaque;
                    context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

                    // Draw transparent objects using BasicPass shader pass
                    drawSettings.sorting.flags = SortFlags.CommonTransparent;
                    filterSettings.renderQueueRange = RenderQueueRange.transparent;
                    context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

                }
                
                using (new RenderPass.SubPass(rp, new[] { m_Albedo }, new[] { m_Albedo, m_Emission }, false))
                {
                    drawSettings.SetShaderPassName (1,passNameadd);

                    // Draw opaque objects using BasicPass shader pass
                    drawSettings.sorting.flags = SortFlags.CommonOpaque;
                    filterSettings.renderQueueRange = RenderQueueRange.opaque;
                    context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

                    // Draw transparent objects using BasicPass shader pass
                    drawSettings.sorting.flags = SortFlags.CommonTransparent;
                    filterSettings.renderQueueRange = RenderQueueRange.transparent;
                    context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
                }
                Debug.Log("Render pass : camera = "+ camera.name + " sample count = " + rp.sampleCount + " width = " + rp.width + " height = " + rp.height );
                rp.Dispose();
            }
            
            //============================================================

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