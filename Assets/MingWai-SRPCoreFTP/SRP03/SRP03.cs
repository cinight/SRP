using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class SRP03 : RenderPipelineAsset
{
    public SRP03CustomParameter SRP03CP = new SRP03CustomParameter();
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;
    public SRP03CustomParameter.CullingCamera culling = SRP03CustomParameter.CullingCamera.MainCamera;

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP03", priority = CoreUtils.assetCreateMenuPriority1)]
    static void CreateSRP03()
    {
        var instance = ScriptableObject.CreateInstance<SRP03>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRP03.asset");
    }
    #endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        SRP03CP.ClearColor = ClearColor;
        SRP03CP.DrawSkybox = DrawSkybox;
        SRP03CP.DrawOpaque = DrawOpaque;
        SRP03CP.DrawTransparent = DrawTransparent;
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
        SRP03Rendering.Render(renderContext, cameras,SRP03CP);
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

        //Culling Cam
        CullResults cullingCamResult = new CullResults();
        bool CullSuccess = CullResults.Cull(cullingCam, context, out cullingCamResult);

        //=============================================
        string tx = "";
        tx += "Culling Camera : " + cullingCam.name + " \n";
        //-------------------------------
        tx += "CullResults : " + CullSuccess + " \n";
        //-------------------------------
        tx += "Lights : \n";
        VisibleLight[] ls = cullingCamResult.visibleLights.ToArray();
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
        tx += "Reflection Probes : \n";
        VisibleReflectionProbe[] rs = cullingCamResult.visibleReflectionProbes.ToArray();
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
        //===============================================

        foreach (Camera cam in cameras)
        {
            if(cam != NoneCam && cam != AllCam)
            {
                //Rendering Cam
                ScriptableCullingParameters cullingParams;
                CullResults.GetCullingParameters(cam, out cullingParams);
                CullResults cull = new CullResults();
                CullResults.Cull(cam, context, out cull);

                // Setup camera for rendering (sets render target, view/projection matrices and other
                // per-camera built-in shader variables).
                context.SetupCameraProperties(cam);

                // clear depth buffer
                CommandBuffer cmd = CommandBufferPool.Get();
                cmd.ClearRenderTarget(true, !SRP03CP.DrawSkybox, SRP03CP.ClearColor);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);

                // Setup global lighting shader variables
                //SetupLightShaderVariables(cull.visibleLights, context);

                if (SRP03CP.DrawSkybox)
                {
                    // Draw skybox
                    context.DrawSkybox(cam);
                }

                // Setup DrawSettings and FilterSettings
                ShaderPassName passName = new ShaderPassName("BasicPass");
                DrawRendererSettings drawSettings = new DrawRendererSettings(cam, passName);
                FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

                if (SRP03CP.DrawOpaque)
                {
                    // Draw opaque objects using BasicPass shader pass
                    drawSettings.sorting.flags = SortFlags.CommonOpaque;
                    filterSettings.renderQueueRange = RenderQueueRange.opaque;
                    context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
                }

                if (SRP03CP.DrawTransparent)
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