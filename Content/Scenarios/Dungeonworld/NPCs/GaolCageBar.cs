using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 囚笼链栏：合围圈上的一节静止鬼链（生成即定位不再动，零同步漂移）。
    /// 淡入 20 帧无伤 → 锁定 160 帧为伤害段 → 锈解崩散。ai[0]=栏杆倾角（沿圈切向）
    /// </summary>
    internal class GaolCageBar : GaolModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int FadeInFrames = 20;
        private const int HoldEnd = 180;
        private const int LifeTotal = 196;
        private const float BarHalfLen = 52f;

        private ref float Life => ref Projectile.localAI[0];
        private float BarRot => Projectile.ai[0];

        private Vector2 BarDir => BarRot.ToRotationVector2();
        private Vector2 EndA => Projectile.Center - BarDir * BarHalfLen;
        private Vector2 EndB => Projectile.Center + BarDir * BarHalfLen;

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = LifeTotal;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;
            int t = (int)Life;

            if (t == FadeInFrames) {
                //锁定拍：链栏坐实（合围一圈同拍响，声道数封顶防炸耳）
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.35f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
            }

            //锈解期掉屑
            if (!Main.dedServ && t > HoldEnd && t % 4 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Vector2.Lerp(EndA, EndB, Main.rand.NextFloat()),
                    new Vector2(0f, Main.rand.NextFloat(0.7f, 1.6f)),
                    DeepGaolWraith.IronDeep * 0.8f, Main.rand.NextFloat(0.28f, 0.45f))
                    ?.Configure(Main.rand.Next(12, 22), 0f);
            }

            if (t >= FadeInFrames && t <= HoldEnd) {
                Lighting.AddLight(Projectile.Center, 0.14f, 0.06f, 0.1f);
            }
        }

        /// <summary>淡入与锈解不打人，只有锁定段是栏</summary>
        public override bool? CanDamage() => Life >= FadeInFrames && Life <= HoldEnd ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                EndA, EndB, 12f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D chainTex = TextureAssets.Chain22?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (chainTex == null || glow == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            int t = (int)Life;

            float alpha = t < FadeInFrames
                ? 0.15f + 0.55f * (t / (float)FadeInFrames)
                : MathHelper.Clamp((LifeTotal - t) / 14f, 0f, 1f);
            //坐实前的落位过冲
            float scale = t < FadeInFrames + 6
                ? 1f + 0.1f * MathF.Sin(MathHelper.Pi * MathHelper.Clamp((t - FadeInFrames + 6) / 12f, 0f, 1f))
                : 1f;
            //锁定段轻微战栗，牢笼是活的
            float shiver = t >= FadeInFrames && t <= HoldEnd
                ? MathF.Sin(Main.GlobalTimeWrappedHourly * 22f + Seed * 5f) * 0.8f
                : 0f;
            Vector2 perp = BarDir.RotatedBy(MathHelper.PiOver2);

            float linkStep = MathF.Max(10f, chainTex.Height - 2f);
            int links = Math.Max(2, (int)(BarHalfLen * 2f / linkStep));
            Vector2 origin = chainTex.Size() * 0.5f;
            Color tint = lightColor.MultiplyRGB(DeepGaolWraith.IronMul) * alpha;

            for (int k = 0; k < links; k++) {
                Vector2 p = Vector2.Lerp(EndA, EndB, (k + 0.5f) / links) + perp * shiver;
                sb.Draw(chainTex, p - Main.screenPosition, null, tint,
                    BarRot + MathHelper.PiOver2, origin, scale, SpriteEffects.None, 0f);
            }

            //两端粉光钉点（A=0 加色）
            Vector2 gOrigin = glow.Size() * 0.5f;
            Color pin = (DeepGaolWraith.GaolPink with { A = 0 }) * (0.5f * alpha);
            sb.Draw(glow, EndA - Main.screenPosition, null, pin, 0f, gOrigin,
                new Vector2(9f * 2f / glow.Width), SpriteEffects.None, 0f);
            sb.Draw(glow, EndB - Main.screenPosition, null, pin, 0f, gOrigin,
                new Vector2(9f * 2f / glow.Width), SpriteEffects.None, 0f);
            return false;
        }
    }
}
