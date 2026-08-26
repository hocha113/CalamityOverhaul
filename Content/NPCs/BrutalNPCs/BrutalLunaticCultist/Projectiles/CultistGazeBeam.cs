using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 凝视光束:月瞳自星心扫出的死光(月明凝视/合相共用)<br/>
    /// ai[0]=起始角(预警期钉死=预告即承诺) ai[1]=扫速rad/f(签名即方向) ai[2]=锚定星球whoAmI<br/>
    /// 生命:预警细丝 30f→扫射 150f→塌缩 10f;扫速声明恒定,跑在光前即安全
    /// </summary>
    internal class CultistGazeBeam : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int WarnFrames = 30;
        internal const int FireFrames = 150;
        internal const int CollapseFrames = 10;
        internal const int Lifetime = WarnFrames + FireFrames + CollapseFrames;
        private const float BeamLength = 2100f;
        /// <summary>判定线宽:可见 quad 高 150,亮体≈中心 70px</summary>
        private const float HitWidth = 56f;

        private float StartAngle => Projectile.ai[0];
        private float SweepRate => Projectile.ai[1];
        private int PlanetWho => (int)Projectile.ai[2];
        private float Age => Lifetime - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        private Projectile Planet {
            get {
                if (PlanetWho < 0 || PlanetWho >= Main.maxProjectiles) {
                    return null;
                }
                Projectile planet = Main.projectile[PlanetWho];
                return planet.active && planet.type == ModContent.ProjectileType<CultistPlanetProj>() ? planet : null;
            }
        }

        /// <summary>当前扫角:预警钉死起始角,开火后匀速扫</summary>
        internal float CurrentAngle => StartAngle + SweepRate * MathHelper.Max(Age - WarnFrames, 0f);

        public override void AI() {
            Projectile planet = Planet;
            if (planet == null) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = planet.Center;
            Projectile.velocity = Vector2.Zero;
            float age = Age;

            //睁眼拍
            if ((int)age == WarnFrames) {
                CultistScreenFX.PushFlash(0.4f);
                CultistMotion.Shake(planet.Center, 7f, 16);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1.2f, Pitch = -0.55f }, planet.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.5f }, planet.Center);
                }
            }
            //扫射低鸣
            if (age > WarnFrames && (int)age % 40 == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.45f, Pitch = -0.6f }, planet.Center);
            }

            Lighting.AddLight(planet.Center + CurrentAngle.ToRotationVector2() * 300f,
                CultistMotion.MoonCore.ToVector3() * 0.8f);
        }

        public override bool CanHitPlayer(Player target) {
            float age = Age;
            return age >= WarnFrames && age < WarnFrames + FireFrames;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float age = Age;
            if (age < WarnFrames || age >= WarnFrames + FireFrames) {
                return false;
            }
            Projectile planet = Planet;
            if (planet == null) {
                return false;
            }
            Vector2 dir = CurrentAngle.ToRotationVector2();
            Vector2 start = planet.Center + dir * 40f;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, start + dir * BeamLength, HitWidth, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            Projectile planet = Planet;
            if (planet == null) {
                return false;
            }
            Effect fx = EffectLoader.CultistOrrery?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || canvas == null || noise == null) {
                return false;
            }

            float age = Age;
            float arm = age < WarnFrames ? MathHelper.Clamp(age / (WarnFrames * 0.7f), 0f, 1f) : 0f;
            float env;
            if (age < WarnFrames) {
                env = 0f;
            }
            else if (age < WarnFrames + 6f) {
                env = (age - WarnFrames) / 6f;
            }
            else if (age < WarnFrames + FireFrames) {
                env = 1f;
            }
            else {
                env = MathHelper.Clamp(1f - (age - WarnFrames - FireFrames) / CollapseFrames, 0f, 1f);
            }

            Color mid = CultistMotion.MoonCore;
            Color bright = new(214, 255, 240);
            Color deep = new(16, 32, 28);
            float angle = CurrentAngle;

            fx.CurrentTechnique = fx.Techniques["TechLance"];
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uAlpha"]?.SetValue(1f);
            fx.Parameters["uColDeep"]?.SetValue(deep.ToVector3());
            fx.Parameters["uColMid"]?.SetValue(mid.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(bright.ToVector3());
            fx.Parameters["uColHot"]?.SetValue(new Vector3(0.96f, 1f, 0.98f));
            fx.Parameters["uCharge"]?.SetValue(1f);
            fx.Parameters["uArm"]?.SetValue(arm);
            fx.Parameters["uEnv"]?.SetValue(env);
            fx.Parameters["uSeed"]?.SetValue(0.83f);
            fx.Parameters["uProgress"]?.SetValue(1f);
            fx.Parameters["uDash"]?.SetValue(0f);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, planet.Center - Main.screenPosition, null, Color.White, angle,
                new Vector2(0f, canvas.Height * 0.5f),
                new Vector2(BeamLength / canvas.Width, 150f / canvas.Height), SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
