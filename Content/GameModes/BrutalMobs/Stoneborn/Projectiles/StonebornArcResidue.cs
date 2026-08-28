using CalamityOverhaul.Content.Items.Stones;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Stoneborn.Projectiles
{
    /// <summary>
    /// 花岗岩精·俯冲残电：俯冲轨迹上滞留的电荷点，视觉为主、接触微伤。
    /// 生成即定点、无追踪、不依赖施主存活（预告由俯冲标线承担，残电落在线内）。
    /// 亮窗 <see cref="LiveFrames"/> 帧可判定，其后无害淡出（伤害窗=可见窗）。
    /// //豁免声明：电弧属光——本体为纯加色电光（M5 闪电豁免条款），不设暗壳遮挡层
    /// </summary>
    internal class StonebornArcResidue : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>判定亮窗帧</summary>
        internal const int LiveFrames = 24;
        /// <summary>无害淡出帧（总时长 30 帧=任务口径「30 帧残电」）</summary>
        internal const int FadeFrames = 6;

        private bool Live => Projectile.timeLeft > FadeFrames;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LiveFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>亮窗才有微伤判定，淡出段绝不判定</summary>
        public override bool? CanDamage() => Live ? null : false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (!Main.dedServ) {
                //电弧尘：亮窗 2 粒/帧、淡出 1 粒/帧
                int budget = Live ? 2 : 1;
                for (int i = 0; i < budget; i++) {
                    Dust arc = Dust.NewDustPerfect(
                        Projectile.Center + Main.rand.NextVector2Circular(Projectile.width * 0.4f, Projectile.height * 0.4f),
                        DustID.Electric, Main.rand.NextVector2Circular(1.2f, 1.2f), 70, default, Main.rand.NextFloat(0.5f, 0.9f));
                    arc.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.18f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float strength = Live ? 1f : Projectile.timeLeft / (float)FadeFrames;
            //确定性电闪抖动（不吃 Main.rand，各端自洽）
            float flicker = 0.55f + 0.45f * MathF.Sin((Projectile.timeLeft * 2.9f + Projectile.identity) * 1.7f);
            Color spark = GraniteMarbleVFX.GraniteSpark with { A = 0 };
            Color core = GraniteMarbleVFX.GraniteCore with { A = 0 };
            //纯加色电光双层（豁免声明见类注释）：窄亮芯 + 宽淡晕
            Main.EntitySpriteDraw(glow, drawPos, null, spark * (0.75f * strength * flicker), 0f,
                glow.Size() / 2f, new Vector2(Projectile.width * 0.9f / glow.Width, Projectile.height * 0.9f / glow.Height),
                SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, core * (0.35f * strength), 0f,
                glow.Size() / 2f, new Vector2(Projectile.width * 1.8f / glow.Width, Projectile.height * 1.8f / glow.Height),
                SpriteEffects.None, 0);
            return false;
        }
    }
}
