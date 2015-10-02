using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Protocol
{
    /// <summary>
    /// Item class to use overloading to quickly serialize and deserialize complex structures and arrays
    /// </summary>
    abstract class SerialItem
    {
		public const int MaxRandomArrayLength = 1000;

		public static int RandomArrayLength(Random random)
		{
			return random.Next(100) < 30 ? 0 : random.Next(MaxRandomArrayLength);
		}

		protected SerialItem(Type type)
        {
            VariableType = type;
			HasFixedSize = HasConstantSize(type);
			FixedSize = HasFixedSize ? GetConstantSize(type) : -1;
        }


		private static void AssertLengthMatch(string variableName, Array afield, Array bfield, Type element)
		{
			if (afield.Length != bfield.Length)
				throw new Exception(variableName+": "+element+" array length mismatch (" + afield.Length + "!=" + bfield.Length + ")");
		}

		private static void AssertPrimitiveArrayMatch<T>(string variableName, T[] afield, T[] bfield) where T: struct, IEquatable<T>
		{
			AssertLengthMatch(variableName, afield, bfield, typeof(T));
			for (int i = 0; i < afield.Length; i++)
				if (!afield[i].Equals(bfield[i]))
					throw new Exception(variableName+"["+i+"/"+afield.Length+"]: "+typeof(T).Name+" " +afield[i]+" != "+bfield[i]);
		}

		private static void buildInto(ref List<System.Reflection.FieldInfo> info, Type t)
        {
            if (t.BaseType != null)
                buildInto(ref info, t.BaseType);
            info.AddRange(t.GetFields(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public));
        }
        public static System.Reflection.FieldInfo[] GetProperlyOrderedFields(Type t)
        {
            List<System.Reflection.FieldInfo> info = new List<System.Reflection.FieldInfo>();
            buildInto(ref info, t);
            return info.ToArray();
        }


        private static SerialItem CompileArray(Type t0, bool derived)
        {
            Type type = t0.GetElementType();
            if (typeof(byte) == type)
                return derived ? (SerialItem)new Byte.Array.Derived() : (SerialItem)new Byte.Array.Embedded();
            if (typeof(bool) == type)
                return derived ? (SerialItem)new Bool.Array.Derived() : (SerialItem)new Bool.Array.Embedded();
            if (typeof(char) == type)
                return derived ? (SerialItem)new Char.Array.Derived() : (SerialItem)new Char.Array.Embedded();
            if (typeof(Int16) == type)
                return derived ? (SerialItem)new TInt16.Array.Derived() : (SerialItem)new TInt16.Array.Embedded();
            if (typeof(UInt16) == type)
                return derived ? (SerialItem)new TUInt16.Array.Derived() : (SerialItem)new TUInt16.Array.Embedded();
            if (typeof(Int32) == type)
                return derived ? (SerialItem)new TInt32.Array.Derived() : (SerialItem)new TInt32.Array.Embedded();
            if (typeof(UInt32) == type)
                return derived ? (SerialItem)new TUInt32.Array.Derived() : (SerialItem)new TUInt32.Array.Embedded();
            if (typeof(Int64) == type)
                return derived ? (SerialItem)new TInt64.Array.Derived() : (SerialItem)new TInt64.Array.Embedded();
            if (typeof(UInt64) == type)
                return derived ? (SerialItem)new TUInt64.Array.Derived() : (SerialItem)new TUInt64.Array.Embedded();
			
            return derived ? (SerialItem)new Complex.Array.Derived(t0) : (SerialItem)new Complex.Array.Embedded(t0);
        }
        private static SerialItem CompileField(Type type, bool derived)
        {
            if (type.IsArray)
                return CompileArray(type, derived);
            if (type.IsEnum)
                return new Enum(type);
            if (typeof(byte) == type)
                return new Byte();
            if (typeof(bool) == type)
                return new Bool();
            if (typeof(char) == type)
                return new Char();
            if (typeof(Int16) == type)
                return new TInt16();
            if (typeof(UInt16) == type)
                return new TUInt16();
            if (typeof(Int32) == type)
                return new TInt32();
            if (typeof(UInt32) == type)
                return new TUInt32();
            if (typeof(Int64) == type)
                return new TInt64();
            if (typeof(UInt64) == type)
                return new TUInt64();
            if (typeof(float) == type)
                return new Float();
            if (typeof(double) == type)
                return new Double();
            if (typeof(string) == type)
                return derived ? (SerialItem)new String.Derived() : (SerialItem)new String.Embedded();

            if (type.IsPrimitive)
                throw new Exception("Unpextected type for compilation: " + type);
            return new Complex(type,derived);
        }

		/// <summary>
		/// Creates a new SerialItem for the specified type
		/// </summary>
		/// <param name="type">Type to create the serial-item for</param>
		/// <param name="derived">True, if contextual size information should be used to derive dynamic content size (specify false, to always read and write dynamic content length to/from the stream)</param>
		/// <param name="offsetFromEnd">Value to write to SerialItem.DerivedOffsetFromEnd</param>
		/// <returns>New serial item that sattisfies the specified type constraints</returns>
		public static SerialItem Compile(Type type, bool derived, int offsetFromEnd)
        {
            SerialItem result = CompileField(type, derived);
            result.DerivedOffsetFromEnd = offsetFromEnd;
            return result;
        }

        private static bool CompileDerived(System.Reflection.FieldInfo[] fields, SerialItem[] result)
        {
            int numDerived = 0;
            int derivedAt = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                Type t = fields[i].FieldType;
                if (BreaksDerivation(t))
                    return false;
                if (CanUtilizeDerivation(t))
                {
                    numDerived++;
                    derivedAt = i;
                }
            }
            if (numDerived != 1)
                return false;

            int derivationOffset = 0;
            for (int i = derivedAt + 1; i < fields.Length; i++)
                derivationOffset += GetConstantSize(fields[i].FieldType);
            for (int i = 0; i < fields.Length; i++)
            {
                Type t = fields[i].FieldType;
                if (derivedAt == i)
                    result[i] = Compile(t, true, derivationOffset);
                else
                    result[i] = Compile(t, false, 0);
            }

            return true;
        }

        /// <summary>
        /// Auto-compiles a whole set of SerialItem derivates depending on the configuration of fields
        /// For up to three fields, the method will attempt to find a derived composition. If no such could be identified,
        /// or if more than three fields are provided, embedded overloads are used to all types.
        /// </summary>
        /// <param name="fields">Field composition to find a serial item solution for</param>
        /// <returns></returns>
        public static SerialItem[] Compile(System.Reflection.FieldInfo[] fields)
        {
            SerialItem[] result = new SerialItem[fields.Length];
            if (fields.Length <= 3)
            {
                if (CompileDerived(fields, result))
                    return result;
            }
            for (int i = 0; i < result.Length; i++)
                result[i] = Compile(fields[i].FieldType, false, 0);
            return result;
        }

        /// <summary>
        /// Identifies whether the specified type completely prevents derivation
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static bool BreaksDerivation(Type t)
        {
            if (t.IsPrimitive)
                return false;
            if (t == typeof(string))
                return false;
			if (t.IsArray)
			{
				return !HasConstantSize(t.GetElementType());
                //return true;
			}

            System.Reflection.FieldInfo[] fields = t.GetFields(System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);


            int numDerived = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                Type subT = fields[i].FieldType;
                if (BreaksDerivation(subT))
                    return true;
                if (CanUtilizeDerivation(subT))
                {
                    numDerived++;
                }
            }
            if (fields.Length <= 3)
                return numDerived > 1;
            return numDerived > 0;
        }

        /// <summary>
        /// Identifies whether the specified type could at all use derivation
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static bool CanUtilizeDerivation(Type t)
        {
            if (t.IsPrimitive)
                return false;
            if (t == typeof(string))
                return true;
            if (t.IsArray)
                return HasConstantSize(t.GetElementType());

            System.Reflection.FieldInfo[] fields = t.GetFields(System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

            if (fields.Length > 3)
                return false;

            int numDerived = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                Type subT = fields[i].FieldType;
                if (BreaksDerivation(subT))
                    return false;
                if (CanUtilizeDerivation(subT))
                {
                    numDerived++;
                }
            }
            return numDerived == 1;
        }
        private static bool HasConstantSize(Type t)
        {
            if (t.IsArray)
                return false;
            if (t.IsPrimitive)
                return true;
            if (t == typeof(string))
                return false;
            System.Reflection.FieldInfo[] fields = t.GetFields(System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
			if (fields.Length == 0)
				throw new Exception("Struct with no fields detected: "+t.Name);
//			Debug.Assert(fields.Length > 0);
            foreach (var field in fields)
                if (!HasConstantSize(field.FieldType))
                    return false;
            return true;
        }
        /// <summary>
        /// If the specified type is a primitive, this method retrieves its size in bytes. Booleans are given 1 byte each
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static int GetConstantSize(Type type)
        {
            if (typeof(byte) == type)
                return 1;
            if (typeof(bool) == type)
                return 1;
            if (typeof(char) == type)
                return 1;
            if (typeof(Int16) == type)
                return 2;
            if (typeof(UInt16) == type)
                return 2;
            if (typeof(Int32) == type)
                return 4;
            if (typeof(UInt32) == type)
                return 4;
            if (typeof(float) == type)
                return 4;
            if (typeof(double) == type)
                return 8;
            if (typeof(Int64) == type)
                return 8;
            if (typeof(UInt64) == type)
                return 8;
            int size;
            if (type.IsEnum)
            {
                int[] values = (int[])type.GetEnumValues();
                size = 1;
                foreach (int v in values)
                    if (v > 127 || v < -128)
                        if (v > 32767 || v < -32768)
                            size = 4;
                        else
                            size = Math.Max(size, 2);
                return size;                        
            }
            Debug.Assert(HasConstantSize(type));
            System.Reflection.FieldInfo[] fields = type.GetFields(System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
			if (fields.Length == 0)
				throw new Exception("Struct with no fields detected: "+type.Name);
            size = 0;
            foreach (var field in fields)
                size += GetConstantSize(field.FieldType);

            //Debug.Fail("Unexpected type parameter for GetConstantSize()");
            return size;
        }

        /// <summary>
        /// Retrieves the underlying type of the local serial item
        /// </summary>
        public Type VariableType { get; protected set; }
        /// <summary>
        /// Non-0 only for derived serial items. Describes by how much the packet size must be decremeneted to derive the
        /// size of any dynamic content. Basically, for derived types this is the size of any following constant sized elements
        /// in the local packet.
        /// </summary>
        public int DerivedOffsetFromEnd { get; private set; }
		public bool HasFixedSize { get; private set; }
		public int FixedSize { get; private set; }

		/// <summary>
		/// Attempts to deserialize the content of the local variable from a byte array
		/// </summary>
		/// <param name="data">Binary data array that contains the entire package</param>
		/// <param name="offset">Reference to the current offset in that array. Must be incremented whenever data is read from the stream</param>
		/// <param name="data_size">Total remaining data size. This value should be decremented by DerivationOffsetFromEnd prior to calling this method</param>
		/// <returns>New deserialized object (may be primitive, class, or array of primitives/classes)</returns>
		public abstract Object Deserialize(byte[] data, ref int offset, int data_size);
        /// <summary>
        /// Serializes the content of the local variable to a byte buffer
        /// </summary>
        /// <param name="item">Object to serialize</param>
        /// <param name="stream">Byte stream to serialize into</param>
        public abstract void Serialize(Object item, ByteBuffer stream);

		/// <summary>
		/// Creates a new random object
		/// </summary>
		/// <param name="random">Random source</param>
		/// <returns></returns>
		public abstract object CreateRandom(Random random);
		/// <summary>
		/// Checks whether or not the two objects are bitwise equal. Throws an exception if some elements are not equal
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		public abstract void AssertBitwiseEqual(string variableName, object a, object b);

        protected void RequireBytes(int bytes, int remaining)
        {
            if (bytes > remaining)
                throw new Exception("Streaming exception: Unable to deserialize variable. Expected at least " + bytes + " additional bytes to deserialize variable of type " + VariableType + " but got " + remaining + " bytes left.");
        }
        protected static int ReadSize(byte[] data, ref int offset, int data_size)	//! Reads the content of a size variable in little endian 
        {
            int remaining = data_size - offset;
            if (remaining <= 0)
                throw new Exception("Streaming exception: Unable to deserialize size. Expected at least one additional byte to deserialize variable.");

            byte flags = (byte)(data[offset] & 0xC0);

            uint result;
            switch (flags)
            {
                case 0:
                    return data[offset++];
                case 0x40:
                    if (remaining <= 1)
                        throw new Exception("Streaming exception: Unable to deserialize size. Expected at least two additional bytes to deserialize variable.");
                    result = (((uint)(data[offset] & 0x3F)) << 8) | data[offset + 1];
                    offset += 2;
                    return (int)result;
                case 0x80:
                    if (remaining <= 2)
                        throw new Exception("Streaming exception: Unable to deserialize size. Expected at least three additional bytes to deserialize variable.");
                    result = (((uint)(data[offset] & 0x3F)) << 16) | (((uint)data[offset + 1]) << 8) | (((uint)data[offset + 2]));
                    offset += 3;
                    return (int)result;
                case 0xC0:
                    if (remaining <= 3)
                        throw new Exception("Streaming exception: Unable to deserialize size. Expected at least four additional bytes to deserialize variable.");
                    result = (((uint)(data[offset] & 0x3F)) << 24) | (((uint)data[offset + 1]) << 16) | (((uint)data[offset + 2]) << 8) | (((uint)data[offset + 3]));
                    offset += 4;
                    return (int)result;
                default:
                    throw new Exception("Logical Streaming exception");
            }
        }

		/// <summary>
		/// Complex variable that contains fields of its own
		/// </summary>
		public class Complex : SerialItem
		{
			SerialItem[] items;
			System.Reflection.FieldInfo[] fields;
			//System.Reflection.ConstructorInfo constructor;





			public Complex(Type t, bool derived)
				: base(t)
			{
				List<System.Reflection.FieldInfo> info = new List<System.Reflection.FieldInfo>();
				buildInto(ref info, t);
				fields = info.ToArray();
				items = new SerialItem[fields.Length];
				if (!derived || !CompileDerived(fields, items))
				{
					for (int i = 0; i < items.Length; i++)
						items[i] = Compile(fields[i].FieldType, false, 0);
				}
				//var test = t.GetConstructors();
				//constructor = t.GetConstructor(Type.EmptyTypes);
				//Debug.Assert(constructor != null);
			}
			public override Object Deserialize(byte[] data, ref int offset, int data_size)
			{
				Object result = Activator.CreateInstance(VariableType);
				//constructor.Invoke(null);
				for (int i = 0; i < items.Length; i++)
					fields[i].SetValue(result, items[i].Deserialize(data, ref offset, data_size - items[i].DerivedOffsetFromEnd));
				return result;
			}

			public override void Serialize(Object item, ByteBuffer stream)
			{
				for (int i = 0; i < items.Length; i++)
					items[i].Serialize(fields[i].GetValue(item), stream);
			}

			public override object CreateRandom(Random random)
			{
				Object result = Activator.CreateInstance(VariableType);
				for (int i = 0; i < items.Length; i++)
					fields[i].SetValue(result, items[i].CreateRandom(random));
				return result;
			}

			public override void AssertBitwiseEqual(string variableName, object a, object b)
			{
				for (int i = 0; i < items.Length; i++)
					items[i].AssertBitwiseEqual(variableName+"."+fields[i].Name, fields[i].GetValue(a), fields[i].GetValue(b));
			}

			public class Array
			{
				public abstract class Base : SerialItem
				{
					protected SerialItem element;

					protected Base(Type t) : base(t)
					{
						element = Compile(t.GetElementType(), false, 0);
					}
					public override object CreateRandom(Random random)
					{
						int length = RandomArrayLength(random);
						System.Array ar = System.Array.CreateInstance(element.VariableType, length);
						for (int i = 0; i < length; i++)
							ar.SetValue(element.CreateRandom(random), i);
						return ar;
					}

					public override void AssertBitwiseEqual(string variableName, object a, object b)
					{
						System.Array afield = (System.Array)a;
						System.Array bfield = (System.Array)b;
						AssertLengthMatch(variableName,afield, bfield, element.VariableType);
						for (int i = 0; i < afield.Length; i++)
							element.AssertBitwiseEqual(variableName+"["+i+"/"+afield.Length+"]", afield.GetValue(i), bfield.GetValue(i));
					}
				}
				public class Embedded : Base
				{

					public Embedded(Type t)
		                : base(t)
				    {}


					public override Object Deserialize(byte[] data, ref int offset, int data_size)
					{
						int length;
						length = ReadSize(data, ref offset, data_size);
						RequireBytes(length, data_size - offset);   //at least one byte per element.
						System.Array ar = System.Array.CreateInstance(element.VariableType, length);
						for (int i = 0; i < length; i++)
							ar.SetValue(element.Deserialize(data, ref offset, data_size), i);
						return ar;
					}

					public override void Serialize(Object item, ByteBuffer stream)
					{
						System.Array field = (System.Array)item;
						stream.WriteSize(field.Length);
						for (int i = 0; i < field.Length; i++)
							element.Serialize(field.GetValue(i), stream);
					}
				}

				public class Derived : Base
				{

					public Derived(Type t)
						: base(t)
					{ }


					public override Object Deserialize(byte[] data, ref int offset, int data_size)
					{
						int length;
						int available = data_size - offset;
						length = available / element.FixedSize;

						System.Array ar = System.Array.CreateInstance(element.VariableType, length);
						for (int i = 0; i < length; i++)
							ar.SetValue(element.Deserialize(data, ref offset, data_size), i);
						return ar;
					}

					public override void Serialize(Object item, ByteBuffer stream)
					{
						System.Array field = (System.Array)item;
						for (int i = 0; i < field.Length; i++)
							element.Serialize(field.GetValue(i), stream);
					}
				}

			}
		}


        /// <summary>
        /// Primitive base type for SerialItem's
        /// </summary>
        /// <typeparam name="T"></typeparam>
        abstract public class Primitive<T> : SerialItem
        {
            protected Primitive() : base(typeof(T)) { }

			public override void AssertBitwiseEqual(string variableName, object a, object b)
			{
				//T ta = (T)a;
				//T tb = (T)b;
				if (!a.Equals(b))
				{
					throw new Exception(variableName+": "+a + "!=" + b + " (" + typeof(T) + ")");
				}
			}
		}

        /// <summary>
        /// Byte-typed SerialItem
        /// </summary>
        public class Byte : Primitive<byte>
        {
            public class Array
            {
				public abstract class Base : SerialItem
				{
					protected Base(Type t) : base(t)
					{ }
					public override void AssertBitwiseEqual(string variableName, object a, object b)
					{
						byte[] afield = (byte[])a;
						byte[] bfield = (byte[])b;
						AssertPrimitiveArrayMatch(variableName, afield, bfield);
					}

					public override object CreateRandom(Random random)
					{
						int length = RandomArrayLength(random);
						byte[] result = new byte[length];
						random.NextBytes(result);
						return result;
					}
				}

                public class Embedded : Base
                {
                    public Embedded()
                        : base(typeof(byte[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = ReadSize(data, ref offset, data_size);
                        RequireBytes(length, data_size - offset);
                        byte[] result = new byte[length];
                        System.Array.Copy(data, offset, result, 0, length);
                        offset += length;
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        byte[] field = (byte[])item;
                        stream.WriteSize(field.Length);
                        stream.Append(field, 0, field.Length);
                    }
                }
                public class Derived : Base
                {
                    public Derived()
                        : base(typeof(byte[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = (data_size - offset);
                        byte[] result = new byte[length];
                        System.Array.Copy(data, offset, result, 0, length);
                        offset += length;
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        byte[] field = (byte[])item;
                        stream.Append(field, 0, field.Length);
                    }
                }
            }
            public override Object Deserialize(byte[] data, ref int offset, int data_size)
            {
                RequireBytes(1, data_size - offset);
                return data[offset++];
            }

            public override void Serialize(Object item, ByteBuffer stream)
            {
                stream.Append((byte)item);
            }

			public override object CreateRandom(Random random)
			{
				return (byte)random.Next(256);
			}
		}

        public class Enum : SerialItem
        {
            private int size;
            public Enum(Type t)
                : base(t)
            {
                int[] values = (int[])t.GetEnumValues();
                size = 1;
                foreach (int v in values)
                    if (v > 127 || v < -128)
                        if (v > 32767 || v < -32768)
                            size = 4;
                        else
                            size = Math.Max(size, 2);
            }
            public override Object Deserialize(byte[] data, ref int offset, int data_size)
            {
                object result;
                RequireBytes(size, data_size - offset);

                switch (size)
                {
                    case 4:
                        result = System.Enum.ToObject(VariableType,BitConverter.ToInt32(data, offset));
                        offset += 4;
                        break;
                    case 2:
                        result = System.Enum.ToObject(VariableType,BitConverter.ToInt16(data, offset));
                        offset += 2;
                        break;
                    case 1:
                        result = System.Enum.ToObject(VariableType, (sbyte)data[offset]);
                        offset += 1;
                        break;
                    default:
                        Debug.Fail("this shouldn't happen");
                        result = Activator.CreateInstance(VariableType);
                        break;
                }
                return result;
            }

            public override void Serialize(Object item, ByteBuffer stream)
            {
                switch (size)
                {
                    case 4:
                        stream.AppendU32((uint)(int)item);
                        break;
                    case 2:
                        stream.AppendU16((ushort)(short)(int)item);
                        break;
                    case 1:
                        stream.Append((byte)(sbyte)(int)item);
                        break;

                }
            }

			public override void AssertBitwiseEqual(string variableName, object a, object b)
			{
				//System.Enum ea = (System.Enum)a;
				//System.Enum eb = (System.Enum)b;
				if (!a.Equals(b))
				{
					throw new Exception(variableName+": "+a + "!=" + b + " (" + VariableType + ")");
				}
			}

			public override object CreateRandom(Random random)
			{
				int[] values = (int[])this.VariableType.GetEnumValues();
				return System.Enum.ToObject(VariableType, values[random.Next(values.Length)]);
            }
		}

        public class Bool : Primitive<bool>
        {
            public class Array
            {
				public abstract class Base : SerialItem
				{
					protected Base(Type t) : base(t)
					{ }
					public override void AssertBitwiseEqual(string variableName, object a, object b)
					{
						bool[] afield = (bool[])a;
						bool[] bfield = (bool[])b;
						AssertPrimitiveArrayMatch(variableName, afield, bfield);
					}

					public override object CreateRandom(Random random)
					{
						int length = RandomArrayLength(random);
						bool[] result = new bool[length];
						for (int i = 0; i < length; i++)
							result[i] = random.Next(100) >= 50;
						return result;
					}
				}

				public class Embedded : Base
                {
                    public Embedded()
                        : base(typeof(bool[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = ReadSize(data, ref offset, data_size);
                        RequireBytes(length, data_size - offset);
                        bool[] result = new bool[length];

                        for (int i = 0; i < length; i++)
                            result[i] = data[offset++] != 0;
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        bool[] field = (bool[])item;
                        stream.WriteSize(field.Length);
                        for (int i = 0; i < field.Length; i++)
                        {
                            stream.Append((byte)(field[i] ? 1 : 0));
                        }
                    }
                }
                public class Derived : Base
                {
                    public Derived()
                        : base(typeof(bool[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = (data_size - offset);
                        bool[] result = new bool[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = data[offset++] != 0;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        bool[] field = (bool[])item;
                        for (int i = 0; i < field.Length; i++)
                        {
                            stream.Append((byte)(field[i] ? 1 : 0));
                        }
                    }
                }
            }
            public override Object Deserialize(byte[] data, ref int offset, int data_size)
            {
                RequireBytes(1, data_size - offset);
                return data[offset++] != 0;
            }

            public override void Serialize(Object item, ByteBuffer stream)
            {
                stream.Append((byte)((bool)item ? 1 : 0));
            }
			public override object CreateRandom(Random random)
			{
				return random.Next(100) >= 50;
			}
		}

		public class Char : Primitive<char>
        {
			protected const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRTSTUVWXYZ0123456789!@#$%^&*()_+=-]}[{'\"\\|/.,<>?äöüÄÖÜß";

			public static char Rand(Random random)
			{
				return alphabet[random.Next(alphabet.Length)];
            }

			public class Array
            {
				public abstract class Base : SerialItem
				{

					protected Base(Type t) : base(t)
					{ }
					public override void AssertBitwiseEqual(string variableName, object a, object b)
					{
						char[] afield = (char[])a;
						char[] bfield = (char[])b;
						AssertPrimitiveArrayMatch(variableName, afield, bfield);
					}

					public override object CreateRandom(Random random)
					{
						int length = RandomArrayLength(random);
						char[] result = new char[length];
						for (int i = 0; i < length; i++)
							result[i] = Rand(random);
						return result;
					}
				}

				public class Embedded : Base
                {
                    public Embedded()
                        : base(typeof(char[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = ReadSize(data, ref offset, data_size);
                        RequireBytes(length, data_size - offset);
                        char[] result = new char[length];

                        for (int i = 0; i < length; i++)
                            result[i] = (char)data[offset++];
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        char[] field = (char[])item;
                        stream.WriteSize(field.Length);
                        for (int i = 0; i < field.Length; i++)
                        {
                            stream.Append((byte)field[i]);
                        }
                    }
                }
                public class Derived : Base
                {
                    public Derived()
                        : base(typeof(char[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = (data_size - offset);
                        char[] result = new char[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = (char)data[offset++];
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        char[] field = (char[])item;
                        for (int i = 0; i < field.Length; i++)
                        {
                            stream.Append((byte)field[i]);
                        }
                    }
                }
            }
            public override Object Deserialize(byte[] data, ref int offset, int data_size)
            {
                RequireBytes(1, data_size - offset);
                return (char)(data[offset++]);
            }

            public override void Serialize(Object item, ByteBuffer stream)
            {
                stream.Append((byte)((char)item));
            }
			public override object CreateRandom(Random random)
			{
				return Rand(random);
			}

		}


		public class TInt16 : Primitive<Int16>
        {
            public class Array
            {
				public abstract class Base : SerialItem
				{

					protected Base(Type t) : base(t)
					{ }
					public override void AssertBitwiseEqual(string variableName, object a, object b)
					{
						Int16[] afield = (Int16[])a;
						Int16[] bfield = (Int16[])b;
						AssertPrimitiveArrayMatch(variableName, afield, bfield);
					}

					public override object CreateRandom(Random random)
					{
						int length = RandomArrayLength(random);
						Int16[] result = new Int16[length];
						for (int i = 0; i < length; i++)
							result[i] = Rand(random);
						return result;
					}
				}

				public class Embedded : Base
                {
                    public Embedded()
                        : base(typeof(Int16[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = ReadSize(data, ref offset, data_size);
                        RequireBytes(length * 2, data_size - offset);
                        Int16[] result = new Int16[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToInt16(data, offset);
                            offset += 2;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        Int16[] field = (Int16[])item;
                        stream.WriteSize(field.Length);
                        for (int i = 0; i < field.Length; i++)
                        {
                            stream.AppendU16((UInt16)field[i]);
                        }
                    }
                }
                public class Derived : Base
                {
                    public Derived()
                        : base(typeof(Int16[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = (data_size - offset) / 2;
                        Int16[] result = new Int16[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToInt16(data, offset);
                            offset += 2;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        Int16[] field = (Int16[])item;
                        for (int i = 0; i < field.Length; i++)
                        {
                            stream.AppendU16((UInt16)field[i]);
                        }
                    }
                }
            }
            public override Object Deserialize(byte[] data, ref int offset, int data_size)
            {
                RequireBytes(2, data_size - offset);
                Int16 result = BitConverter.ToInt16(data, offset);
                offset += 2;
                return result;
            }

            public override void Serialize(Object item, ByteBuffer stream)
            {
                Int16 v = (Int16)item;
                stream.Append((byte)(v & 0xFF),
                                (byte)((v >> 8) & 0xFF));
            }

			protected static Int16 Rand(Random random)
			{
				return (Int16)random.Next(Int16.MinValue, (int)Int16.MaxValue + 1);
			}
			public override object CreateRandom(Random random)
			{
				return Rand(random);
			}

		}
		public class TUInt16 : Primitive<UInt16>
        {
            public class Array
            {
				public abstract class Base : SerialItem
				{

					protected Base(Type t) : base(t)
					{ }
					public override void AssertBitwiseEqual(string variableName, object a, object b)
					{
						UInt16[] afield = (UInt16[])a;
						UInt16[] bfield = (UInt16[])b;
						AssertPrimitiveArrayMatch(variableName, afield, bfield);
					}

					public override object CreateRandom(Random random)
					{
						int length = RandomArrayLength(random);
						UInt16[] result = new UInt16[length];
						for (int i = 0; i < length; i++)
							result[i] = Rand(random);
						return result;
					}
				}

				public class Embedded : Base
                {
                    public Embedded()
                        : base(typeof(UInt16[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = ReadSize(data, ref offset, data_size);
                        RequireBytes(length * 2, data_size - offset);
                        UInt16[] result = new UInt16[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToUInt16(data, offset);
                            offset += 2;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        UInt16[] field = (UInt16[])item;
                        stream.WriteSize(field.Length);
                        for (int i = 0; i < field.Length; i++)
                        {
                            stream.AppendU16(field[i]);
                        }
                    }
                }
                public class Derived : Base
                {
                    public Derived()
                        : base(typeof(UInt16[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = (data_size - offset) / 2;
                        UInt16[] result = new UInt16[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToUInt16(data, offset);
                            offset += 2;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        UInt16[] field = (UInt16[])item;
                        for (int i = 0; i < field.Length; i++)
                        {
                            stream.AppendU16(field[i]);
                        }
                    }
                }
            }
            public override Object Deserialize(byte[] data, ref int offset, int data_size)
            {
                RequireBytes(2, data_size - offset);
                UInt16 result = (UInt16)BitConverter.ToInt16(data, offset);
                offset += 2;
                return result;
            }

            public override void Serialize(Object item, ByteBuffer stream)
            {
                UInt16 v = (UInt16)item;
                stream.Append((byte)(v & 0xFF),
                                (byte)((v >> 8) & 0xFF));
            }
			protected static UInt16 Rand(Random random)
			{
				return (UInt16)random.Next((int)UInt16.MaxValue + 1);
			}
			public override object CreateRandom(Random random)
			{
				return Rand(random);
			}

		}
		public class TInt32 : Primitive<Int32>
        {
            public class Array
            {
				public abstract class Base : SerialItem
				{

					protected Base(Type t) : base(t)
					{ }
					public override void AssertBitwiseEqual(string variableName, object a, object b)
					{
						Int32[] afield = (Int32[])a;
						Int32[] bfield = (Int32[])b;
						AssertPrimitiveArrayMatch(variableName, afield, bfield);
					}

					public override object CreateRandom(Random random)
					{
						int length = RandomArrayLength(random);
						Int32[] result = new Int32[length];
						for (int i = 0; i < length; i++)
							result[i] = Rand(random);
						return result;
					}
				}


				public class Embedded : Base
                {
                    public Embedded()
                        : base(typeof(Int32[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = ReadSize(data, ref offset, data_size);
                        RequireBytes(length * 4, data_size - offset);
                        Int32[] result = new Int32[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToInt32(data, offset);
                            offset += 4;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        Int32[] field = (Int32[])item;
                        stream.WriteSize(field.Length);
                        for (int i = 0; i < field.Length; i++)
                        {
                            stream.AppendU32((UInt32)field[i]);
                        }
                    }
                }
                public class Derived : Base
                {
                    public Derived()
                        : base(typeof(Int32[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = (data_size - offset) / 4;
                        Int32[] result = new Int32[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToInt32(data, offset);
                            offset += 4;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        Int32[] field = (Int32[])item;
                        for (int i = 0; i < field.Length; i++)
                        {
                            stream.AppendU32((UInt32)field[i]);
                        }
                    }
                }
            }
            public override Object Deserialize(byte[] data, ref int offset, int data_size)
            {
                RequireBytes(4, data_size - offset);
                Int32 result = BitConverter.ToInt32(data, offset);
                offset += 4;
                return result;
            }

            public override void Serialize(Object item, ByteBuffer stream)
            {
                Int32 v = (Int32)item;
                stream.Append((byte)(v & 0xFF),
                                (byte)((v >> 8) & 0xFF),
                                (byte)((v >> 16) & 0xFF),
                                (byte)((v >> 24) & 0xFF));
            }
			protected static Int32 Rand(Random random)
			{
				return (Int32)random.Next(Int32.MinValue,Int32.MaxValue);
			}
			public override object CreateRandom(Random random)
			{
				return Rand(random);
			}

		}
		public class TUInt32 : Primitive<UInt32>
        {
            public class Array
            {
				public abstract class Base : SerialItem
				{

					protected Base(Type t) : base(t)
					{ }
					public override void AssertBitwiseEqual(string variableName, object a, object b)
					{
						UInt32[] afield = (UInt32[])a;
						UInt32[] bfield = (UInt32[])b;
						AssertPrimitiveArrayMatch(variableName, afield, bfield);
					}

					public override object CreateRandom(Random random)
					{
						int length = RandomArrayLength(random);
						UInt32[] result = new UInt32[length];
						for (int i = 0; i < length; i++)
							result[i] = Rand(random);
						return result;
					}
				}
				public class Embedded : Base
                {
                    public Embedded()
                        : base(typeof(UInt32[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = ReadSize(data, ref offset, data_size);
                        RequireBytes(length * 4, data_size - offset);
                        UInt32[] result = new UInt32[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToUInt32(data, offset);
                            offset += 4;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        UInt32[] field = (UInt32[])item;
                        stream.WriteSize(field.Length);
                        for (int i = 0; i < field.Length; i++)
                        {
                            stream.AppendU32(field[i]);
                        }
                    }
                }
                public class Derived : Base
				{
                    public Derived()
                        : base(typeof(UInt32[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = (data_size - offset) / 4;
                        UInt32[] result = new UInt32[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToUInt32(data, offset);
                            offset += 4;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        UInt32[] field = (UInt32[])item;
                        for (int i = 0; i < field.Length; i++)
                        {
                            stream.AppendU32(field[i]);
                        }
                    }
                }
            }
            public override Object Deserialize(byte[] data, ref int offset, int data_size)
            {
                RequireBytes(4, data_size - offset);
                UInt32 result = (UInt32)BitConverter.ToInt32(data, offset);
                offset += 4;
                return result;
            }

            public override void Serialize(Object item, ByteBuffer stream)
            {
                UInt32 v = (UInt32)item;
                stream.AppendU32(v);
                return;
            }
			protected static UInt32 Rand(Random random)
			{
				return (UInt32)(random.NextDouble()*(double)UInt32.MaxValue);
			}
			public override object CreateRandom(Random random)
			{
				return Rand(random);
			}

		}
		public class TInt64 : Primitive<Int64>
        {
            public class Array
            {
				public abstract class Base : SerialItem
				{
					protected Base(Type t) : base(t)
					{ }
					public override void AssertBitwiseEqual(string variableName, object a, object b)
					{
						Int64[] afield = (Int64[])a;
						Int64[] bfield = (Int64[])b;
						AssertPrimitiveArrayMatch(variableName, afield, bfield);
					}

					public override object CreateRandom(Random random)
					{
						int length = RandomArrayLength(random);
						Int64[] result = new Int64[length];
						for (int i = 0; i < length; i++)
							result[i] = Rand(random);
						return result;
					}
				}

				public class Embedded : Base
                {
                    public Embedded()
                        : base(typeof(Int64[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = ReadSize(data, ref offset, data_size);
                        RequireBytes(length * 8, data_size - offset);
                        Int64[] result = new Int64[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToInt64(data, offset);
                            offset += 8;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        Int64[] field = (Int64[])item;
                        stream.WriteSize(field.Length);
                        for (int i = 0; i < field.Length; i++)
                        {
                            UInt64 v = (UInt64)field[i];
                            stream.Append((byte)(v & 0xFF),
                                        (byte)((v >> 8) & 0xFF),
                                        (byte)((v >> 16) & 0xFF),
                                        (byte)((v >> 24) & 0xFF),
                                        (byte)((v >> 32) & 0xFF),
                                        (byte)((v >> 40) & 0xFF),
                                        (byte)((v >> 48) & 0xFF),
                                        (byte)((v >> 56) & 0xFF)
                                        );
                        }
                    }
                }
                public class Derived : Base
				{
                    public Derived()
                        : base(typeof(Int64[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = (data_size - offset) / 8;
                        Int64[] result = new Int64[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToInt64(data, offset);
                            offset += 8;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        Int64[] field = (Int64[])item;
                        for (int i = 0; i < field.Length; i++)
                        {
                            UInt64 v = (UInt64)field[i];
                            stream.Append((byte)(v & 0xFF),
                                        (byte)((v >> 8) & 0xFF),
                                        (byte)((v >> 16) & 0xFF),
                                        (byte)((v >> 24) & 0xFF),
                                        (byte)((v >> 32) & 0xFF),
                                        (byte)((v >> 40) & 0xFF),
                                        (byte)((v >> 48) & 0xFF),
                                        (byte)((v >> 56) & 0xFF)
                                        );
                        }
                    }
                }
            }


            public override Object Deserialize(byte[] data, ref int offset, int data_size)
            {
                RequireBytes(8, data_size - offset);
                Int64 result = BitConverter.ToInt64(data, offset);
                offset += 8;
                return result;
            }

            public override void Serialize(Object item, ByteBuffer stream)
            {
                UInt64 v = (UInt64)(Int64)item;
                stream.Append((byte)(v & 0xFF),
                                (byte)((v >> 8) & 0xFF),
                                (byte)((v >> 16) & 0xFF),
                                (byte)((v >> 24) & 0xFF),
                                (byte)((v >> 32) & 0xFF),
                                (byte)((v >> 40) & 0xFF),
                                (byte)((v >> 48) & 0xFF),
                                (byte)((v >> 56) & 0xFF)
                                );
            }
			protected static Int64 Rand(Random random)
			{
				return (Int64)(random.NextDouble() * (double)(UInt64.MaxValue) + (double)Int64.MinValue);
			}
			public override object CreateRandom(Random random)
			{
				return Rand(random);
			}
		}
		public class TUInt64 : Primitive<UInt64>
        {
            public class Array
            {
				public abstract class Base : SerialItem
				{
					protected Base(Type t) : base(t)
					{ }
					public override void AssertBitwiseEqual(string variableName, object a, object b)
					{
						UInt64[] afield = (UInt64[])a;
						UInt64[] bfield = (UInt64[])b;
						AssertPrimitiveArrayMatch(variableName, afield, bfield);
					}

					public override object CreateRandom(Random random)
					{
						int length = RandomArrayLength(random);
						UInt64[] result = new UInt64[length];
						for (int i = 0; i < length; i++)
							result[i] = Rand(random);
						return result;
					}
				}

				public class Embedded : Base
                {
                    public Embedded()
                        : base(typeof(UInt64[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = ReadSize(data, ref offset, data_size);
                        RequireBytes(length * 8, data_size - offset);
                        UInt64[] result = new UInt64[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToUInt64(data, offset);
                            offset += 8;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        UInt64[] field = (UInt64[])item;
                        stream.WriteSize(field.Length);
                        for (int i = 0; i < field.Length; i++)
                        {
                            UInt64 v = field[i];
                            stream.Append((byte)(v & 0xFF),
                                        (byte)((v >> 8) & 0xFF),
                                        (byte)((v >> 16) & 0xFF),
                                        (byte)((v >> 24) & 0xFF),
                                        (byte)((v >> 32) & 0xFF),
                                        (byte)((v >> 40) & 0xFF),
                                        (byte)((v >> 48) & 0xFF),
                                        (byte)((v >> 56) & 0xFF)
                                        );
                        }
                    }
                }
                public class Derived : Base
                {
                    public Derived()
                        : base(typeof(UInt64[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = (data_size - offset) / 8;
                        UInt64[] result = new UInt64[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToUInt64(data, offset);
                            offset += 8;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        UInt64[] field = (UInt64[])item;
                        for (int i = 0; i < field.Length; i++)
                        {
                            UInt64 v = field[i];
                            stream.AppendU64(v);
                        }
                    }
                }

            }


            public override Object Deserialize(byte[] data, ref int offset, int data_size)
            {
                RequireBytes(8, data_size - offset);
                UInt64 result = (UInt64)BitConverter.ToInt64(data, offset);
                offset += 8;
                return result;
            }

            public override void Serialize(Object item, ByteBuffer stream)
            {
                UInt64 v = (UInt64)item;
                stream.AppendU64(v);
            }
			protected static UInt64 Rand(Random random)
			{
				return (UInt64)(random.NextDouble() * (double)UInt64.MaxValue);
			}
			public override object CreateRandom(Random random)
			{
				return Rand(random);
			}

		}
		public class Float : Primitive<float>
        {
            [StructLayout(LayoutKind.Explicit)]
            struct BitCast
            {
                [FieldOffset(0)]
                public float FloatValue;

                [FieldOffset(0)]
                public UInt32 IntValue;
            }


            public class Array
            {
				public abstract class Base : SerialItem
				{
					protected Base(Type t) : base(t)
					{ }
					public override void AssertBitwiseEqual(string variableName, object a, object b)
					{
						float[] afield = (float[])a;
						float[] bfield = (float[])b;
						AssertPrimitiveArrayMatch(variableName, afield, bfield);
					}

					public override object CreateRandom(Random random)
					{
						int length = RandomArrayLength(random);
						float[] result = new float[length];
						for (int i = 0; i < length; i++)
							result[i] = Rand(random);
						return result;
					}
				}

				public class Embedded : Base
                {
                    public Embedded()
                        : base(typeof(float[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = ReadSize(data, ref offset, data_size);
                        RequireBytes(length * 4, data_size - offset);
                        float[] result = new float[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToSingle(data, offset);
                            offset += 4;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        float[] field = (float[])item;
                        stream.WriteSize(field.Length);
                        BitCast cast = new BitCast();
                        for (int i = 0; i < field.Length; i++)
                        {
                            cast.FloatValue = field[i];
                            stream.AppendU32(cast.IntValue);
                        }
                    }
                }
                public class Derived : Base
                {
                    public Derived()
                        : base(typeof(float[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = (data_size - offset) / 4;
                        float[] result = new float[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToSingle(data, offset);
                            offset += 4;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        float[] field = (float[])item;
                        BitCast cast = new BitCast();
                        for (int i = 0; i < field.Length; i++)
                        {
                            cast.FloatValue = field[i];
                            stream.AppendU32(cast.IntValue);
                        }
                    }
                }
            }
            public override Object Deserialize(byte[] data, ref int offset, int data_size)
            {
                RequireBytes(4, data_size - offset);
                float result = BitConverter.ToSingle(data, offset);
                offset += 4;
                return result;
            }

            public override void Serialize(Object item, ByteBuffer stream)
            {
                BitCast cast = new BitCast();
                cast.FloatValue = (float)item;
                stream.AppendU32(cast.IntValue);
                return;
            }

			protected static float Rand(Random random)
			{
				if (random.Next(100) < 10)
					return 0f;
				if (random.Next(100) < 5)
					return 1f;
				if (random.Next(100) < 5)
					return -1f;
				if (random.Next(100) < 2)
					return float.PositiveInfinity;
				if (random.Next(100) < 2)
					return float.NegativeInfinity;
				return (float)random.NextDouble() * 1000f - 500f;
			}
			public override object CreateRandom(Random random)
			{
				return Rand(random);
			}

		}

		public class Double : Primitive<double>
        {
            [StructLayout(LayoutKind.Explicit)]
            struct BitCast
            {
                [FieldOffset(0)]
                public double FloatValue;

                [FieldOffset(0)]
                public UInt64 IntValue;
            }


            public class Array
            {
				public abstract class Base : SerialItem
				{
					protected Base(Type t) : base(t)
					{ }
					public override void AssertBitwiseEqual(string variableName, object a, object b)
					{
						double[] afield = (double[])a;
						double[] bfield = (double[])b;
						AssertPrimitiveArrayMatch(variableName, afield, bfield);
					}

					public override object CreateRandom(Random random)
					{
						int length = RandomArrayLength(random);
						double[] result = new double[length];
						for (int i = 0; i < length; i++)
							result[i] = Rand(random);
						return result;
					}
				}
				public class Embedded : Base
                {
                    public Embedded()
                        : base(typeof(double[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = ReadSize(data, ref offset, data_size);
                        RequireBytes(length * 8, data_size - offset);
                        double[] result = new double[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToDouble(data, offset);
                            offset += 8;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        double[] field = (double[])item;
                        stream.WriteSize(field.Length);
                        BitCast cast = new BitCast();
                        for (int i = 0; i < field.Length; i++)
                        {
                            cast.FloatValue = field[i];
                            stream.AppendU64(cast.IntValue);
                        }
                    }
                }
                public class Derived : Base
                {
                    public Derived()
                        : base(typeof(double[]))
                    { }


                    public override Object Deserialize(byte[] data, ref int offset, int data_size)
                    {
                        int length = (data_size - offset) / 8;
                        double[] result = new double[length];

                        for (int i = 0; i < length; i++)
                        {
                            result[i] = BitConverter.ToDouble(data, offset);
                            offset += 8;
                        }
                        return result;
                    }

                    public override void Serialize(Object item, ByteBuffer stream)
                    {
                        double[] field = (double[])item;
                        BitCast cast = new BitCast();
                        for (int i = 0; i < field.Length; i++)
                        {
                            cast.FloatValue = field[i];
                            stream.AppendU64(cast.IntValue);
                        }
                    }
                }
            }
            public override Object Deserialize(byte[] data, ref int offset, int data_size)
            {
                RequireBytes(8, data_size - offset);
                double result = BitConverter.ToDouble(data, offset);
                offset += 8;
                return result;
            }

            public override void Serialize(Object item, ByteBuffer stream)
            {
                BitCast cast = new BitCast();
                cast.FloatValue = (double)item;
                stream.AppendU64(cast.IntValue);
                return;
            }

			protected static double Rand(Random random)
			{
				if (random.Next(100) < 10)
					return 0.0;
				if (random.Next(100) < 5)
					return 1.0;
				if (random.Next(100) < 5)
					return -1.0;
				if (random.Next(100) < 2)
					return double.PositiveInfinity;
				if (random.Next(100) < 2)
					return double.NegativeInfinity;
				return random.NextDouble() * 1000.0 - 500.0;
			}
			public override object CreateRandom(Random random)
			{
				return Rand(random);
			}

		}


		public class String
        {
			public abstract class Base : Primitive<string>
			{

				public override void AssertBitwiseEqual(string variableName, object a, object b)
				{
					string ca = (string)a;
					string cb = (string)b;
					if (ca != cb)
					{
						throw new Exception(variableName+": '"+ca+"' != '"+cb+"'");
					}
				}
				public override object CreateRandom(Random random)
				{
					int len = RandomArrayLength(random);
					char[] field = new char[len];
					for (int i = 0; i < len; i++)
						field[i] = Char.Rand(random);
					return new string(field);
				}
			}

			public class Embedded : Base
            {
                public override Object Deserialize(byte[] data, ref int offset, int data_size)
                {
                    int length = ReadSize(data, ref offset, data_size);

                    int remaining = data_size - offset;
                    if (remaining < length)
                    {
                        throw new Exception("Streaming exception: Unable to deserialize variable. Expected at least " + length + " additional bytes to deserialize string but got " + remaining + " bytes left.");
                    }

                    char[] chars = new char[length];
                    for (int i = 0; i < length; i++)
                    {
                        char c = (char)data[offset++];
                        if (c == 0)
                            c = ' ';
                        chars[i] = c;
                    }
                    return new string(chars);
                }

                public override void Serialize(Object item, ByteBuffer stream)
                {
                    string v = (string)item;
                    if (v == null)
                    {
                        stream.WriteSize(0);
                        return;
                    }
                    stream.WriteSize(v.Length);

                    for (int i = 0; i < v.Length; i++)
                        stream.Append((byte)v[i]);
                    return;
                }
            }
            public class Derived : Base
			{
                public override Object Deserialize(byte[] data, ref int offset, int data_size)
                {
                    int length = data_size - offset;

                    char[] chars = new char[length];
                    for (int i = 0; i < length; i++)
                    {
                        char c = (char)data[offset++];
                        if (c == 0)
                            c = ' ';
                        chars[i] = c;
                    }
                    return new string(chars);
                }

                public override void Serialize(Object item, ByteBuffer stream)
                {
                    string v = (string)item;
                    if (v == null)
                        return;
 
                    for (int i = 0; i < v.Length; i++)
                        stream.Append((byte)v[i]);
                    return;
                }
            }
        }

    }



    public class SerialInterface
    {
        private Type serializable;
        private System.Reflection.FieldInfo[] fields;
        private SerialItem[] items;
        private bool isArray, isPrimitive;

        private static Dictionary<Type, SerialInterface> map = new Dictionary<Type, SerialInterface>();


        private SerialInterface(Type t)
        {
            serializable = t;
            isArray = serializable.IsArray;
            isPrimitive = serializable.IsPrimitive || serializable == typeof(string);
            if (isArray)
            {
                items = new SerialItem[1];
                items[0] = SerialItem.Compile(serializable, SerialItem.CanUtilizeDerivation(serializable), 0);
            }
            else
            {
                if (isPrimitive)
                {
                    items = new SerialItem[1];
                    items[0] = SerialItem.Compile(serializable, true, 0);
                }
                else
                {
                    fields = SerialItem.GetProperlyOrderedFields(t);
                    items = SerialItem.Compile(fields);
                }
            }
        }

        public static SerialInterface Build(Type t)
        {
            SerialInterface result;
            if (map.TryGetValue(t, out result))
                return result;
            result = new SerialInterface(t);
            map[t] = result;
            return result;
        }

		public object CreateRandom(Random random)
		{
			object item;

			if (isArray || isPrimitive)
				item = items[0].CreateRandom(random);
			else
			{
				item = Activator.CreateInstance(serializable);
				for (int i = 0; i < items.Length; i++)
					fields[i].SetValue(item, items[i].CreateRandom(random));
			}
			return item;
		}

		public void AssertBitwiseEqual(object a, object b)
		{
			if (isArray || isPrimitive)
				items[0].AssertBitwiseEqual("",a, b);
			else
			{
				for (int i = 0; i < items.Length; i++)
					items[i].AssertBitwiseEqual(fields[i].Name, fields[i].GetValue(a), fields[i].GetValue(b));
					//fields[i].SetValue(item, items[i].CreateRandom(random));
			}


		}


		public Object Deserialize(byte[] data, int dataSize)
        {
            Debug.Assert(dataSize <= data.Length);

            Object item;

            int offset = 0;
            if (isArray || isPrimitive)
                item = items[0].Deserialize(data, ref offset, dataSize);
            else
            {
                item = Activator.CreateInstance(serializable);
				for (int i = 0; i < items.Length; i++)
					//fields[i].SetValueDirect(items[i].VariableType.ref, null);
					fields[i].SetValue(item, items[i].Deserialize(data, ref offset, dataSize - items[i].DerivedOffsetFromEnd));
            }
            if (offset != dataSize)
                throw new Exception("Streaming exception: Unable to deserialize " + serializable.ToString() + ". Expected an object of size " + offset + " bytes, but remote end sent " + dataSize + " bytes");

            return item;
        }

        public void Serialize(Object item, ByteBuffer buffer)
        {
            if (isArray || isPrimitive)
                items[0].Serialize(item, buffer);
            else
                for (int i = 0; i < items.Length; i++)
                    items[i].Serialize(fields[i].GetValue(item), buffer);
        }

        public void SerializePacket(UInt32 channelID, Object item, ByteBuffer buffer)
        {
            buffer.Clear();
            buffer.AppendU32(channelID);
            buffer.AppendU32(0);
            Serialize(item, buffer);
            buffer.SetU32(4, (UInt32)(buffer.Length - 8));
        }

    }


}
