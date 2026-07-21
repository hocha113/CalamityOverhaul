using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>闪光鱼雷的军火配色与共用粒子生成</summary>
    internal static class FishDynamiteVFX
    {
        public static readonly Color HotWhite = new(255, 246, 228);     //白热过冲，只许两帧
        public static readonly Color SparkGold = new(255, 200, 90);     //引信火星亮端
        public static readonly Color SparkDeep = new(235, 128, 40);     //引信火星暗端
        public static readonly Color FireHot = new(255, 162, 66);       //爆炸火球初色
        public static readonly Color ShrapnelEdge = new(255, 168, 78);  //弹片出膛热缘
        public static readonly Color ShrapnelEmber = new(142, 52, 22);  //弹片余烬
        public static readonly Color ShrapnelDark = new(64, 36, 26);    //弹片熄灭
        public static readonly Color SmokeHot = new(118, 110, 102);     //硝烟暖灰
        public static readonly Color SmokeCold = new(50, 48, 48);       //硝烟冷灰
        public static readonly Color DustWallHot = new(112, 98, 82);    //尘墙扬土
        public static readonly Color DustWallCold = new(46, 42, 40);
        public static readonly Color AshGray = new(58, 52, 48);         //灰烬絮片
        public static readonly Color WarnRed = new(255, 62, 40);        //警灯红

        public static void FuseSpark(Vector2 pos, Vector2 vel) {
            Color c = Color.Lerp(SparkGold, SparkDeep, Main.rand.NextFloat());
            PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, c, Main.rand.NextFloat(0.45f, 0.85f))
                ?.Configure(Main.rand.Next(10, 18), true);
        }

        public static void Smoke(Vector2 pos, Vector2 vel, float scale, int lifetime, Color hot, Color cold, float spin = 0.02f) {
            PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel, default, scale)
                ?.Configure(lifetime, hot, cold, spin);
        }

        public static void Ash(Vector2 pos, Vector2 vel, float scale = 1f) {
            PRTLoader.NewParticle<PRT_FishDynamiteAsh>(pos, vel, AshGray, scale)
                ?.Configure(Main.rand.Next(45, 75));
        }

        public static void FusePop(Vector2 pos, float scale) {
            PRTLoader.NewParticle<PRT_FishDynamiteFusePop>(pos, Vector2.Zero, HotWhite, scale);
        }
    }

    /// <summary>弹片流光</summary>
    internal class PRT_FishDynamiteShrapnel : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 2000;

        private float baseScale;

        public PRT_FishDynamiteShrapnel Configure(int lifetime) {
            Lifetime = lifetime;
            baseScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            baseScale = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 1f; //池回收后 Opacity 归零，防首帧绘制早于 AI 时不可见
        }

        public override void AI() {
            float t = LifetimeCompletion;
            Velocity *= 0.965f;
            if (Velocity.Length() < 16f) {
                Velocity.X *= 0.985f;
                Velocity.Y += 0.42f;    //弹片沉重，坠得比火星快
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            Scale = baseScale * (1f - t * 0.35f);

            //冷却
            Color = t < 0.10f
                ? Color.Lerp(FishDynamiteVFX.ShrapnelEdge, FishDynamiteVFX.SparkDeep, t / 0.10f)
                : t < 0.62f
                    ? Color.Lerp(FishDynamiteVFX.SparkDeep, FishDynamiteVFX.ShrapnelEmber, (t - 0.10f) / 0.52f)
                    : Color.Lerp(FishDynamiteVFX.ShrapnelEmber, FishDynamiteVFX.ShrapnelDark, (t - 0.62f) / 0.38f);
            Opacity = 1f - MathF.Pow(t, 2.5f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            float speed = Velocity.Length();
            //短尾
            float stretch = MathHelper.Clamp(speed * 0.11f, 0.8f, 2.1f);
            Vector2 bodyScale = new Vector2(0.34f, stretch) * Scale;

            //暗橙尾
            for (int k = 3; k >= 1; k--) {
                Vector2 gpos = Position - Velocity * (k * 0.6f) - Main.screenPosition;
                float fade = 0.42f - k * 0.11f;
                spriteBatch.Draw(tex, gpos, null, FishDynamiteVFX.ShrapnelEmber * (Opacity * fade), Rotation
                    , origin, bodyScale * (1f - k * 0.16f), SpriteEffects.None, 0f);
            }

            Vector2 pos = Position - Main.screenPosition;
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, bodyScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * (Opacity * 0.85f), Rotation
                , origin, bodyScale * new Vector2(0.45f, 0.92f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>引信白热爆点，前两帧纯白过冲，随即落金收缩熄灭，噼啪节拍的视觉锚</summary>
    internal class PRT_FishDynamiteFusePop : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarGlow01";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Opacity = 1f;
            if (Lifetime <= 0) {
                Lifetime = 5;
            }
        }

        public override void AI() {
            Velocity *= 0.8f;
            //白热只许两帧，之后落金
            Color = Time < 2 ? FishDynamiteVFX.HotWhite : FishDynamiteVFX.SparkGold;
            Opacity = 1f - MathF.Pow(LifetimeCompletion, 1.6f);
            Scale *= 0.90f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D star = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };
            if (SoftGlow?.Value is Texture2D glow) {
                spriteBatch.Draw(glow, pos, null, FishDynamiteVFX.SparkDeep with { A = 0 } * (0.45f * Opacity)
                    , 0f, glow.Size() * 0.5f, Scale * 0.9f, SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(star, pos, null, col * Opacity, Rotation, star.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos, null, col * (Opacity * 0.7f), Rotation + MathHelper.PiOver4
                , star.Size() * 0.5f, Scale * 0.6f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>引信灰烬</summary>
    internal class PRT_FishDynamiteAsh : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float sway;

        public PRT_FishDynamiteAsh Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            sway = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            sway = Main.rand.NextFloat(MathHelper.TwoPi);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(45, 70);
            }
        }

        public override void AI() {
            //飘摆下坠
            Velocity.X = Velocity.X * 0.96f + MathF.Sin(Time * 0.20f + sway) * 0.05f;
            Velocity.Y = MathF.Min(Velocity.Y + 0.08f, 2.0f);
            Rotation += sway > MathHelper.Pi ? -0.04f : 0.04f;

            float t = LifetimeCompletion;
            Opacity = MathF.Min(t / 0.15f, 1f) * (1f - Utils.GetLerpValue(0.65f, 1f, t, true));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 sc = new Vector2(0.16f, 0.11f) * Scale;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * (0.85f * Opacity), Rotation
                , tex.Size() * 0.5f, sc, SpriteEffects.None, 0f);
            return false;
        }
    }
}
