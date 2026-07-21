using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>腐虫漫迹专属 shader 资源（域内加载器，不动 EffectLoader）</summary>
    internal class FishEaterofPlanktonAssets
    {
        /// <summary>蠕虫体节条带（预乘 alpha，配 AlphaBlend）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishPlanktonWorm { get; private set; }
    }

    /// <summary>腐虫漫迹</summary>
    internal static class FishEaterofPlanktonVFX
    {
        /// <summary>暗腐绿黑（外缘/剪影底/腐解余渣）</summary>
        public static readonly Color RotDark = new(26, 30, 14);
        /// <summary>腐绿褐（虫体主色）</summary>
        public static readonly Color RotGreen = new(88, 102, 40);
        /// <summary>粘液绿（滴液、湿痕）</summary>
        public static readonly Color RotSlime = new(126, 146, 60);
        /// <summary>肉粉（节间膜、碎屑截面）</summary>
        public static readonly Color FleshPink = new(172, 118, 112);
        /// <summary>湿光淡绿白（非纯白，仅小面积瞬时）</summary>
        public static readonly Color WetPale = new(198, 208, 168);

        /// <summary>腐绿粘液族随机取色（滴液用）</summary>
        public static Color Slime(float t) => Color.Lerp(RotDark, RotSlime, t);

        /// <summary>出生甩出</summary>
        public static void SpawnLurch(Vector2 pos, Vector2 dir) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.55f, 0.55f)) * Main.rand.NextFloat(1.5f, 4f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos + Main.rand.NextVector2Circular(5f, 5f)
                    , vel, Slime(Main.rand.NextFloat(0.4f, 1f)), Main.rand.NextFloat(0.4f, 0.62f))
                    ?.Configure(Main.rand.Next(14, 22), 0.22f, 0.985f);
            }
            for (int i = 0; i < 2; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.CorruptGibs
                    , dir * Main.rand.NextFloat(0.5f, 1.5f) + Main.rand.NextVector2Circular(0.8f, 0.8f)
                    , 120, default, Main.rand.NextFloat(0.7f, 1f));
                d.noGravity = false;
            }
        }

        /// <summary>体节渗液</summary>
        public static void SegmentOoze(Vector2 pos, Vector2 bodyVel) {
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos + Main.rand.NextVector2Circular(2f, 2f)
                , bodyVel * 0.25f + new Vector2(0f, Main.rand.NextFloat(0.2f, 0.7f))
                , Slime(Main.rand.NextFloat(0.35f, 0.9f)), Main.rand.NextFloat(0.32f, 0.5f))
                ?.Configure(Main.rand.Next(16, 26), 0.24f, 0.99f);
        }

        /// <summary>撕咬爆发，咬向锥形滴液喷 + 肉屑块 + 腐肉 Dust 底噪，ke 0..1 动能</summary>
        public static void BiteBurst(Vector2 pos, Vector2 dir, float ke) {
            if (Main.dedServ) {
                return;
            }
            int drops = 5 + (int)(3f * ke);
            for (int i = 0; i < drops; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(2f, 4.5f + 3f * ke);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel
                    , Slime(Main.rand.NextFloat(0.3f, 1f)), Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(16, 26), 0.26f);
            }
            int scraps = 2 + (ke > 0.5f ? 1 : 0);
            for (int i = 0; i < scraps; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(2f, 4.5f) - Vector2.UnitY * 1.4f;
                PRTLoader.NewParticle<PRT_FishPlanktonScrap>(pos, vel, default, Main.rand.NextFloat(0.7f, 1f))
                    ?.Configure(Main.rand.Next(22, 34));
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(6f, 6f), DustID.CorruptGibs
                    , dir.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(1f, 3f)
                    , 110, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }
        }

        /// <summary>腐解剥落</summary>
        public static void DecaySlough(Vector2 pos, Vector2 drift) {
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, drift * 0.4f + new Vector2(0f, 0.4f)
                , Slime(Main.rand.NextFloat(0.3f, 0.8f)), Main.rand.NextFloat(0.35f, 0.55f))
                ?.Configure(Main.rand.Next(18, 28), 0.2f, 0.99f);
            if (Main.rand.NextBool()) {
                PRTLoader.NewParticle<PRT_FishPlanktonScrap>(pos, drift * 0.5f + Main.rand.NextVector2Circular(1f, 1f) - Vector2.UnitY * 0.8f
                    , default, Main.rand.NextFloat(0.55f, 0.8f))?.Configure(Main.rand.Next(20, 32));
            }
        }
    }

    /// <summary>腐虫肉屑块</summary>
    internal class PRT_FishPlanktonScrap : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private float gravity;
        private float spin;
        private bool bounced;
        private float tone;//块面深浅随机, 群体有层次

        public PRT_FishPlanktonScrap Configure(int lifetime, float gravityStrength = 0.26f) {
            Lifetime = lifetime;
            gravity = gravityStrength;
            return this;
        }

        public override void Reset() {
            base.Reset();
            gravity = 0f;
            spin = 0f;
            bounced = false;
            tone = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.1f, 0.26f) * (Main.rand.NextBool() ? 1f : -1f);
            tone = Main.rand.NextFloat(0.7f, 1.05f);
            //防漏 Configure 兜底
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(22, 34);
            }
            if (gravity == 0f) {
                gravity = 0.26f;
            }
        }

        public override void AI() {
            if (Velocity.Y < 14f) {
                Velocity.Y += gravity;
            }
            Velocity.X *= 0.985f;
            //翻滚速率挂水平速度:滚得快转得快
            Rotation += spin * (0.5f + Math.Abs(Velocity.X) * 0.06f);

            //飞行中甩落细绿点
            if (!bounced && Main.rand.NextBool(9) && Velocity.LengthSquared() > 2f) {
                Dust d = Dust.NewDustPerfect(Position, DustID.CorruptGibs, Velocity * 0.2f, 140, default, 0.7f);
                d.noGravity = false;
            }

            if (Velocity.Y > 0f && Collision.SolidCollision(Position - new Vector2(3f), 6, 6)) {
                if (!bounced) {
                    bounced = true;
                    Velocity.Y = -Math.Abs(Velocity.Y) * 0.32f;
                    Velocity.X *= 0.5f;
                    spin *= 1.4f;
                }
                else {
                    //二次触地落定:停移缓转, 提前进入收尾
                    Velocity *= 0.2f;
                    spin *= 0.4f;
                    if (Lifetime - Time > 7) {
                        Time = Lifetime - 7;
                    }
                }
            }

            Opacity = MathHelper.Clamp(Time / 2f, 0f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 3f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.02f) {
                return false;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = new(0.5f, 0.5f);
            Color light = Lighting.GetColor(Position.ToTileCoordinates());
            Color under = FishEaterofPlanktonVFX.RotDark.MultiplyRGB(light);
            Color face = Color.Lerp(FishEaterofPlanktonVFX.RotGreen, FishEaterofPlanktonVFX.RotDark, 0.2f).MultiplyRGB(light) * tone;
            Color sinew = FishEaterofPlanktonVFX.FleshPink.MultiplyRGB(light) * (tone * 0.9f);

            float w = 5.6f * Scale;
            float h = 3.8f * Scale;
            Vector2 rotDown = (Rotation + MathHelper.PiOver2).ToRotationVector2();

            //暗底错位在下:给碎屑厚度
            spriteBatch.Draw(pixel, pos + rotDown * (h * 0.22f), src, under * Opacity, Rotation
                , origin, new Vector2(w, h), SpriteEffects.None, 0f);
            //腐绿肉面
            spriteBatch.Draw(pixel, pos, src, face * Opacity, Rotation
                , origin, new Vector2(w * 0.9f, h * 0.84f), SpriteEffects.None, 0f);
            //肉粉截面小棱:偏转错位打破矩形轮廓
            spriteBatch.Draw(pixel, pos - rotDown * (h * 0.2f), src, sinew * (Opacity * 0.8f), Rotation + 0.6f
                , origin, new Vector2(w * 0.38f, h * 0.32f), SpriteEffects.None, 0f);
            //年轻期湿光点(极小面积, 非纯白)
            if (LifetimeCompletion < 0.3f) {
                spriteBatch.Draw(pixel, pos - rotDown * (h * 0.28f), src
                    , (FishEaterofPlanktonVFX.WetPale with { A = 0 }) * (Opacity * 0.35f)
                    , Rotation, origin, new Vector2(w * 0.18f, h * 0.15f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
