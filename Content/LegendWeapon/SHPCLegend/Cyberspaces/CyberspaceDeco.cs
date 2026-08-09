using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>L3 接管期环境装饰：屏外下缘生成的上浮数据尘（client-only，读 Viewed）</summary>
    internal static class CyberspaceDeco
    {
        public static void Update() {
            if (Main.gameMenu) {
                return;
            }
            float takeover = Cyberspace.ViewedTakeover;
            if (takeover < 0.25f) {
                return;
            }
            //简约偏好不加装饰粒子
            if (DomainVisuals.Concise) {
                return;
            }
            //期望稳态屏内约20余粒（生成率 1/8 × 寿命 150~240）
            if (!Main.rand.NextBool(8)) {
                return;
            }

            float x = Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth);
            float y = Main.screenPosition.Y + Main.screenHeight + Main.rand.NextFloat(20f, 80f);
            Vector2 vel = new(0f, -Main.rand.NextFloat(0.7f, 1.6f));
            Color core = new(242, 52, 32);
            PRTLoader.NewParticle<PRT_CyberMote>(new Vector2(x, y), vel, core,
                Main.rand.NextFloat(0.6f, 1.3f))?.Configure(Main.rand.Next(150, 240));
        }
    }

    /// <summary>上浮数据尘：速度拉伸微光条，长寿命缓升，两端淡入淡出</summary>
    internal class PRT_CyberMote : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 120;
        public override bool CanPool => true;

        private float drift;
        private float driftPhase;

        public PRT_CyberMote Configure(int lifeTime) {
            Lifetime = lifeTime;
            drift = Main.rand.NextFloat(0.5f, 1.3f);
            driftPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            drift = 0f;
            driftPhase = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            //缓慢上浮 + 轻微横向蛇行（每粒相位独立）
            Velocity.X = MathF.Sin(Time * 0.02f + driftPhase) * 0.22f * drift;

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
            float len = (7f + MathF.Abs(Velocity.Y) * 7f) * Scale;
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
