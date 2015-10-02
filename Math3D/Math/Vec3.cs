using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace Math3D
{
    public struct TVec3<T>
    {
        public T x, y, z;
        public TVec2<T> xy { get { return new TVec2<T>(x, y); } set { x = value.x; y = value.y; } }
        public TVec2<T> yz { get { return new TVec2<T>(y, z); } set { y = value.x; z = value.y; } }

        public TVec3(T x_, T y_, T z_)
        {
            x = x_;
            y = y_;
            z = z_;
        }
        public TVec3(T v)
        {
            x = v;
            y = v;
            z = v;
        }

    }

    public struct Vec3
    {
        public float x, y, z;
        public Vec2 xy { get { return new Vec2(x, y); } set { x = value.x; y = value.y; } }
        public Vec2 yz { get { return new Vec2(y, z); } set { y = value.x; z = value.y; } }

        public static readonly Vec3 Zero = new Vec3(0);
        public static readonly Vec3 XAxis = new Vec3(1, 0, 0);
        public static readonly Vec3 YAxis = new Vec3(0, 1, 0);
        public static readonly Vec3 ZAxis = new Vec3(0, 0, 1);


        public float this[int key]
        {
            get
            {
                switch (key)
                {
                    case 0:
                        return x;
                    case 1:
                        return y;
                    case 2:
                        return z;
                }
                throw new Exception("Unexpected index for Vec3[]");
            }
            set
            {
                switch (key)
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
                        throw new Exception("Unexpected index for Vec3[]");
                }
            }
        }


        public Vec3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        public Vec3(float v)
        {
            this.x = v;
            this.y = v;
            this.z = v;
        }
        public Vec3(Vec2 xy, float z)
        {
            this.x = xy.x;
            this.y = xy.y;
            this.z = z;
        }
        public Vec3(float x, Vec2 yz)
        {
            this.x = x;
            this.y = yz.x;
            this.z = yz.y;
        }

        public float Length { get { return (float)Math.Sqrt(Vec.Dot(this, this)); } set { this *= value / Length; } }
        public Vec3 Normalize() { return this / Length; }

        public static Vec3 operator+(Vec3 u, Vec3 v)
        {
            return new Vec3(u.x + v.x,u.y + v.y,u.z + v.z);
        }
        public static Vec3 operator -(Vec3 u, Vec3 v)
        {
            return new Vec3(u.x - v.x, u.y - v.y, u.z - v.z);
        }
        public static Vec3 operator +(Vec3 u, float v)
        {
            return new Vec3(u.x + v, u.y + v, u.z + v);
        }
        public static Vec3 operator +(float v, Vec3 u)
        {
            return new Vec3(u.x + v, u.y + v, u.z + v);
        }
        public static Vec3 operator -(Vec3 u, float v)
        {
            return new Vec3(u.x - v, u.y - v, u.z - v);
        }
        public static Vec3 operator /(Vec3 u, float v)
        {
            return new Vec3(u.x / v, u.y / v, u.z / v);
        }
        public static Vec3 operator *(Vec3 u, float v)
        {
            return new Vec3(u.x * v, u.y * v, u.z * v);
        }
        public static Vec3 operator *(float v, Vec3 u)
        {
            return new Vec3(u.x * v, u.y * v, u.z * v);
        }

        private bool Eq(Vec3 other)
        {
            return x.SimilarTo(other.x) && y.SimilarTo(other.y) && z.SimilarTo(other.z);
        }

        public static bool operator ==(Vec3 u, Vec3 v)
        {
            return u.Eq(v);
        }
        public static bool operator !=(Vec3 u, Vec3 v)
        {
            return !u.Eq(v);
        }

        public override bool Equals(object obj)
        {
            return obj is Vec3 && Eq((Vec3)obj);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + x.GetHashCode();
            hash = hash * 31 + y.GetHashCode();
            hash = hash * 31 + z.GetHashCode();
            return hash;
        }


        //public static implicit operator string(Vec3 v)
        //{
        //    return Convert.ToString(v.x) + ", " + Convert.ToString(v.y) + ", " + Convert.ToString(v.z);
        //}
        public override string ToString()
        {
            return "(" + Convert.ToString(x) + ", " + Convert.ToString(y) + ", " + Convert.ToString(z) + ")";
        }

    }






}
