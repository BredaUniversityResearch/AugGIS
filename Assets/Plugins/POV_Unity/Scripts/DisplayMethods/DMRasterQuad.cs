using POV_Unity;
using UnityEngine;

public class DMRasterQuad : ARasterDisplayMethod
{
  protected override IDisplayMethodRenderData DisplayRasterLayer(RasterLayer a_layer, GameObject a_displayMethodRoot)
    {
		Material mat = new Material(AssetManager.GetRasterQuadMaterial());
		mat.SetTexture("_BaseMap", a_layer.Raster);
		MeshRenderer renderer = ImportedConfigRoot.Instance.CreateConfigSpaceQuad(a_layer.coordinate0, a_layer.coordinate1, a_displayMethodRoot.transform, "Raster quad");
				renderer.material = mat;

        return null;
    }
}
