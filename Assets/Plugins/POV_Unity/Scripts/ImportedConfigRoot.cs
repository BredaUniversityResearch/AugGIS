using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Clipper2Lib;

namespace POV_Unity
{
	public class ImportedConfigRoot : SerializedMonoBehaviour
	{
		public const float LAYER_HEIGHT_OFFSET = 0.00001f;
		public const float DM_HEIGHT_OFFSET = 0.00001f;

		static ImportedConfigRoot m_instance;
		public static ImportedConfigRoot Instance
		{
			get
			{
				if (m_instance == null)
				{
					m_instance = FindFirstObjectByType<ImportedConfigRoot>();
					if(m_instance == null)
					{
						Debug.LogError("Could not find ImportedConfigRoot");
					}
				}
				return m_instance;
			}
		}

		[SerializeField, HideInInspector] Vector3 m_configToWorldOffset;
		[SerializeField, HideInInspector] float m_configToWorldScale;
		[SerializeField, HideInInspector] Dictionary<Type, List<ADisplayMethod>> m_displayMethodsByType;
		int m_nextLayerToLoad = 0;
		bool m_horizontalLongEdge;
		float m_areaAspectRatio;

		[HideInInspector] public DMBathymetry m_bathymetry;
		[HideInInspector] public DataConfig m_dataConfig;
		[HideInInspector] public DisplayMethodConfig m_displayMethodConfig;
		PathD m_areaPath;
		RectD m_areaRect;
		Vector2 m_areaCornerBL, m_areaCornerTR;
		Vector3 m_areaCornerBLWorld, m_areaCornerTRWorld;

		//Import events
		public event Action<ALayer> m_onLayerImported;
		public event Action<ALayer, ADisplayMethod, int> m_onDMAddedToLayer;
		public event Action m_onImportComplete;
		public event Action<float, string> m_onImportProgress;
		float m_importProgress;
		float m_importStepIncrement;
		bool m_importComplete;

		public float ConfigToWorldScale => m_configToWorldScale;
		public Vector3 ConfigToWorldOffset => m_configToWorldOffset;
		public bool ImportComplete => m_importComplete;
		public float ImportProgress => m_importProgress;
		public bool HorizontalLongEdge => m_horizontalLongEdge;
		public float AreaAspectRatio => m_areaAspectRatio;
		public PathD AreaPath => m_areaPath;
		public RectD AreaRect => m_areaRect;
		public Vector2 AreaCornerBL => m_areaCornerBL;
		public Vector2 AreaCornerTR => m_areaCornerTR;
		public Vector3 AreaCornerBLWorld => m_areaCornerBLWorld;
		public Vector3 AreaCornerTRWorld => m_areaCornerTRWorld;

		private void Awake()
		{
			if (m_instance == null)
			{
				m_instance = this;
			}
			else if(m_instance != this)
			{
				Debug.LogWarning("Multiple ConfigimporterSettings active in the scene. Destroying new instance.");
				Destroy(gameObject);
				return;
			}
		}

		public void NotifyLoadStarted()
		{
			m_importProgress = 0f;
			m_onImportProgress?.Invoke(0f, "Loading configuration file");
		}

		void LoadNextStep(string a_description)
		{
			m_importProgress += m_importStepIncrement;
			m_onImportProgress?.Invoke(m_importProgress, a_description);
		}

		public void Initialise(DataConfig a_dataConfig, DisplayMethodConfig a_displayMethodConfig)
		{
			if (m_instance == null)
			{
				m_instance = this;
			}
			else if (m_instance != this)
			{
				Debug.LogWarning("Multiple ConfigimporterSettings active in the scene. Destroying new instance.");
				Destroy(gameObject);
				return;
			}

			m_importStepIncrement = 100f / (a_displayMethodConfig.display_methods.Length + a_dataConfig.datamodel.raster_layers.Length + a_dataConfig.datamodel.vector_layers.Length);
			m_dataConfig = a_dataConfig;
			m_displayMethodConfig = a_displayMethodConfig;
			m_displayMethodsByType = new Dictionary<Type, List<ADisplayMethod>>();
			m_areaCornerBL = new Vector2(a_dataConfig.datamodel.coordinate0[0], a_dataConfig.datamodel.coordinate0[1]);
			m_areaCornerTR = new Vector2(a_dataConfig.datamodel.coordinate1[0], a_dataConfig.datamodel.coordinate1[1]);
			m_areaPath = new PathD(new PointD[] {
				new PointD(m_areaCornerBL[0], m_areaCornerBL[1]),
				new PointD(m_areaCornerBL[0], m_areaCornerTR[1]),
				new PointD(m_areaCornerTR[0], m_areaCornerTR[1]),
				new PointD(m_areaCornerTR[0], m_areaCornerBL[1])
			});
			m_areaRect = new RectD(m_areaCornerBL[0], m_areaCornerBL[1],
				m_areaCornerTR[0], m_areaCornerTR[1]);

			float xSize = Mathf.Abs(m_areaCornerTR[0] - m_areaCornerBL[0]);
			float ySize = Mathf.Abs(m_areaCornerTR[1] - m_areaCornerBL[1]);
			m_areaAspectRatio = xSize / ySize;
			m_horizontalLongEdge = xSize > ySize;
			m_configToWorldScale = 1f / (m_horizontalLongEdge ? xSize : ySize);
			m_configToWorldOffset = new Vector3((m_areaCornerTR[0] - m_areaCornerBL[0]) * 0.5f + m_areaCornerBL[0]
				, 0f,
				(m_areaCornerTR[1] - m_areaCornerBL[1]) * 0.5f + m_areaCornerBL[1]);
			m_areaCornerBLWorld = new Vector3((m_areaCornerBL.x - m_configToWorldOffset.x) * ConfigToWorldScale, 0f, (m_areaCornerBL.y - m_configToWorldOffset.z) * ConfigToWorldScale);
			m_areaCornerTRWorld = new Vector3((m_areaCornerTR.x - m_configToWorldOffset.x) * ConfigToWorldScale, 0f, (m_areaCornerTR.y - m_configToWorldOffset.z) * ConfigToWorldScale);

			RemoveInvalidOrInvisibleLayers();
			LoadNextLayer();
		}

		private void RemoveInvalidOrInvisibleLayers()
		{
			List<RasterLayer> rasterLayers = new List<RasterLayer>(m_dataConfig.datamodel.raster_layers);
			for (int i = rasterLayers.Count - 1; i >= 0; i--)
			{
				if (rasterLayers[i].HasTag("Invisible"))
				{
					Debug.Log("** CONFIG NOTICE ** Layer '" + rasterLayers[i].name + "' is marked as invisible and will be removed from the config.");
					rasterLayers.RemoveAt(i);
					continue;
				}
				if (rasterLayers[i].Validate()) continue;
				Debug.LogWarning("** CONFIG WARNING ** Layer '" + rasterLayers[i].name + "' is invalid and will be removed from the config.");
				rasterLayers.RemoveAt(i);
			}
			m_dataConfig.datamodel.raster_layers = rasterLayers.ToArray();

			List<VectorLayer> vectorLayers = new List<VectorLayer>(m_dataConfig.datamodel.vector_layers);
			for (int i = vectorLayers.Count - 1; i >= 0; i--)
			{
				if (vectorLayers[i].HasTag("Invisible"))
				{
					Debug.LogWarning("** CONFIG WARNING ** Layer '" + vectorLayers[i].name + "' is marked as invisible and will be removed from the config.");
					vectorLayers.RemoveAt(i);
					continue;
				}
				if (vectorLayers[i].Validate()) continue;
				Debug.Log("** CONFIG ERROR ** Layer '" + vectorLayers[i].name + "' is invalid and will be removed from the config.");
				vectorLayers.RemoveAt(i);
			}
			m_dataConfig.datamodel.vector_layers = vectorLayers.ToArray();
		}

		private void OnDestroy()
		{
			if (m_instance == this)
				m_instance = null;
		}

		void LoadNextLayer()
		{
			//m_lastLayerLoaded = false;
			if (m_nextLayerToLoad < m_dataConfig.datamodel.raster_layers.Length)
			{
				LoadNextStep($"Loading layer ({m_dataConfig.datamodel.raster_layers[m_nextLayerToLoad].@short})");
				Debug.Log($"Config import progress: Loading layer ({m_nextLayerToLoad+1}/{m_dataConfig.datamodel.vector_layers.Length + m_dataConfig.datamodel.raster_layers.Length}, {m_dataConfig.datamodel.raster_layers[m_nextLayerToLoad].@short})");
				m_dataConfig.datamodel.raster_layers[m_nextLayerToLoad].Initialise(transform, OnLayerLoaded, m_nextLayerToLoad);
				m_onLayerImported?.Invoke(m_dataConfig.datamodel.raster_layers[m_nextLayerToLoad]);
			}
			else
			{
				LoadNextStep($"Loading layer ({m_dataConfig.datamodel.vector_layers[m_nextLayerToLoad - m_dataConfig.datamodel.raster_layers.Length].@short})");
				Debug.Log($"Config import progress: Loading layer ({m_nextLayerToLoad+1}/{m_dataConfig.datamodel.vector_layers.Length + m_dataConfig.datamodel.raster_layers.Length}, {m_dataConfig.datamodel.vector_layers[m_nextLayerToLoad - m_dataConfig.datamodel.raster_layers.Length].@short})");
				m_dataConfig.datamodel.vector_layers[m_nextLayerToLoad - m_dataConfig.datamodel.raster_layers.Length].Initialise(transform, OnLayerLoaded, m_nextLayerToLoad);
				m_onLayerImported?.Invoke(m_dataConfig.datamodel.vector_layers[m_nextLayerToLoad - m_dataConfig.datamodel.raster_layers.Length]);
			}
		}

		void OnLayerLoaded()
		{
			m_nextLayerToLoad++;
			if (m_nextLayerToLoad == m_dataConfig.datamodel.vector_layers.Length + m_dataConfig.datamodel.raster_layers.Length)
			{
				LoadDisplayMethods();
			}
			else
			{
				LoadNextLayer();
			}
		}

		void LoadDisplayMethods()
		{
			for (int i = 0; i < m_displayMethodConfig.display_methods.Length; i++)
			{
				LoadNextStep($"Loading display method ({m_displayMethodConfig.display_methods[i].name}");

				Debug.Log($"Config import progress: Loading DisplayMethod ({i+1}/{m_displayMethodConfig.display_methods.Length}, {m_displayMethodConfig.display_methods[i].name})");
				if (m_displayMethodsByType.TryGetValue(m_displayMethodConfig.display_methods[i].GetType(), out var list))
				{
					list.Add(m_displayMethodConfig.display_methods[i]);
				}
				else
				{
					m_displayMethodsByType.Add(m_displayMethodConfig.display_methods[i].GetType(), new List<ADisplayMethod>() { m_displayMethodConfig.display_methods[i] });
				}

				ADisplayMethod result = null;
				for(int j = 0; j < m_dataConfig.datamodel.raster_layers.Length; j++)
				{
					result = m_displayMethodConfig.display_methods[i].TryDisplayLayer(m_dataConfig.datamodel.raster_layers[j]);
					if (result != null)
						m_onDMAddedToLayer?.Invoke(m_dataConfig.datamodel.raster_layers[j], m_displayMethodConfig.display_methods[i], m_dataConfig.datamodel.raster_layers[j].LastDMIndex);
				}
				for (int j = 0; j < m_dataConfig.datamodel.vector_layers.Length; j++)
				{
					result = m_displayMethodConfig.display_methods[i].TryDisplayLayer(m_dataConfig.datamodel.vector_layers[j]);
					if (result != null)
						m_onDMAddedToLayer?.Invoke(m_dataConfig.datamodel.vector_layers[j], m_displayMethodConfig.display_methods[i], m_dataConfig.datamodel.vector_layers[j].LastDMIndex);
				}
			}

#if UNITY_EDITOR
			if (Application.isPlaying)
				Destroy(AssetManager.Instance.gameObject);
			else
				DestroyImmediate(AssetManager.Instance.gameObject);
#else
			Destroy(AssetManager.Instance.gameObject);
#endif
			m_importComplete = true;
			m_onImportComplete?.Invoke();
			Debug.Log("Config import completed!");
		}

		public ALayer GetLayerByIndex(int a_index)
		{
			if(a_index < m_dataConfig.datamodel.raster_layers.Length)
			{
				return m_dataConfig.datamodel.raster_layers[a_index];
			}
			else
			{
				return m_dataConfig.datamodel.vector_layers[a_index - m_dataConfig.datamodel.raster_layers.Length];
			}
		}

		public Vector3 ConfigToWorldSpaceXZ(float[] a_position)
		{
			return (new Vector3(a_position[0], 0f, a_position[1]) - m_configToWorldOffset) * m_configToWorldScale;
		}

		public Vector3 ConfigToWorldSpaceXY(float[] a_position)
		{
			return (new Vector3(a_position[0] - m_configToWorldOffset.x, a_position[1] - m_configToWorldOffset.z, -m_configToWorldOffset.y)) * m_configToWorldScale;
		}

		public Vector3 ConfigToWorldSpace(Vector3 a_position)
		{
			return (a_position - m_configToWorldOffset) * m_configToWorldScale;
		}

		public float ConfigToWorldSpaceX(float a_x)
		{
			return (a_x - m_configToWorldOffset.x) * m_configToWorldScale;
		}

		public float ConfigToWorldSpaceZ(float a_z)
		{
			return (a_z - m_configToWorldOffset.z) * m_configToWorldScale;
		}

		public Vector3 WorldToConfigSpace(Vector3 a_position)
		{
			return a_position / m_configToWorldScale + m_configToWorldOffset;
		}

		public static void DrawDebugBounding(float a_xMin, float a_xMax, float a_zMin, float a_zMax)
		{
			Debug.DrawLine(new Vector3(a_xMin, 0.01f, a_zMin), new Vector3(a_xMin, 0.01f, a_zMax), Color.red, 100f);
			Debug.DrawLine(new Vector3(a_xMin, 0.01f, a_zMax), new Vector3(a_xMax, 0.01f, a_zMax), Color.red, 100f);
			Debug.DrawLine(new Vector3(a_xMax, 0.01f, a_zMax), new Vector3(a_xMax, 0.01f, a_zMin), Color.red, 100f);
			Debug.DrawLine(new Vector3(a_xMax, 0.01f, a_zMin), new Vector3(a_xMin, 0.01f, a_zMin), Color.red, 100f);
		}

		public MeshRenderer CreateConfigSpaceQuad(float[] a_min, float[] a_max, Transform a_parent, string a_name)
		{
			Vector3 min = ConfigToWorldSpaceXZ(a_min);
			Vector3 max = ConfigToWorldSpaceXZ(a_max);

			//Create mesh and set properties
			Mesh procMesh = new Mesh();
			procMesh.vertices = new Vector3[]
			{
				min,
				new Vector3(min.x, 0, max.z),
				max,
				new Vector3(max.x, 0, min.z)
			}; ;
			procMesh.uv = new Vector2[]
			{
				new Vector2(0f, 0f),
				new Vector2(0f, 1f),
				new Vector2(1f, 1f),
				new Vector2(1f, 0f)
			}; ;
			procMesh.triangles = new int[]
			{
				0, 1, 2,
				2, 3, 0
			}; ;
			procMesh.RecalculateNormals();

			//Create gameobject and add mesh renderer
			GameObject displayMethodRoot = new GameObject(a_name);
			displayMethodRoot.transform.SetParent(a_parent, false);
			MeshFilter meshFilter = displayMethodRoot.AddComponent<MeshFilter>();
			meshFilter.mesh = procMesh;

			MeshRenderer renderer = displayMethodRoot.AddComponent<MeshRenderer>();
			return renderer;

		}
	}
}