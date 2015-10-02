using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Math3D
{
    public struct Matrix4
    {
        public Vec4 x, y, z, w;
        //public static Matrix4 Identity = new Matrix4(1);

        public Matrix3 Orientation { get { return new Matrix3(x.xyz, y.xyz, z.xyz); } set { x.xyz = value.x; y.xyz = value.y; z.xyz = value.z; } }
        public Vec3 Position { get { return w.xyz; } set { w.xyz = value; } }
        public static readonly Matrix4 Identity = new Matrix4(1);

        public Matrix4(float scalar)
        {
            x = new Vec4(scalar, 0, 0, 0);
            y = new Vec4(0, scalar, 0, 0);
            z = new Vec4(0, 0, scalar, 0);
            w = new Vec4(0, 0, 0, scalar);
        }
        public Matrix4(Vec4 newX, Vec4 newY, Vec4 newZ, Vec4 newW)
        {
            x = newX;
            y = newY;
            z = newZ;
            w = newW;
        }

        public void ResetBottomRow()
        {
            x.w = y.w = z.w = 0;
            w.w = 1;
        }

        public static Vec4 operator *(Matrix4 m, Vec4 v)
        {
            return m.x * v.x + m.y * v.y + m.z * v.z + m.w * v.w;
        }
        public static Matrix4 operator *(Matrix4 m, Matrix4 n)
        {
            return new Matrix4(m *n.x, m * n.y, m*n.z, m*n.w);
        }


        private bool Eq(Matrix4 other)
        {
            return x == other.x && y == other.y && z == other.z && w == other.w;
        }

        public static bool operator ==(Matrix4 u, Matrix4 v)
        {
            return u.Eq(v);
        }
        public static bool operator !=(Matrix4 u, Matrix4 v)
        {
            return !u.Eq(v);
        }

        public override bool Equals(object obj)
        {
            return obj is Matrix4 && Eq((Matrix4)obj);
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


        public Matrix4 Transform(Matrix4 n)
        {
            return new Matrix4(new Vec4(Rotate(n.x.xyz)),new Vec4(Rotate(n.y.xyz)), new Vec4(Rotate(n.z.xyz)), new Vec4(Transform(n.w.xyz),1));
        }

        public Vec3 Transform(Vec3 v)
        {
            return x.xyz * v.x + y.xyz * v.y + z.xyz * v.z + w.xyz;
                //(this * new Vec4(v, 1)).XYZ;
        }
        public Vec3 Rotate(Vec3 v)
        {
            return x.xyz * v.x + y.xyz * v.y + z.xyz * v.z;
            //(this * new Vec4(v, 0)).XYZ;
        }


        public static Matrix4 Assemble(Vec3 orientationX, Vec3 orientationY, Vec3 position)
        {
            return new Matrix4(new Vec4(orientationX.Normalize(), 0), new Vec4(orientationY.Normalize(), 0), new Vec4(Vec.Cross(orientationX, orientationY).Normalize(), 0), new Vec4(position, 1));
        }
    }
}
