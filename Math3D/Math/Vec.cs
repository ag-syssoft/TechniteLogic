using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Math3D
{
    public static class Vec
    {
        public static float Dot(Vec2 u, Vec2 v)
        {
            return u.x * v.x + u.y * v.y;
        }
        public static float Dot(Vec3 u, Vec3 v)
        {
            return u.x * v.x + u.y * v.y + u.z * v.z;
        }
        public static float Dot(Vec4 u, Vec4 v)
        {
            return u.x * v.x + u.y * v.y + u.z * v.z + u.w * v.w;
        }

        public static float Sqr(Vec2 v)
        {
            return v.x * v.x + v.y * v.y;
        }
        public static float Sqr(Vec3 v)
        {
            return v.x * v.x + v.y * v.y + v.z * v.z;
        }
        public static float Sqr(Vec4 v)
        {
            return v.x * v.x + v.y * v.y + v.z * v.z + v.w * v.w;
        }

        public static Vec3 Cross(Vec3 u, Vec3 v)
        {
            return new Vec3(u.y * v.z - u.z * v.y, u.z * v.x - u.x * v.z, u.x * v.y - u.y * v.x);
        }

        public static float Distance(Vec3 u, Vec3 v)
        {
            return (u - v).Length;
        }

        public static float QuadraticDistance(Vec3 u, Vec3 v)
        {
            return Sqr(u - v);
        }
    }
}
