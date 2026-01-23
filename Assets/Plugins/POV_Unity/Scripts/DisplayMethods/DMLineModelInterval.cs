using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static POV_Unity.DMLineColour;

namespace POV_Unity
{
	public class DMLineModelInterval : AVectorDisplayMethod
	{
		public string[] models;
		public int amount = 1;
		public float spacing;
		public LineDirection direction = LineDirection.None;
		public bool seabottom = false;
		public float offset = 0f;
		public string material = "Unlit";

		public string type_models;
		public string type_amount;
		public string type_spacing;
		public string type_direction;
		public string type_seabottom;
		public string type_offset;
		public string type_material;

		public string meta_models;
		public string meta_amount;
		public string meta_spacing;
		public string meta_direction;
		public string meta_seabottom;
		public string meta_offset;
		public string meta_material;

		protected override IDisplayMethodRenderData DisplayVectorLayer(VectorLayer a_layer, GameObject a_displayMethodRoot)
		{
			//TODO
			return null;
		}
	}
}
