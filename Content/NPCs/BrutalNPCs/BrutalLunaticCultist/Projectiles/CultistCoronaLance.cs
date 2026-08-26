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
    /// 冕矛:自主星表面径向喷发的辐射矛(蚀祭/合相共用)<br/>
    /// ai[0]=角度 ai[1]=锚定星球whoAmI ai[2]=阶段色<br/>
    /// 生命:预警细丝 18f→喷发 24f→塌缩 8f;伤害窗=喷发帧,判定线藏在可见焰体内
    /// </summary>
    internal class CultistCoronaLance : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int WarnFrames = 18;
        internal const int FireFrames = 24;
        internal const int CollapseFrames = 8;
        internal const int Lifetime = WarnFrames + FireFrames + CollapseFrames;
        private const float LanceLength = 1900f;
        /// <summary>判定线宽:窄于可见焰体(quad 高 110,亮体≈55px)</summary>
        private const float HitWidth = 34f;

        private float Angle => Projectile.ai[0];
        private int PlanetWho => (int)Projectile.ai[1];
        private int Palette => (int)Projectile.ai[2];
        private float Age => Lifetime - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
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

        private float PlanetVisRadius(Projectile planet) {
            return planet.ModProjectile is CultistPlanetProj planetProj
                ? planetProj.VisRadius * planet.scale : 200f;
        }

        public override void AI() {
            Projectile planet = Planet;
            if (planet == null) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = planet.Center;
            Projectile.velocity = Vector2.Zero;

            //喷发拍(各端本地)
            if ((int)Age == WarnFrames) {
                CultistMotion.Shake(planet.Center + Angle.ToRotationVector2() * PlanetVisRadius(planet), 3f, 8,
                    Angle.ToRotationVector2());
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = -0.35f }, planet.Center);
                }
            }
            Lighting.AddLight(planet.Center + Angle.ToRotationVector2() * PlanetVisRadius(planet),
                CultistMotion.PhaseCore(Palette).ToVector3() * 0.6f);
        }

        /// <summary>伤害窗=喷发可见窗</summary>
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
            Vector2 dir = Angle.ToRotationVector2();
            Vector2 start = planet.Center + dir * PlanetVisRadius(planet) * 0.85f;
            Vector2 end = start + dir * LanceLength;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, HitWidth, ref point);
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
            float arm = age < WarnFrames ? MathHelper.Clamp(age / (WarnFrames * 0.6f), 0f, 1f) : 0f;
            //宽度生命周期:5 帧展开→保持→塌缩收根
            float env;
            if (age < WarnFrames) {
                env = 0f;
            }
            else if (age < WarnFrames + 5f) {
                env = (age - WarnFrames) / 5f;
            }
            else if (age < WarnFrames + FireFrames) {
                env = 1f;
            }
            else {
                env = MathHelper.Clamp(1f - (age - WarnFrames - FireFrames) / CollapseFrames, 0f, 1f);
            }

            Color mid = CultistMotion.PhaseCore(Palette);
            Color bright = Color.Lerp(mid, Color.White, 0.45f);
            Color deep = Color.Lerp(CultistMotion.PhaseEdge(Palette), Color.Black, 0.55f);
            Vector2 dir = Angle.ToRotationVector2();
            Vector2 rootPos = planet.Center + dir * PlanetVisRadius(planet) * 0.85f;

            fx.CurrentTechnique = fx.Techniques["TechLance"];
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uAlpha"]?.SetValue(1f);
            fx.Parameters["uColDeep"]?.SetValue(deep.ToVector3());
            fx.Parameters["uColMid"]?.SetValue(mid.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(bright.ToVector3());
            fx.Parameters["uColHot"]?.SetValue(new Vector3(1f, 0.97f, 0.88f));
            fx.Parameters["uCharge"]?.SetValue(0.8f);
            fx.Parameters["uArm"]?.SetValue(arm);
            fx.Parameters["uEnv"]?.SetValue(env);
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity % 100 * 0.073f);
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
            //quad:根在星缘,u 沿矛;高 110px(判定 34 藏于亮体)
            sb.Draw(canvas, rootPos - Main.screenPosition, null, Color.White, Angle,
                new Vector2(0f, canvas.Height * 0.5f),
                new Vector2(LanceLength / canvas.Width, 110f / canvas.Height), SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
