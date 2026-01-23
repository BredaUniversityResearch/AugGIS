using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace POV_Unity
{
	public class LayerCategory : IEquatable<LayerCategory> 
	{
		public string[] tag_includes;
		public bool require_all_tags = true;
		public string[] tag_excludes;
		public string[] name_includes;
		public string[] name_excludes;
		public string icon;
		public string name;

		[JsonConverter(typeof(JsonConverterHexColor))]
		public Color colour;

		List<ALayer> m_layers = new List<ALayer>();
		public List<ALayer> Layers => m_layers;

		public bool Equals(LayerCategory other)
		{
			return name.Equals(other.name);
		}

		public bool MatchesFilters(ALayer a_layer)
		{
			if (require_all_tags)
			{
				if (tag_includes != null && tag_includes.Length > 0 && tag_includes.Any(x => !a_layer.tags.Contains(x, StringComparer.InvariantCultureIgnoreCase)))
					return false;
			}
			else if (tag_includes != null && tag_includes.Length > 0 && !tag_includes.Any(x => a_layer.tags.Contains(x, StringComparer.InvariantCultureIgnoreCase)))
				return false;
			if (tag_excludes != null && tag_excludes.Length > 0 && tag_excludes.Any(x => a_layer.tags.Contains(x, StringComparer.InvariantCultureIgnoreCase)))
				return false;
			if (name_includes != null && name_includes.Length > 0 && name_includes.Any(x => !a_layer.@short.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
				return false;
			if (name_excludes != null && name_excludes.Length > 0 && name_excludes.Any(x => a_layer.@short.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
				return false;

			m_layers.Add(a_layer);
			return true;
		}
	}
}