using UnityEngine;

public class CheckCullVisible : MonoBehaviour
{
    public TextMesh tm;
    public Renderer[] rens;
	
	// Update is called once per frame
	void Update ()
    {
        tm.text = "";
        for (int i =0;i<rens.Length;i++)
        {
            if (rens[i].isVisible)
            {

                tm.text += rens[i].gameObject.name + " <color=#0F0>Yes</color> \n";
            }
            else
            {
                tm.text += rens[i].gameObject.name + " <color=#F00>No</color> \n";
            }
        }
 
    }
}
