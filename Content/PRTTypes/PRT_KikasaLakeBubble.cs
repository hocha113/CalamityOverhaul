using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 血湖水下气泡：暗血光泽的小泡上浮摆动，触及水线破裂，偶尔留一圈微涟漪。
    /// 仅观看端生成（NPC 潜行、墨滴穿水的拖尾都用它），色板随鬼雨异化冷化
    /// </summary>
    internal class PRT_KikasaLakeBubble : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle6";

        public override bool CanPool => true;

        private float driftPhase;
        //破裂线：生成帧水线兜底，Viewed 在场时逐帧刷新成活水线（潮在动，泡跟着活线破）
        private float popY;

        public PRT_KikasaLakeBubble Configure(int lifetime, float lakeY) {
            Lifetime = lifetime;
            popY = lakeY;
            driftPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            driftPhase = 0f;
            popY = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(36, 70);
            }
            //暗血高光泡，鬼雨异化随观看域转冷灰
            Color = KikasaDomain.CoolTint(new Color(206, 96, 88, 0), new Color(142, 168, 174, 0));
        }

        public override void AI() {
            //浮力渐增到上浮终速，横向缓摆——稠液里的泡，慢
            driftPhase += 0.11f + Scale * 0.04f;
            Velocity.X = Velocity.X * 0.92f + MathF.Sin(driftPhase) * 0.085f;
            Velocity.Y = MathF.Max(Velocity.Y - 0.045f, -(0.8f + Scale * 0.7f));

            KikasaDomainPlayer viewed = KikasaDomain.Viewed;
            if (viewed != null && viewed.AnyActive) {
                popY = viewed.LakeWorldY;
            }
            if (popY != 0f && Position.Y <= popY + 2f) {
                //触线破裂：微圈限量放，不抢主涟漪池
                if (Main.rand.NextBool(4)) {
                    KikasaDomainDeco.RippleAt(new Vector2(Position.X, popY), 0.16f);
                }
                active = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;
            float alpha = MathF.Sin(LifetimeCompletion * MathHelper.Pi);

            //泡壁弱环 + 更小的实心亮点，读作泡不是光球
            spriteBatch.Draw(texture, drawPos, null, Color * (alpha * 0.4f),
                0f, origin, Scale * 1.1f, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, drawPos, null, Color * (alpha * 0.85f),
                0f, origin, Scale * 0.5f, SpriteEffects.None, 0f);
            Vector2 highlightOffset = new Vector2(-Scale * 1.2f, -Scale * 1.2f);
            spriteBatch.Draw(texture, drawPos + highlightOffset, null,
                new Color(255, 230, 224, 0) * (alpha * 0.5f),
                0f, origin, Scale * 0.2f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
