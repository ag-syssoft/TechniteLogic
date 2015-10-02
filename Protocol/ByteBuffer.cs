using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Protocol
{
    public class ByteBuffer
    {
        private byte[] data;
        private int fillState;


        public ByteBuffer(int initialSize)
        {
            data = new byte[initialSize];
            fillState = 0;
        }

        public int Length { get { return fillState; } }

        private void RequireSpace(int space)
        {
            if (fillState + space <= data.Length)
                return;
            int size = data.Length;
            if (size <= 0)
                size = 4;
            while (size < fillState + space)
                size *= 2;

            byte[] newField = new byte[size];
            Array.Copy(data, newField, fillState);
            data = newField;
        }


        public void Append(byte value)
        {
            RequireSpace(1);
            data[fillState++] = value;
        }
        public void Append(byte v0, byte v1)
        {
            RequireSpace(2);
            data[fillState++] = v0;
            data[fillState++] = v1;
        }
        public void Append(byte v0, byte v1, byte v2)
        {
            RequireSpace(3);
            data[fillState++] = v0;
            data[fillState++] = v1;
            data[fillState++] = v2;
        }
        public void Append(byte v0, byte v1, byte v2, byte v3)
        {
            RequireSpace(4);
            data[fillState++] = v0;
            data[fillState++] = v1;
            data[fillState++] = v2;
            data[fillState++] = v3;
        }
        public void Append(byte v0, byte v1, byte v2, byte v3, byte v4, byte v5, byte v6, byte v7)
        {
            RequireSpace(8);
            data[fillState++] = v0;
            data[fillState++] = v1;
            data[fillState++] = v2;
            data[fillState++] = v3;
            data[fillState++] = v4;
            data[fillState++] = v5;
            data[fillState++] = v6;
            data[fillState++] = v7;
        }

        public void AppendU64(UInt64 v)
        {
            Append( (byte)(v & 0xFF),
                    (byte)((v >> 8) & 0xFF),
                    (byte)((v >> 16) & 0xFF),
                    (byte)((v >> 24) & 0xFF),
                    (byte)((v >> 32) & 0xFF),
                    (byte)((v >> 40) & 0xFF),
                    (byte)((v >> 48) & 0xFF),
                    (byte)((v >> 56) & 0xFF)
                    );
}

        public void AppendU32(UInt32 v)
        {
            Append( (byte)(v & 0xFF),
                    (byte)((v >> 8) & 0xFF),
                    (byte)((v >> 16) & 0xFF),
                    (byte)((v >> 24) & 0xFF));
        }

        public void AppendU16(UInt16 v)
        {
            Append( (byte)(v & 0xFF),
                    (byte)((v >> 8) & 0xFF)
                    );
        }


        public void Append(byte[] array, int begin, int count)
        {
            RequireSpace(count);
            Array.Copy(array, begin, data, fillState, count);
            fillState += count;
        }

        public void SetU32(uint address, UInt32 v)
        {
            Debug.Assert(address + 4 <= Length);

            data[address] = (byte)(v & 0xFF);
            data[address+1] = (byte)((v >> 8) & 0xFF);
            data[address+2] = (byte)((v >> 16) & 0xFF);
            data[address+3] = (byte)((v >> 24) & 0xFF);
        }


        public void WriteSize(int size)
        {
            if (size <= 0x3F)
            {
                Append((byte)size);
                return;
            }
            if (size <= 0x3FFF)
            {
                Append((byte)(((size >> 8) & 0x3F) | 0x40),
                                (byte)(size & 0xFF));
                return;
            }
            if (size <= 0x3FFFFF)
            {
                Append((byte)(((size >> 16) & 0x3F) | 0x80),
                                (byte)((size >> 8) & 0xFF),
                                (byte)(size & 0xFF));
                return;
            }
            Append((byte)(((size >> 24) & 0x3F) | 0xC0),
                         (byte)((size >> 16) & 0xFF),
                            (byte)((size >> 8) & 0xFF),
                            (byte)(size & 0xFF));
            return;
        }


        public byte[] GetArray()
        {
            return data;
        }


        public void Clear() { fillState = 0; }

    }

}
