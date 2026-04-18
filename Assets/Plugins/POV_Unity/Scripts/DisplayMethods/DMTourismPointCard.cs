using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace POV_Unity
{
	public class DMTourismPointCard : AVectorDisplayMethod
	{
        public string title;
        public string description;
        public string category;
        public string type;
        public string rating;
        public string website;

        public string meta_title;
        public string meta_description;
        public string meta_category;
        public string meta_type;
        public string meta_rating;
        public string meta_website;

        protected override IDisplayMethodRenderData DisplayVectorLayer(VectorLayer a_layer, GameObject a_displayMethodRoot)
		{

            int objectsPlaced = 0;
            foreach (VectorObject obj in a_layer.data)
            {
                string title = GetVariable<string>("title", a_layer, obj);
                string description = GetVariable<string>("description", a_layer, obj);
                string category = GetVariable<string>("category", a_layer, obj);
                string type = GetVariable<string>("type", a_layer, obj);
                string rating = GetVariable<string>("rating", a_layer, obj);
                string website = GetVariable<string>("website", a_layer, obj);

                GameObject go = new GameObject("TourismPointCard_" + objectsPlaced.ToString());
                go.transform.SetParent(a_displayMethodRoot.transform, false);
				go.transform.localPosition = obj.FirstPosition;
                GameObject go1 = new GameObject("description:" + description);
                GameObject go2 = new GameObject("category:" + category);
                GameObject go3 = new GameObject("rating:" + rating);
                GameObject go4 = new GameObject("type:" + type);
                GameObject go5 = new GameObject("website:" + website);
                go1.transform.SetParent(go.transform, false);
                go2.transform.SetParent(go.transform, false);
                go3.transform.SetParent(go.transform, false);
                go4.transform.SetParent(go.transform, false);
                go5.transform.SetParent(go.transform, false);
                //go.AddComponent<CardObject>().Initialise(a_layer, obj, this);
                objectsPlaced++;

            }

            return null;
		}
	}
}
