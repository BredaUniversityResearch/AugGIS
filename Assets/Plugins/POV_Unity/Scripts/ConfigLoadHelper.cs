using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
//using UnityEditor.PackageManager.Requests;
using UnityEngine.Diagnostics;

namespace POV_Unity
{
    public class ConfigLoadHelper
    {
        static ConfigLoadHelper m_instance;
        public static ConfigLoadHelper Instance => m_instance;

        Dictionary<string, Type> m_displayMethodTypes;

        public ConfigLoadHelper()
        {
            m_instance = this;
            m_displayMethodTypes = new Dictionary<string, Type>()
            {
                { "Group", typeof(DMGroup) },
                { "Bathymetry", typeof(DMBathymetry) },
                { "Topography", typeof(DMTopography) },
                { "ValueMapBars", typeof(DMValueMapBars) },
                { "ValueHeightMap", typeof(DMValueHeightMap) },
                { "ValueMapCubes", typeof(DMValueMapCubes) },
                { "ValueMapSurfaceNorm", typeof(DMValueMapSurfaceNorm) },
                { "ValueMapQuad", typeof(DMValueMapQuad) },
                { "TypeMap", typeof(DMTypeMap) },
                { "AreaColour", typeof(DMAreaColour) },
                { "AreaPattern", typeof(DMAreaPattern) },
                { "AreaColourOpaque", typeof(DMAreaColourOpaque) },
                { "AreaModelScatter", typeof(DMAreaModelScatter) },
                { "PointModel", typeof(DMPointModel) },
                { "PointColour", typeof(DMPointColour) },
                { "LineColour", typeof(DMLineColour) },
                { "LineModel", typeof(DMLineModel) },
                { "LineModelMovement", typeof(DMLineModelMovement) },
                { "LineModelInterval", typeof(DMLineModelInterval) },
                { "RasterQuad", typeof(DMRasterQuad) }
            };
		}

        public void Clear()
        {
            m_instance = null;
		}

		public bool TryGetDisplayMethodType(string a_name, out Type a_type)
        {
            return m_displayMethodTypes.TryGetValue(a_name, out a_type);
        }

        public DataConfig ParseConfig(string a_jsonConfig)
        {
			MemoryTraceWriter traceWriter = new MemoryTraceWriter();
			traceWriter.LevelFilter = System.Diagnostics.TraceLevel.Warning;

			return JsonConvert.DeserializeObject<DataConfig>(a_jsonConfig, new JsonSerializerSettings
			{
                TraceWriter = traceWriter,
                Error = (sender, errorArgs) =>
                {
					Debug.LogError("Deserialization error: " + errorArgs.ErrorContext.Error);
				}
			});
        }

        public DisplayMethodConfig ParseDisplayMethodConfig(string a_displayMethodConfigJson)
        {
			return JsonConvert.DeserializeObject<DisplayMethodConfig>(a_displayMethodConfigJson);
		}
    }
}
