using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs
{
    /// <summary>
    /// 绯红裂空命中材质分流：金属走火花/白热，血肉走重力血珠（复用刻心者液滴）。
    /// 挥空刀光呼吸粒子不走此处。
    /// </summary>
    internal static class CrimsonRendHitVFX
    {
        public static readonly Color Blood = new(156, 22, 28);
        public static readonly Color BloodDeep = new(96, 12, 18);
        public static readonly Color Arterial = new(188, 32, 40);
        public static readonly Color WoundHot = new(210, 70, 58);

        /// <summary>每拍首次命中爆点粒子：金属火花 vs 血肉四溅</summary>
        public static void SpawnImpactBurst(Vector2 pos, Vector2 aimDir, float power, float sizeMul, bool steel) {
            if (Main.dedServ) {
                return;
            }
            if (steel) {
                SpawnSteelBurst(pos, aimDir, power, sizeMul);
            }
            else {
                SpawnFleshBurst(pos, aimDir, power, sizeMul);
            }
        }

        /// <summary>同拍后续命中的轻量跟刀粒子</summary>
        public static void SpawnHitTick(Vector2 pos, Vector2 aimDir, float sizeMul, bool steel) {
            if (Main.dedServ) {
                return;
            }
            if (steel) {
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = aimDir.RotatedByRandom(0.65) * Main.rand.NextFloat(4f, 12f) * sizeMul;
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 96, 60)
                        , Main.rand.NextFloat(0.4f, 0.8f) * sizeMul)
                        ?.Configure(Main.rand.Next(16, 28), affectedByGravity: true);
                }
            }
            else {
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = aimDir.RotatedByRandom(0.75) * Main.rand.NextFloat(4.5f, 11f) * sizeMul;
                    vel.Y -= Main.rand.NextFloat(0.4f, 1.8f);
                    Color c = Main.rand.NextBool(3) ? Arterial : Blood;
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, c
                        , Main.rand.NextFloat(0.95f, 1.55f) * sizeMul)
                        ?.Configure(Main.rand.Next(20, 34), 0.30f);
                }
                //一两滴慢重余韵
                for (int i = 0; i < 2; i++) {
                    Vector2 vel = aimDir.RotatedByRandom(1.1) * Main.rand.NextFloat(1.2f, 3.5f) * sizeMul;
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, BloodDeep
                        , Main.rand.NextFloat(1.1f, 1.7f) * sizeMul)
                        ?.Configure(Main.rand.Next(28, 42), 0.36f);
                }
            }
        }

        private static void SpawnSteelBurst(Vector2 pos, Vector2 aimDir, float power, float sizeMul) {
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(pos, Vector2.Zero
                , new Color(255, 225, 205), (0.75f + power * 0.8f) * sizeMul);
            int satellites = 1 + (int)(power * 2f);
            for (int i = 0; i < satellites; i++) {
                Vector2 off = Main.rand.NextVector2Circular(24f, 24f) * sizeMul;
                PRTLoader.NewParticle<PRT_CrimsonHitFlash>(pos + off, off * 0.05f
                    , new Color(255, 140, 110), Main.rand.NextFloat(0.5f, 0.75f) * sizeMul);
            }

            int mainSparks = 8 + (int)(power * 14f);
            for (int i = 0; i < mainSparks; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.78) * Main.rand.NextFloat(5f, 12f + power * 10f) * sizeMul;
                Color c = Main.rand.NextBool(3) ? new Color(255, 236, 210) : new Color(255, 92, 58);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, c
                    , Main.rand.NextFloat(0.45f, 0.7f + power * 0.4f) * sizeMul)
                    ?.Configure(Main.rand.Next(18, 30 + (int)(power * 12f)), affectedByGravity: true);
            }
            int backSparks = 2 + (int)(power * 5f);
            for (int i = 0; i < backSparks; i++) {
                Vector2 vel = (-aimDir).RotatedByRandom(1.1) * Main.rand.NextFloat(3f, 8f) * sizeMul;
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 70, 46)
                    , Main.rand.NextFloat(0.35f, 0.6f) * sizeMul)
                    ?.Configure(Main.rand.Next(16, 26), affectedByGravity: false);
            }
        }

        private static void SpawnFleshBurst(Vector2 pos, Vector2 aimDir, float power, float sizeMul) {
            //伤口暗红雾：体积垫底，不发光
            for (int i = 0; i < 2 + (int)(power * 2f); i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1.6f) * sizeMul;
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos + Main.rand.NextVector2Circular(8f, 6f) * sizeMul
                    , vel, Color.White, Main.rand.NextFloat(0.08f, 0.14f) * sizeMul)
                    ?.Configure(Main.rand.Next(22, 36), Blood, BloodDeep, 0.01f);
            }

            //动脉喷溅：沿刃向锥形甩出，出膛拉丝、重力坠弧
            int mainDrops = 10 + (int)(power * 16f);
            for (int i = 0; i < mainDrops; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.82) * Main.rand.NextFloat(6f, 13f + power * 10f) * sizeMul;
                vel.Y -= Main.rand.NextFloat(0.8f, 2.8f);
                Color c = Main.rand.NextBool(4) ? Arterial : (Main.rand.NextBool() ? Blood : WoundHot);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, c
                    , Main.rand.NextFloat(1.0f, 1.75f + power * 0.35f) * sizeMul)
                    ?.Configure(Main.rand.Next(22, 36 + (int)(power * 10f)), 0.30f);
            }

            //慢重血珠：喷溅余韵，读作液体而非火花
            int slowDrops = 3 + (int)(power * 5f);
            for (int i = 0; i < slowDrops; i++) {
                Vector2 vel = aimDir.RotatedByRandom(1.15) * Main.rand.NextFloat(1.4f, 4.2f) * sizeMul;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, BloodDeep
                    , Main.rand.NextFloat(1.2f, 1.9f) * sizeMul)
                    ?.Configure(Main.rand.Next(30, 48), 0.36f, 0.978f);
            }

            //背向溅出：伤口反侧的慢弧
            int backDrops = 2 + (int)(power * 4f);
            for (int i = 0; i < backDrops; i++) {
                Vector2 vel = (-aimDir).RotatedByRandom(1.0) * Main.rand.NextFloat(2.5f, 7f) * sizeMul;
                vel.Y -= Main.rand.NextFloat(0.3f, 1.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, Blood
                    , Main.rand.NextFloat(0.9f, 1.4f) * sizeMul)
                    ?.Configure(Main.rand.Next(20, 34), 0.28f);
            }
        }
    }

    /// <summary>刀光燃尽烟：暗红→焦黑 AlphaBlend 染色烟团，缓慢外漂、放大、消散</summary>
    internal class PRT_CrimsonSmoke : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;

        private float spin;
        private Color hotColor;
        private Color coldColor;

        public PRT_CrimsonSmoke Configure(int lifetime, Color hot, Color cold, float rotSpeed = 0.012f) {
            Lifetime = lifetime;
            hotColor = hot;
            coldColor = cold;
            spin = rotSpeed * (Main.rand.NextBool() ? 1f : -1f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            hotColor = coldColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            ai[0] = Main.rand.Next(4);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(34, 50);
                hotColor = new Color(120, 24, 30);
                coldColor = new Color(30, 14, 24);
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            Scale *= 1.008f;
            Rotation += spin;
            Velocity *= 0.94f;
            Velocity.Y -= 0.012f;   //烟微微上浮

            Color = Color.Lerp(hotColor, coldColor, MathF.Min(1f, t * 1.3f));
            //快进快出的透明度包络，峰值压低避免烟层堆积吞掉刀光；
            //提前收尾让接近焦黑的末段几乎不可见，白天背景下不残留灰色剪影
            Opacity = MathF.Min(t / 0.12f, 1f) * (1f - SmoothStep01((t - 0.42f) / 0.50f)) * 0.42f;
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int index = (int)ai[0];
            int frameSize = tex.Width / 2;
            Rectangle frame = new(index % 2 * frameSize, index / 2 * frameSize, frameSize, frameSize);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation
                , frame.Size() * 0.5f, Scale * 0.5f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>冲击火花：加色四芒星拉长条，惯性抛物 + 末段重力下坠</summary>
    internal class PRT_CrimsonSpark : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarGlow01";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 4000;

        private Color initialColor;
        private bool gravity;

        public PRT_CrimsonSpark Configure(int lifetime, bool affectedByGravity) {
            Lifetime = lifetime;
            initialColor = Color;
            gravity = affectedByGravity;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = false;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Scale *= 0.955f;
            Velocity *= 0.94f;
            if (gravity && Velocity.Length() < 11f) {
                Velocity.X *= 0.96f;
                Velocity.Y += 0.30f;
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(LifetimeCompletion, 2.4f));
            if (Scale < 0.04f) {
                active = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            //沿速度方向拉长成火花条，叠一层窄条提亮芯部
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.16f, 0.9f, 2.6f);
            Vector2 scale = new Vector2(0.42f, stretch) * Scale;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation
                , tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * 0.8f, Rotation
                , tex.Size() * 0.5f, scale * new Vector2(0.45f, 1f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>命中火花序列帧：2×2 手绘火花图集单次播放，加色</summary>
    internal class PRT_CrimsonHitFlash : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "HitSparkSheet01";
        public override bool CanPool => true;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = 14;
            }
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            Velocity *= 0.9f;
            Opacity = 1f - MathF.Pow(LifetimeCompletion, 3f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int frameIdx = (int)MathHelper.Clamp(LifetimeCompletion * 4f, 0f, 3f);
            Rectangle frame = new(frameIdx % 2 * 128, frameIdx / 2 * 128, 128, 128);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation
                , frame.Size() * 0.5f, Scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
