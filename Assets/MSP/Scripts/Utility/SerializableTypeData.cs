using System;

[Serializable]
public class SerializableTypeData
{
	public string name = string.Empty;
	public Type Type => Type.GetType(name);

	public bool IsValid => name != string.Empty && Type != null;
}
