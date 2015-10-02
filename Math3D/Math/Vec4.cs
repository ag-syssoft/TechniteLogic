using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Math3D
{
    public struct TVec4<T>
    {
        public T x, y, z, w;

        public TVec3<T> XYZ {get {return new TVec3<T>(x,y,z);} set {x = value.x; y = value.y; z = value.z;}}
        public TVec2<T> XY { get { return new TVec2<T>(x, y); } set { x = value.x; y = value.y; } }
        public TVec2<T> ZW { get { return new TVec2<T>(z, w); } set { z = value.x; w = value.y; } }


        public TVec4(T x_, T y_, T z_, T w_)
        {
            x = x_;
            y = y_;
            z = z_;
            w = w_;
        }

        public TVec4(T v)
        {
            x = v;
            y = v;
            z = v;
            w = v;
        }
        public TVec4(TVec3<T> v, T w_)
        {
            x = v.x;
            y = v.y;
            z = v.z;
            w = w_;
        }
        public TVec4(TVec2<T> v, TVec2<T> w_)
        {
            x = v.x;
            y = v.y;
            z = w_.x;
            w = w_.y;
        }
    }

    public struct Vec4
    {
        public float x, y, z, w;

        public Vec3 xyz { get { return new Vec3(x, y, z); } set { x = value.x; y = value.y; z = value.z; } }
        public Vec2 xy { get { return new Vec2(x, y); } set { x = value.x; y = value.y; } }
        public Vec2 zw { get { return new Vec2(z, w); } set { z = value.x; w = value.y; } }


        public Vec4(Vec3 v, float w = 1)
        {
            this.x = v.x;
            this.y = v.y;
            this.z = v.z;
            this.w = w;
        }
        public Vec4(Vec2 xy, Vec2 zw)
        {
            this.x = xy.x;
            this.y = xy.y;
            this.z = zw.x;
            this.w = zw.y;
        }
        public Vec4(Vec2 xy, float z, float w=1)
        {
            this.x = xy.x;
            this.y = xy.y;
            this.z = z;
            this.w = w;
        }
         
        public Vec4(float x, float y, float z, float w=1)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public Vec4(float v)
        {
            this.x = v;
            this.y = v;
            this.z = v;
            this.w = v;
        }

        public float Length { get { return (float)Math.Sqrt(Vec.Dot(this, this)); } set { this *= value / Length; } }
        public Vec4 Normalize() { return this / Length; }

        public static Vec4 operator +(Vec4 u, Vec4 v)
        {
            return new Vec4(u.x + v.x, u.y + v.y, u.z + v.z, u.w + v.w);
        }
        public static Vec4 operator -(Vec4 u, Vec4 v)
        {
            return new Vec4(u.x - v.x, u.y - v.y, u.z - v.z, u.w - v.w);
        }
        public static Vec4 operator +(Vec4 u, float v)
        {
            return new Vec4(u.x + v, u.y + v, u.z + v, u.w + v);
        }
        public static Vec4 operator +(float v, Vec4 u)
        {
            return new Vec4(u.x + v, u.y + v, u.z + v, u.w + v);
        }
        public static Vec4 operator -(Vec4 u, float v)
        {
            return new Vec4(u.x - v, u.y - v, u.z - v, u.w - v);
        }
        public static Vec4 operator /(Vec4 u, float v)
        {
            return new Vec4(u.x / v, u.y / v, u.z / v, u.w / v);
        }
        public static Vec4 operator *(Vec4 u, float v)
        {
            return new Vec4(u.x * v, u.y * v, u.z * v, u.w * v);
        }
        public static Vec4 operator *(float v, Vec4 u)
        {
            return new Vec4(u.x * v, u.y * v, u.z * v, u.w * v);
        }
        //public static implicit operator string(Vec4 v)
        //{
        //    return Convert.ToString(v.x) + ", " + Convert.ToString(v.y) + ", " + Convert.ToString(v.z) + ", " + Convert.ToString(v.w);
        //}

        private bool Eq(Vec4 other)
        {
            return x.SimilarTo(other.x) && y.SimilarTo(other.y) && z.SimilarTo(other.z) && w.SimilarTo(other.w);
        }
        public static bool operator ==(Vec4 u, Vec4 v)
        {
            return u.Eq(v);
        }
        public static bool operator !=(Vec4 u, Vec4 v)
        {
            return !u.Eq(v);
        }

        public override bool Equals(object obj)
        {
            return obj is Vec4 && Eq((Vec4)obj);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + x.GetHashCode();
            hash = hash * 31 + y.GetHashCode();
            hash = hash * 31 + z.GetHashCode();
            hash = hash * 31 + w.GetHashCode();
            return hash;
        }


        public override string ToString()
        {
            return "(" + Convert.ToString(x) + ", " + Convert.ToString(y)+ ", " + Convert.ToString(z) + ", " + Convert.ToString(w) + ")";
        }

    }

}
