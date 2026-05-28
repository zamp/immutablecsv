using System.Runtime.CompilerServices;

namespace ImmutableData.Utilities
{
	public static class MathUtility
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Remap(this float value, float beforeRangeMin, float beforeRangeMax, float targetRangeMin, float targetRangeMax)
		{
			var denominator = beforeRangeMax - beforeRangeMin;
			if (denominator == 0) return targetRangeMin;
        
			var ratio = (value - beforeRangeMin) / denominator;
			var result = (targetRangeMax - targetRangeMin) * ratio + targetRangeMin;
			return result;
		}
	}
}