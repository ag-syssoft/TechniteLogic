using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Math3D
{
    public struct Matrix3
    {
        public Vec3 x, y, z;
        public static Matrix3 Identity { get { return new Matrix3(1); } }

        public Matrix3(float scalar)
        {
            x = new Vec3(scalar, 0, 0);
            y = new Vec3(0, scalar, 0);
            z = new Vec3(0, 0, scalar);
        }
        public Matrix3(Vec3 x, Vec3 y, Vec3 z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        private bool Eq(Matrix3 other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public static bool operator ==(Matrix3 u, Matrix3 v)
        {
            return u.Eq(v);
        }
        public static bool operator !=(Matrix3 u, Matrix3 v)
        {
            return !u.Eq(v);
        }

        public override bool Equals(object obj)
        {
            return obj is Matrix3 && Eq((Matrix3)obj);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + x.GetHashCode();
            hash = hash * 31 + y.GetHashCode();
            hash = hash * 31 + z.GetHashCode();
            return hash;
        }

        public static Vec3 operator *(Matrix3 m, Vec3 v)
        {
            return m.x * v.x + m.y * v.y + m.z * v.z;
        }
        public static Matrix3 operator *(Matrix3 m, Matrix3 n)
        {
            return new Matrix3(m *n.x, m * n.y, m*n.z);
        }
    }
}
