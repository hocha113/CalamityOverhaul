using System;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core
{
    /// <summary>
    /// 浑天仪 3D 骨架:三环姿态、透视投影、掷环飞行姿态,渲染器与掷环弹幕共用<br/>
    /// 坐标约定:X 右 Y 下 Z 入屏(远),near = 负 Z;透视 scale = F / (F + z)
    /// </summary>
    internal static class CultistOrreryRig
    {
        internal const int RingCount = 3;
        /// <summary>各环半径(px)</summary>
        internal static readonly float[] RingRadius = [88f, 120f, 152f];
        /// <summary>各环带半宽(px)</summary>
        internal static readonly float[] RingWidth = [7f, 8.5f, 10f];
        /// <summary>透视焦距(px)</summary>
        internal const float FocalLength = 540f;

        /// <summary>
        /// 环平面基向量:进动欧拉角随时间演化;align→1 时全环收拢共面(合相的空间化读数)
        /// </summary>
        internal static void GetRingBasis(int ring, float time, float align, out Vector3 e1, out Vector3 e2) {
            float yaw = time * (0.34f + ring * 0.145f) + ring * 2.09f;
            float basePitch = ring switch { 0 => 0.46f, 1 => 1.02f, 2 => 1.55f, _ => 0.8f };
            float pitch = basePitch + (float)Math.Sin(time * (0.21f + ring * 0.06f) + ring * 1.7f) * 0.24f;

            //合相:收拢到共享倾角+共享缓转,三环并盘即满格
            float alignedYaw = time * 0.42f;
            const float AlignedPitch = 0.58f;
            yaw = MathHelper.Lerp(yaw, alignedYaw, align);
            pitch = MathHelper.Lerp(pitch, AlignedPitch, align);

            BuildBasis(yaw, pitch, out e1, out e2);
        }

        /// <summary>由 yaw/pitch 生成环平面正交基</summary>
        internal static void BuildBasis(float yaw, float pitch, out Vector3 e1, out Vector3 e2) {
            float cy = (float)Math.Cos(yaw);
            float sy = (float)Math.Sin(yaw);
            float cp = (float)Math.Cos(pitch);
            float sp = (float)Math.Sin(pitch);
            //RotY(yaw) * RotX(pitch) 作用于 X/Y 轴
            e1 = new Vector3(cy, 0f, -sy);
            e2 = new Vector3(sy * sp, cp, cy * sp);
        }

        /// <summary>
        /// 掷环飞行基:环面恒含飞行轴(主轴=飞行向,碰撞胶囊同轴),副轴绕飞行轴进动<br/>
        /// precession=0 时纯侧立(细刃线,瞄准预告形),飞行中进动呼吸出椭圆
        /// </summary>
        internal static void GetHurlBasis(Vector2 flightDir, float precession, out Vector3 e1, out Vector3 e2) {
            Vector3 a = new(flightDir.X, flightDir.Y, 0f);
            if (a.LengthSquared() < 1e-6f) {
                a = Vector3.UnitX;
            }
            a.Normalize();
            //面内垂轴与屏轴之间进动
            Vector3 b = new(-a.Y, a.X, 0f);
            Vector3 w = Vector3.UnitZ * (float)Math.Cos(precession) + b * (float)Math.Sin(precession);
            //Gram-Schmidt 保正交
            Vector3 e2v = w - a * Vector3.Dot(w, a);
            if (e2v.LengthSquared() < 1e-6f) {
                e2v = b;
            }
            e2v.Normalize();
            e1 = a;
            e2 = e2v;
        }

        /// <summary>透视投影:3D 局部点→屏平面偏移;scale 同步给宽度</summary>
        internal static Vector2 Project(Vector3 p, out float scale) {
            return Project(p, FocalLength, out scale);
        }

        /// <summary>指定焦距的透视投影(大结构须用远大于自身半径的焦距,防近平面除法爆炸)</summary>
        internal static Vector2 Project(Vector3 p, float focal, out float scale) {
            scale = focal / MathHelper.Max(focal + p.Z, focal * 0.12f);
            return new Vector2(p.X, p.Y) * scale;
        }

        /// <summary>纵深光照:近亮远暗</summary>
        internal static float DepthLit(float z, float radius) {
            float t = MathHelper.Clamp((z / MathHelper.Max(radius, 1f) + 1f) * 0.5f, 0f, 1f);
            return MathHelper.Lerp(1.14f, 0.38f, t);
        }
    }
}
