using System;

namespace ImmutableData
{
	[Serializable]
	public class ScriptRef
	{
		/// <summary>
		/// Gets the asset path
		/// </summary>
		public string Path { get; internal set; }
	}
}