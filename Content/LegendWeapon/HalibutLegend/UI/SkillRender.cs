using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    internal class SkillRender : RenderHandle
    {
        //技能切换演出相关
        public static FishSkill SwitchingSkill;//正在切换的技能
        public static float SwitchAnimProgress;//切换动画进度(0-1)
        public static int SwitchAnimTimer;//切换动画计时器
        private const int SwitchAnimDuration = 60;//切换动画持续时间

        //缓存的动画状态(在Update中计算,在Draw中使用)
        private static float cachedAlpha = 1f;
        private static float cachedScale = 1.5f;
        private static float cachedYOffset = 0f;
        private static float cachedRingRotation = 0f;

        public override void UpdateBySystem(int index) {//此处为逻辑更新
            if (SwitchingSkill == null) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (!player.active) {
                SwitchingSkill = null;
                SwitchAnimProgress = 0f;
                SwitchAnimTimer = 0;
                return;
            }

            //更新动画
            SwitchAnimTimer++;
            SwitchAnimProgress = SwitchAnimTimer / (float)SwitchAnimDuration;

            if (SwitchAnimProgress >= 1f) {
                SwitchingSkill = null;
                SwitchAnimProgress = 0f;
                SwitchAnimTimer = 0;
                return;
            }

            //计算动画状态
            CalculateAnimationState();
        }

        private static void CalculateAnimationState() {
            //动画阶段
            float fadeInEnd = 0.2f;
            float holdEnd = 0.8f;
            float fadeOutEnd = 1f;

            if (SwitchAnimProgress < fadeInEnd) {
                //淡入+上升
                float t = SwitchAnimProgress / fadeInEnd;
                cachedAlpha = t;
                cachedScale = MathHelper.Lerp(0.5f, 1.5f, CWRUtils.EaseOutBack(t));
                cachedYOffset = MathHelper.Lerp(-20f, 0f, CWRUtils.EaseOutCubic(t));
            }
            else if (SwitchAnimProgress < holdEnd) {
                //悬停
                float t = (SwitchAnimProgress - fadeInEnd) / (holdEnd - fadeInEnd);
                cachedAlpha = 1f;
                cachedScale = 1.5f + MathF.Sin(t * MathHelper.TwoPi * 2f) * 0.1f;//轻微缩放
                cachedYOffset = MathF.Sin(t * MathHelper.TwoPi) * 3f;//轻微浮动
            }
            else {
                //淡出+上升
                float t = (SwitchAnimProgress - holdEnd) / (fadeOutEnd - holdEnd);
                cachedAlpha = 1f - t;
                cachedScale = MathHelper.Lerp(1.5f, 2f, CWRUtils.EaseInCubic(t));
                cachedYOffset = MathHelper.Lerp(0f, 20f, CWRUtils.EaseInCubic(t));
            }

            //计算光环旋转
            cachedRingRotation = SwitchAnimProgress * MathHelper.TwoPi * 2f;
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (SwitchingSkill == null || SwitchAnimProgress <= 0f) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (!player.active) {
                return;
            }

            //计算技能图标位置(玩家头顶)
            Vector2 playerHeadPos = player.Top + new Vector2(0, -60);
            Vector2 screenPos = playerHeadPos - Main.screenPosition;
            screenPos.Y += cachedYOffset;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //绘制发光效果
            Texture2D glowTex = CWRAsset.StarTexture.Value;
            Color glowColor = Color.Gold with { A = 0 } * cachedAlpha * 0.6f;
            Main.spriteBatch.Draw(glowTex, screenPos, null, glowColor, 0f, glowTex.Size() / 2f, cachedScale * 0.2f, SpriteEffects.None, 0);

            //绘制外圈旋转光环
            Color ringColor = Color.Gold with { A = 0 } * cachedAlpha * 0.8f;
            Main.spriteBatch.Draw(glowTex, screenPos, null, ringColor, cachedRingRotation, glowTex.Size() / 2f, cachedScale * 0.25f, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(glowTex, screenPos, null, ringColor, -cachedRingRotation, glowTex.Size() / 2f, cachedScale * 0.22f, SpriteEffects.None, 0);

            //绘制技能图标
            Texture2D iconTex = SwitchingSkill.Icon;
            Color iconColor = Color.White * cachedAlpha;
            Main.spriteBatch.Draw(iconTex, screenPos, null, iconColor, 0f, iconTex.Size() / 2f, cachedScale, SpriteEffects.None, 0);

            Main.spriteBatch.End();

            //绘制粒子效果
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 particlePos = screenPos + angle.ToRotationVector2() * Main.rand.NextFloat(30f, 50f) * cachedScale;
                int dust = Dust.NewDust(particlePos, 1, 1, DustID.GoldCoin, 0, -2f, 100, default, Main.rand.NextFloat(1f, 1.5f));
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Vector2.Zero;
            }
        }
    }
}
