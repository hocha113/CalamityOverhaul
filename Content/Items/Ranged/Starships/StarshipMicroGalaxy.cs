using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Starships
{
    //陨石雨命中后汇聚于光标处的微型星系：毁灭性范围伤害
    internal class StarshipMicroGalaxy : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float TargetX => ref Projectile.ai[0];
        private ref float TargetY => ref Projectile.ai[1];

        //总寿命与三阶段切分
        private const int TotalLife = 420;
        private const int ConvergeEnd = 90;
        private const int SustainEnd = 330;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults() {
            Projectile.width = 320;
            Projectile.height = 320;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.hide = true;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 12;
        }

        public override bool ShouldUpdatePosition() => false;

        private int Age => TotalLife - Projectile.timeLeft;

        private float Phase01 {
            get {
                int age = Age;
                if (age < ConvergeEnd) {
                    return age / (float)ConvergeEnd;
                }
                if (age < SustainEnd) {
                    return 1f;
                }
                return 1f - (age - SustainEnd) / (float)(TotalLife - SustainEnd);
            }
        }

        public override void AI() {
            Vector2 target = new(TargetX, TargetY);
            Projectile.Center = target;

            int age = Age;

            if (age == 0) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f, Pitch = -0.5f }, Projectile.Center);
            }
            if (age == ConvergeEnd) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyScream with { Volume = 1.0f, Pitch = -0.3f }, Projectile.Center);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.7f, 0.6f, 1f) * Phase01);

            //汇聚阶段吸入粒子
            if (age < ConvergeEnd) {
                for (int i = 0; i < 6; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(220f, 360f);
                    Vector2 pos = Projectile.Center + ang.ToRotationVector2() * dist;
                    Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 9f);
                    Dust d = Dust.NewDustPerfect(pos, DustID.PinkStarfish, vel, 80, default, 1.4f);
                    d.noGravity = true;
                }
            }
            //持续阶段旋转粒子
            else if (age < SustainEnd) {
                for (int i = 0; i < 4; i++) {
                    float ang = Main.GameUpdateCount * 0.08f + i * MathHelper.PiOver2;
                    float r = 80f + (float)Math.Sin(Main.GameUpdateCount * 0.1f + i) * 30f;
                    Vector2 pos = Projectile.Center + ang.ToRotationVector2() * r;
                    Vector2 tangent = (ang + MathHelper.PiOver2).ToRotationVector2() * 6f;
                    Dust d = Dust.NewDustPerfect(pos, DustID.YellowStarDust, tangent, 60, default, 1.3f);
                    d.noGravity = true;
                }
            }
        }

        public override bool? CanDamage() {
            int age = Age;
            return age >= ConvergeEnd && age < SustainEnd;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = 180f * Phase01;
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, r, targetHitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<HyperDisintegration>(), 600);
            //吸入微量减速
            target.velocity = Vector2.Lerp(target.velocity, (Projectile.Center - target.Center).SafeNormalize(Vector2.Zero) * 2f, 0.1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D twill = CWRUtils.GetT2DValue(CWRConstant.Masking + "TransverseTwill");
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float time = Main.GameUpdateCount * 0.02f;
            float phase = Phase01;

            //吸积盘着色器绘制
            DrawAccretionDisk(sb, twill, time, phase);

            //中心黑洞/光球
            sb.Draw(glow, drawPos, null, new Color(255, 220, 255, 0) * phase * 0.85f, 0f, glow.Size() * 0.5f, 3.0f * phase, SpriteEffects.None, 0);
            sb.Draw(glow, drawPos, null, new Color(140, 90, 255, 0) * phase * 0.95f, 0f, glow.Size() * 0.5f, 2.0f * phase, SpriteEffects.None, 0);
            sb.Draw(glow, drawPos, null, Color.Black with { A = 0 } * phase, 0f, glow.Size() * 0.5f, 1.2f * phase, SpriteEffects.None, 0);
            return false;
        }

        private void DrawAccretionDisk(SpriteBatch sb, Texture2D noise, float time, float phase) {
            Effect shader = EffectLoader.AccretionDisk?.Value;
            if (shader == null || noise == null) {
                return;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Matrix finalMatrix = Main.GameViewMatrix.TransformationMatrix * Matrix.CreateOrthographicOffCenter(
                0, Main.screenWidth, Main.screenHeight, 0, -1, 1);
            shader.Parameters["transformMatrix"]?.SetValue(finalMatrix);
            shader.Parameters["uTime"]?.SetValue(time);
            shader.Parameters["rotationSpeed"]?.SetValue(3f);
            shader.Parameters["innerRadius"]?.SetValue(0.08f);
            shader.Parameters["outerRadius"]?.SetValue(0.85f);
            shader.Parameters["brightness"]?.SetValue(1.8f * phase);
            shader.Parameters["distortionStrength"]?.SetValue(0.25f);
            shader.Parameters["noiseTexture"]?.SetValue(noise);
            shader.Parameters["centerPos"]?.SetValue(Projectile.Center - Main.screenPosition);
            shader.Parameters["innerColor"]?.SetValue(new Color(230, 180, 255).ToVector4());
            shader.Parameters["midColor"]?.SetValue(new Color(120, 80, 255).ToVector4());
            shader.Parameters["outerColor"]?.SetValue(new Color(40, 0, 90).ToVector4());

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            if (shader.CurrentTechnique.Passes["AccretionDiskPass"] is EffectPass pass) {
                pass.Apply();
            }
            else {
                shader.CurrentTechnique.Passes[0].Apply();
            }

            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = noise.Size() * 0.5f;
            float scale = phase * 2.6f;

            for (int i = 0; i < 10; i++) {
                sb.Draw(noise, drawPosition, null, new Color(180, 120, 255, 180)
                    , i * 0.18f + time, origin, scale * (0.75f + i * 0.18f), SpriteEffects.None, 0);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
