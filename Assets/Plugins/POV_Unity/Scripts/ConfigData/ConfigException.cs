using System;
using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace POV_Unity
{
    public class ConfigException : Exception
    {
        public ConfigException()
        {
        }

        public ConfigException(string message) : base(message)
        {
        }

        public ConfigException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}