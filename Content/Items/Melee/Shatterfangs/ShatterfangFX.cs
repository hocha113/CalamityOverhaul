using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Melee.Shatterfangs
{
    /// <summary>
    /// 崩牙獠刃资产与克眼红白色板。白锋红体，区别于血腥屠刀的全红系
    /// </summary>
    internal class ShatterfangAssets
    {
        [VaultLoaden(CWRConstant.Item_Melee + "Shatterfang")]
        public static Asset<Texture2D> FullBlade { get; private set; }

        /// <summary>半刃剑贴图，崩坏态换用</summary>
        [VaultLoaden(CWRConstant.Item_Melee + "ShatterfangBroken")]
        public static Asset<Texture2D> BrokenBlade { get; private set; }

        [VaultLoaden(CWRConstant.Projectile_Melee + "ShatterfangShard1")]
        public static Asset<Texture2D> Shard1 { get; private set; }

        [VaultLoaden(CWRConstant.Projectile_Melee + "ShatterfangShard2")]
        public static Asset<Texture2D> Shard2 { get; private set; }

        [VaultLoaden(CWRConstant.Projectile_Melee + "ShatterfangBigShard")]
        public static Asset<Texture2D> BigShard { get; private set; }
    }

    /// <summary>崩牙獠刃共用色板、粒子与反馈入口</summary>
    internal static class ShatterfangFX
    {
        //克眼红白色板
        /// <summary>骨白前沿</summary>
        public static readonly Color BoneLead = new(255, 246, 232);
        /// <summary>亮猩红</summary>
        public static readonly Color ScarletBright = new(224, 62, 58);
        /// <summary>主体血红</summary>
        public static readonly Color BloodMain = new(150, 26, 36);
        /// <summary>暗血拖尾</summary>
        public static readonly Color BloodDeep = new(58, 10, 18);
        /// <summary>牙体象牙</summary>
        public static readonly Color Ivory = new(238, 224, 206);
        /// <summary>牙体暗面</summary>
        public static readonly Color IvoryDark = new(178, 152, 130);

        //刀光专用色板，白压成米白、红整体加深(2026-08-26 实机反馈：白太亮红太浅)
        /// <summary>刀光前沿米白</summary>
        public static readonly Color ArcLead = new(250, 234, 218);
        /// <summary>刀光高亮暗猩红</summary>
        public static readonly Color ArcBright = new(186, 38, 40);
        /// <summary>刀光主体深血红</summary>
        public static readonly Color ArcMain = new(112, 14, 24);
        /// <summary>刀光拖尾近黑</summary>
        public static readonly Color ArcDeep = new(40, 6, 12);

        /// <summary>
        /// 刃轴相对贴图水平线的对角偏移。刃沿左下柄→右上尖的对角走，
        /// 48×64 画布的对角是 53° 不是 45°，按真实宽高算才贴合刀光
        /// </summary>
        public static float BladeAxisOffset(Texture2D tex) => MathF.Atan2(tex.Height, tex.Width);

        /// <summary>定向震屏统一入口</summary>
        public static void Punch(Vector2 pos, Vector2 dir, float strength, float vibrationsPerSec, int frames, float falloff = 1100f) {
            if (Main.dedServ || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                pos, dir.SafeNormalize(Vector2.UnitY), strength, vibrationsPerSec, frames, falloff, "Shatterfang"));
        }

        /// <summary>
        /// 牙骨碎屑迸溅。power 0..1 规模，bloodShare 带血根碎屑占比
        /// </summary>
        public static void ChipBurst(Vector2 pos, Vector2 dir, float power, float bloodShare = 0.3f) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            int count = 4 + (int)(power * 9f);
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedByRandom(0.85f) * Main.rand.NextFloat(2.5f, 6.5f + power * 5f);
                vel.Y -= Main.rand.NextFloat(0.5f, 2f);
                bool bloody = Main.rand.NextFloat() < bloodShare;
                PRTLoader.NewParticle<PRT_ToothChip>(pos + Main.rand.NextVector2Circular(5f, 5f), vel
                    , Ivory, Main.rand.NextFloat(0.22f, 0.42f) * (0.8f + power * 0.4f))
                    ?.Configure(Main.rand.Next(24, 42), 0.22f, bloody ? Main.rand.NextFloat(0.5f, 0.9f) : 0f);
            }
            //骨粉底噪
            for (int i = 0; i < count / 2; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Bone
                    , dir.RotatedByRandom(1.1f) * Main.rand.NextFloat(1.5f, 4f), 90, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>骨灰白尘雾，碎裂余韵</summary>
        public static void BonePuff(Vector2 pos, int count, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos + Main.rand.NextVector2Circular(8f, 6f)
                    , Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1.3f)
                    , Color.White, Main.rand.NextFloat(0.07f, 0.12f) * scale)
                    ?.Configure(Main.rand.Next(18, 30), new Color(216, 204, 190), new Color(116, 100, 92), 0.012f);
            }
        }

        /// <summary>血珠+血渍喷溅，克眼血肉命中语汇</summary>
        public static void BloodBurst(Vector2 pos, Vector2 dir, float power) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            int drops = 5 + (int)(power * 7f);
            for (int i = 0; i < drops; i++) {
                Vector2 vel = dir.RotatedByRandom(0.85f) * Main.rand.NextFloat(4f, 9f + power * 5f);
                vel.Y -= Main.rand.NextFloat(0.5f, 2f);
                Color c = Main.rand.NextBool(4) ? ScarletBright : (Main.rand.NextBool() ? BloodMain : BloodDeep);
                float sc = Main.rand.NextFloat(0.85f, 1.4f + power * 0.3f);
                if (!Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos, vel, c, sc)
                        ?.Configure(Main.rand.Next(36, 54), 0.42f, 0.99f, stuckLifetime: Main.rand.Next(34, 52));
                }
                else {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, c, sc)
                        ?.Configure(Main.rand.Next(18, 30), 0.30f);
                }
            }
        }
    }
}
