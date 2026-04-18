using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace POV_Unity
{
	public class DMPointModel : AVectorDisplayMethod
	{
		public string model;
		public bool seabottom = false;
		public float offset = 0f;
		public string material = null;//"Unlit";
		public float scale;

        public string type_model;
		public string type_seabottom;
		public string type_offset;
		public string type_material;

		public string meta_model;
		public string meta_seabottom;
		public string meta_offset;
		public string meta_material;

		protected override IDisplayMethodRenderData DisplayVectorLayer(VectorLayer a_layer, GameObject a_displayMethodRoot)
		{
			int objectsPlaced = 0;
            foreach (VectorObject obj in a_layer.data)
			{
                GameObject go = new GameObject("PointObject_" + objectsPlaced.ToString());
                go.transform.SetParent(a_displayMethodRoot.transform, false);
				go.transform.localPosition = obj.FirstPosition;
                go.AddComponent<ModelObject>().Initialise(a_layer, obj, this);
                float modelScale = GetVariable<float>("scale", a_layer, obj);
				go.transform.localScale = Vector3.one * modelScale;
                objectsPlaced++;
			}

			return null;
		}
	}
}
