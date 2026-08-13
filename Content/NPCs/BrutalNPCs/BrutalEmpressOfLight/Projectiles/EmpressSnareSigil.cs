using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>
    /// 光笼捕获符印：宣告"这座笼收拢即缚"的专属预告，纯视觉零伤害；
    /// ai[0]=收拢帧数 ai[1]=色相 ai[2]=捕获半径；行为是生成参数的确定函数，各端一致
    /// </summary>
    internal class EmpressSnareSigil : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private ref float Timer => ref Projectile.localAI[0];
        private ref float ChimeStep => ref Projectile.localAI[1];
        private int Closure => Math.Max((int)Projectile.ai[0], 30);
        private float Hue => Projectile.ai[1];
        private float CaptureR => Math.Max(Projectile.ai[2], 60f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.damage = 0;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override void AI() {
            if (Timer == 0f) {
                Projectile.timeLeft = Closure + 6;
            }
            Timer++;
            Projectile.velocity = Vector2.Zero;

            float p = MathHelper.Clamp(Timer / Closure, 0f, 1f);
            float fadeIn = MathHelper.Clamp(Timer / 12f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 6f, 0f, 1f);
            Projectile.Opacity = fadeIn * fadeOut;

            //音阶催逼：越近收拢音越高越急
            int step = p < 0.25f ? 0 : p < 0.55f ? 1 : p < 0.8f ? 2 : p < 0.94f ? 3 : 4;
            if (step > (int)ChimeStep) {
                ChimeStep = step;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item163 with { Volume = 0.5f, Pitch = -0.1f + step * 0.17f, MaxInstances = 3 }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero,
                        Main.hslToRgb(Hue, 0.9f, 0.7f), 0.3f + step * 0.06f)?.Configure(12, Hue);
                }
            }

            //尾段金尘内旋：光绫在环缘凝形
            if (!VaultUtils.isServer && p > 0.5f && Main.rand.NextBool(3)) {
                Vector2 spawn = Projectile.Center + Main.rand.NextVector2CircularEdge(CaptureR, CaptureR);
                PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, (Projectile.Center - spawn) * 0.055f,
                    Main.hslToRgb(Hue, 0.9f, 0.7f), Main.rand.NextFloat(0.5f, 0.85f))?.Configure(16, Hue);
            }

            Lighting.AddLight(Projectile.Center, Main.hslToRgb(Hue, 0.85f, 0.65f).ToVector3() * (0.35f + p * 0.4f) * Projectile.Opacity);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero, Color.White, 0.55f)?.Configure(14, Hue);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center, VaultUtils.RandVr(1.5f, 4f),
                    Main.hslToRgb(Hue, 0.9f, 0.72f), Main.rand.NextFloat(0.5f, 0.9f))?.Configure(14, Hue);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float p = MathHelper.Clamp(Timer / Closure, 0f, 1f);
            float time = Main.GlobalTimeWrappedHourly;
            Color gold = Main.hslToRgb(Hue, 0.9f, 0.64f);
            Color goldAdd = gold with { A = 0 };
            Color whiteAdd = Color.White with { A = 0 };

            //捕获圈：呼吸随进度加急，末段轻微内缩——笼在收口
            float pulse = (float)Math.Sin(time * (4f + p * p * 22f) + Projectile.identity * 1.3f);
            float ringR = CaptureR * (1f - 0.04f * p * (0.5f + 0.5f * pulse));
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, ringR, CaptureR * 0.085f,
                Color.White, gold, Main.hslToRgb((Hue + 0.08f) % 1f, 0.85f, 0.6f),
                (0.2f + 0.55f * p) * Projectile.Opacity, timeSeed: Projectile.identity * 0.41f);

            //中央符印：三重星芒缓旋+柔光芯
            Texture2D star = CWRAsset.StarTexture_White.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float sigScale = 0.085f * (1f + 0.35f * p) * (1f + 0.06f * pulse);
            for (int k = 0; k < 3; k++) {
                float rot = time * 0.8f + k * MathHelper.Pi / 3f;
                Main.EntitySpriteDraw(star, drawPos, null, goldAdd * (0.55f * Projectile.Opacity), rot,
                    star.Size() / 2f, sigScale, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(star, drawPos, null, whiteAdd * (0.5f * Projectile.Opacity), time * -0.5f,
                star.Size() / 2f, sigScale * 0.5f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, goldAdd * (0.4f * Projectile.Opacity), 0f,
                glow.Size() / 2f, 0.5f + 0.2f * p, SpriteEffects.None, 0);
            return false;
        }
    }
}
