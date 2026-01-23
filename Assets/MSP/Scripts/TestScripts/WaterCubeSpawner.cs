using System.Collections.Generic;
using UnityEngine;

public class WaterCubeSpawner : MonoBehaviour
{
	public List<Vector2> UVList = new List<Vector2>
	{
				new Vector2(0, 0),
				new Vector2(1, 0),
				new Vector2(0, 1),
				new Vector2(1, 1),
				new Vector2(0, 0),
				new Vector2(1, 0),
				new Vector2(0, 1),
				new Vector2(1, 1),
				new Vector2(0, 0),
				new Vector2(1, 0),
				new Vector2(0, 1),
				new Vector2(1, 1),
				new Vector2(0, 0),
				new Vector2(1, 0),
				new Vector2(0, 1),
				new Vector2(1, 1)
	};

	public float water_cube_offset = 0.0001f;
	public float side_y_base = -0.2f;
	public float y_1;
	public Material my_material;

	public Vector3 pos0;
	public Vector3 pos1;

	public bool generate_cube = false;

    private void Update()
    {
        if (generate_cube)
		{
			GenerateWaterCube();
			generate_cube=false;

		}
	}

    void GenerateWaterCube()
	{
		//Create mesh and set properties
		Mesh procMesh = new Mesh();
		procMesh.vertices = new Vector3[]
		{
			//Top
			new Vector3(pos0.x, pos1.y, pos0.z),
			new Vector3(pos1.x, pos1.y, pos0.z),
			pos1,
			new Vector3(pos0.x, pos1.y, pos1.z),

			//Left
			pos0,
			new Vector3(pos0.x, pos0.y, pos1.z),
			new Vector3(pos0.x, pos1.y, pos1.z),
			new Vector3(pos0.x, pos1.y, pos0.z),

			//Right
			new Vector3(pos1.x, pos0.y, pos0.z),
			new Vector3(pos1.x, pos0.y, pos1.z),
			pos1,
			new Vector3(pos1.x, pos1.y, pos0.z),

			//Front
			pos0,
			new Vector3(pos1.x, pos0.y, pos0.z),
			new Vector3(pos1.x, pos1.y, pos0.z),
			new Vector3(pos0.x, pos1.y, pos0.z),

			//Back
			new Vector3(pos0.x, pos0.y, pos1.z),
			new Vector3(pos1.x, pos0.y, pos1.z),
			pos1,
			new Vector3(pos0.x, pos1.y, pos1.z),
		};
		procMesh.triangles = new int[]
		{
				0,3,2,
				2,1,0,
				5,6,7,
				7,4,5,
				8,11,10,
				10,9,8,
				12,15,14,
				14,13,12,
				17,18,19,
				19,16,17
		};
		procMesh.uv = new Vector2[]
		{
			new Vector2(0,0),
			new Vector2(1,0),
			new Vector2(1,1),
			new Vector2(0,1),
			new Vector2(0,0),
			new Vector2(1,0),
			new Vector2(1,1),
			new Vector2(0,1),
			new Vector2(0,0),
			new Vector2(1,0),
			new Vector2(1,1),
			new Vector2(0,1),
			new Vector2(0,0),
			new Vector2(1,0),
			new Vector2(1,1),
			new Vector2(0,1),
			new Vector2(0,0),
			new Vector2(1,0),
			new Vector2(1,1),
			new Vector2(0,1),
		};

		procMesh.Optimize();
		procMesh.RecalculateNormals();

		//Create gameobject and add mesh renderer
		GameObject meshObject = new GameObject("WaterCube");
		MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
		meshFilter.mesh = procMesh;
		MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
		renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
		renderer.material = my_material;
	}

}
