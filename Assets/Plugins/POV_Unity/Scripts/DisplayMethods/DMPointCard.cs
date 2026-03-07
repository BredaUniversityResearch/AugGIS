using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace POV_Unity
{
	public class DMPointCard : AVectorDisplayMethod
	{
		public string model;
		public float offset = 0f;
		public string material = null;//"Unlit";
		public float scale;

        public string type_model;
		public string type_offset;
		public string type_material;

		public string meta_model;
		public string meta_offset;
        public string meta_material;
        public string meta_name;
        public string meta_description;

        protected override IDisplayMethodRenderData DisplayVectorLayer(VectorLayer a_layer, GameObject a_displayMethodRoot)
		{
			int objectsPlaced = 0;
            foreach (VectorObject obj in a_layer.data)
			{
                GameObject go = new GameObject("PointCard_" + objectsPlaced.ToString());
				go.transform.SetParent(a_displayMethodRoot.transform, false);
				go.transform.localPosition = obj.FirstPosition;
                //Replace this this CardObject, for the TemplateObject
                //go.AddComponent<ModelObject>().Initialise(a_layer, obj, this);
				objectsPlaced++;
            }

            return null;
		}
	}
}
