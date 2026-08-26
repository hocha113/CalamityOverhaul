using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles
{
    /// <summary>
    /// 妖灵弓「双灵环绕」处决光灵：绕标记敌公转 2 秒的彩虹光珠，每 30 帧一次接触伤。
    /// ai[0] = 目标 NPC whoAmI（跨端一致），ai[1] = 初相位（双灵对置）。
    /// 位置由目标位置 + 确定相位驱动（各端同步量），不走速度积分。
    /// 数量护栏：生成端按 ownedProjectileCounts 限 4/玩家
    /// </summary>
    internal class GsFaeOrbiterProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float TargetIndex => ref Projectile.ai[0];

        private ref float PhaseOffset => ref Projectile.ai[1];

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>彩虹相位色（identity 定相，各端一致）</summary>
        private Color RainbowColor(float shift = 0f) {
            float hue = (Main.GlobalTimeWrappedHourly * 0.42f + Projectile.identity * 0.173f + shift) % 1f;
            return Main.hslToRgb(hue, 1f, 0.62f);
        }

        public override void AI() {
            Life++;
            int idx = (int)TargetIndex;
            NPC target = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
            if (target == null || !target.active) {
                //目标失效：光灵散逸
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 8);
                Projectile.Center += new Vector2(0f, -1.2f);
                return;
            }
            //公转：角速度 + 呼吸半径，全部确定性输入
            float angle = PhaseOffset + Life * 0.085f;
            float radius = 46f + 7f * MathF.Sin(Life * 0.11f + Projectile.identity * 0.61f);
            Projectile.Center = target.Center + angle.ToRotationVector2() * radius;

            if (!VaultUtils.isServer && Life % 6 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, Main.rand.NextVector2Circular(0.5f, 0.5f),
                    RainbowColor(), 0.6f)?.Configure(RainbowColor(0.3f), 16);
            }
            Lighting.AddLight(Projectile.Center, RainbowColor().ToVector3() * 0.3f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() * 0.5f;
            float fadeIn = MathHelper.Clamp(Life / 8f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
            float fade = fadeIn * fadeOut;
            float pulse = 0.85f + 0.15f * MathF.Sin(Life * 0.23f + Projectile.identity);
            //SoftGlow 黑底灰度：染色必须 A=0 走加色，否则黑块
            Color outer = RainbowColor() with { A = 0 };
            Color core = Color.White with { A = 0 };

            Main.EntitySpriteDraw(glow, pos, null, outer * (0.5f * fade), 0f, origin, 0.62f * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, outer * (0.85f * fade), 0f, origin, 0.36f * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, core * (0.55f * fade), 0f, origin, 0.17f * pulse, SpriteEffects.None, 0);
            if (star != null) {
                float rot = Life * 0.05f + Projectile.identity * 0.8f;
                Main.EntitySpriteDraw(star, pos, null, (RainbowColor(0.5f) with { A = 0 }) * (0.45f * fade),
                    rot, star.Size() * 0.5f, 0.12f * pulse, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center,
                    Main.rand.NextVector2Circular(1.2f, 1.2f),
                    RainbowColor(i * 0.2f), 0.1f)?.Configure(12, 0.8f);
            }
        }
    }
}
