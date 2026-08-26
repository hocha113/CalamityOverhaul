using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Ranged.TiroFinales
{
    /// <summary>
    /// 环绕枪阵 3D 骨架:倾斜圆环姿态、透视投影、椭圆投影、纵深光照,
    /// 持握弹幕与渲染层共用(血统:CultistOrreryRig)<br/>
    /// 坐标约定:X 右 Y 下 Z 入屏(远),near = 负 Z;透视 scale = F / (F + z)
    /// </summary>
    internal static class TiroFinaleRig
    {
        /// <summary>枪阵槽位数</summary>
        internal const int SlotCount = 8;
        /// <summary>环半径(px)</summary>
        internal const float Radius = 132f;
        /// <summary>透视焦距(px),环径远小于焦距,近平面安全</summary>
        internal const float FocalLength = 540f;
        /// <summary>环自旋角速度(rad/帧)</summary>
        internal const float SpinRate = 0.012f;

        /// <summary>
        /// 环平面正交基:yaw 缓慢进动 + 倾角呼吸,维持"看得出是 3D"的椭圆度(压扁率约 0.38~0.55)
        /// </summary>
        internal static void GetBasis(float time, out Vector3 e1, out Vector3 e2) {
            float yaw = time * 0.0085f;
            float pitch = 1.08f + MathF.Sin(time * 0.0136f) * 0.13f;
            float cy = MathF.Cos(yaw);
            float sy = MathF.Sin(yaw);
            float cp = MathF.Cos(pitch);
            float sp = MathF.Sin(pitch);
            //RotY(yaw) * RotX(pitch) 作用于 X/Y 轴
            e1 = new Vector3(cy, 0f, -sy);
            e2 = new Vector3(sy * sp, cp, cy * sp);
        }

        /// <summary>槽位在环上的相位角(含整环自旋与逐槽微错拍)</summary>
        internal static float SlotAngle(int slot, float time) {
            return slot / (float)SlotCount * MathHelper.TwoPi + time * SpinRate
                + MathF.Sin(time * 0.031f + slot * 1.73f) * 0.05f;
        }

        /// <summary>槽位局部 3D 坐标(相对环心),半径带逐槽呼吸</summary>
        internal static Vector3 SlotLocal(int slot, float time, in Vector3 e1, in Vector3 e2, float radiusMul = 1f) {
            float angle = SlotAngle(slot, time);
            float breathe = 1f + 0.045f * MathF.Sin(time * 0.052f + slot * 2.4f);
            float r = Radius * breathe * radiusMul;
            return (e1 * MathF.Cos(angle) + e2 * MathF.Sin(angle)) * r;
        }

        /// <summary>透视投影:3D 局部点→屏平面偏移;scale 同步给宽度</summary>
        internal static Vector2 Project(Vector3 p, out float scale) {
            scale = FocalLength / MathHelper.Max(FocalLength + p.Z, FocalLength * 0.12f);
            return new Vector2(p.X, p.Y) * scale;
        }

        /// <summary>纵深光照:近亮远暗</summary>
        internal static float DepthLit(float z, float radius) {
            float t = MathHelper.Clamp((z / MathHelper.Max(radius, 1f) + 1f) * 0.5f, 0f, 1f);
            return MathHelper.Lerp(1.12f, 0.42f, t);
        }

        /// <summary>
        /// 枪管指向的屏幕表现:从局部 3D 位置指向 z=0 平面上的目标,
        /// 返回屏幕旋转与轴向缩短系数(1=完全在屏平面,越小越朝向镜头内外)
        /// </summary>
        internal static void AimScreen(in Vector3 pos3, Vector2 targetLocal2, out float rotation, out float axialK) {
            Vector3 dir = new Vector3(targetLocal2.X, targetLocal2.Y, 0f) - pos3;
            if (dir.LengthSquared() < 1e-4f) {
                dir = Vector3.UnitX;
            }
            dir.Normalize();
            //探两点求投影方向,倍率与透视无关
            Vector2 p0 = Project(pos3, out float s0);
            Vector2 p1 = Project(pos3 + dir * 60f, out _);
            Vector2 screenDir = p1 - p0;
            rotation = screenDir.ToRotation();
            axialK = MathHelper.Clamp(screenDir.Length() / (60f * s0), 0.24f, 1f);
        }

        /// <summary>枪管朝向的圆形枪口阵屏幕短轴比:sqrt(1-axialK²),越朝镜头越圆</summary>
        internal static float CircleMinorRatio(float axialK) {
            float k = MathHelper.Clamp(axialK, 0f, 1f);
            return MathHelper.Clamp(MathF.Sqrt(1f - k * k), 0.22f, 1f);
        }
    }
}
