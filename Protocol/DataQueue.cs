using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Protocol
{
    class DataQueue
    {
        private byte[] Data;
        int ReadFrom, WriteTo;


        public DataQueue(int InitialSize)
        {
            Data = new byte[InitialSize];
            ReadFrom = WriteTo = 0;
        }


        public bool IsEmpty
        {
            get { return ReadFrom == WriteTo; }
        }

        public int Length
        {
            get { return WriteTo >= ReadFrom ? WriteTo - ReadFrom : WriteTo + (Data.Length - ReadFrom); }
        }

        public void Append(byte[] data, int length)
        {
            int MyLength = Length;
            if (MyLength + data.Length >= Data.Length)
            {
                byte[] NewData = new byte[(MyLength + length) * 2];
                if (WriteTo < ReadFrom)
                {
                    Array.Copy(Data, ReadFrom, NewData, 0, Data.Length - ReadFrom);
                    Array.Copy(Data, 0, NewData, Data.Length - ReadFrom, WriteTo);
                }
                else
                    Array.Copy(Data, ReadFrom, NewData, 0, WriteTo - ReadFrom);
                Array.Copy(data, 0, NewData, MyLength, length);
                Data = NewData;
                ReadFrom = 0;
                WriteTo = MyLength + length;
            }
            else
            {
                if (WriteTo + length > Data.Length)
                {
                    int Chunk = Data.Length - WriteTo;
                    Array.Copy(data, 0, Data, WriteTo, Chunk);
                    Array.Copy(data, Chunk, Data, 0, length - Chunk);
                    WriteTo = length - Chunk;
                }
                else
                {
                    Array.Copy(data, 0, Data, WriteTo, length);
                    WriteTo = (WriteTo + length) % Data.Length;
                }
            }
        }

        static byte[] HeaderPeekData = new byte[8];
        public bool PeekHeader(ref UInt32 Channel, ref UInt32 Size)
        {
            if (Length < 8)
                return false;
            PeekData(HeaderPeekData, 8);
            Channel = (UInt32)BitConverter.ToInt32(HeaderPeekData, 0);
            Size = (UInt32)BitConverter.ToInt32(HeaderPeekData, 4);
            return true;
        }
        public bool GetHeader(ref UInt32 Channel, ref UInt32 Size)
        {
            if (!PeekHeader(ref Channel, ref Size))
                return false;
            ReadFrom = (ReadFrom + 8) % Data.Length;
            return true;
        }

        public void Skip(int bytes)
        {
            Debug.Assert(Length >= bytes);
            ReadFrom = (ReadFrom + bytes) % Data.Length;
        }

        public uint PeekUInt32()
        {
            Debug.Assert(Length >= 4);

            byte b0 = Data[ReadFrom],
                b1 = Data[(ReadFrom + 1) % Data.Length],
                b2 = Data[(ReadFrom + 2) % Data.Length],
                b3 = Data[(ReadFrom + 3) % Data.Length];
            return (uint)b0 | (((uint)b1) << 8) | (((uint)b2) << 16) | (((uint)b3) << 24);
        }
        public uint PopUInt32()
        {
            uint result = PeekUInt32();
            ReadFrom = (ReadFrom + 4) % Data.Length;
            return result;
        }

        public void PeekData(byte[] OutData, int OutSize)
        {
            Debug.Assert(Length >= OutSize);

            if (Data.Length - ReadFrom >= OutSize)
            {
                Array.Copy(Data, ReadFrom, OutData, 0, OutSize);
                // ReadFrom = (ReadFrom + OutSize) % Data.Length;
            }
            else
            {
                int Chunk = Data.Length - ReadFrom;
                Array.Copy(Data, ReadFrom, OutData, 0, Chunk);
                Array.Copy(Data, 0, OutData, Chunk, OutSize - Chunk);
                //ReadFrom = OutSize - Chunk;
            }
        }

        public void PopData(byte[] OutData, int OutSize)
        {
            PeekData(OutData, OutSize);
            ReadFrom = (ReadFrom + OutSize) % Data.Length;
        }


        public static void Test()
        {
            DataQueue queue = new DataQueue(0);

            byte[] field = new byte[256];
            for (int i = 0; i < 256; i++)
                field[i] = (byte)i;

            byte[] exported = new byte[256];


            for (int i = 1; i < 257; i++)
            {
                Console.Out.Write(i);
                queue.Append(field, i);

                if (i > 1)
                {
                    queue.PopData(exported, i - 1);
                    for (int j = 0; j < i - 1; j++)
                    {
                        Debug.Assert(exported[j] == (byte)j);
                        Console.Out.Write(" ");
                        Console.Out.Write(exported[j]);
                    }
                }
                Console.Out.WriteLine();
            }

            Console.Out.WriteLine(queue.Length);
            Debug.Assert(queue.Length == 256);
        }

    };
}
