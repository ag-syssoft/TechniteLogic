using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Math3D
{
    public struct Range
    {
        public float Min, 
                    Max;

        public float Center { get { return (Min + Max) / 2.0f; } set { float extend = Extend; Min = value - extend / 2.0f; Max = value + extend / 2.0f; } }
        public float Extend { get { return Max - Min; } set { float center = Center; Min = center - value / 2.0f; Max = center + value / 2.0f; } }
        public bool IsValid { get { return Max >= Min; } }

        public Range(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public bool Contains(float v)
        {
            return v >= Min && v <= Max;
        }
        public void Include(float v)
        {
            Min = Math.Min(v, Min);
            Max = Math.Max(v, Max);
        }
        public void Include(Range r)
        {
            Min = Math.Min(r.Min, Min);
            Max = Math.Max(r.Max, Max);
        }

        public bool Intersects(Range r)
        {
            return r.Max >= Min && r.Min <= Max;
        }

        public override string ToString()
        {
            return "[" + Min + ", " + Max + "]";
        }
    }

    public struct Rect
    {
        public Range x,y;
        public float Area { get { return x.Extend * y.Extend; } }
        public bool IsValid { get { return x.IsValid && y.IsValid; } }
        public Vec2 Center { get { return new Vec2(x.Center, y.Center); } }
        public Vec2 Min { get { return new Vec2(x.Min, y.Min); } set { x.Min = value.x; y.Min = value.y; } }
        public Vec2 Max { get { return new Vec2(x.Max, y.Max); } set { x.Max = value.x; y.Max = value.y; } }




        public Rect(Vec2 min, Vec2 max)
        {
            x = new Range(min.x,max.x);
            y = new Range(min.y,max.y);
        }

        public Range this[int axis]
        {
            get
            {
                switch (axis)
                {
                    case 0:
                        return x;
                    case 1:
                        return y;
                }
                throw new Exception("Unexpected index for Rect[]");
            }
            set
            {
                switch (axis)
                {
                    case 0:
                        x = value;
                        break;
                    case 1:
                        y = value;
                        break;
                    default:
                        throw new Exception("Unexpected index for Rect[]");
                }
            }
        }

        public bool Contains(Vec2 coords)
        {
            return x.Contains(coords.x) && y.Contains(coords.y);
        }

        public void Include(Vec2 coords)
        {
            x.Include(coords.x);
            y.Include(coords.y);
        }

        public void Include(Rect r)
        {
            x.Include(r.x);
            y.Include(r.y);
        }

        public override string ToString()
        {
            return "[ (" + x.Min + ", " + y.Min + "), (" + x.Max + ", " + y.Max + ") ]";
        }


    }

    public struct Box
    {
        public Range x, y, z;
        public float Volume { get { return x.Extend * y.Extend * z.Extend; } }
        public bool IsValid { get { return x.IsValid && y.IsValid && z.IsValid; } }
        public Vec3 Center { get { return new Vec3(x.Center, y.Center, z.Center); } }
        public Vec3 Min { get { return new Vec3(x.Min, y.Min, z.Min); } set { x.Min = value.x; y.Min = value.y; z.Min = value.z; } }
        public Vec3 Max { get { return new Vec3(x.Max, y.Max, z.Max); } set { x.Max = value.x; y.Max = value.y; z.Max = value.z; } }

        public Box(Range r)
        {
            x = r;
            y = r;
            z = r;
        }

        public Box(Vec3 min, Vec3 max)
        {
            x = new Range(min.x,max.x);
            y = new Range(min.y,max.y);
            z = new Range(min.z,max.z);
        }

        public override string ToString()
        {
            return "[ (" + x.Min + ", " + y.Min + ", " + z.Min + "), (" + x.Max + ", " + y.Max + ", " + z.Max + ") ]";
        }


        public Range this[int axis]
        {
            get
            {
                switch (axis)
                {
                    case 0:
                        return x;
                    case 1:
                        return y;
                    case 2:
                        return z;
                }
                throw new Exception("Unexpected index for Box[]");
            }
            set
            {
                switch (axis)
                {
                    case 0:
                        x = value;
                        break;
                    case 1:
                        y = value;
                        break;
                    case 2:
                        z = value;
                        break;
                    default:
                        throw new Exception("Unexpected index for Box[]");
                }
            }
        }

        public bool Contains(Vec3 coords)
        {
            return x.Contains(coords.x) && y.Contains(coords.y) && z.Contains(coords.z);
        }

        public void Include(Vec3 coords)
        {
            x.Include(coords.x);
            y.Include(coords.y);
            z.Include(coords.z);
        }
        public void Include(Box box)
        {
            x.Include(box.x);
            y.Include(box.y);
            z.Include(box.z);
        }

        public int MaxAxis()
        {
            float xExt = x.Extend,
                    yExt = y.Extend,
                    zExt = z.Extend;
            if (xExt > yExt)
            {
                if (xExt > zExt)
                    return 0;
                return 2;
            }
            if (yExt > zExt)
                return 1;
            return 2;
        }

        public bool Intersects(Box box)
        {
            return x.Intersects(box.x) && y.Intersects(box.y) && z.Intersects(box.z);
        }


        public static Box Wrap(Box box0, Box box1)
        {
            box0.Include(box1);
            return box0;
        }
    }
}
