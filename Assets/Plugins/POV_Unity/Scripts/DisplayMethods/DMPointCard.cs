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
        public string title;
        public string description;

		public string meta_title;
		public string meta_description;

        protected override IDisplayMethodRenderData DisplayVectorLayer(VectorLayer a_layer, GameObject a_displayMethodRoot)
		{

            int objectsPlaced = 0;
            foreach (VectorObject obj in a_layer.data)
            {
                GameObject go = new GameObject("PointCard_" + objectsPlaced.ToString());
				go.transform.SetParent(a_displayMethodRoot.transform, false);
				go.transform.localPosition = obj.FirstPosition;
                go.AddComponent<CardObject>().Initialise(a_layer, obj, this);
				objectsPlaced++;


            }

            return null;
		}
	}
}
