using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[System.Serializable]
public class BufferList
{
    public GameObject gObject;
    public int guid;
    public List<Material> standardMaterialList = new List<Material>();
    public List<Material> lightweightMaterialList = new List<Material>();
    public bool isParticle;
}

[CreateAssetMenu(fileName = "Pipefier Profile", menuName = "Pipefier/PipefierProfile", order = 1), System.Serializable]
public class ScriptableRenderPipeInfoProfile : ScriptableObject
{
    [Header("Current scene info"), HideInInspector]
    /// <summary>
    /// Holds the infomation for the current scene.
    /// </summary>
    public SRPConverteedSceneInfo sceneInfo;

    [Header("SRP Assets")]
    public RenderPipelineAsset lightweightProfile;
    public RenderPipelineAsset highDefinitionProfile;

    //list of shaders for LW pipe
    [Header("Approved shaders list")]
    public List<Shader> ApprovedShaders;

    [Header("Shader Keywords")]
    public string shaderKeywords;

    public void ScanForShaders()
    {
        string[] allfiles = System.IO.Directory.GetFiles(Application.dataPath, "*.shader*", System.IO.SearchOption.AllDirectories);

        Debug.Log("we found " + allfiles.Length.ToString() + " shader files");
        foreach (var file in allfiles)
        {
            if (file.ToString().Contains(shaderKeywords))
            {
                FileInfo T = new FileInfo(file);
                // Shader S = new Shader(file);
                //    ApprovedShaders.Add( CType(FileInfo, Shader));
            }
        }
    }

    public void GenerateShaderList()
    {
        Shader lwS = Shader.Find("LightweightPipeline/Standard (Physically Based)");
        Shader lws2 = Shader.Find("LightweightPipeline/Standard (Simple Lighting)");
        Shader lwP = Shader.Find("LightweightPipeline/Particles/Standard");
        if (!ApprovedShaders.Contains(lwS))
        {
            ApprovedShaders.Add(lwS);
        }
        if (!ApprovedShaders.Contains(lws2))
        {
            ApprovedShaders.Add(lws2);
        }

        if (!ApprovedShaders.Contains(lwP))
        {
            ApprovedShaders.Add(lwP);
        }
        
    }
}
