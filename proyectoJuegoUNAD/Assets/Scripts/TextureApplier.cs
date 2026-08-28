using UnityEngine;

public class TextureApplier : MonoBehaviour
{
    public Material droneMaterial;
    public Texture2D baseTexture;

    void Start()
    {
        ApplyTextures();
    }

    void ApplyTextures()
    {
        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null && droneMaterial != null && baseTexture != null)
        {
            meshRenderer.material = droneMaterial;
            meshRenderer.material.SetTexture("_BaseMap", baseTexture);
            meshRenderer.material.SetTexture("_MainTex", baseTexture);
        }
    }
}
