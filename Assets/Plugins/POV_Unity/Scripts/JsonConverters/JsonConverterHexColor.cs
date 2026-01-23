using System;
using Newtonsoft.Json;
using UnityEngine;

namespace POV_Unity
{
	public class JsonConverterHexColor : JsonConverter
	{
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			Color color = (Color)value;
			writer.WriteValue(ColorToHex(color));
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			Color result = HexToColor((string)reader.Value);
			return result;
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Color);
		}

		private Color HexToColor(string hex)
		{
			hex = hex.Replace("0x", "");//in case the string is formatted 0xFFFFFF
			hex = hex.Replace("#", "");//in case the string is formatted #FFFFFF
			byte a = 255;//assume fully visible unless specified in hex
			byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
			byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
			byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
			//Only use alpha if the string has enough characters
			if (hex.Length == 8)
			{
				a = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
			}
			return new Color32(r, g, b, a);
		}

		public string ColorToHex(Color color)
		{
			byte rByte = (byte)(color.r * 255);
			byte gByte = (byte)(color.g * 255);
			byte bByte = (byte)(color.b * 255);
			byte aByte = (byte)(color.a * 255);

			return rByte.ToString("X2") + gByte.ToString("X2") + bByte.ToString("X2") + aByte.ToString("X2");
		}
	}
}