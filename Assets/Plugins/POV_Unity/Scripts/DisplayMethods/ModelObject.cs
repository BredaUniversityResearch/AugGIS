using System.Collections;
using UnityEngine;

namespace POV_Unity
{
	public class ModelObject : MonoBehaviour
	{
		[HideInInspector] public VectorLayer m_layer;
		[HideInInspector] public VectorObject m_object;
		[HideInInspector] public ADisplayMethod m_displayMethod;

        public void Initialise(VectorLayer a_layer, VectorObject a_object, ADisplayMethod a_displayMethod)
		{
			m_layer = a_layer;
			m_object = a_object;
			m_displayMethod = a_displayMethod;

			transform.localScale = new Vector3(ImportedConfigRoot.Instance.ConfigToWorldScale, ImportedConfigRoot.Instance.ConfigToWorldScale, ImportedConfigRoot.Instance.ConfigToWorldScale);

			string model = a_displayMethod.GetVariable<string>("model", a_layer, a_object);
			string material = a_displayMethod.GetVariable<string>("material", a_layer, a_object);
			bool seabottom = a_displayMethod.GetVariable<bool>("seabottom", a_layer, a_object);
			float offset = a_displayMethod.GetVariable<float>("offset", a_layer, a_object);

			GameObject go = Instantiate(AssetManager.GetModel(model), transform);
			if(!string.IsNullOrEmpty(material))
			{
				Material mat = AssetManager.GetMaterial(material);
				foreach (MeshRenderer renderer in go.GetComponents<MeshRenderer>())
				{
					renderer.material = mat;
					renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
					renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
				}
			}

			if (seabottom)
			{
				//Find height of seabottom at this point, lower to that + offset
				float seaBottomDepth = -ImportedConfigRoot.Instance.m_bathymetry.GetHeightAtWorldPosition(transform.position) * ImportedConfigRoot.Instance.ConfigToWorldScale;
				go.transform.localPosition = new Vector3(0f, offset * ImportedConfigRoot.Instance.ConfigToWorldScale - seaBottomDepth, 0f);
			}
			else
			{
				go.transform.localPosition = new Vector3(0f, offset * ImportedConfigRoot.Instance.ConfigToWorldScale, 0f);
				
			}
			foreach (IPostSpawnCallback stretchObj in GetComponentsInChildren<IPostSpawnCallback>())
			{
				stretchObj.Initialise();
			}
		}
	}
}