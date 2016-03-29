using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace TechniteLogic
{
	public struct Uuid
	{
		public static string ByteArrayToString(byte[] ba)
		{
			StringBuilder hex = new StringBuilder(ba.Length * 2);
			foreach (byte b in ba)
				hex.AppendFormat("{0:x2}", b);
			return hex.ToString();
		}


		public UInt64 v0, v1;

		public Uuid(byte[] bytes)
		{
			v0 = 0;
			v1 = 0;
			Bytes = bytes;
			Debug.Assert(Enumerable.SequenceEqual(Bytes, bytes));
		}
		private Uuid(UInt64 v0, UInt64 v1)
		{
			this.v0 = v0;
			this.v1 = v1;
		}

		public static readonly Uuid Empty = new Uuid(0,0);

		public byte[] Bytes
		{
			get
			{
				byte[] rs = new byte[2 * 8];
				BitConverter.GetBytes(v0).CopyTo(rs, 0);
				BitConverter.GetBytes(v1).CopyTo(rs, 8);
				return rs;
			}
			set
			{
				v0 = BitConverter.ToUInt64(value, 0);
				v1 = BitConverter.ToUInt64(value, 8);
			}
		}

		public static bool operator==(Uuid a, Uuid b)
		{
			return a.v0 == b.v0 && a.v1 == b.v1;
		}
		public static bool operator !=(Uuid a, Uuid b)
		{
			return !(a == b);
		}

		public override bool Equals(object obj)
		{
			return obj is Uuid && (Uuid)obj == this;
		}

		public override int GetHashCode()
		{
			return v0.GetHashCode() * 17 + v1.GetHashCode();
		}

		public override string ToString()
		{
			return ByteArrayToString(Bytes);
		}
	}


}