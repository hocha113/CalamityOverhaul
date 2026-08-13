using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles
{
    /// <summary>砸击骨刺：地面隆起→耸立→崩解；ai[0]=起刺延迟，ai[1]=高度倍率，位置锚定地表</summary>
    internal class SkeletronBoneSpike : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int GrowFrames = 8;
        private const int HoldFrames = 20;
        private const int CrumbleFrames = 12;
        private const float BaseHeight = 132f;

        private ref float Delay => ref Projectile.ai[0];
        private ref float HeightScale => ref Projectile.ai[1];
        private ref float Age => ref Projectile.localAI[0];

        private float SpikeHeight => BaseHeight * (HeightScale <= 0f ? 1f : HeightScale);

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.netImportant = true;
        }

        /// <summary>0~1 隆起进度（尖锐 ease-out）</summary>
        private float GrowProgress {
            get {
                float t = MathHelper.Clamp((Age - Delay) / GrowFrames, 0f, 1f);
                return 1f - MathF.Pow(1f - t, 5f);
            }
        }

        private bool Crumbling => Age > Delay + GrowFrames + HoldFrames;

        public override void AI() {
            Age++;
            Projectile.velocity = Vector2.Zero;

            //延迟期：地表预震（可读预告）
            if (Age <= Delay) {
                Projectile.timeLeft = (int)(Delay - Age) + GrowFrames + HoldFrames + CrumbleFrames + 4;
                if (!VaultUtils.isServer && Age % 2 == 0) {
                    Dust dust = Dust.NewDustDirect(Projectile.Center + new Vector2(-14f, -6f), 28, 6, DustID.Bone,
                        Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2.4f, -0.6f), 140, default, 1.1f);
                    dust.noGravity = false;
                }
                return;
            }

            //起刺瞬间
            if ((int)Age == (int)Delay + 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item51 with { Volume = 0.7f, Pitch = -0.7f }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_SkeleBoneChip>(Projectile.Center + new Vector2(Main.rand.NextFloat(-16f, 16f), -6f),
                        new Vector2(Main.rand.NextFloat(-2.6f, 2.6f), Main.rand.NextFloat(-5.5f, -2f)),
                        Color.White, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(28, 46));
                }
            }

            //崩解
            if (Crumbling && !VaultUtils.isServer && Age % 3 == 0) {
                float h = SpikeHeight * GrowProgress;
                PRTLoader.NewParticle<PRT_SkeleBoneChip>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), -Main.rand.NextFloat(0f, h)),
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-1f, 0.6f)),
                    Color.White, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(24, 40));
            }

            Lighting.AddLight(Projectile.Center - new Vector2(0f, SpikeHeight * 0.5f),
                SkeletronRenderHelper.GhostDeep.ToVector3() * 0.25f);
        }

        /// <summary>只有耸立主体伤人：隆起过半到崩解前</summary>
        public override bool? CanDamage() {
            if (Age <= Delay || Crumbling) {
                return false;
            }
            return GrowProgress > 0.45f ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //命中区 = 自地表向上生长的窄条
            float h = SpikeHeight * GrowProgress;
            Rectangle spikeRect = new Rectangle((int)(Projectile.Center.X - 15f), (int)(Projectile.Center.Y - h), 30, (int)h);
            return spikeRect.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Age <= Delay) {
                return false;
            }
            Main.instance.LoadProjectile(ProjectileID.Bone);
            Texture2D bone = TextureAssets.Projectile[ProjectileID.Bone].Value;
            Vector2 orig = bone.Size() / 2f;
            Vector2 basePos = Projectile.Center - Main.screenPosition;

            float grow = GrowProgress;
            float h = SpikeHeight * grow;
            float crumbleFade = Crumbling
                ? MathHelper.Clamp(1f - (Age - Delay - GrowFrames - HoldFrames) / CrumbleFrames, 0f, 1f)
                : 1f;

            //幽光衬底（隆起时最亮，预乘批 A=0 加色）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.spriteBatch.Draw(glow, basePos - new Vector2(0f, h * 0.4f), null,
                SkeletronRenderHelper.AsAdditive(SkeletronRenderHelper.GhostDeep) * (0.4f * grow * crumbleFade),
                0f, glow.Size() / 2f,
                new Vector2(1.4f, h / glow.Height * 1.4f + 0.3f), SpriteEffects.None, 0f);

            //骨簇塔：三柱交叠，中柱最高，侧柱外倾
            for (int i = -1; i <= 1; i++) {
                float lean = i * 0.24f;
                float segH = h * (i == 0 ? 1f : 0.62f);
                int segs = Math.Max(1, (int)(segH / 22f));
                for (int s = 0; s < segs; s++) {
                    float t = s / (float)segs;
                    Vector2 pos = basePos + new Vector2(i * 12f + MathF.Sin(lean) * segH * t, -segH * t);
                    float segScale = MathHelper.Lerp(1.05f, 0.5f, t) * MathHelper.Clamp(grow * 1.2f, 0f, 1f);
                    Color col = Color.Lerp(SkeletronRenderHelper.BoneShadow, SkeletronRenderHelper.BonePale, t)
                        .MultiplyRGB(lightColor) * crumbleFade;
                    Main.spriteBatch.Draw(bone, pos, null, col, lean + (s % 2 == 0 ? 0.5f : -0.4f),
                        orig, segScale, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
