using UnityEngine;

// Attach to an empty GameObject. Scale/position its transform to match
// the same room bounds you used in HeatmapVolumeBaker (boundsMin/boundsMax).
// Non-uniform scale will stretch this into an ellipsoid if your room
// isn't a perfect cube — that's fine, it still matches the baked bounds.
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class ToxicVolumeRenderer : MonoBehaviour
{
    public Texture3D volumeTexture;
    public Material volumeMaterial; // uses Custom/ToxicVolumeRaymarchURP

    void Start()
    {
        if (volumeMaterial != null && volumeTexture != null)
            volumeMaterial.SetTexture("_VolumeTex", volumeTexture);

        GetComponent<MeshFilter>().mesh = BuildSphere();
        GetComponent<MeshRenderer>().material = volumeMaterial;
    }

    // Grabs Unity's built-in sphere mesh (radius 0.5, same bounding box
    // as the old unit cube), so the shader's -0.5..0.5 -> 0..1 texture
    // mapping still works unchanged.
    Mesh BuildSphere()
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return sphereMesh;
    }
}