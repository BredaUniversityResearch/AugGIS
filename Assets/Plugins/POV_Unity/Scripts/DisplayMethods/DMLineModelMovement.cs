using System;
using System.Collections.Generic;
using UnityEngine;

namespace POV_Unity
{
	public class DMLineModelMovement : AVectorDisplayMethod
	{
		public string model;
		public float size = 1f;
		public string material = "Unlit";
		public bool? direction = null;
		public float speed = 100f;
		public float spawn_interval_min = 10f;
		public float spawn_interval_max = 20f;
		public float lifetime = -1f;

		public string type_model;
		public string type_size;
		public string type_material;
		public string type_direction;

		public string meta_model;
		public string meta_size;
		public string meta_material;
		public string meta_direction;

		protected override IDisplayMethodRenderData DisplayVectorLayer(VectorLayer a_layer, GameObject a_displayMethodRoot)
		{
			int objectsPlaced = 0;
			foreach (VectorObject obj in a_layer.data)
			{
				GameObject go = new GameObject("LineMovementSpawner_" + objectsPlaced.ToString());
				go.transform.SetParent(a_displayMethodRoot.transform, false);
				go.AddComponent<LineMovementSpawner>().Initialise(a_layer, obj, this);
				objectsPlaced++;
			}

			return null;
		}
	}
}
