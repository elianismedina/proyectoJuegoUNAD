using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A soft round shadow straight below the player, so the landing spot of a jump can be judged in 3D
/// (Docs/ThirdPersonConversionPlan.md; design review proposal 1). The sun's shadow falls at an angle, so it does not
/// say where the player will land; this one always sits on the ground right underneath.
/// The disc shrinks as the player rises, and disappears when there is no ground below (over a stream), which
/// is itself a warning. It is a flat, low-poly disc with no collider, built at runtime as a child named "JumpShadow".
/// </summary>
public class JumpShadow : MonoBehaviour
{
    public const string ShadowName = "JumpShadow";

    [Tooltip("Transparent dark material for the disc (Assets/Materials/Player_JumpShadow.mat).")]
    [SerializeField] private Material material;

    [Tooltip("Layers the shadow lands on: the forest floor and the obstacles.")]
    [SerializeField] private LayerMask groundLayers;

    [Tooltip("Disc radius in metres when the player stands on the ground.")]
    [SerializeField, Min(0.05f)] private float radius = 0.4f;

    [Tooltip("Height above the ground at which the disc reaches its smallest size.")]
    [SerializeField, Min(0.5f)] private float fadeHeight = 3f;

    [Tooltip("Disc size at fadeHeight and above, relative to its size on the ground.")]
    [SerializeField, Range(0.1f, 1f)] private float minScale = 0.5f;

    [Tooltip("Furthest the ground can be below the player and still get a shadow.")]
    [SerializeField, Min(1f)] private float maxDistance = 12f;

    private const int Sides = 16;
    private const float Lift = 0.02f;      // Above the ground so the disc does not flicker into it.
    private const float RayStart = 0.5f;   // Above the feet, so the ray starts outside a slope the player stands on.

    private static Mesh discMesh;
    private Transform shadow;
    private Renderer shadowRenderer;

    /// <summary>The shadow disc, for tests.</summary>
    public Transform Shadow => shadow;

    /// <summary>True when the disc is shown (there is ground below).</summary>
    public bool IsVisible => shadowRenderer != null && shadowRenderer.enabled;

    private void Awake()
    {
        if (groundLayers.value == 0) groundLayers = LayerMask.GetMask("Ground");

        var go = new GameObject(ShadowName);
        shadow = go.transform;
        shadow.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = DiscMesh();

        var meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        shadowRenderer = meshRenderer;

        if (material == null)
        {
            Debug.LogWarning("JumpShadow: no material set on " + name + "; the shadow is hidden.", this);
            meshRenderer.enabled = false;
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        Vector3 origin = transform.position + Vector3.up * RayStart;
        if (!Physics.Raycast(origin, Vector3.down, out var hit, maxDistance + RayStart, groundLayers, QueryTriggerInteraction.Ignore))
        {
            shadowRenderer.enabled = false;
            return;
        }

        shadowRenderer.enabled = true;
        float height = Mathf.Max(0f, transform.position.y - hit.point.y);
        float scale = radius * Mathf.Lerp(1f, minScale, height / fadeHeight);

        shadow.SetPositionAndRotation(hit.point + hit.normal * Lift, Quaternion.FromToRotation(Vector3.up, hit.normal));
        // Divide out the player's own scale so the disc keeps its size in metres.
        Vector3 parentScale = transform.lossyScale;
        shadow.localScale = new Vector3(scale / parentScale.x, 1f / parentScale.y, scale / parentScale.z);
    }

    /// <summary>A flat polygon of radius 1 facing up, shared by every shadow.</summary>
    private static Mesh DiscMesh()
    {
        if (discMesh != null) return discMesh;

        var vertices = new Vector3[Sides + 1];
        var normals = new Vector3[Sides + 1];
        var triangles = new int[Sides * 3];
        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;
        for (int i = 0; i < Sides; i++)
        {
            float angle = i * Mathf.PI * 2f / Sides;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            normals[i + 1] = Vector3.up;

            // Clockwise seen from above, so the front face points up (Cross(b - a, c - a) is +Y).
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1) % Sides + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        discMesh = new Mesh { name = "JumpShadowDisc", vertices = vertices, normals = normals, triangles = triangles };
        discMesh.RecalculateBounds();
        return discMesh;
    }
}
