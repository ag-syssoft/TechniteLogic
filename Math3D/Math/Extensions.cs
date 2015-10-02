using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Math3D
{
	public static class Convert
	{
		public static CultureInfo culture = new CultureInfo("en-US");

		public static float ToFloat(string str)
		{
			return System.Convert.ToSingle(str, culture);
		}
		public static uint ToUInt32(string str)
		{
			return System.Convert.ToUInt32(str, culture);
		}
		public static int ToInt32(string str)
		{
			return System.Convert.ToInt32(str, culture);
		}
		public static string ToString(float v)
		{
			return v.ToString(culture);
		}
		public static string ToString(int v)
		{
			return v.ToString(culture);
		}
		public static string ToString(uint v)
		{
			return v.ToString(culture);
		}
	}



	public static partial class Extensions
	{
		public static bool SimilarTo(this float value, float otherValue)
		{
			return Math.Abs(value - otherValue) <= 1e-6f;
		}
		public static bool SimilarTo(this float value, float otherValue, float maxError)
		{
			return Math.Abs(value - otherValue) <= maxError;
		}


	}
}
