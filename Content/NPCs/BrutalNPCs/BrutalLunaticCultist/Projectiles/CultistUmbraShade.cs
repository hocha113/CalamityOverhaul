using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 蚀祭主控:暗影盘滑过主星(食相自身即预告),全食期冕矛自星面辐射,本影楔=唯一安全走廊<br/>
    /// ai[0]=宿主npc ai[1]=本影基角(出生锁定,朝向当时的目标=先给玩家安全区) ai[2]=漂移率(符号即方向)<br/>
    /// 公平阀:GapHalfAngle 声明角缺口,冕矛发射循环与本影楔绘制同读;漂移慢到步行可跟
    /// </summary>
    internal class CultistUmbraShade : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int Lifetime = 336;
        private const int SlideInEnd = 90;
        private const int TotalityEnd = 252;
        private const int SlideOutEnd = 312;
        /// <summary>声明缺口半角(rad):本影楔可见宽度与冕矛跳角同源</summary>
        internal const float GapHalfAngle = 0.30f;
        /// <summary>冕矛志愿间隔(帧)</summary>
        private const int VolleyInterval = 26;

        private int OwnerWho => (int)Projectile.ai[0];
        private float UmbraBase => Projectile.ai[1];
        private float DriftRate => Projectile.ai[2];
        private float Age => Lifetime - Projectile.timeLeft;

        /// <summary>暗影盘滑入方向(屏幕系固定)</summary>
        private static readonly Vector2 SlideDir = new Vector2(1f, 0.28f).SafeNormalize(Vector2.UnitX);

        private int planetCache = -1;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.netImportant = true;
        }

        /// <summary>找主星(常驻非幻象),缓存失效重扫</summary>
        private Projectile FindPlanet() {
            if (planetCache >= 0 && planetCache < Main.maxProjectiles) {
                Projectile cached = Main.projectile[planetCache];
                if (cached.active && cached.type == ModContent.ProjectileType<CultistPlanetProj>()
                    && (int)cached.ai[1] == OwnerWho) {
                    return cached;
                }
            }
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == OwnerWho
                    && (int)proj.ai[2] % 10 == 1 && (int)proj.ai[2] / 10 == 0) {
                    planetCache = proj.whoAmI;
                    return proj;
                }
            }
            return null;
        }

        /// <summary>食相进度 0~1(1=全食);滑出段回落</summary>
        internal float Coverage {
            get {
                float age = Age;
                if (age < SlideInEnd) {
                    float t = age / SlideInEnd;
                    return t * t;
                }
                if (age < TotalityEnd) {
                    return 1f;
                }
                if (age < SlideOutEnd) {
                    float t = (age - TotalityEnd) / (SlideOutEnd - TotalityEnd);
                    return 1f - t * t;
                }
                return 0f;
            }
        }

        internal float UmbraAngle => UmbraBase + DriftRate * Age;

        public override void AI() {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            Projectile planet = FindPlanet();
            if (owner == null || !owner.active || owner.type != NPCID.CultistBoss || planet == null) {
                Projectile.Kill();
                return;
            }
            float age = Age;
            Projectile.Center = planet.Center;
            float coverage = Coverage;

            //全食起拍(各端本地)
            if ((int)age == SlideInEnd) {
                CultistScreenFX.PushFlash(0.35f);
                CultistMotion.Shake(planet.Center, 6f, 14);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie103 with { Volume = 1.1f, Pitch = -0.7f }, planet.Center);
                }
            }
            //复圆拍:钻石环闪
            if ((int)age == TotalityEnd) {
                CultistScreenFX.PushFlash(0.5f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.9f, Pitch = -0.4f }, planet.Center);
                }
            }

            //全食压场(本地):天黑+去饱和
            if (coverage > 0.6f && !VaultUtils.isServer) {
                CultistScreenFX.SetVeil(0.45f * coverage, planet.Center, new Color(30, 40, 46), 1500f);
                CultistScreenFX.BreakDesat = MathHelper.Max(CultistScreenFX.BreakDesat, 0.22f * coverage);
            }

            //冕矛志愿(权威端):12 槽跳本影扇区,GapHalfAngle 与楔绘制同一常量
            if (!VaultUtils.isClient && age > SlideInEnd + 8 && age < TotalityEnd - 12
                && (int)age % VolleyInterval == 0) {
                int volley = (int)age / VolleyInterval;
                float baseRot = volley * 0.26f;
                int palette = (int)planet.ai[0];
                float umbra = UmbraAngle;
                for (int i = 0; i < 12; i++) {
                    float angle = baseRot + i * MathHelper.TwoPi / 12f;
                    float delta = MathHelper.WrapAngle(angle - umbra);
                    if (Math.Abs(delta) < GapHalfAngle) {
                        continue;
                    }
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), planet.Center,
                        angle.ToRotationVector2() * 0.01f, ModContent.ProjectileType<CultistCoronaLance>(),
                        42, 0f, Main.myPlayer, angle, planet.whoAmI, palette);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.5f, Pitch = -0.2f }, planet.Center);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Projectile planet = FindPlanet();
            if (planet == null) {
                return false;
            }
            float coverage = Coverage;
            if (coverage <= 0.01f) {
                return false;
            }

            float visR = planet.Hitbox.Width * 0.5f;
            if (planet.ModProjectile is CultistPlanetProj planetProj) {
                visR = planetProj.VisRadius * planet.scale;
            }
            int palette = (int)planet.ai[0];
            Color mid = CultistMotion.PhaseCore(palette);
            Color bright = Color.Lerp(mid, Color.White, 0.4f);

            SpriteBatch sb = Main.spriteBatch;

            //本影楔:全食期的安全走廊(缺口即所见,略窄于判定缺口=对玩家宽容)
            float wedgeStrength = MathHelper.Clamp((coverage - 0.72f) / 0.28f, 0f, 1f);
            if (wedgeStrength > 0.01f) {
                float umbra = UmbraAngle;
                Vector2 dir = umbra.ToRotationVector2();
                const int WedgePts = 10;
                const float WedgeLen = 1750f;
                float tanHalf = (float)Math.Tan(GapHalfAngle * 0.94f);
                Vector2[] pts = new Vector2[WedgePts];
                float[] widths = new float[WedgePts];
                float[] alphas = new float[WedgePts];
                for (int i = 0; i < WedgePts; i++) {
                    float t = i / (float)(WedgePts - 1);
                    float dist = visR * 0.8f + t * WedgeLen;
                    pts[i] = planet.Center + dir * dist - Main.screenPosition;
                    widths[i] = dist * tanHalf;
                    alphas[i] = 1f;
                }
                sb.End();
                CultistOrreryRenderer.DrawTechniqueStrip("TechUmbra", pts, widths, alphas,
                    new Color(6, 10, 18), mid, bright, wedgeStrength, 0f, 0f, 0.51f);
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //暗影盘:滑过主星,盘径契约 0.42 与行星同
            Effect fx = EffectLoader.CultistOrrery?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || canvas == null || noise == null) {
                return false;
            }
            float age = Age;
            float slideOffset;
            if (age < SlideInEnd) {
                float t = age / SlideInEnd;
                slideOffset = MathHelper.Lerp(visR * 2.6f, 0f, 1f - (1f - t) * (1f - t));
            }
            else if (age < TotalityEnd) {
                slideOffset = 0f;
            }
            else {
                float t = MathHelper.Clamp((age - TotalityEnd) / (SlideOutEnd - TotalityEnd), 0f, 1f);
                slideOffset = -visR * 2.6f * t * t;
            }
            Vector2 shadePos = planet.Center + SlideDir * slideOffset;
            float shadeR = visR * 0.94f;

            fx.CurrentTechnique = fx.Techniques["TechShade"];
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uAlpha"]?.SetValue(1f);
            fx.Parameters["uColDeep"]?.SetValue(new Vector3(0.10f, 0.10f, 0.13f));
            fx.Parameters["uColMid"]?.SetValue(mid.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(bright.ToVector3());
            fx.Parameters["uColHot"]?.SetValue(new Vector3(1f, 0.96f, 0.85f));
            fx.Parameters["uCharge"]?.SetValue(MathHelper.Clamp((coverage - 0.85f) / 0.15f, 0f, 1f));
            fx.Parameters["uSeed"]?.SetValue(0.41f);
            fx.Parameters["uProgress"]?.SetValue(1f);
            fx.Parameters["uDash"]?.SetValue(0f);
            fx.Parameters["uArm"]?.SetValue(0f);
            fx.Parameters["uEnv"]?.SetValue(0f);

            float quadSize = shadeR / 0.42f * 2f;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, shadePos - Main.screenPosition, null, Color.White, 0f,
                canvas.Size() * 0.5f, quadSize / canvas.Width, SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
