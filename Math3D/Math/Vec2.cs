using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Math3D
{
    public struct TVec2<T>
    {
        public T x, y;

        public TVec2(T x_, T y_)
        {
            x = x_;
            y = y_;
        }

        public TVec2(T v)
        {
            x = v;
            y = v;
        }
    }

    public struct Vec2
    {
        public float x, y;

        public Vec2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public Vec2(float v)
        {
            this.x = v;
            this.y = v;
        }

        public Vec2(Resolution res)
        {
            this.x = (float)res.Width;
            this.y = (float)res.Height;
        }

        public float Length { get { return (float)Math.Sqrt(Vec.Dot(this, this)); } set { this *= value / Length; } }
        public Vec2 Normalize() { return this / Length; }

        public static Vec2 operator +(Vec2 u, Vec2 v)
        {
            return new Vec2(u.x + v.x, u.y + v.y);
        }
        public static Vec2 operator -(Vec2 u, Vec2 v)
        {
            return new Vec2(u.x - v.x, u.y - v.y);
        }
        public static Vec2 operator +(Vec2 u, float v)
        {
            return new Vec2(u.x + v, u.y + v);
        }
        public static Vec2 operator +(float v, Vec2 u)
        {
            return new Vec2(u.x + v, u.y + v);
        }
        public static Vec2 operator -(Vec2 u, float v)
        {
            return new Vec2(u.x - v, u.y - v);
        }
        public static Vec2 operator /(Vec2 u, float v)
        {
            return new Vec2(u.x / v, u.y / v);
        }
        public static Vec2 operator *(Vec2 u, float v)
        {
            return new Vec2(u.x * v, u.y * v);
        }
        public static Vec2 operator *(float v, Vec2 u)
        {
            return new Vec2(u.x * v, u.y * v);
        }
        //public static implicit operator string(Vec2 v)
        //{
        //    return Convert.ToString(v.x) + ", " + Convert.ToString(v.y);
        //}
        private bool Eq(Vec2 other)
        {
            return x.SimilarTo(other.x) && y.SimilarTo(other.y);
        }

        public static bool operator ==(Vec2 u, Vec2 v)
        {
            return u.Eq(v);
        }
        public static bool operator !=(Vec2 u, Vec2 v)
        {
            return !u.Eq(v);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + x.GetHashCode();
            hash = hash * 31 + y.GetHashCode();
            return hash;
        }

        public override bool Equals(object obj)
        {
            return obj is Vec2 && Eq((Vec2)obj);
        }

        public override string ToString()
        {
            return "("+Convert.ToString(x) + ", " + Convert.ToString(y)+")";
        }
    }
}
