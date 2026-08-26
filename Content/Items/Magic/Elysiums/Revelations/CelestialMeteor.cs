using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Revelations
{
    /// <summary>
    /// 天体陨石：自天穹坠向落点的鎏金天体，坠速渐增拖出焰尾，
    /// 触地或近目标时炸开冲击圈。ai[0]/ai[1]=落点坐标
    /// </summary>
    internal class CelestialMeteor : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private Vector2 TargetPoint => new(Projectile.ai[0], Projectile.ai[1]);

        private const float ImpactRadius = 150f;
        private const int ImpactLife = 26;

        //-1=坠落中，>=0 冲击计时
        private int impactTimer = -1;
        private float spinPhase;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            spinPhase += 0.06f;

            if (impactTimer >= 0) {
                impactTimer++;
                if (impactTimer >= ImpactLife) {
                    Projectile.Kill();
                }
                return;
            }

            //坠落：朝落点复利加速，微微追正
            Vector2 toTarget = TargetPoint - Projectile.Center;
            float dist = toTarget.Length();
            Vector2 dir = toTarget.SafeNormalize(Vector2.UnitY);
            float speed = Math.Min(Projectile.velocity.Length() * 1.045f + 0.6f, 34f);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity.SafeNormalize(dir), dir, 0.12f) * speed;
            Projectile.rotation = Projectile.velocity.ToRotation();

            //焰尾光尘
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 tailPos = Projectile.Center - Projectile.velocity * 0.6f + Main.rand.NextVector2Circular(10f, 10f);
                PRTLoader.NewParticle<PRT_Light>(tailPos, -Projectile.velocity * 0.08f
                    , new Color(255, 190, 110), Main.rand.NextFloat(0.26f, 0.44f))?.Configure(Main.rand.Next(14, 22), 0.9f);
            }

            Lighting.AddLight(Projectile.Center, 0.9f, 0.7f, 0.4f);

            //近落点或触及实体地形：起爆
            if (dist < 40f || speed * 1.2f > dist
                || Framing.GetTileSafely(Projectile.Center + dir * 24f).HasSolidTile()) {
                BeginImpact();
            }
        }

        private void BeginImpact() {
            impactTimer = 0;
            Projectile.velocity = Vector2.Zero;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.3f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
            Main.player[Projectile.owner].CWR().ScreenShakeValue =
                Math.Max(Main.player[Projectile.owner].CWR().ScreenShakeValue, 4f);

            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center
                    , angle.ToRotationVector2() * Main.rand.NextFloat(4f, 9f)
                    , new Color(255, 210, 120), Main.rand.NextFloat(0.7f, 1.2f))?.Configure(true, Main.rand.Next(18, 30));
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(40f, 20f)
                    , new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(2f, 6f))
                    , new Color(255, 226, 160), Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(26, 44), 0.95f);
            }
        }

        /// <summary>坠落中按天体本体判定，冲击时按圆域判定</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (impactTimer < 0) {
                return null;//坠落中用默认碰撞箱
            }
            if (impactTimer > 6) {
                return false;
            }
            Vector2 nearest = new(MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right)
                , MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(Projectile.Center, nearest) <= ImpactRadius;
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect effect = EffectLoader.CelestialStar?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            SpriteBatch sb = Main.spriteBatch;

            if (impactTimer >= 0) {
                //冲击圈 + 白闪核
                float prog = impactTimer / (float)ImpactLife;
                float fade = 1f - prog;
                ShockRingDraw.Draw(sb, Projectile.Center, MathHelper.Lerp(40f, ImpactRadius + 40f, VaultUtils.EaseOutCubic(prog))
                    , 10f, new Color(255, 244, 210), new Color(255, 196, 96), new Color(150, 90, 40)
                    , fade * 0.9f, innerGlow: 0.4f, timeSeed: Projectile.identity * 0.19f);
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    sb.Draw(glow, Projectile.Center - Main.screenPosition, null
                        , new Color(255, 235, 180) with { A = 0 } * (0.85f * fade), 0f
                        , glow.Size() / 2f, 1.1f * fade + 0.2f, SpriteEffects.None, 0f);
                }
                return false;
            }

            if (effect == null || canvas == null || noise == null) {
                return false;
            }

            //坠落天体：旋转画布让尾焰沿速度反向
            float quadSize = 190f;
            effect.CurrentTechnique = effect.Techniques["CelestialBody"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + spinPhase);
            effect.Parameters["fadeAlpha"]?.SetValue(1f);
            effect.Parameters["fallSpeed"]?.SetValue(Math.Min(Projectile.velocity.Length() / 30f, 1f));
            effect.Parameters["coreColor"]?.SetValue(new Vector3(1f, 0.98f, 0.92f));
            effect.Parameters["surfaceColor"]?.SetValue(new Vector3(1f, 0.82f, 0.4f));
            effect.Parameters["coronaColor"]?.SetValue(new Vector3(1f, 0.55f, 0.25f));
            effect.Parameters["trailColor"]?.SetValue(new Vector3(1f, 0.72f, 0.35f));
            effect.Parameters["sphereRadius"]?.SetValue(0.16f);
            effect.Parameters["coronaWidth"]?.SetValue(0.07f);
            effect.Parameters["intensity"]?.SetValue(1f);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();
            //贴图空间尾焰朝+Y上方，旋转对齐速度反向
            sb.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White,
                Projectile.rotation - MathHelper.PiOver2, canvas.Size() * 0.5f, quadSize, SpriteEffects.None, 0f);
            sb.End();

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
