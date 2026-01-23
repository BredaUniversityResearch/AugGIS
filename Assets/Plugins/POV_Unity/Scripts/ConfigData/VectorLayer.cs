using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace POV_Unity
{
	public class VectorLayer : ALayer
	{
		public VectorObject[] data;

		HashSet<Material> m_layerMaterials;

		const int LAYER_RENDER_ORDER_STEP_MULT = 10;

		public static int s_HighestBaseRenderQueueValue = 0;

		private int m_currentRenderOrderValue = 0;
		public int CurrentRenderOrderValue => m_currentRenderOrderValue;

		private int m_startRenderQueueValue = 0;

		protected override IEnumerator LoadData(Action a_completeCallback)
		{
			m_layerMaterials = new HashSet<Material>();
			yield return null;

			if (HasTag("Polygon"))
			{
				foreach (VectorObject vo in data)
				{
					vo.ProcessDataPolygon();
				}
			} 
			else if(HasTag("Line"))
			{
				foreach (VectorObject vo in data)
				{
					vo.ProcessDataLine();
				}
			}
			else
			{
				foreach (VectorObject vo in data)
				{
					vo.ProcessDataPoint();
				}
			}

			a_completeCallback();
			yield break;
		}

		public void RegisterLayerMaterial(Material a_material)
		{
			m_layerMaterials.Add(a_material);
			m_startRenderQueueValue = a_material.renderQueue;

			int baseRenderQueue = LayerIndex + (int)a_material.renderQueue;
			if (baseRenderQueue > s_HighestBaseRenderQueueValue)
			{
				s_HighestBaseRenderQueueValue = baseRenderQueue;
			}
		}

		public override void UpdateLayerMaterialHeight(int a_verticalStep)
		{
			if (m_layerMaterials == null)
				return;

			foreach (Material mat in m_layerMaterials)
			{
				int baseRenderQueue = LayerIndex;
				baseRenderQueue += a_verticalStep == 0 ? m_startRenderQueueValue : VectorLayer.s_HighestBaseRenderQueueValue;
				m_currentRenderOrderValue = baseRenderQueue + a_verticalStep * LAYER_RENDER_ORDER_STEP_MULT;
				mat.renderQueue = m_currentRenderOrderValue;
			}
		}

		public override bool IsPointInsideLayer(Vector2 a_point, float a_maxDistance, out string outTypeData)
		{			
			if (HasTag("Polygon"))
			{
				foreach (VectorObject vo in data)
				{
					foreach (VectorShape shape in vo.Shapes)
					{
						if (Util.PointCollidesWithPolygon(a_point, shape.m_points, shape.m_holes, a_maxDistance))
						{
							outTypeData = GetTypeStringValue(vo);
							return true;
						}
					}
				}
			}
			else if (HasTag("Line"))
			{
				foreach (VectorObject vo in data)
				{
					foreach (VectorShape shape in vo.Shapes)
					{
						if (Util.PointCollidesWithLineString(a_point, shape.m_points, a_maxDistance))
						{
							outTypeData = GetTypeStringValue(vo);
							return true;
						}
					}
				}
			}
			else
			{
				foreach (VectorObject vo in data)
				{
					foreach (VectorShape shape in vo.Shapes)
					{
						Vector2 pointXZ = new Vector2(shape.m_points[0].x, shape.m_points[0].z);
						if (Util.PointCollidesWithPoint(a_point, pointXZ, a_maxDistance))
						{
							outTypeData = GetTypeStringValue(vo);
							return true;
						}
					}
				}
			}

			outTypeData = string.Empty;
			return false;
		}

		private string GetTypeStringValue(VectorObject a_vectorObject)
		{
			string result;
			if (types.Length > 0 && a_vectorObject.types.Length > 0)
			{
				types[a_vectorObject.types[0]].TryGetValue("name", out var token);
				result = token.ToString();
			}
			else
			{
				Debug.LogWarning("No Type Data Found for Vector Object"); 
				result = "No Type Data";
			}

			return result;
		}
	}
}
