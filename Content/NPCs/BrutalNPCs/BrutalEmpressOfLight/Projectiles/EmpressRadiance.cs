using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>
    /// 辉光爆放：一次性棱彩绽放（涟漪环+放射星芒+光尘），纯演出载体，零伤害；
    /// 走弹幕同步所以各端都看得到；ai[0]=最大半径 ai[1]=寿命 ai[2]=色相种子
    /// </summary>
    internal class EmpressRadiance : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private ref float Timer => ref Projectile.localAI[0];
        private float MaxRadius => Math.Max(Projectile.ai[0], 60f);
        private int Life => Math.Max((int)Projectile.ai[1], 12);
        private float HueSeed => Projectile.ai[2];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.damage = 0;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
        }

        public override void AI() {
            if (Timer == 0f) {
                Projectile.timeLeft = Life;
                //首帧光尘绽放，客户端
                if (!VaultUtils.isServer) {
                    int motes = (int)MathHelper.Clamp(MaxRadius * 0.09f, 14f, 46f);
                    for (int i = 0; i < motes; i++) {
                        float hue = (HueSeed + i / (float)motes) % 1f;
                        Vector2 vel = (MathHelper.TwoPi / motes * i).ToRotationVector2() * Main.rand.NextFloat(4f, 11f);
                        PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center, vel,
                            Main.hslToRgb(hue, 1f, 0.62f), Main.rand.NextFloat(0.9f, 1.5f))?.Configure(26, hue);
                    }
                    PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero, Color.White,
                        MaxRadius / 180f)?.Configure(Math.Min(Life, 26), HueSeed);
                }
            }
            Timer++;
            Projectile.velocity = Vector2.Zero;

            float p = Timer / Life;
            Lighting.AddLight(Projectile.Center, Main.hslToRgb(HueSeed, 0.8f, 0.7f).ToVector3() * (1f - p) * 1.4f);

            if (Timer >= Life) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float p = MathHelper.Clamp(Timer / Life, 0f, 1f);
            float fade = (1f - p) * (1f - p);
            float radius = MaxRadius * VaultUtils.EaseOutCubic(p);

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D flare = CWRAsset.StarFlare01.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Color prismCol = Main.hslToRgb(HueSeed, 0.85f, 0.68f);
            Color prism = prismCol with { A = 0 };
            Color white = Color.White with { A = 0 };

            //扩散环双层：白环+色环滞后（共享冲击环 shader，撕裂缘；可见半径与旧 Ring01 对齐）
            float ringR = radius * 0.83f;
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, ringR, ringR * 0.22f,
                Color.White, Color.White, prismCol, 0.8f * fade,
                timeSeed: Projectile.whoAmI * 0.37f);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, ringR * 0.86f, ringR * 0.2f,
                Color.White, prismCol, prismCol, 0.65f * fade,
                innerGlow: 0.3f, timeSeed: Projectile.whoAmI * 0.37f + 5.1f);

            //中心闪耀：星芒旋转收缩
            float flareScale = (0.4f + 0.6f * (1f - p)) * MaxRadius / 340f;
            Main.EntitySpriteDraw(flare, drawPos, null, white * (0.85f * fade), p * 0.7f,
                flare.Size() / 2f, flareScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, prism * fade, 0f, glow.Size() / 2f,
                MaxRadius / 90f * (1f - p * 0.5f), SpriteEffects.None, 0);
            return false;
        }
    }
}
