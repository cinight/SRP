using UnityEngine;

public class S05_SetTextMesh : MonoBehaviour
{
    public TextMesh tm;

	void Start ()
    {
        SRP05Rendering.textMesh = tm;
	}

}
