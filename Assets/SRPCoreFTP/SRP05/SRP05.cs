using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System.Linq;

[ExecuteInEditMode]
public class SRP05 : RenderPipelineAsset
{
    public SRP05CustomParameter SRP05CP = new SRP05CustomParameter();
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;

    #if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Render Pipeline/SRPFTP/SRP05", priority = 1)]
    static void CreateSRP05()
    {
        var instance = ScriptableObject.CreateInstance<SRP05>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRP05.asset");
    }
    #endif

    protected override IRenderPipeline InternalCreatePipeline()
    {
        SRP05CP.ClearColor = ClearColor;
        SRP05CP.DrawSkybox = DrawSkybox;
        SRP05CP.DrawOpaque = DrawOpaque;
        SRP05CP.DrawTransparent = DrawTransparent;
        return new SRP05Instance(SRP05CP);
    }
}

public class SRP05Instance : RenderPipeline
{
    public SRP05CustomParameter SRP05CP;

    public SRP05Instance(SRP05CustomParameter SRP05CustomParameter)
    {
        SRP05CP = SRP05CustomParameter;
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        //base.Render(renderContext, cameras);
        SRP05Rendering.Render(renderContext, cameras,SRP05CP);
    }
}

public static class SRP05Rendering
{
    public static TextMesh textMesh;

    public static void Render(ScriptableRenderContext context, IEnumerable<Camera> cameras, SRP05CustomParameter SRP05CP)
    {
        string tx = "";
        foreach (Camera camera in cameras)
        {
            ScriptableCullingParameters cullingParams;

            // Stereo-aware culling parameters are configured to perform a single cull for both eyes
            if (!CullResults.GetCullingParameters(camera, out cullingParams))
                continue;
            CullResults cull = new CullResults();
            CullResults.Cull(ref cullingParams, context, ref cull);

            //cull.visibleRenderers;

            //
            if(camera == Camera.main) //Only generate result from main cam
            {
                tx = "";
                tx += "<color=#0FF>cullingPlaneCount : </color>" + cullingParams.cullingPlaneCount + "\n";
                for (int i = 0; i < cullingParams.cullingPlaneCount; i++)
                {
                    if (cullingParams.cameraProperties.GetCameraCullingPlane(i).ToString() == cullingParams.cameraProperties.GetShadowCullingPlane(i).ToString())
                    {
                        tx += "<color=#0F0>cameraProperties.GetCameraCullingPlane =  GetShadowCullingPlane (" + i + ") : </color>" + cullingParams.cameraProperties.GetCameraCullingPlane(i) + "\n";
                    }
                    else
                    {
                        tx += "<color=#0FF>cameraProperties.GetCameraCullingPlane(" + i + ") : </color>" + cullingParams.cameraProperties.GetCameraCullingPlane(i) + "\n";
                        tx += "<color=#0FF>cameraProperties.GetShadowCullingPlane(" + i + ") : </color>" + cullingParams.cameraProperties.GetShadowCullingPlane(i) + "\n";
                    }
                }
                //
                tx += "<color=#0FF>cullingFlags : </color>" + cullingParams.cullingFlags.ToString() + "\n";
                tx += "<color=#0FF>cullingMask : </color>" + cullingParams.cullingMask.ToString() + "\n";
                tx += "<color=#0FF>cullingMatrix : </color>" + cullingParams.cullingMatrix + "\n";
                //
                if (cullingParams.isOrthographic == cullingParams.lodParameters.isOrthographic)
                {
                    tx += "<color=#0F0>isOrthographic = lodParameters.isOrthographic : </color>" + cullingParams.isOrthographic + "\n";
                }
                else
                {
                    tx += "<color=#0FF>isOrthographic : </color>" + cullingParams.isOrthographic + "\n";
                }
                if (cullingParams.position == cullingParams.lodParameters.cameraPosition)
                {
                    tx += "<color=#0F0>position = lodParameters.cameraPosition : </color>" + cullingParams.position + "\n";
                }
                else
                {
                    tx += "<color=#0FF>position : </color>" + cullingParams.position + "\n";
                }
                //
                tx += "<color=#0FF>reflectionProbeSortOptions : </color>" + cullingParams.reflectionProbeSortOptions.ToString() + "\n";
                tx += "<color=#0FF>sceneMask : </color>" + cullingParams.sceneMask + "\n";
                tx += "<color=#0FF>shadowDistance : </color>" + cullingParams.shadowDistance + "\n";
                //
                tx += "<color=#0FF>layerCull : </color>" + cullingParams.layerCull + "\n";
                for (int i = 0; i < cullingParams.layerCull; i++)
                {
                    tx += "<color=#0FF>GetLayerCullDistance(" + i + ") : </color>" + cullingParams.GetLayerCullDistance(i) + "\n";
                }
                //
                tx += "<color=#0FF>lodParameters : </color>" + "\n";
                tx += "cameraPixelHeight = " + cullingParams.lodParameters.cameraPixelHeight + "\n";
                if (camera.fieldOfView == cullingParams.lodParameters.fieldOfView)
                {
                    tx += "fieldOfView = camera.fieldOfView : " + "<color=#0F0>" + cullingParams.lodParameters.fieldOfView + "</color>" + "\n";
                }
                else
                {
                    tx += "fieldOfView != camera.fieldOfView : " + "<color=#FF0>" + cullingParams.lodParameters.fieldOfView + "</color>" + "\n";
                }
                if (camera.orthographicSize == cullingParams.lodParameters.orthoSize)
                {
                    tx += "orthoSize = camera.orthographicSize : " + "<color=#0F0>" + cullingParams.lodParameters.orthoSize + "</color>" + "\n";
                }
                else
                {
                    tx += "orthoSize != camera.orthographicSize : " + "<color=#FF0>" + cullingParams.lodParameters.orthoSize + "</color>" + "\n";
                }
                if (cullingParams.isOrthographic != cullingParams.lodParameters.isOrthographic)
                {
                    tx += "isOrthographic = " + cullingParams.lodParameters.isOrthographic + "\n";
                }
                if (cullingParams.position != cullingParams.lodParameters.cameraPosition)
                {
                    tx += "cameraPosition = " + cullingParams.lodParameters.cameraPosition + "\n";
                }

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
            }


            // Setup camera for rendering (sets render target, view/projection matrices and other
            // per-camera built-in shader variables).
            context.SetupCameraProperties(camera);

            // clear depth buffer
            CommandBuffer cmd = new CommandBuffer();
            cmd.ClearRenderTarget(true, !SRP05CP.DrawSkybox, SRP05CP.ClearColor);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();

            // Setup global lighting shader variables
            //SetupLightShaderVariables(cull.visibleLights, context);

            if(SRP05CP.DrawSkybox)
            {
                // Draw skybox
                context.DrawSkybox(camera);
            }

            // Setup DrawSettings and FilterSettings
            ShaderPassName passName = new ShaderPassName("BasicPass");
            DrawRendererSettings drawSettings = new DrawRendererSettings(camera, passName);
            FilterRenderersSettings filterSettings = new FilterRenderersSettings(true);

            if (SRP05CP.DrawOpaque)
            {
                // Draw opaque objects using BasicPass shader pass
                drawSettings.sorting.flags = SortFlags.CommonOpaque;
                filterSettings.renderQueueRange = RenderQueueRange.opaque;
                context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
            }

            if (SRP05CP.DrawTransparent)
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

public class SRP05CustomParameter
{
    public Color ClearColor = Color.white;
    public bool DrawSkybox = true;
    public bool DrawOpaque = true;
    public bool DrawTransparent = true;

    public SRP05CustomParameter()
    {

    }
}