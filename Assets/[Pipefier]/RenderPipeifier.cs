using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.IO;

public class objectInformation
{
    public List<GameObject> go = new List<GameObject>();
    public bool expand;
    public Material matIssue;
    public Shader shaderIssue;
}

public class MaterialBuffer
{
    public objectInformation objInfo;

    public Texture mainTex;
    public Color mainTexColor;

    public Texture metalicTex;
    public float metalicFloat;
    public float smoothnessFloat;

    public Texture normalMap;
    public float normalFloat;

    public Texture occlusionTex;

    public bool emission;

    public Vector2 tiling;
    public Vector2 offset;
    internal Color emissionColor;

    public float OcclusionFloat { get; internal set; }
    public Texture EmissionMap { get; internal set; }

    public string[] keywords;
}

public enum RenderPipeSettings
{
    StandardLegacy,
    LightweightPipe,
    highDefinitionPipe
}

public enum UpdateType
{
    CacheMaterials,
    UpdateMaterials
}

public class RenderPipeifier : EditorWindow
{

    public ScriptableRenderPipeInfoProfile targetPipeProfile;

    private Texture2D title;
    private Texture2D warning;
    private int selectedInt;
    private UpdateType updateType = UpdateType.CacheMaterials;

    bool shaderToolsWindow;

    public RenderPipeSettings pipeSettings;

    [MenuItem("Pipefier/Scene Converter")]
    public static void Init()
    {
        RenderPipeifier window = (RenderPipeifier)EditorWindow.GetWindow(typeof(RenderPipeifier));
    }

    private List<GameObject> shaderlessGameobjects = new List<GameObject>();

    // Use this for initialization
    private Dictionary<Material, objectInformation> OnConvert()
    {
        //    UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset = targetPipe;
        Dictionary<Material, objectInformation> issueObjects = new Dictionary<Material, objectInformation>();
        brokenObjects.Clear();
        //check entire scene for pipe conversion.
        foreach (GameObject T in FindObjectsOfType<GameObject>())
        {
            if (T.GetComponent<Renderer>())
            {
                //set materials
                foreach (Material mat in T.GetComponent<Renderer>().sharedMaterials)
                {
                    try
                    {
                        if (!T.GetComponent<ParticleSystem>())
                        {
                            if (mat.shader != null)
                            {

                                if (!issueObjects.ContainsKey(mat))
                                {
                                    Shader shader = mat.shader;

                                    if (targetPipeProfile.ApprovedShaders.Contains(shader))
                                    {
                                        // we good
                                    }
                                    else
                                    {
                                        Debug.Log("<color=orange>Shader: " + shader.name + " is not in the approved shaders list for " + targetPipeProfile.name + "</color>");
                                        objectInformation issue = new objectInformation();
                                        issue.matIssue = mat;
                                        issue.go.Add(T);
                                        issue.shaderIssue = shader;
                                        issueObjects.Add(mat, issue);
                                    }
                                }
                                else
                                {
                                    //we already have the mat, add it to this list.#
                                    issueObjects[mat].go.Add(T);
                                }
                            }
                            else
                            {
                                shaderlessGameobjects.Add(T);
                            }
                        }
                        else
                        {
                            //handle particle system
                            
                        }
                    }
                    catch (System.Exception)
                    {
                        shaderlessGameobjects.Add(T);
                    }
                }
            }
        }

        return issueObjects;
    }
    List<MaterialBuffer> materialList = new List<MaterialBuffer>();
    private Dictionary<Material, objectInformation> brokenObjects = new Dictionary<Material, objectInformation>();
    GameObject shaderHolder;
    private bool advancedOptions;

    void OnGUI()
    {

        if (!title) title = Resources.Load("PipefierTitle") as Texture2D;
        if (!warning) warning = Resources.Load("WarningText") as Texture2D;

        if (title)
        {
            GUI.DrawTexture(new Rect(20, 10, title.width / 2, title.height / 2), title, ScaleMode.ScaleToFit, true, 10.0F);
        }

        if (warning)
        {
            GUI.DrawTexture(new Rect(40, 45, warning.width / 2, warning.height / 2), warning, ScaleMode.ScaleToFit, true, 10.0f);
        }

        if(!AssetDatabase.IsValidFolder("[Pipefier]"))
        {
            Directory.CreateDirectory("[Pipefier]");
        }

        EditorGUILayout.BeginVertical();

        for (int i = 0; i < 3; i++)
        {
            GUILayout.Label("", EditorStyles.boldLabel);
        }

        GUILayout.Label("Target pipe profile", EditorStyles.boldLabel);
        targetPipeProfile = (ScriptableRenderPipeInfoProfile)EditorGUILayout.ObjectField(targetPipeProfile, typeof(ScriptableRenderPipeInfoProfile));

        if(PlayerSettings.colorSpace == ColorSpace.Gamma)
        {
            GUILayout.Label("SRP only supports LINEAR color space.", EditorStyles.boldLabel);
            if(GUILayout.Button("Switch to Linear"))
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
            }

        }

        //spacing
        for (int i = 0; i < 1; i++)
        {
            GUILayout.Label("", EditorStyles.boldLabel);
        }

        if (targetPipeProfile)
        {

            if (!targetPipeProfile.sceneInfo)
            {
                if (FindObjectOfType<SRPConverteedSceneInfo>()) targetPipeProfile.sceneInfo = FindObjectOfType<SRPConverteedSceneInfo>();

                GUILayout.Label("There's no scene information.", EditorStyles.boldLabel);
                if (GUILayout.Button("Create new scene info"))
                {
                    GameObject T = new GameObject();
                    T.name = "[SRP cache]";
                    targetPipeProfile.sceneInfo = T.AddComponent<SRPConverteedSceneInfo>();
                    if (targetPipeProfile.ApprovedShaders.Count < 1)
                    {
                        targetPipeProfile.GenerateShaderList();
                    }
                    SaveScene();
                }

                return;
            }


            GUILayout.Label("Material upgrade method", EditorStyles.boldLabel);
            updateType = (UpdateType)EditorGUILayout.EnumPopup(updateType);


            shaderToolsWindow = EditorGUILayout.Foldout(shaderToolsWindow, "Shader tools - Experimental");
            if (shaderToolsWindow)
            {
                GUILayout.Label("Shader Tools - Experimenal", EditorStyles.boldLabel);
                if (GUILayout.Button("Generate shaderlist"))
                {
                    targetPipeProfile.GenerateShaderList();
                }



                if (brokenObjects.Count > 0)
                {
                    if (GUILayout.Button("Force convert scene"))
                    {

                    }
                    GUILayout.Label("The following objects are incompatible", EditorStyles.boldLabel);
                }

                GUILayout.Label("___________________________________________________________________________", EditorStyles.boldLabel);
            }

            ///Advanced settings region
            #region Advanced
            advancedOptions = EditorGUILayout.Foldout(advancedOptions, "Advanced tools - Destructive!");
            if (advancedOptions)
            {

                if (GUILayout.Button("Check Scene"))
                {
                    brokenObjects.Clear();
                    shaderlessGameobjects.Clear();
                    brokenObjects = OnConvert();
                }

                GUILayout.Label("Number of objects with invalid shader: " + brokenObjects.Count);
                GUILayout.Label("Number of objects without shaders: " + shaderlessGameobjects.Count);


                foreach (KeyValuePair<Material, objectInformation> T in brokenObjects)
                {
                    Shader selectedShader;
                    GUILayout.Label("___________________________________________________________________________");
                    T.Value.expand = EditorGUILayout.Foldout(T.Value.expand, T.Value.matIssue.name);

                    if (GUILayout.Button("Upgrade Shader"))
                    {
                        //convert object to selected shader
                        materialList.Clear();

                        Material targetMat = T.Value.matIssue;
                        MaterialBuffer buffer = new MaterialBuffer();
                        buffer.objInfo = T.Value;


                        if (targetMat.mainTexture)
                        {
                            Texture mainTex = targetMat.mainTexture;
                            buffer.mainTex = mainTex;
                        }
                        if (targetMat.GetTexture("_BumpMap"))
                        {
                            Texture mainTex = targetMat.GetTexture("_BumpMap");
                            buffer.normalMap = mainTex;
                        }

                        if (targetMat.GetTexture("_MetallicGlossMap"))
                        {
                            Texture mainTex = targetMat.GetTexture("_MetallicGlossMap");
                            buffer.metalicTex = mainTex;
                        }

                        if (targetMat.GetTexture("_OcclusionMap"))
                        {
                            Texture mainTex = targetMat.GetTexture("_OcclusionMap");
                            buffer.occlusionTex = mainTex;
                        }
                        //float fields
                        bool cont = true;
                        try { buffer.normalFloat = targetMat.GetFloat("_BumpScale"); }
                        catch { Debug.Log("Shader: " + T.Value.shaderIssue.name + " has no bumpscale"); cont = false; }

                        if (cont)
                        {

                            buffer.metalicFloat = targetMat.GetFloat("_Metallic");
                            buffer.smoothnessFloat = targetMat.GetFloat("_Glossiness");
                            buffer.OcclusionFloat = targetMat.GetFloat("_OcclusionStrength");
                        }

                        buffer.keywords = targetMat.shaderKeywords;

                        materialList.Add(buffer);

                        if (materialList.Count > 0)
                        {
                            foreach (MaterialBuffer Tbuffer in materialList)
                            {
                                //if (buffer.mainTex) GUILayout.Label(buffer.mainTex.name);
                                //if (buffer.normalMap) GUILayout.Label(buffer.normalMap.name);
                                //if (buffer.metalicTex) GUILayout.Label(buffer.metalicTex.name);
                                //if (buffer.occlusionTex) GUILayout.Label(buffer.occlusionTex.name);

                                Material mat = buffer.objInfo.matIssue;

                                selectedShader = targetPipeProfile.ApprovedShaders[selectedInt];
                                Debug.Log("We selecte: " + selectedInt + " which is shader: " + selectedShader.name);
                                mat.shader = selectedShader;

                                foreach (string key in buffer.keywords)
                                {
                                    mat.EnableKeyword(key);
                                }

                                mat.mainTexture = Tbuffer.mainTex;
                                if (buffer.normalMap) mat.SetTexture("_BumpMap", buffer.normalMap);
                                if (buffer.EmissionMap) mat.SetTexture("_EmissionMap", buffer.EmissionMap);
                                if (buffer.metalicTex) mat.SetTexture("_MetallicGlossMap", buffer.metalicTex);
                                if (buffer.occlusionTex) mat.SetTexture("_OcclussionMap", buffer.occlusionTex);

                                if (buffer.EmissionMap != null)
                                {
                                    mat.SetTexture("_EmissionMap", buffer.EmissionMap);
                                }

                                //floats
                                mat.SetFloat("_BumpScale", buffer.normalFloat);
                                mat.SetFloat("_Glossiness", buffer.smoothnessFloat);
                                mat.SetFloat("_Metallic", buffer.metalicFloat);
                                mat.SetFloat("_OcclusionStrength", buffer.OcclusionFloat);
                                mat.SetColor("_EmissionColor", buffer.emissionColor);


                            }
                        }

                    }


                    if (T.Value.expand)
                    {
                        GUILayout.Label("Shader: " + T.Value.shaderIssue.name);
                        foreach (GameObject g in T.Value.go)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label(g.name + " - " + T.Value.shaderIssue.name);




                            string[] pipeArrays = new string[targetPipeProfile.ApprovedShaders.Count];
                            for (int i = 0; i < pipeArrays.Length; i++)
                            {
                                pipeArrays[i] = targetPipeProfile.ApprovedShaders[i].name;
                            }
                            selectedInt = EditorGUILayout.Popup(selectedInt, pipeArrays);

                            if (GUILayout.Button("Visualise"))
                            {

                                Selection.activeGameObject = g;
                                EditorApplication.ExecuteMenuItem("Edit/Frame Selected");
                            }
                            EditorGUILayout.EndHorizontal();
                        }




                        GUILayout.Label("___________________________________________________________________________");
                    }
                }

                if (GUILayout.Button("Upgrade Scene settings"))
                {
                    ReflectionProbe[] probes = FindObjectsOfType<ReflectionProbe>();

                    foreach (ReflectionProbe T in probes)
                    {
                        if (T.enabled)
                        {
                            //refresh probes
                            T.enabled = false;
                            T.enabled = true;
                        }
                    }

                    //check lighting & shadows.

                    //check post

                    //check 

                    EditorGUILayout.EndVertical();
                    // GUILayout.Label("SRP project converter", EditorStyles.boldLabel);

                }

                foreach (GameObject b in shaderlessGameobjects)
                {
                    GUILayout.Label("Object: " + b.name + " has no shader!");
                }


                Shader sampleShader;
                GUILayout.Label("Shader extractor");
                shaderHolder = (GameObject)EditorGUILayout.ObjectField(shaderHolder, typeof(GameObject));

                if (shaderHolder)
                {
                    if (shaderHolder.GetComponent<Renderer>().sharedMaterial)
                    {
                        if (GUILayout.Button("Get/Save shader"))
                        {
                            if (targetPipeProfile)
                            {
                                targetPipeProfile.ApprovedShaders.Add(shaderHolder.GetComponent<Renderer>().sharedMaterial.shader);
                            }
                        }
                    }
                }
            }
        }
        #endregion


        if (targetPipeProfile)
        {

            GUILayout.Label("--------------- Render Pipe Settings ---------------");
            if (GUILayout.Button("Scan and prepare scene"))
            {
                ScanAndPrepareScene();
            }
            GUILayout.Label("Objects ready for SRP: " + targetPipeProfile.sceneInfo.objectsBuffer.Count);

            if (GUILayout.Button("Convert Scene for SRP"))
            {
                WriteMaterialsToLWPipe();
            }

            GUILayout.Label("Current render pipe: " + pipeSettings.ToString(), EditorStyles.boldLabel);
            //pipeSettings = (RenderPipeSettings)EditorGUILayout.EnumPopup(pipeSettings);

            updatePipeInGraphicsSettings = GUILayout.Toggle(updatePipeInGraphicsSettings, "Auto set SRP asset");

            if (pipeSettings == RenderPipeSettings.highDefinitionPipe && !targetPipeProfile.lightweightProfile || pipeSettings == RenderPipeSettings.highDefinitionPipe && !targetPipeProfile.highDefinitionProfile)
            {
                if (pipeSettings == RenderPipeSettings.LightweightPipe)
                {
                    targetPipeProfile.lightweightProfile = (RenderPipelineAsset)EditorGUILayout.ObjectField(targetPipeProfile.lightweightProfile, typeof(RenderPipelineAsset));
                }

                if (pipeSettings == RenderPipeSettings.highDefinitionPipe)
                {
                    targetPipeProfile.highDefinitionProfile = (RenderPipelineAsset)EditorGUILayout.ObjectField(targetPipeProfile.highDefinitionProfile, typeof(RenderPipelineAsset));
                }
            }
            //else if (GUILayout.Button("Set renderpipe: " + pipeSettings.ToString()))
            //{
            //    SetPipe();
            //}
 
            if(GUILayout.Button("Set Legacy Pipe"))
            {
                pipeSettings = RenderPipeSettings.StandardLegacy;
                targetPipeProfile.lightweightProfile = (RenderPipelineAsset)EditorGUILayout.ObjectField(targetPipeProfile.lightweightProfile, typeof(RenderPipelineAsset));
                SetPipe();
            }

            if(GUILayout.Button("Set LW pipe"))
            {
                pipeSettings = RenderPipeSettings.LightweightPipe;
                targetPipeProfile.highDefinitionProfile = (RenderPipelineAsset)EditorGUILayout.ObjectField(targetPipeProfile.highDefinitionProfile, typeof(RenderPipelineAsset));
                SetPipe();
            }

            if(GUILayout.Button("Set HD Pipe"))
            {
                Debug.Log("not implemented yet.");
            }

        }
    }

    private bool updatePipeInGraphicsSettings = true;

    public void SetPipe()
    {

        foreach (BufferList T in targetPipeProfile.sceneInfo.objectsBuffer)
        {
            if (pipeSettings == RenderPipeSettings.StandardLegacy)
            {
                try
                {
                    Material[] L = new Material[T.standardMaterialList.Count];
                    for (int i = 0; i < T.standardMaterialList.Count; i++)
                    {
                        L[i] = T.standardMaterialList[i];
                    }
                    if (T.gObject.GetComponent<MeshRenderer>())
                    {
                        T.gObject.GetComponent<MeshRenderer>().materials = L;
                    }
                    else if (T.gObject.GetComponent<SkinnedMeshRenderer>())
                    {
                        T.gObject.GetComponent<SkinnedMeshRenderer>().materials = L;

                    }

                }
                catch (System.Exception)
                {
                    if (T.gObject != null)
                    {
                        Debug.Log("Unable to assign new material to: " + T.gObject.name);
                    }
                }

                if (updatePipeInGraphicsSettings)
                {
                    UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset = null;
                }
            }

            if (pipeSettings == RenderPipeSettings.LightweightPipe)
            {
                try
                {
                    Material[] L = new Material[T.lightweightMaterialList.Count];
                    for (int i = 0; i < T.lightweightMaterialList.Count; i++)
                    {
                        L[i] = T.lightweightMaterialList[i];
                    }

                    if (T.gObject.GetComponent<MeshRenderer>())
                    {
                        T.gObject.GetComponent<MeshRenderer>().materials = L;
                    }
                    else if (T.gObject.GetComponent<SkinnedMeshRenderer>())
                    {
                        T.gObject.GetComponent<SkinnedMeshRenderer>().materials = L;

                    }
                }
                catch (System.Exception)
                {
                    if (T.gObject)
                    {
                        Debug.Log("Unable to assign new material to: " + T.gObject.name);
                    }
                }

                if (updatePipeInGraphicsSettings)
                {
                    UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset = targetPipeProfile.lightweightProfile;
                }

            }

        }
    }

    private void WriteMaterialsToLWPipe()
    {

        foreach (BufferList T in targetPipeProfile.sceneInfo.objectsBuffer)
        {
            foreach (Material buffer in T.standardMaterialList)
            {
                try
                {
                    //create a new material and assign it to the objects.
                    
                    Material newMat = (!T.isParticle ? new Material(targetPipeProfile.ApprovedShaders[0]) : new Material(targetPipeProfile.ApprovedShaders[2])); 

                    string[] keys = buffer.shaderKeywords;

                    foreach (string key in keys)
                    {
                        newMat.EnableKeyword(key);
                    }

                    newMat.CopyPropertiesFromMaterial(buffer);
                    newMat.name = buffer.name + "-LW";

                    // T.lightweightMaterialList.Add(newMat);

                    //store it 

                    if (!Resources.Load(newMat.name))
                    {
                        AssetDatabase.CreateAsset(newMat, "Assets/[Pipefier]/Resources/" + newMat.name + ".mat");
                    }

                    T.lightweightMaterialList.Add(Resources.Load(newMat.name) as Material);
                }
                catch (System.Exception exp)
                {
                    Debug.LogError("<color=orange>Object: " + T.gObject.name + " has compiling issues. Skipped. </color>" + exp.Message);
                }
            }
        }

        //copy materials over to new pipe

        // target.material = new Material(Shader.Find("Standard"));
        //    AssetDatabase.CreateAsset(target.material, "Assets/" + target.material)
    }


    private void ScanAndPrepareScene()
    {
        targetPipeProfile.sceneInfo.objectsBuffer.Clear();
        foreach (GameObject T in FindObjectsOfType<GameObject>())
        {
            //register game objects and their materials.
            if (!T.GetComponent<ParticleSystem>())
            {
                if (T.GetComponent<Renderer>())
                {
                    BufferList objectMats = new BufferList();
                    targetPipeProfile.sceneInfo.objectsBuffer.Add(objectMats);

                    objectMats.guid = T.GetInstanceID();
                    objectMats.gObject = (GameObject)T;

                    foreach (Material mat in T.GetComponent<Renderer>().sharedMaterials)
                    {
                        objectMats.standardMaterialList.Add(mat);
                    }
                }
            }
            else
            {
                ////handle particles
                //BufferList objectsMatParticles = new BufferList();
                //targetPipeProfile.sceneInfo.objectsBuffer.Add(objectsMatParticles);

                //objectsMatParticles.guid = T.GetInstanceID();
                //objectsMatParticles.gObject = (GameObject)T;
                //objectsMatParticles.isParticle = true;

                //foreach (Material mat in T.GetComponent<Renderer>().sharedMaterials)
                //{
                //    objectsMatParticles.standardMaterialList.Add(mat);
                //}
            }
        }
        SaveScene();
    }

    private void SaveScene()
    {
        //string[] path = EditorApplication.currentScene.Split(char.Parse("/"));
        //path[path.Length - 1] = "AutoSave_" + path[path.Length - 1];
        //EditorApplication.SaveScene(string.Join("/", path), true);
        // Debug.Log("<color=orange>Saved Scene</color>");
    }
}
