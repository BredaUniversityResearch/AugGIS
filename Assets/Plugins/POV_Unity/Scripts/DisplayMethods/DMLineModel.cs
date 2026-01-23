using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace POV_Unity
{
	public class DMLineModel : AVectorDisplayMethod
	{
		public string model;
		public float size = 1;
		public string material = "Unlit";

		public string type_model;
		public string type_size;
		public string type_material;

		public string meta_model;
		public string meta_size;
		public string meta_material;

		protected override IDisplayMethodRenderData DisplayVectorLayer(VectorLayer a_layer, GameObject a_displayMethodRoot)
		{
			//TODO
			return null;
		}
	}
}
