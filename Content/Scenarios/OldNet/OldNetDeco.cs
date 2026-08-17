using CalamityOverhaul.Content.Scenarios.OldNet.Backgrounds;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    /// <summary>
    /// 旧网环境装饰：上浮数据尘（PRT_CyberMote 的旧网变体，烬红衰败色板 +
    /// ~4% 冷青幸存残光，密度随带内腐化上量）；黑墙涌动期自西向东加放红烬横波。
    /// client-only，由 <see cref="Backgrounds.OldNetAmbience"/> 每帧驱动
    /// </summary>
    internal static class OldNetDeco
    {
        public static void Update() {
            if (Main.gameMenu) {
                return;
            }
            float presence = OldNetAmbience.Presence;
            if (presence < 0.25f) {
                return;
            }
            float corrupt = OldNetMetrics.CorruptionAt((int)(Main.LocalPlayer.Center.X / 16f));

            //基础上浮尘：密度随腐化 1/12 → 1/5，稳态屏内约 20~45 粒
            int rate = (int)MathHelper.Lerp(12f, 5f, corrupt);
            if (Main.rand.NextBool(Math.Max(rate, 1))) {
                float x = Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth);
                float y = Main.screenPosition.Y + Main.screenHeight + Main.rand.NextFloat(20f, 80f);
                Vector2 vel = new(0f, -Main.rand.NextFloat(0.6f, 1.5f));
                //烬红为主，~4% 冷青幸存残光（与天幕余烬同语汇）
                Color core = Main.rand.NextBool(25)
                    ? new Color(66, 160, 170) : new Color(205, 62, 34);
                PRTLoader.NewParticle<PRT_OldNetMote>(new Vector2(x, y), vel, core,
                    Main.rand.NextFloat(0.6f, 1.2f))?.Configure(Main.rand.Next(150, 240));
            }

            //黑墙涌动：自西向东的红烬横波（墙在向旧网深处呼气）
            float surge = OldNetSkyEvents.Surge;
            if (surge > 0.25f && Main.rand.NextBool(3)) {
                float x = Main.screenPosition.X - Main.rand.NextFloat(30f, 90f);
                float y = Main.screenPosition.Y + Main.rand.NextFloat(Main.screenHeight);
                Vector2 vel = new(1.2f + Main.rand.NextFloat(2.2f, 4.5f) * surge,
                    Main.rand.NextFloat(-0.35f, 0.35f));
                PRTLoader.NewParticle<PRT_OldNetMote>(new Vector2(x, y), vel,
                    new Color(235, 70, 36), Main.rand.NextFloat(0.7f, 1.3f))
                    ?.Configure(Main.rand.Next(90, 150), horizontal: true);
            }
        }
    }

    /// <summary>旧网数据尘：速度拉伸微光条；竖尘缓升蛇行，横尘（涌动波）直线掠过</summary>
    internal class PRT_OldNetMote : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 150;
        public override bool CanPool => true;

        private float drift;
        private float driftPhase;
        private bool horizontal;

        public PRT_OldNetMote Configure(int lifeTime, bool horizontal = false) {
            Lifetime = lifeTime;
            this.horizontal = horizontal;
            drift = Main.rand.NextFloat(0.5f, 1.3f);
            driftPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            drift = 0f;
            driftPhase = 0f;
            horizontal = false;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            //竖尘：缓慢上浮 + 横向蛇行；横尘：保持冲势 + 纵向轻摆
            if (horizontal) {
                Velocity.Y = MathF.Sin(Time * 0.03f + driftPhase) * 0.18f * drift;
            }
            else {
                Velocity.X = MathF.Sin(Time * 0.02f + driftPhase) * 0.22f * drift;
            }

            float life = LifetimeCompletion;
            float fadeIn = MathHelper.Clamp(Time / 30f, 0f, 1f);
            float fadeOut = 1f - MathHelper.Clamp((life - 0.75f) / 0.25f, 0f, 1f);
            Opacity = fadeIn * fadeOut * 0.85f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.02f) {
                return false;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;

            //沿速度方向拉伸的微光条
            float speed = Velocity.Length();
            float len = (7f + speed * 6f) * Scale;
            float wid = 1.3f * Scale;
            float rot = Velocity.ToRotation();
            Vector2 origin = new(0.5f, 0.5f);
            Rectangle src = new(0, 0, 1, 1);

            Color outer = Color * (Opacity * 0.35f);
            spriteBatch.Draw(pixel, drawPos, src, outer, rot,
                origin, new Vector2(len * 1.3f, wid * 2.6f), SpriteEffects.None, 0f);

            Color inner = Color * Opacity;
            spriteBatch.Draw(pixel, drawPos, src, inner, rot,
                origin, new Vector2(len, wid), SpriteEffects.None, 0f);

            return false;
        }
    }
}
