using System.Collections;
using UnityEngine;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace POV_Unity
{
	public class JsonConverterDisplayMethod : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return typeof(ADisplayMethod).IsAssignableFrom(objectType);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			JObject jo = JObject.Load(reader);

			string methodName = jo["method"].ToObject<string>();

			object target = null;

			if (ConfigLoadHelper.Instance.TryGetDisplayMethodType(methodName, out Type type))
			{
				target = Activator.CreateInstance(type);
			}
			else
			{
				Debug.LogError("Unknown display method encountered: " + methodName);
				return null;
			}

			serializer.Populate(jo.CreateReader(), target);
			return target;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}

		public override bool CanWrite
		{
			get { return false; }
		}
	}
}