using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 血柱的飞沫编舞:着色器柱体只负责"连续的那一股",液体感的另一半靠有物理的飞沫
    /// (<see cref="PRT_KikasaBloodSpray"/>)——起柱时水面被顶起的溅裙与随头冲上去再抛物落回的血团、
    /// 持续段柱头不停甩滴、两翼回落的血丝、塌回时整柱碎成落血。全部落回湖面荡微圈。
    /// 普攻血柱与血形态三泉共用;只在观看端调用,数量随柱高缩放
    /// </summary>
    internal static class KikasaBloodColumnFX
    {
        private static float SizeMul(float heightPx)
            => MathHelper.Clamp(heightPx / KikasaBloodForm.ColumnHeightMax, 0.35f, 1.1f);

        private static Color PickColor()
            => Main.rand.NextBool(3) ? KikasaInk.BloodBright : KikasaInk.BloodBody;

        /// <summary>
        /// 起柱拍:溅裙(近水平向两侧掀起的薄片,速度快、寿命短)+ 随头冲天的血团
        /// (上抛 9~16px/f,越过柱头再抛物落回,是"液体被顶上天"的第一证据)
        /// </summary>
        public static void Erupt(Vector2 root, float widthPx, float heightPx, float ke, float lakeY) {
            float k = SizeMul(heightPx);
            int skirt = (int)(8 * k) + 2;
            for (int i = 0; i < skirt; i++) {
                float side = i % 2 == 0 ? 1f : -1f;
                float ang = -Main.rand.NextFloat(0.28f, 1.05f);
                Vector2 vel = new Vector2(MathF.Cos(ang) * side, MathF.Sin(ang))
                    * Main.rand.NextFloat(3.2f, 6.8f) * (0.8f + 0.4f * ke);
                PRTLoader.NewParticle<PRT_KikasaBloodSpray>(
                    root + new Vector2(side * Main.rand.NextFloat(0.2f, 0.6f) * widthPx, -2f),
                    vel, PickColor(), Main.rand.NextFloat(0.34f, 0.6f) * k)
                    ?.Configure(Main.rand.Next(26, 44), lakeY, true, 0.4f, 0.985f);
            }
            int up = (int)(8 * k) + 2;
            for (int i = 0; i < up; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-2.4f, 2.4f),
                    -Main.rand.NextFloat(9f, 16f) * (0.75f + 0.45f * ke) * k);
                PRTLoader.NewParticle<PRT_KikasaBloodSpray>(
                    root + new Vector2(Main.rand.NextFloat(-0.35f, 0.35f) * widthPx, -4f),
                    vel, PickColor(), Main.rand.NextFloat(0.4f, 0.72f) * k)
                    ?.Configure(Main.rand.Next(40, 64), lakeY, true);
            }
        }

        /// <summary>持续段柱头甩滴(每帧):顶上液团不断撕出的血团,略带横向,上抛后落回</summary>
        public static void ShedHead(Vector2 head, float widthPx, float heightPx, float ke, float lakeY) {
            float k = SizeMul(heightPx);
            int count = Main.rand.NextBool(3) ? 3 : 2;
            for (int i = 0; i < count; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-2.6f, 2.6f),
                    -Main.rand.NextFloat(1.6f, 4.8f) * (0.6f + 0.6f * ke));
                PRTLoader.NewParticle<PRT_KikasaBloodSpray>(
                    head + new Vector2(Main.rand.NextFloat(-0.45f, 0.45f) * widthPx, Main.rand.NextFloat(-8f, 4f)),
                    vel, PickColor(), Main.rand.NextFloat(0.3f, 0.52f) * k)
                    ?.Configure(Main.rand.Next(34, 56), lakeY, Main.rand.NextBool(3));
            }
        }

        /// <summary>两翼回落血丝(每帧一颗):自柱身随机高度的外缘剥离、贴着柱身往下落</summary>
        public static void Curtain(Vector2 root, float widthPx, float heightNowPx, float lakeY) {
            if (heightNowPx < 24f) {
                return;
            }
            float side = Main.rand.NextBool() ? 1f : -1f;
            float h = Main.rand.NextFloat(0.3f, 0.95f) * heightNowPx;
            PRTLoader.NewParticle<PRT_KikasaBloodSpray>(
                root + new Vector2(side * widthPx * Main.rand.NextFloat(0.5f, 0.7f), -h),
                new Vector2(side * Main.rand.NextFloat(0.3f, 1.1f), Main.rand.NextFloat(0.5f, 1.8f)),
                KikasaInk.BloodBody, Main.rand.NextFloat(0.26f, 0.42f))
                ?.Configure(Main.rand.Next(30, 50), lakeY, false, 0.3f, 0.99f);
        }

        /// <summary>塌回拍:失去支撑的柱身沿高度碎成落血,横向散开一点,重力接管</summary>
        public static void Collapse(Vector2 root, float widthPx, float heightNowPx, float lakeY) {
            float k = SizeMul(heightNowPx);
            int count = (int)(9 * k) + 3;
            for (int i = 0; i < count; i++) {
                float h = Main.rand.NextFloat(0.15f, 1.0f) * heightNowPx;
                Vector2 vel = new(Main.rand.NextFloat(-2.6f, 2.6f), Main.rand.NextFloat(-1.2f, 2.2f));
                PRTLoader.NewParticle<PRT_KikasaBloodSpray>(
                    root + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * widthPx, -h),
                    vel, PickColor(), Main.rand.NextFloat(0.36f, 0.66f) * k)
                    ?.Configure(Main.rand.Next(30, 52), lakeY, Main.rand.NextBool(2));
            }
        }
    }
}
