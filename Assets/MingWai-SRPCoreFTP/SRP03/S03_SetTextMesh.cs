using UnityEngine;

[ExecuteInEditMode]
public class S03_SetTextMesh : MonoBehaviour
{
    public TextMesh tm;
    public Light[] lights;
    public Renderer[] rens;
    public ReflectionProbe[] refl;
    public Camera MainCam;
    public Camera AllCam;
    public Camera NoneCam;

    void Start ()
    {
        SRP03Rendering.textMesh = tm;
        SRP03Rendering.lights = lights;
        SRP03Rendering.reflprobes = refl;
        SRP03Rendering.rens = rens;
        SRP03Rendering.MainCam = MainCam;
        SRP03Rendering.AllCam = AllCam;
        SRP03Rendering.NoneCam = NoneCam;
    }

}
