using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 灵慰领域：阿比盖尔命中点留下的灵光圈（50 帧，半径 90）。本身不伤害；
    /// 圈内自家仆从 +10% 由 <see cref="MinionDoctrine.ApplyCommandBonuses"/> 在 owner 端统一查询。
    /// 真弹幕承载，队友可见
    /// </summary>
    internal class GsSoulSolaceProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        internal const float FieldRadius = 90f;
        internal const int FieldFrames = 50;

        private static readonly Color SoulTeal = new(120, 226, 210);
        private static readonly Color SoulViolet = new(150, 120, 226);

        private ref float Life => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.6733f % MathHelper.TwoPi;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FieldFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, SoulTeal.ToVector3() * 0.18f);
            //圈缘灵火缓升（每帧 ≤1）
            if (Main.rand.NextBool(3)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                PRTLoader.NewParticle<PRT_SoulLight>(
                    Projectile.Center + ang.ToRotationVector2()
                        * Main.rand.NextFloat(FieldRadius * 0.5f, FieldRadius * 0.95f),
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)),
                    Main.rand.NextBool() ? SoulTeal : SoulViolet,
                    Main.rand.NextFloat(0.3f, 0.55f));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D soft = CWRAsset.Extra_98?.Value;
            if (glow == null || soft == null) {
                return false;
            }
            float fadeIn = MathHelper.Clamp(Life / 8f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
            float fade = fadeIn * fadeOut;
            float breathe = 1f + 0.05f * (float)Math.Sin(Life * 0.16f + Seed);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float span = FieldRadius / (glow.Width * 0.5f) * breathe;

            //灵光底盘（加色）+ 圈心灵核
            Main.EntitySpriteDraw(glow, pos, null, (SoulTeal with { A = 0 }) * (0.3f * fade),
                0f, glow.Size() / 2f, span, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, (SoulViolet with { A = 0 }) * (0.22f * fade),
                0f, glow.Size() / 2f, span * 0.72f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(soft, pos, null, (SoulTeal * 0.4f) * fade,
                Seed + Life * 0.02f, soft.Size() / 2f, 0.16f * breathe, SpriteEffects.None, 0);
            return false;
        }
    }
}
