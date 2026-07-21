using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>双鳕伴飞域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishDoubleCodAssets
    {
        /// <summary>细水尾流条带</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishDoubleCodWake { get; private set; }
    }

    /// <summary>双鳕伴飞</summary>
    internal static class FishDoubleCodVFX
    {
        /// <summary>深水暗蓝（外圈/尾端压底）</summary>
        public static readonly Color Deep = new(26, 44, 62);
        /// <summary>水流蓝（饱和中层主色）</summary>
        public static readonly Color Flow = new(74, 132, 178);
        /// <summary>鳞银（鱼体染色基准）</summary>
        public static readonly Color Scale = new(158, 186, 208);
        /// <summary>银鳞碎光（近白冷银，仅限 ≤2 帧瞬闪）</summary>
        public static readonly Color Spec = new(216, 234, 246);


        /// <summary>FishDoubleCodWake 标准参数；phase 传弹幕 whoAmI 派生量避免双带同相</summary>
        public static void ApplyWake(Effect fx, float phase) {
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 0.8f + phase);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }
            fx.Parameters["uColDeep"]?.SetValue(Deep.ToVector3());
            fx.Parameters["uColFlow"]?.SetValue(Flow.ToVector3());
            fx.Parameters["uColSpec"]?.SetValue(Spec.ToVector3());
        }

        /// <summary>水尾流拖尾，oldPos 逐点按 oldRot 朝尾侧回退 tailOffset，条带根部锚定鱼尾而非鱼心。 Additive 绘制后恢复 AlphaBlend；effect 需已 <see cref="ApplyWake"/></summary>
        public static void DrawWakeTrail(Projectile projectile, ref Trail trail
            , TrailThicknessCalculator widthFunc, TrailColorEvaluator colorFunc, Effect effect, float tailOffset) {
            if (effect == null || projectile.oldPos == null || projectile.oldPos.Length == 0) {
                return;
            }
            Vector2[] positions = new Vector2[projectile.oldPos.Length];
            for (int i = 0; i < positions.Length; i++) {
                if (projectile.oldPos[i] == Vector2.Zero) {
                    projectile.oldPos[i] = projectile.position;
                }
                float rot = i < projectile.oldRot.Length ? projectile.oldRot[i] : projectile.rotation;
                positions[i] = projectile.oldPos[i] + projectile.Size * 0.5f - rot.ToRotationVector2() * tailOffset;
            }
            trail ??= new Trail(positions, widthFunc, colorFunc);
            trail.TrailPositions = positions;
            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            trail.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }


        /// <summary>水珠扇，沿 dir 锥形甩出的受重力水珠（出生自带 1 帧银闪）</summary>
        public static void DropletFan(Vector2 pos, Vector2 dir, int count, float speedMin, float speedMax, float spread = 0.5f) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedByRandom(spread) * Main.rand.NextFloat(speedMin, speedMax);
                Color col = Color.Lerp(Spec, Flow, Main.rand.NextFloat(0.25f, 0.75f));
                PRTLoader.NewParticle<PRT_FishDoubleCodDroplet>(pos, vel, col, Main.rand.NextFloat(0.55f, 0.9f))
                    ?.Configure(Main.rand.Next(20, 32), Main.rand.NextFloat(0.2f, 0.3f));
            }
        }

        /// <summary>水面破开的扁椭圆冲击环</summary>
        public static void SplashRing(Vector2 pos, float rot, float startScale, float finalScale, int lifetime) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, Flow, startScale)
                ?.Configure(new Vector2(1f, 0.55f), rot + MathHelper.PiOver2, finalScale, lifetime);
        }

        /// <summary>银鳞碎闪</summary>
        public static void Glints(Vector2 pos, int count, float speed = 2f) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(pos + Main.rand.NextVector2Circular(6f, 6f)
                    , Main.rand.NextVector2Circular(speed, speed), Spec, Main.rand.NextFloat(0.28f, 0.45f))
                    ?.Configure(Flow, Main.rand.Next(9, 14), 0.1f, 0.5f);
            }
        }
    }

    /// <summary>双鳕水珠</summary>
    internal class PRT_FishDoubleCodDroplet : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color initialColor;
        private float gravity;
        private float drag;
        private int age;

        public PRT_FishDoubleCodDroplet Configure(int lifetime, float gravityPerFrame = 0.26f, float dragMul = 0.982f) {
            Lifetime = lifetime;
            initialColor = Color;
            gravity = gravityPerFrame;
            drag = dragMul;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = 0f;
            drag = 1f;
            age = 0;
        }

        public override void AI() {
            age++;
            Velocity.X *= drag;
            Velocity.Y += gravity;
            if (Velocity.Y > 12f) {
                Velocity.Y = 12f;
            }

            //水珠坠落中凝缩淡出
            Scale *= 0.986f;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(LifetimeCompletion, 2.2f));
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //随速度纵向拉伸
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 0.9f);
            Vector2 scale = new Vector2(0.3f * (1f - stretch * 0.35f), 0.55f * (1f + stretch * 1.6f)) * Scale;

            //双层同色窄叠
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale * new Vector2(0.45f, 1f), SpriteEffects.None, 0f);

            //出生 2 帧银闪
            if (age <= 2) {
                Texture2D star = CWRAsset.StarGlow01?.Value;
                if (star != null) {
                    float flash = age == 1 ? 1f : 0.4f;
                    spriteBatch.Draw(star, pos, null, FishDoubleCodVFX.Spec with { A = 0 } * flash
                        , Main.rand.NextFloat(MathHelper.TwoPi), star.Size() * 0.5f, 0.1f * Scale, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }

    /// <summary>双鳕死后水痕</summary>
    internal class PRT_FishDoubleCodWake : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private const int MaxPoints = 12;
        private readonly Vector2[] points = new Vector2[MaxPoints];
        private int count;
        private float width;

        /// <summary>从弹幕 oldPos 隔点采样路径（points[0]=死亡位置侧）</summary>
        public PRT_FishDoubleCodWake Configure(Projectile proj, float tailOffset, float baseWidth, int lifetime) {
            count = 0;
            if (proj.oldPos != null) {
                for (int i = 0; i < proj.oldPos.Length && count < MaxPoints; i += 2) {
                    if (proj.oldPos[i] == Vector2.Zero) {
                        break;
                    }
                    float rot = i < proj.oldRot.Length ? proj.oldRot[i] : proj.rotation;
                    points[count++] = proj.oldPos[i] + proj.Size * 0.5f - rot.ToRotationVector2() * tailOffset;
                }
            }
            width = baseWidth;
            Lifetime = lifetime;
            Position = count > 0 ? points[0] : proj.Center;
            return this;
        }

        public override void Reset() {
            base.Reset();
            count = 0;
            width = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Velocity = Vector2.Zero;
        }

        public override void AI() {
            Opacity = MathF.Pow(1f - LifetimeCompletion, 1.5f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (count < 2) {
                return false;
            }
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            float lc = LifetimeCompletion;

            for (int i = 0; i < count - 1; i++) {
                float t = i / (float)(count - 1);   //0=死亡位置侧 1=旧尾
                //旧尾先蚀
                float aliveEdge = 1f - lc * 1.15f;
                float alive = MathHelper.Clamp((aliveEdge - t) / 0.18f, 0f, 1f);
                if (alive <= 0.01f) {
                    continue;
                }
                //失去动力的水痕缓缓下沉
                Vector2 sag = new(0f, lc * lc * 12f * (0.4f + t));
                Vector2 a = points[i] + sag - Main.screenPosition;
                Vector2 b = points[i + 1] + sag - Main.screenPosition;
                Vector2 seg = b - a;
                float len = seg.Length();
                if (len < 0.5f) {
                    continue;
                }
                Color col = Color.Lerp(FishDoubleCodVFX.Flow, FishDoubleCodVFX.Deep, t)
                    * (Opacity * alive * (0.5f - t * 0.3f));
                spriteBatch.Draw(tex, (a + b) * 0.5f, null, col, seg.ToRotation() + MathHelper.PiOver2, origin
                    , new Vector2(width * (1f - t * 0.6f) / tex.Width, len / tex.Height * 1.1f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
