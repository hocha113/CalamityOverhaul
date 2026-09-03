using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 冕矛:自主星表面径向喷发的星质光矛,月总幻影死光语法(蚀祭/合相共用)<br/>
    /// ai[0]=角度 ai[1]=锚定星球whoAmI ai[2]=阶段色<br/>
    /// 生命:预警细丝 18f(末 2 帧静默)→喷发 24f(2f 过冲 1.25 再回落)→塌缩 8f(自根烧断);伤害窗=喷发帧,判定线藏在可见亮体内<br/>
    /// 根埋星体内 0.55R,破面渐显(uProgress)穿临边收满,消掉根部平切;绘制走最小编号代画单批
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

        /// <summary>喷发音全局去重:齐射同帧多矛只响一声(客户端本地表现)</summary>
        private static int lastEruptSoundTick = -1;

        private float Angle => Projectile.ai[0];
        private int PlanetWho => (int)Projectile.ai[1];
        private int Palette => (int)Projectile.ai[2];
        private float Age => Lifetime - Projectile.timeLeft;

        /// <summary>矛长 1900 远超弹体:屏检余量放到束长级,星球出屏时冕矛不消失</summary>
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2600;

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

            //喷发拍(各端本地):足点辉光钉在临边交点=喷发基部,音效全局去重防齐射同帧叠响
            if ((int)Age == WarnFrames) {
                Vector2 footPoint = planet.Center + Angle.ToRotationVector2() * PlanetVisRadius(planet);
                CultistMotion.Shake(footPoint, 3f, 8, Angle.ToRotationVector2());
                CultistMotion.CastFlash(footPoint, CultistMotion.PhaseCore(Palette), 0.85f);
                if (!VaultUtils.isServer && lastEruptSoundTick != (int)Main.GameUpdateCount) {
                    lastEruptSoundTick = (int)Main.GameUpdateCount;
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.7f, Pitch = -0.35f }, planet.Center);
                }
            }
            Lighting.AddLight(planet.Center + Angle.ToRotationVector2() * PlanetVisRadius(planet),
                CultistMotion.PhaseCore(Palette).ToVector3() * 0.6f);
        }

        /// <summary>伤害窗=喷发可见窗;蚀祭本影楔内玩家豁免(安全区承诺绝对化,漂移时序兜底;无本影在场时不生效)</summary>
        public override bool CanHitPlayer(Player target) {
            float age = Age;
            if (age < WarnFrames || age >= WarnFrames + FireFrames) {
                return false;
            }
            return !CultistUmbraShade.PointInUmbra(PlanetWho, target.Center);
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
            //最小编号代画:全体冕矛共享一次批次进出,Immediate 内逐矛上参(齐射 50+ 矛不再各自重启批)
            int type = Projectile.type;
            foreach (Projectile other in Main.ActiveProjectiles) {
                if (other.type == type && other.whoAmI < Projectile.whoAmI) {
                    return false;
                }
            }
            Effect fx = EffectLoader.CultistOrrery?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || canvas == null || noise == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique = fx.Techniques["TechLance"];
            //批级共享 uniform 一次上载,逐矛差异参数在 DrawSelf
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uAlpha"]?.SetValue(1f);
            fx.Parameters["uDash"]?.SetValue(0f);

            foreach (Projectile other in Main.ActiveProjectiles) {
                if (other.type == type && other.ModProjectile is CultistCoronaLance lance) {
                    lance.DrawSelf(sb, fx, canvas);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>批内单矛:节拍包络+上参+Apply+Draw(共享 uniform 已由批头设置)</summary>
        private void DrawSelf(SpriteBatch sb, Effect fx, Texture2D canvas) {
            Projectile planet = Planet;
            if (planet == null) {
                return;
            }
            float visR = PlanetVisRadius(planet);
            if (!CultistMotion.OnScreen(planet.Center, visR + LanceLength)) {
                return;
            }

            float age = Age;
            float fireAge = age - WarnFrames;
            //预警:亮度爬升,末 2 帧熄灭(骤暗=爆发前的蓄势拍)
            float arm = age < WarnFrames - 2 ? MathHelper.Clamp(age / (WarnFrames * 0.6f), 0f, 1f) : 0f;
            //破面渐显:根埋 0.55R,渐显段 0.5R 恰在临边外收满
            float rootFade = visR * 0.5f / LanceLength;
            //宽度包络:2f 过冲 1.25(砸出来不是充气)→3f 回落 1.0→塌缩自根烧断
            float env;
            if (age < WarnFrames) {
                env = 0f;
            }
            else if (fireAge < 2f) {
                env = 1.25f * (fireAge / 2f);
            }
            else if (fireAge < 5f) {
                env = MathHelper.Lerp(1.25f, 1f, (fireAge - 2f) / 3f);
            }
            else if (age < WarnFrames + FireFrames) {
                env = 1f;
            }
            else {
                float t = MathHelper.Clamp((age - WarnFrames - FireFrames) / CollapseFrames, 0f, 1f);
                env = 1f - t * 0.55f;
                rootFade = MathHelper.Lerp(rootFade, 0.62f, t * t);
            }
            //炽度:爆发帧打满,30f 衰到 0.62 叠呼吸脉动;塌缩期压暗(余辉不能读作仍致命)
            float charge;
            if (age < WarnFrames) {
                charge = 0f;
            }
            else if (age < WarnFrames + FireFrames) {
                charge = 0.62f + 0.38f * MathF.Exp(-MathHelper.Max(fireAge, 0f) / 30f)
                    + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 55f + Projectile.identity);
            }
            else {
                charge = 0.22f;
            }

            Color mid = CultistMotion.PhaseCore(Palette);
            Color bright = Color.Lerp(mid, Color.White, 0.45f);
            Color deep = Color.Lerp(CultistMotion.PhaseEdge(Palette), Color.Black, 0.55f);
            Vector2 dir = Angle.ToRotationVector2();
            Vector2 rootPos = planet.Center + dir * visR * 0.55f;

            fx.Parameters["uColDeep"]?.SetValue(deep.ToVector3());
            fx.Parameters["uColMid"]?.SetValue(mid.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(bright.ToVector3());
            fx.Parameters["uColHot"]?.SetValue(new Vector3(1f, 0.97f, 0.88f));
            fx.Parameters["uCharge"]?.SetValue(charge);
            fx.Parameters["uArm"]?.SetValue(arm);
            fx.Parameters["uEnv"]?.SetValue(env);
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity % 100 * 0.073f);
            fx.Parameters["uProgress"]?.SetValue(rootFade);
            fx.CurrentTechnique.Passes[0].Apply();
            //quad:根埋星体内,u 沿矛;高 110px(判定 34 藏于亮体)
            sb.Draw(canvas, rootPos - Main.screenPosition, null, Color.White, Angle,
                new Vector2(0f, canvas.Height * 0.5f),
                new Vector2(LanceLength / canvas.Width, 110f / canvas.Height), SpriteEffects.None, 0f);
        }
    }
}
