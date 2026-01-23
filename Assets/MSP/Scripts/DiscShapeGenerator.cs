using POV_Unity;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DiscShapeGenerator : MonoBehaviour
{
    [Title("Circle / Arc stored defaults")]
    [SerializeField]
    private float m_radius = 0.5f;
    [SerializeField]
    private float m_thickness = 0.1f;
    [SerializeField]
    private int m_segments = 100;

    [SerializeField]
    private float m_angleStart = 0f;
    [SerializeField]
    private float m_angleEnd = 90f;


    public float radius => m_radius;
    public float thickness => m_thickness;
    public int segments => m_segments;
    public float angleStart => m_angleStart;
    public float angleEnd => m_angleEnd;

    private void OnValidate()
    {
        if (0.5f * m_thickness > m_radius)
            m_thickness = m_radius;
    }

    [SerializeField]
    private string m_meshAssetPath = "Assets/GeneratedMeshes/";
    
    [SerializeField]
    private string m_meshAssetName = "Unknown";


#if UNITY_EDITOR
    [ExecuteInEditMode]
    [Button("Generate Shape")]
    private void GenerateShapeInEditor()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogError("No MeshFilter found on the GameObject, cannot generate shape!");
            return;
        }

        Mesh mesh = GenerateShape(m_radius, m_thickness, m_segments, m_angleStart, m_angleEnd);

		AssetDatabase.CreateAsset(mesh, m_meshAssetPath + m_meshAssetName + ".asset");

        meshFilter.mesh = mesh;
    }
#endif

    public void GenerateShapeRuntime(float a_radius, float a_thickness, int a_segments, float a_angleStart, float a_angleEnd)
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.mesh = GenerateShape(a_radius, a_thickness, a_segments, a_angleStart, a_angleEnd);
        }
    }


    public static Mesh GenerateShape(float a_radius, float a_thickness, int a_segments, float a_angleStart, float a_angleEnd)
    {
        Mesh mesh = new Mesh();

        mesh.name = "Custom disc shape";

        int vertexCount = (a_segments + 1) * 2; // Inner and outer vertices
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[((a_segments + 1) * 2 * 3)]; // 2 triangles per segment, 3 indices each
        Vector2[] uv = new Vector2[vertexCount];

        float angleRange = a_angleEnd - a_angleStart;
        for (int i = 0; i <= a_segments; i++)
        {
            float t = (float)i / a_segments;
            float angle = Mathf.Deg2Rad * (a_angleStart + t * angleRange);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices[i * 2] = new Vector3(cos * (a_radius + a_thickness / 2), sin * (a_radius + a_thickness / 2), 0);
            vertices[i * 2 + 1] = new Vector3(cos * (a_radius - a_thickness / 2), sin * (a_radius - a_thickness / 2), 0);

            uv[i * 2] = new Vector2(t, 1);
            uv[i * 2 + 1] = new Vector2(t, 0);
        }

        for (int i = 0; i < a_segments; i++)
        {
            int baseIndex = i * 2;
            triangles[i * 6] = baseIndex;
            triangles[i * 6 + 1] = baseIndex + 2;
            triangles[i * 6 + 2] = baseIndex + 1;

            triangles[i * 6 + 3] = baseIndex + 1;
            triangles[i * 6 + 4] = baseIndex + 2;
            triangles[i * 6 + 5] = baseIndex + 3;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();

        return mesh;
    }
}