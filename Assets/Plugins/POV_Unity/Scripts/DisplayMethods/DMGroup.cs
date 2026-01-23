using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace POV_Unity
{
	public class DMGroup : ADisplayMethod
	{
		[JsonProperty(ItemConverterType = typeof(JsonConverterDisplayMethod))]
		public ADisplayMethod[] display_methods;

		[JsonProperty(ItemConverterType = typeof(JsonConverterDisplayMethod))]
		public ADisplayMethod default_method;

		public override ADisplayMethod TryDisplayLayer(ALayer a_layer)
		{
			if (!MatchesFilters(a_layer))
			{
				return null;
			}

			ADisplayMethod result = null;
			foreach (ADisplayMethod method in display_methods)
			{
				result = method.TryDisplayLayer(a_layer);
				if (result != null)
					return result;
			}

			return default_method.TryDisplayLayer(a_layer);
		}
	}
}
