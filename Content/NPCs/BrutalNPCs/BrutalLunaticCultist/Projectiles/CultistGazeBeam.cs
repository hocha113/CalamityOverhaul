using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.PRTTypes;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 凝视死光:月瞳自星心扫出的湮灭射线(月明凝视/合相共用),TechGaze 专属材质<br/>
    /// ai[0]=起始角(预警期钉死=预告即承诺) ai[1]=巡航扫速rad/f(签名即方向,符号喂扫向不对称) ai[2]=锚定星球whoAmI<br/>
    /// 生命:预警倒吸 22f(末 2 帧静默)→扫射 150f(2f 过冲 1.2 再回落;角速度 30f 二次缓起后匀速)→崩断 10f<br/>
    /// 尺度:锥形喇叭里窄外宽——亮体自瞳口≈82px 线性放宽到墙际≈200px(quad 高 460);判定=白热芯同锥折算(HitWidthAt)=所见即判定;端点恒落黄道环墙上=撞墙飞溅
    /// </summary>
    internal class CultistGazeBeam : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int WarnFrames = 22;
        internal const int FireFrames = 150;
        internal const int CollapseFrames = 10;
        internal const int Lifetime = WarnFrames + FireFrames + CollapseFrames;
        /// <summary>束长:自月心过黄道环墙(1800)再穿 450,任何视角读作贯穿全场</summary>
        private const float BeamLength = 2250f;
        /// <summary>可见 quad 高:锥形末端亮体+软晕的画布余量(GazePS edgeGuard 在缘前收光)</summary>
        private const float QuadHeight = 460f;
        /// <summary>shader 锥形宽度轮廓两端(GazePS w=lerp(ConeRootW,ConeTipW,u)),改 shader 必同步改这里</summary>
        private const float ConeRootW = 0.38f;
        private const float ConeTipW = 1.06f;
        /// <summary>白热芯半宽占画布半高比(GazePS hot=smoothstep(0.32,0.25,q) 视觉缘 q≈0.285)</summary>
        private const float CoreEdgeQ = 0.285f;

        private float StartAngle => Projectile.ai[0];
        private float SweepRate => Projectile.ai[1];
        private int PlanetWho => (int)Projectile.ai[2];
        private float Age => Lifetime - Projectile.timeLeft;

        /// <summary>束长远超弹体:屏检余量放到束长级,锚点离屏时束体/扭曲不弹跳消失</summary>
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2600;

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

        /// <summary>角速度缓起帧数:开火后初速≈0,线性升到巡航扫速(月总死光的从容起手)</summary>
        internal const int SweepEaseFrames = 30;

        /// <summary>扫掠相位时间:缓起段的闭式积分 t²/2T,之后 t-T/2;纯 Age 函数,多端天然一致</summary>
        private static float SweepPhase(float fireAge) {
            if (fireAge <= 0f) {
                return 0f;
            }
            if (fireAge < SweepEaseFrames) {
                return fireAge * fireAge / (2f * SweepEaseFrames);
            }
            return fireAge - SweepEaseFrames * 0.5f;
        }

        /// <summary>当前扫角:预警钉死起始角,开火后缓起→匀速扫</summary>
        internal float CurrentAngle => StartAngle + SweepRate * SweepPhase(Age - WarnFrames);

        /// <summary>宽度包络:预警 0→出膛 2f 过冲 1.2→4f 回落 1→崩断前 1/4 细闪 0.55 后快散(PreDraw/Warp 共用)</summary>
        private float WidthEnv {
            get {
                float age = Age;
                if (age < WarnFrames) {
                    return 0f;
                }
                if (age < WarnFrames + FireFrames) {
                    float fireAge = age - WarnFrames;
                    if (fireAge < 2f) {
                        return 1.2f * (fireAge / 2f);
                    }
                    if (fireAge < 6f) {
                        return MathHelper.Lerp(1.2f, 1f, (fireAge - 2f) / 4f);
                    }
                    return 1f;
                }
                float t = MathHelper.Clamp((age - WarnFrames - FireFrames) / CollapseFrames, 0f, 1f);
                return t < 0.25f ? 0.55f : 0.55f * (1f - (t - 0.25f) / 0.75f);
            }
        }

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
                CultistScreenFX.PushFlash(0.45f);
                CultistMotion.Shake(planet.Center, 9f, 16);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1.2f, Pitch = -0.55f }, planet.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.5f }, planet.Center);
                }
            }
            //扫射低鸣:音高随扫过进度爬升(张力线)
            if (age > WarnFrames && age < WarnFrames + FireFrames && (int)age % 20 == 0 && !VaultUtils.isServer) {
                float prog = (age - WarnFrames) / FireFrames;
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.4f, Pitch = -0.7f + prog * 0.35f }, planet.Center);
            }

            //撞墙飞溅(各端本地表现):端点火花沿墙切向甩出,随扫扫过整圈墙
            if (!VaultUtils.isServer && age >= WarnFrames && age < WarnFrames + FireFrames && (int)age % 3 == 0) {
                Vector2 wallPoint = GetWallPoint(planet, out bool hasWall);
                if (hasWall) {
                    Vector2 dir = CurrentAngle.ToRotationVector2();
                    Vector2 tangent = new Vector2(-dir.Y, dir.X) * MathF.Sign(SweepRate);
                    for (int i = 0; i < 2; i++) {
                        InnoVault.PRT.PRTLoader.NewParticle<PRT_Spark>(
                            wallPoint + Main.rand.NextVector2Circular(20f, 20f),
                            tangent * Main.rand.NextFloat(3f, 10f) - dir * Main.rand.NextFloat(0.5f, 3f),
                            Color.Lerp(CultistMotion.MoonCore, Color.White, Main.rand.NextFloat(0.4f)),
                            Main.rand.NextFloat(0.7f, 1.2f))?.Configure(true, Main.rand.Next(10, 22));
                    }
                    Lighting.AddLight(wallPoint, CultistMotion.MoonCore.ToVector3() * 0.9f);
                }
            }

            //束身世界照明:预警期瞳口一盏弱光,开火后沿束到墙点串灯(死光把场地照亮)
            Vector2 beamDir = CurrentAngle.ToRotationVector2();
            if (age < WarnFrames) {
                Lighting.AddLight(planet.Center + beamDir * 300f, CultistMotion.MoonCore.ToVector3() * 0.5f);
            }
            else {
                float lightEnv = age < WarnFrames + FireFrames
                    ? 1f : 1f - (age - WarnFrames - FireFrames) / CollapseFrames;
                Vector2 wallLightPoint = GetWallPoint(planet, out bool lightHasWall);
                float lightLen = lightHasWall
                    ? Vector2.Distance(planet.Center, wallLightPoint) : BeamLength;
                for (float d = 180f; d < lightLen; d += 300f) {
                    Lighting.AddLight(planet.Center + beamDir * d,
                        CultistMotion.MoonCore.ToVector3() * (1.1f * lightEnv));
                }
            }
        }

        /// <summary>死光端点=束线与黄道环墙交点(月明钉场心恒有解;场未立/越束长时无墙)</summary>
        private Vector2 GetWallPoint(Projectile planet, out bool hasWall) {
            hasWall = false;
            int ownerWho = (int)planet.ai[1];
            NPC owner = ownerWho >= 0 && ownerWho < Main.maxNPCs ? Main.npc[ownerWho] : null;
            if (owner == null || !owner.active || !owner.TryGetOverride(out CultistBossAI overrideAI)
                || overrideAI.Context is not CultistStateContext context || !context.ArenaSpawned) {
                return default;
            }
            Vector2 dir = CurrentAngle.ToRotationVector2();
            Vector2 toCenter = context.ArenaCenter - planet.Center;
            float along = Vector2.Dot(toCenter, dir);
            float perpSq = toCenter.LengthSquared() - along * along;
            float radiusSq = CultistStateContext.ArenaRadius * CultistStateContext.ArenaRadius;
            if (perpSq >= radiusSq) {
                return default;
            }
            float dist = along + MathF.Sqrt(radiusSq - perpSq);
            if (dist <= 0f || dist > BeamLength) {
                return default;
            }
            hasWall = true;
            return planet.Center + dir * dist;
        }

        public override bool CanHitPlayer(Player target) {
            float age = Age;
            return age >= WarnFrames && age < WarnFrames + FireFrames;
        }

        /// <summary>锥形判定全宽:与可见白热芯同锥(所见即判定)——芯全宽=CoreEdgeQ·w(u)·QuadHeight,瞳口≈50px→末端≈139px</summary>
        private static float HitWidthAt(float along) {
            float u = MathHelper.Clamp(along / BeamLength, 0f, 1f);
            return CoreEdgeQ * MathHelper.Lerp(ConeRootW, ConeTipW, u) * QuadHeight;
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
            //锥形判定:分 6 段各按段中点宽度查线,段内宽差 ±7px 藏在亮体宽容带(每侧 24px+)内
            const int Segments = 6;
            const float StartOffset = 40f;
            float segLen = (BeamLength - StartOffset) / Segments;
            for (int i = 0; i < Segments; i++) {
                float d0 = StartOffset + segLen * i;
                float point = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    planet.Center + dir * d0, planet.Center + dir * (d0 + segLen),
                    HitWidthAt(d0 + segLen * 0.5f), ref point)) {
                    return true;
                }
            }
            return false;
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
            float fireAge = age - WarnFrames;
            //预警倒吸:亮度爬升,末 2 帧静默(骤暗=爆发前的吸气)
            float arm = age < WarnFrames - 2 ? MathHelper.Clamp(age / (WarnFrames * 0.7f), 0f, 1f) : 0f;
            //宽度包络共用 WidthEnv;炽度:出膛峰值缓落+高频颤,崩断前 1/4 过亮细闪
            float env = WidthEnv;
            float charge;
            if (age < WarnFrames) {
                charge = 0f;
            }
            else if (age < WarnFrames + FireFrames) {
                charge = 0.62f + 0.38f * MathF.Exp(-MathHelper.Max(fireAge, 0f) / 34f)
                    + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 55f);
            }
            else {
                float t = MathHelper.Clamp((age - WarnFrames - FireFrames) / CollapseFrames, 0f, 1f);
                charge = t < 0.25f ? 1.3f : 0f;
            }

            Color mid = CultistMotion.MoonCore;
            Color bright = new(214, 255, 240);
            Color deep = new(16, 32, 28);
            float angle = CurrentAngle;

            fx.CurrentTechnique = fx.Techniques["TechGaze"];
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uAlpha"]?.SetValue(1f);
            fx.Parameters["uColDeep"]?.SetValue(deep.ToVector3());
            fx.Parameters["uColMid"]?.SetValue(mid.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(bright.ToVector3());
            fx.Parameters["uColHot"]?.SetValue(new Vector3(0.96f, 1f, 0.98f));
            fx.Parameters["uCharge"]?.SetValue(charge);
            fx.Parameters["uArm"]?.SetValue(arm);
            fx.Parameters["uEnv"]?.SetValue(env);
            fx.Parameters["uSeed"]?.SetValue(0.83f);
            fx.Parameters["uProgress"]?.SetValue(0.05f);
            fx.Parameters["uDash"]?.SetValue(MathF.Sign(SweepRate));
            fx.Parameters["uAspect"]?.SetValue(BeamLength / QuadHeight);

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
                new Vector2(BeamLength / canvas.Width, QuadHeight / canvas.Height), SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //撞墙辉斑:端点飞溅的光垫(SoftGlow 黑底贴图走 A=0 纯加光契约)
            if (env > 0.05f) {
                Vector2 wallPoint = GetWallPoint(planet, out bool hasWall);
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (hasWall && glow != null) {
                    float pulse = 1f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 40f);
                    float strength = MathHelper.Min(env, 1f);
                    sb.Draw(glow, wallPoint - Main.screenPosition, null,
                        (mid with { A = 0 }) * (0.95f * strength), 0f,
                        glow.Size() * 0.5f, 3.4f * pulse, SpriteEffects.None, 0f);
                    sb.Draw(glow, wallPoint - Main.screenPosition, null,
                        (Color.White with { A = 0 }) * (0.6f * strength), 0f,
                        glow.Size() * 0.5f, 1.6f * pulse, SpriteEffects.None, 0f);
                }
            }
            return false;
        }

        public bool CanDrawCustom() => false;

        /// <summary>月绿死光不吃蓝移色偏,走弱色差通道</summary>
        public bool DontUseBlueshiftEffect() => true;

        public void DrawCustom(SpriteBatch spriteBatch) { }

        /// <summary>扭曲采样源:TechGazeWarp 位移图与可见束同锥同涌动,缘外空气被死光挤开(毁灭者热浪同管线)</summary>
        public void Warp() {
            Projectile planet = Planet;
            float env = WidthEnv;
            if (planet == null || env <= 0.05f) {
                return;
            }
            Effect fx = EffectLoader.CultistOrrery?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || canvas == null || noise == null) {
                return;
            }

            float angle = CurrentAngle;
            fx.CurrentTechnique = fx.Techniques["TechGazeWarp"];
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uEnv"]?.SetValue(env);
            fx.Parameters["uSeed"]?.SetValue(0.83f);
            fx.Parameters["uAspect"]?.SetValue(BeamLength / QuadHeight);
            fx.Parameters["uRotation"]?.SetValue(angle);

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
                new Vector2(BeamLength / canvas.Width, QuadHeight / canvas.Height), SpriteEffects.None, 0f);
            sb.End();
            //还原 WarpEffectRender 的采集批设定
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
