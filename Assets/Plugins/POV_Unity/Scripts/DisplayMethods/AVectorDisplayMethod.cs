using System;
using System.Collections.Generic;
using UnityEngine;

namespace POV_Unity
{
	public abstract class AVectorDisplayMethod : ADisplayMethod
	{
		public override ADisplayMethod TryDisplayLayer(ALayer a_layer)
		{
			if (!MatchesFilters(a_layer))
			{
				return null;
			}

			VectorLayer vlayer = a_layer as VectorLayer;
			if (vlayer == null)
				return null;

			GameObject displayMethodRoot = new GameObject(method);
			displayMethodRoot.transform.SetParent(a_layer.LayerRoot.transform, false);
			displayMethodRoot.transform.localPosition = new Vector3(0f, ImportedConfigRoot.DM_HEIGHT_OFFSET * displayMethodRoot.transform.GetSiblingIndex(), 0f);

			IDisplayMethodRenderData data = DisplayVectorLayer(vlayer, displayMethodRoot);
			a_layer.OnDispayMethodAdded(this, displayMethodRoot, data);

			return this;
		}

		protected abstract IDisplayMethodRenderData DisplayVectorLayer(VectorLayer a_layer, GameObject a_displayMethodRoot);
	}
}
