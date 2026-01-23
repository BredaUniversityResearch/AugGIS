using Newtonsoft.Json.Linq;
using System.Linq;
using System;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace POV_Unity
{
	public interface IDisplayMethodRenderData
	{

	}
	
	public abstract class ADisplayMethod
	{
		public string name;
		public string method;
		public string[] tag_includes;
		public string[] tag_excludes;
		public string[] name_includes;
		public string[] name_excludes;
		public LayerVisualizationMode visualization_mode;

		protected Dictionary<ALayer, GameObject> m_displayedLayers;

		public T GetVariable<T>(string a_name, VectorLayer a_layer, VectorObject a_geometry)
		{
			System.Reflection.FieldInfo field = this.GetType().GetField("meta_" + a_name);
			string refName;
			if (field != null)
			{
				refName = (string)field.GetValue(this);
				if (!string.IsNullOrEmpty(refName) && (a_geometry.metadata != null))
				{
					if (a_geometry.metadata.TryGetValue(refName, StringComparison.InvariantCultureIgnoreCase, out var tempResult))
						return tempResult.ToObject<T>();
				}
			}

			field = this.GetType().GetField("type_" + a_name);
			if (field != null)
			{
				refName = (string)field.GetValue(this);
				if (!string.IsNullOrEmpty(refName))
				{
					if (a_layer.types[a_geometry.types[0]].TryGetValue(refName, StringComparison.InvariantCultureIgnoreCase, out var tempResult))
						return tempResult.ToObject<T>();
				}
			}
			field = this.GetType().GetField(a_name);
			if (field != null)
				return (T)field.GetValue(this);
			return default(T);
		}

		public T GetVariable<T>(string a_name, RasterLayer a_layer)
		{
			return (T)this.GetType().GetField(a_name).GetValue(this);
		}

		public Color GetColour(string a_name, VectorLayer a_layer, VectorObject a_geometry)
		{
			JToken tempResult = null;
			string metaRef = (string)this.GetType().GetField("meta_" + a_name).GetValue(this);
			if (!string.IsNullOrEmpty(metaRef))
			{
				a_geometry.metadata.TryGetValue(metaRef, StringComparison.InvariantCultureIgnoreCase, out tempResult);
			}
			string typeRef = (string)this.GetType().GetField("type_" + a_name).GetValue(this);
			if (tempResult == null && !string.IsNullOrEmpty(typeRef))
			{
				a_layer.types[a_geometry.types[0]].TryGetValue(typeRef, StringComparison.InvariantCultureIgnoreCase, out tempResult);
			}

			if (tempResult != null)
			{
				JsonSerializer serializer = new JsonSerializer();
				serializer.Converters.Add(new JsonConverterHexColor());
				return tempResult.ToObject<Color>(serializer);
			}
			return (Color)this.GetType().GetField(a_name).GetValue(this);
		}

		public bool MatchesFilters(ALayer a_layer)
		{
			if (tag_includes != null && tag_includes.Length > 0 && tag_includes.Any(x => !a_layer.tags.Contains(x, StringComparer.InvariantCultureIgnoreCase)))
				return false;
			if (tag_excludes != null && tag_excludes.Length > 0 && tag_excludes.Any(x => a_layer.tags.Contains(x, StringComparer.InvariantCultureIgnoreCase)))
				return false;
			if (name_includes != null && name_includes.Length > 0 && name_includes.Any(x => !a_layer.@short.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
				return false;
			if (name_excludes != null && name_excludes.Length > 0 && name_excludes.Any(x => a_layer.@short.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
				return false;
			return true;
		}

		public abstract ADisplayMethod TryDisplayLayer(ALayer a_layer);

		public virtual void OnWorldScaleChanged(float a_worldScale, GameObject a_displayMethodRoot)
		{ }

		public virtual void Render(IDisplayMethodRenderData data)
		{

		}
	}
}
