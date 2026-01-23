using System;
using System.Collections.Generic;
using UnityEngine;

namespace POV_Unity
{
	public abstract class ARasterDisplayMethod : ADisplayMethod
	{
		public override ADisplayMethod TryDisplayLayer(ALayer a_layer)
		{
			if (!MatchesFilters(a_layer))
			{
				return null;
			}

			RasterLayer rlayer = a_layer as RasterLayer;
			if (rlayer == null)
				return null;

			GameObject displayMethodRoot = new GameObject(method);
			displayMethodRoot.transform.SetParent(a_layer.LayerRoot.transform, false);
			displayMethodRoot.transform.localPosition = new Vector3(0f, ImportedConfigRoot.DM_HEIGHT_OFFSET * displayMethodRoot.transform.GetSiblingIndex(), 0f);
			IDisplayMethodRenderData renderData = DisplayRasterLayer(rlayer, displayMethodRoot);
			a_layer.OnDispayMethodAdded(this, displayMethodRoot, renderData);

			return this;
		}

		protected abstract IDisplayMethodRenderData DisplayRasterLayer(RasterLayer a_layer, GameObject a_displayMethodRoot);
	}
}
