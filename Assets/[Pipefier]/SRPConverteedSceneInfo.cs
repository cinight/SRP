using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// This object stores all information and references for a scene. It will be scanned for by a profile when started.
/// </summary>
public class SRPConverteedSceneInfo : MonoBehaviour {

    [Header("Render pipe profile")]
    public List<BufferList> objectsBuffer = new List<BufferList>();
}
