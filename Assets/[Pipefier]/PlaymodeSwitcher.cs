using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaymodeSwitcher : MonoBehaviour
{
#if UNITY_EDITOR
    private RenderPipeifier pipe;

    // Update is called once per frame

    void Start()
    {
        pipe = FindObjectOfType<RenderPipeifier>();
    }

    void Update()
    {
        if (Input.GetKeyDown("1"))
        {
            pipe.pipeSettings = RenderPipeSettings.StandardLegacy;
            pipe.SetPipe();
        }

        if (Input.GetKeyDown("1"))
        {
            pipe.pipeSettings = RenderPipeSettings.LightweightPipe;
            pipe.SetPipe();
        }
    }
#endif
}
