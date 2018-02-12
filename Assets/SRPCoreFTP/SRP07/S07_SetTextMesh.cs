using UnityEngine;

public class S07_SetTextMesh : MonoBehaviour
{
    public TextMesh tm;

	void Start ()
    {
        SRP07Rendering.textMesh = tm;
	}

}
