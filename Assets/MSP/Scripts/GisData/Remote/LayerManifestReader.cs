using System;
using Newtonsoft.Json.Linq;
using POV_Unity;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Builds the runtime layer classes (RasterLayer/VectorLayer) from the JSON
	/// manifests served by GisHttpServer (see SqliteLayerPayloadProvider for the
	/// shape). The manifest-to-layer mapping lives here, per layer kind, so the
	/// transport code stays agnostic of what a layer contains.
	/// </summary>
	public static class LayerManifestReader
	{
		public static RasterLayer BuildRasterLayer(JObject a_manifest)
		{
			JToken data = a_manifest["data"];

			RasterLayer layer = new RasterLayer
			{
				coordinate0 = ToObjectOrNull<float[]>(data["coordinate0"]),
				coordinate1 = ToObjectOrNull<float[]>(data["coordinate1"]),
				data = (string)data["dataPath"],
				mapping = ToObjectOrNull<TypeMapping[]>(data["mapping"]),
				scale = BuildScale(data["scale"])
			};
			ApplyCommonFields(layer, a_manifest);
			return layer;
		}

		public static VectorLayer BuildVectorLayer(JObject a_manifest)
		{
			JArray objects = (JArray)a_manifest["data"]["objects"];
			VectorObject[] vectorObjects = new VectorObject[objects.Count];

			for (int i = 0; i < objects.Count; i++)
			{
				JToken objectToken = objects[i];
				vectorObjects[i] = new VectorObject
				{
					points = ToObjectOrNull<float[][]>(objectToken["points"]),
					gaps = ToObjectOrNull<float[][][]>(objectToken["gaps"]),
					types = ToObjectOrNull<int[]>(objectToken["types"]),
					metadata = objectToken["metadata"] as JObject
				};
			}

			VectorLayer layer = new VectorLayer { data = vectorObjects };
			ApplyCommonFields(layer, a_manifest);
			return layer;
		}

		private static void ApplyCommonFields(ALayer a_layer, JObject a_manifest)
		{
			a_layer.LayerId = (int)a_manifest["id"];
			a_layer.name = (string)a_manifest["name"];
			a_layer.@short = (string)a_manifest["short"];
			a_layer.tags = ToObjectOrNull<string[]>(a_manifest["tags"]);
			a_layer.types = ToObjectOrNull<JObject[]>(a_manifest["types"]);
		}

		private static ScaleConfig BuildScale(JToken a_scale)
		{
			if (a_scale == null || a_scale.Type == JTokenType.Null)
				return null;

			return new ScaleConfig
			{
				interpolation = (ScaleConfig.DensitymapInterpolation)Enum.Parse(typeof(ScaleConfig.DensitymapInterpolation), (string)a_scale["interpolation"]),
				min_value = (float)a_scale["minValue"],
				max_value = (float)a_scale["maxValue"],
				groups = ToObjectOrNull<ScaleConfigGrouping[]>(a_scale["groups"])
			};
		}

		private static T ToObjectOrNull<T>(JToken a_token) where T : class
		{
			if (a_token == null || a_token.Type == JTokenType.Null)
				return null;

			return a_token.ToObject<T>();
		}
	}
}
