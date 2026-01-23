using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace POV_Unity
{
	public class DataConfig
	{
		public ConfigMetaData metadata;
		public DataConfigContent datamodel;
	}

	public class ConfigMetaData
	{
		public string date_modified;
		public string data_model_hash;
		public int errors;
		public string editor_version;
	}

	public class DataConfigContent
	{
		public float[] coordinate0;
		public float[] coordinate1;
		public string projection;
		public RasterLayer[] raster_layers;
		public VectorLayer[] vector_layers;
	}

	public class LabelInfoConfig
	{
		//TODO
	}
}
