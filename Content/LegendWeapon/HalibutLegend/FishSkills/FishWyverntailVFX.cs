using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishWyverntailAssets
    {
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishWyverntailBody { get; private set; }
    }

    /// <summary>
    /// 云蛟蜕落的云絮：SmokeSheet01 随机帧 AlphaBlend 染色，珍珠白亮絮与灰蓝暗絮双色，
    /// 微升力慢散、快进慢出。召唤云涡/出膛破云/飞行蜕鳞/命中云爆共用
    /// </summary>
    internal class PRT_FishWyverntailFluff : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;

        private float spin;
        private float baseScale;
        private float peakOpacity;
        private float lift;
        private Color baseColor;

        public PRT_FishWyverntailFluff Configure(int lifetime, Color color, float peak = 0.5f, float liftForce = 0.02f) {
            Lifetime = lifetime;
            baseColor = color;
            peakOpacity = peak;
            lift = liftForce;
            baseScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            baseScale = 0f;
            peakOpacity = 0f;
            lift = 0f;
            baseColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            ai[0] = Main.rand.Next(4);//SmokeSheet01 2×2 帧
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.03f, 0.03f);
            //防漏 Configure 兜底
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 40);
                baseColor = FishWyverntailVFX.PearlBright;
                peakOpacity = 0.45f;
                baseScale = Scale;
            }
        }

        public override void AI() {
            Velocity *= 0.94f;
            Velocity.Y -= lift;
            Rotation += spin;

            float lc = LifetimeCompletion;
            //快进慢出：前20%胀出峰值，其后长尾消散
            float env = lc < 0.2f
                ? lc / 0.2f
                : MathHelper.Lerp(1f, 0f, (lc - 0.2f) / 0.8f * (lc - 0.2f) / 0.8f);
            Opacity = peakOpacity * env;
            Scale = baseScale * (0.8f + lc * 0.55f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int fi = (int)ai[0];
            Rectangle frame = new(fi % 2 * 512, fi / 2 * 512, 512, 512);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, baseColor * Opacity,
                Rotation, frame.Size() / 2f, Scale * 0.22f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>长白贯影爆发演出集合：召唤云涡、出膛破云、命中云爆、自然化散，全 client-only</summary>
    internal static class FishWyverntailVFX
    {
        //珍珠白亮絮 / 灰蓝暗絮 / 金鬃点缀 / 冲击环冷灰蓝
        public static readonly Color PearlBright = new(236, 242, 249);
        public static readonly Color CloudShadow = new(94, 108, 134);
        public static readonly Color ManeGold = new(226, 184, 94);
        public static readonly Color RingBlue = new(150, 165, 195);

        /// <summary>召唤云涡：四周云絮向心聚拢+收缩环，materialize 禁 pop-in</summary>
        public static void SummonBurst(Vector2 center) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(0.5f);
                Vector2 pos = center + angle.ToRotationVector2() * Main.rand.NextFloat(62f, 92f);
                Vector2 vel = (center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4.5f, 7f);
                Color col = i % 3 == 0 ? CloudShadow : PearlBright;
                PRTLoader.NewParticle<PRT_FishWyverntailFluff>(pos, vel, col, Main.rand.NextFloat(0.8f, 1.2f))
                    ?.Configure(Main.rand.Next(22, 30), col, 0.45f, 0.015f);
            }

            //向内收缩的云门环
            var ring = PRTLoader.NewParticle<PRT_DWave>(center, Vector2.Zero, RingBlue, 0.55f);
            ring?.Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 0.12f, 15);

            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(center + Main.rand.NextVector2Circular(26f, 26f),
                    Main.rand.NextVector2Circular(1.5f, 1.5f), ManeGold, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(ManeGold, Main.rand.Next(16, 24), 0.04f, 0.8f);
            }
        }

        /// <summary>出膛破云：沿发射方向的锥形云爆+定向椭圆环+金屑</summary>
        public static void MuzzleBurst(Vector2 pos, Vector2 dir) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 7; i++) {
                Vector2 vel = dir.RotatedByRandom(0.55f) * Main.rand.NextFloat(2.5f, 6.5f);
                Color col = i % 3 == 0 ? CloudShadow : PearlBright;
                PRTLoader.NewParticle<PRT_FishWyverntailFluff>(pos + Main.rand.NextVector2Circular(10f, 10f),
                    vel, col, Main.rand.NextFloat(0.55f, 0.95f))
                    ?.Configure(Main.rand.Next(20, 32), col, 0.5f, 0.02f);
            }

            var ring = PRTLoader.NewParticle<PRT_DWave>(pos, dir * 1.5f, RingBlue, 0.10f);
            ring?.Configure(new Vector2(1f, 0.55f), dir.ToRotation(), 0.42f, 12);

            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(pos, dir * Main.rand.NextFloat(3f, 6f) + Main.rand.NextVector2Circular(1.5f, 1.5f),
                    ManeGold, Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(ManeGold, Main.rand.Next(14, 20), 0.05f, 0.7f);
            }
        }

        /// <summary>命中云爆：≤2帧珍珠白过冲闪、暗云压底亮云覆面、冲击环、金屑、云 Gore</summary>
        public static void ImpactBurst(Vector2 center, Vector2 impactVel, IEntitySource goreSource) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dirBias = impactVel.SafeNormalize(Vector2.Zero);

            //珍珠白过冲闪：sin 包络峰值仅 1-2 帧
            PRTLoader.NewParticle<PRT_Sparkle>(center, Vector2.Zero, new Color(242, 246, 252), 1.4f)
                ?.Configure(new Color(242, 246, 252), 7, 0f, 1.6f);

            //暗云先落底
            for (int i = 0; i < 6; i++) {
                Vector2 vel = dirBias * 2f + Main.rand.NextVector2Circular(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_FishWyverntailFluff>(center + Main.rand.NextVector2Circular(14f, 14f),
                    vel, CloudShadow, Main.rand.NextFloat(1.1f, 1.6f))
                    ?.Configure(Main.rand.Next(40, 56), CloudShadow, 0.5f, 0.008f);
            }
            //珍珠白亮云覆面（更小更短命）
            for (int i = 0; i < 4; i++) {
                Vector2 vel = dirBias * 2.5f + Main.rand.NextVector2Circular(2.8f, 2.8f);
                PRTLoader.NewParticle<PRT_FishWyverntailFluff>(center + Main.rand.NextVector2Circular(8f, 8f),
                    vel, PearlBright, Main.rand.NextFloat(0.7f, 1f))
                    ?.Configure(Main.rand.Next(26, 40), PearlBright, 0.55f, 0.015f);
            }

            var ring = PRTLoader.NewParticle<PRT_DWave>(center, Vector2.Zero, RingBlue, 0.14f);
            ring?.Configure(new Vector2(1f, 0.85f), dirBias.ToRotation(), 0.62f, 13);

            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(center, dirBias * 2f + Main.rand.NextVector2Circular(5f, 5f),
                    ManeGold, Main.rand.NextFloat(0.5f, 0.85f))
                    ?.Configure(ManeGold, Main.rand.Next(16, 22), 0.06f, 0.75f);
            }

            //原版云 Gore：实体碎屑给云爆咬合感
            for (int i = 0; i < 5; i++) {
                Gore gore = Gore.NewGoreDirect(goreSource, center,
                    Main.rand.NextVector2Circular(3f, 3f) + impactVel * 0.25f, Main.rand.Next(11, 14));
                gore.timeLeft = Main.rand.Next(20, 30);
                gore.scale *= 1.15f;
            }
        }

        /// <summary>自然到期的静默化散：少量白絮原地慢散，无声无环</summary>
        public static void QuietDissolve(Vector2 center) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_FishWyverntailFluff>(center + Main.rand.NextVector2Circular(16f, 16f),
                    Main.rand.NextVector2Circular(1.2f, 1.2f), PearlBright, Main.rand.NextFloat(0.7f, 1f))
                    ?.Configure(Main.rand.Next(28, 42), PearlBright, 0.4f, 0.02f);
            }
        }

        /// <summary>飞行蜕云：龙身某骨节掉一片云屑</summary>
        public static void ShedFluff(Vector2 pos, Vector2 vel) {
            if (VaultUtils.isServer) {
                return;
            }
            Color col = Main.rand.NextBool(3) ? CloudShadow : PearlBright;
            PRTLoader.NewParticle<PRT_FishWyverntailFluff>(pos + Main.rand.NextVector2Circular(6f, 6f),
                vel, col, Main.rand.NextFloat(0.5f, 0.85f))
                ?.Configure(Main.rand.Next(30, 48), col, 0.42f, 0.022f);
        }
    }
}
