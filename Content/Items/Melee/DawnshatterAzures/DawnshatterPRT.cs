using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.DawnshatterAzures
{
    /// <summary>日冕余烬,速度拉丝,生命期内 金→橙红→焦暗 冷却,先浮后坠</summary>
    internal class PRT_DawnEmber : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        internal static Asset<Texture2D> StreakTex = null;

        private static readonly Color HotGold = new(255, 208, 96);
        private static readonly Color EmberRed = new(255, 92, 30);
        private static readonly Color Charred = new(118, 42, 26);

        private float flickerSeed;
        private float buoyancy;

        public PRT_DawnEmber Configure(int lifetime, float buoyancyStrength = 0.035f) {
            Lifetime = lifetime;
            buoyancy = buoyancyStrength;
            return this;
        }

        public override void Reset() {
            base.Reset();
            flickerSeed = 0f;
            buoyancy = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            flickerSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(18, 30);
            }
            if (buoyancy == 0f) {
                buoyancy = 0.035f;
            }
        }

        public override void AI() {
            Velocity *= 0.91f;
            float lc = LifetimeCompletion;
            //热态上浮,冷却后坠落
            Velocity.Y += lc < 0.45f ? -buoyancy : buoyancy * 1.8f;

            //冷却斜坡,颜色即温度叙事
            Color = lc < 0.4f
                ? Color.Lerp(HotGold, EmberRed, lc / 0.4f)
                : Color.Lerp(EmberRed, Charred, (lc - 0.4f) / 0.6f);

            float flicker = 0.76f + 0.24f * MathF.Sin(Time * 1.1f + flickerSeed);
            Opacity = MathF.Min(lc * 7f, 1f) * (1f - lc * lc) * flicker;
            Scale *= 0.968f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D core = TexValue;
            Texture2D streak = StreakTex?.Value;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };

            //运动各向异性,顺速拉丝
            float speed = Velocity.Length();
            if (streak != null && speed > 1.4f) {
                float stretch = MathHelper.Clamp(speed * 0.16f, 0.3f, 1.6f);
                spriteBatch.Draw(streak, pos, null, col * (0.7f * Opacity)
                    , Velocity.ToRotation() + MathHelper.PiOver2, streak.Size() * 0.5f
                    , new Vector2(0.2f, stretch) * Scale, SpriteEffects.None, 0f);
            }

            Vector2 origin = core.Size() * 0.5f;
            spriteBatch.Draw(core, pos, null, col * (0.5f * Opacity), 0f, origin, 0.28f * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(core, pos, null, col * (0.95f * Opacity), 0f, origin, 0.12f * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 气浪环:伪3D透视椭圆,法线向压扁读作"被穿过的空气";AlphaBlend 下焦烟暗环(带A遮挡)+金边(A=0加法)双层<br/>
    /// 日食模式给终结演出用:负扩张速度合拢,环上挂贝利珠亮点,终帧向外爆散余烬
    /// </summary>
    internal class PRT_DawnRing : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Ring01";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        private static readonly Color RingGold = new(255, 198, 92);
        private static readonly Color RingSoot = new(88, 38, 22);
        private static readonly Color BeadWhite = new(255, 236, 190);

        private Vector2 normal = Vector2.UnitX;
        private float radius;
        private float expandSpeed;
        /// 法线向压扁比,1=屏幕面正圆
        private float squash;
        private bool eclipse;
        private int beadCount;
        private float beadPhase;

        public PRT_DawnRing Configure(Vector2 normalDir, float radius0, float expand
            , float squashK, int lifetime, bool eclipseMode = false, int beads = 0) {
            normal = normalDir == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(normalDir);
            radius = radius0;
            expandSpeed = expand;
            squash = squashK;
            Lifetime = lifetime;
            eclipse = eclipseMode;
            beadCount = beads;
            return this;
        }

        public override void Reset() {
            base.Reset();
            normal = Vector2.UnitX;
            radius = 0f;
            expandSpeed = 0f;
            squash = 1f;
            eclipse = false;
            beadCount = 0;
            beadPhase = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            beadPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = 16;
            }
            if (squash <= 0f) {
                squash = 0.35f;
            }
        }

        public override void AI() {
            Position += Velocity;
            Velocity *= 0.92f;
            radius = MathF.Max(radius + expandSpeed, 10f);
            //扩张软着陆;合拢保持速率,收干脆
            if (expandSpeed > 0f) {
                expandSpeed *= 0.93f;
            }

            float lc = LifetimeCompletion;
            //扩张环耗散淡出;日食环收缩聚能,越收越亮,终帧爆散接走
            Opacity = MathF.Min(lc * 6f, 1f) * (eclipse ? 0.55f + 0.45f * lc : MathF.Sqrt(1.001f - lc));

            //日食终帧:贝利珠向外爆散成余烬
            if (eclipse && Time == Lifetime - 1) {
                int n = Math.Max(beadCount, 4) * 2;
                for (int i = 0; i < n; i++) {
                    Vector2 dir = (MathHelper.TwoPi * i / n + beadPhase).ToRotationVector2();
                    PRTLoader.NewParticle<PRT_DawnEmber>(Position + OnRing(dir)
                        , dir * Main.rand.NextFloat(4f, 9f), default, Main.rand.NextFloat(0.9f, 1.5f))
                        .Configure(Main.rand.Next(18, 28));
                }
            }
        }

        /// <summary>环参数方向→世界偏移,法线向压扁再旋回</summary>
        private Vector2 OnRing(Vector2 unit) {
            float rot = normal.ToRotation() - MathHelper.PiOver2;
            return new Vector2(unit.X, unit.Y * squash).RotatedBy(rot) * radius;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            float rot = normal.ToRotation() - MathHelper.PiOver2;
            float sx = radius * 2f / tex.Width;
            var scale = new Vector2(sx, sx * squash);
            Vector2 origin = tex.Size() * 0.5f;

            //焦烟暗环带 A 遮挡出实体感,金边 A=0 走加法;日食模式暗层更重更靠内
            float sootA = eclipse ? 0.72f : 0.4f;
            spriteBatch.Draw(tex, pos, null, RingSoot * (sootA * Opacity)
                , rot, origin, scale * (eclipse ? 0.9f : 1.08f), SpriteEffects.None, 0f);
            Color gold = RingGold with { A = 0 };
            spriteBatch.Draw(tex, pos, null, gold * (0.85f * Opacity), rot, origin, scale, SpriteEffects.None, 0f);

            //贝利珠沿环均布,随环走
            Texture2D glow = GlowTex?.Value;
            if (beadCount > 0 && glow != null) {
                Color bead = BeadWhite with { A = 0 };
                for (int i = 0; i < beadCount; i++) {
                    Vector2 unit = (MathHelper.TwoPi * i / beadCount + beadPhase).ToRotationVector2();
                    spriteBatch.Draw(glow, pos + OnRing(unit), null, bead * Opacity
                        , 0f, glow.Size() * 0.5f, 0.16f * Scale, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }

    /// <summary>贴根火舌,锚在刃缘外舔,2~5 帧高频闪变,噪声撕裂端头由贴图承担</summary>
    internal class PRT_DawnTongue : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "TearFlame01";
        public override bool CanPool => true;

        private static readonly Color TongueGold = new(255, 186, 74);
        private static readonly Color TongueRed = new(240, 96, 34);

        private float tongueRot;
        private float lengthMul;
        private float jitterSeed;

        public PRT_DawnTongue Configure(Vector2 outwardDir, float length, int lifetime) {
            tongueRot = outwardDir.ToRotation() + MathHelper.PiOver2;
            lengthMul = length;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            tongueRot = 0f;
            lengthMul = 1f;
            jitterSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            jitterSeed = Main.rand.NextFloat(100f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(3, 6);
            }
        }

        public override void AI() {
            Velocity *= 0.8f;
            float lc = LifetimeCompletion;
            Opacity = (1f - lc) * (0.75f + 0.25f * MathF.Sin((Time + jitterSeed) * 2.7f));
            Color = Color.Lerp(TongueGold, TongueRed, lc);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };
            //根锚底边,向外舔出;逐帧长度抖动是火的时域签名
            float jitter = 0.85f + 0.3f * MathF.Sin((Time * 2.1f + jitterSeed) * 3.7f);
            var stretch = new Vector2(0.5f, lengthMul * jitter) * Scale;
            var origin = new Vector2(tex.Width * 0.5f, tex.Height);
            spriteBatch.Draw(tex, pos, null, col * Opacity, tongueRot, origin, stretch, SpriteEffects.None, 0f);
            return false;
        }
    }

}
