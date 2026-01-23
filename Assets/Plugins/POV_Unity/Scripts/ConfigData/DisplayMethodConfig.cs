using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace POV_Unity
{
	public class DisplayMethodConfig
	{
		[JsonProperty(ItemConverterType = typeof(JsonConverterDisplayMethod))]
		public ADisplayMethod[] display_methods;
		public LayerCategory[] categories;
		
		//layers that are turned on by default
		public string[] starting_layer_tags;

		//layers that are static( they can not be moved or toggled on or off). They will also be on by default
		public string[] static_layer_tags;

		public bool IsStartingLayer(ALayer a_layer)
		{
			if (starting_layer_tags != null && starting_layer_tags.Length > 0 && starting_layer_tags.Any(x => a_layer.tags.Contains(x, StringComparer.InvariantCultureIgnoreCase)))
				return true;
			return false;
		}

		public bool IsStaticLayer(ALayer a_layer)
		{
			if (static_layer_tags != null && static_layer_tags.Length > 0 && static_layer_tags.Any(x => a_layer.tags.Contains(x, StringComparer.InvariantCultureIgnoreCase)))
				return true;
			return false;
		}
	}
}
