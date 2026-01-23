using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using Unity.EditorCoroutines.Editor;
#endif

namespace POV_Unity
{
	public enum LayerVisualizationMode { Off, Basic, Fancy, Hybrid }

	public abstract class ALayer
	{
		//Config fields
		public string name;
		public string @short;
		public string[] tags;
		public JObject[] types;

		//Other fields
		protected GameObject m_layerRoot;
		protected List<ADisplayMethod> m_displayMethods;
		protected List<IDisplayMethodRenderData> m_displayMethodRenderData;
		protected List<GameObject> m_displayMethodRoots;
		protected LayerCategory m_category;
		protected int m_layerIndex;

		public GameObject LayerRoot => m_layerRoot;
		public int LastDMIndex => m_displayMethods.Count - 1;
		public int LayerIndex => m_layerIndex;
		public LayerCategory Category => m_category;
		public List<ADisplayMethod> DisplayMethods => m_displayMethods;
		public List<IDisplayMethodRenderData> DisplayMethodRenderData => m_displayMethodRenderData;
		public float VerticalOffset => m_layerIndex * ImportedConfigRoot.LAYER_HEIGHT_OFFSET;

		public bool Validate()
		{
			bool valid = true;
			if (tags == null || tags.Length == 0) {
				Debug.LogWarning("** CONFIG WARNING ** 'tags'-field is not set for layer: " + name +
					". Please check the layer configuration. Creating a layer without tags is not allowed.");
				valid = false;
			}
			if (string.IsNullOrEmpty(@short))
			{
				Debug.LogWarning("** CONFIG WARNING ** 'short'-field is not set for later: " + name +
					". Please check the layer configuration. Creating a layer without a short name is not allowed.");
				valid = false;
			}
			return valid;
		}

		public virtual void Initialise(Transform a_layerRootParent, Action a_completeCallback, int a_layerIndex)
		{
			m_layerIndex = a_layerIndex;
			m_layerRoot = new GameObject(@short);
			m_layerRoot.transform.SetParent(a_layerRootParent, false);
			m_layerRoot.transform.localPosition = new Vector3(0f, VerticalOffset, 0f);

			m_displayMethods = new List<ADisplayMethod>();
			m_displayMethodRoots = new List<GameObject>();
			m_displayMethodRenderData = new List<IDisplayMethodRenderData>();

			foreach (LayerCategory category in ImportedConfigRoot.Instance.m_displayMethodConfig.categories)
			{
				if (category.MatchesFilters(this))
				{
					m_category = category;
					break;
				}
			}

#if UNITY_EDITOR
				if (Application.isPlaying)
				AssetManager.Instance.StartCoroutine(LoadData(a_completeCallback));
			else
				EditorCoroutineUtility.StartCoroutine(LoadData(a_completeCallback), this);
#else
			AssetManager.Instance.StartCoroutine(LoadData(a_completeCallback));
#endif
		}

		public GameObject GetDisplayMethodRoot(int a_index)
		{
			return m_displayMethodRoots[a_index];
		}

		public ADisplayMethod GetDisplayMethod(int a_index)
		{
			return m_displayMethods[a_index];
		}

		public void OnDispayMethodAdded(ADisplayMethod a_displayMethod, GameObject a_displayMethodRoot, IDisplayMethodRenderData displayMethodRenderData)
		{
			m_displayMethods.Add(a_displayMethod);
			m_displayMethodRoots.Add(a_displayMethodRoot);
			m_displayMethodRenderData.Add(displayMethodRenderData);
		}

		protected abstract IEnumerator LoadData(Action a_completeCallback);

		public void SetVisualizationMode(LayerVisualizationMode a_mode)
		{
			for (int i = 0; i < m_displayMethods.Count; i++)
			{
				LayerVisualizationMode displayMethodVisualisationMode = m_displayMethods[i].visualization_mode;

				if (displayMethodVisualisationMode == LayerVisualizationMode.Hybrid && a_mode != LayerVisualizationMode.Off)
				{
					m_displayMethodRoots[i].SetActive(true);
				}
				else
				{
					m_displayMethodRoots[i].SetActive(displayMethodVisualisationMode == a_mode);
				}
			}
		}

		public bool HasTag(string a_tag)
		{
			return tags != null && tags.Any(x => string.Equals(x, a_tag, StringComparison.InvariantCultureIgnoreCase));
		}

		public virtual void UpdateLayerMaterialHeight(int a_verticalStep)
		{ }

		public virtual void OnWorldScaleChanged(float a_worldScale)
		{
			for (int i = 0; i < m_displayMethods.Count; i++)
			{
				m_displayMethods[i].OnWorldScaleChanged(a_worldScale, m_displayMethodRoots[i]);
			}
		}

		public abstract bool IsPointInsideLayer(Vector2 a_point, float a_maxDistance, out string outTypeData);
	}
}
