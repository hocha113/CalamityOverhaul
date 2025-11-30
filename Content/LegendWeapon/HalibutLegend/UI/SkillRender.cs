using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
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
        
        //技能名称动画状态
        private static float cachedNameAlpha = 1f;
        private static float cachedNameScale = 1f;
        private static float cachedNameYOffset = 0f;
        private static float cachedNameWavePhase = 0f;

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
                
                //名称动画 - 稍微延迟，从下方飞入
                float nameT = Math.Max(0, (t - 0.3f) / 0.7f);
                cachedNameAlpha = CWRUtils.EaseOutCubic(nameT);
                cachedNameScale = MathHelper.Lerp(0.3f, 1f, CWRUtils.EaseOutBack(nameT));
                cachedNameYOffset = MathHelper.Lerp(30f, 0f, CWRUtils.EaseOutCubic(nameT));
            }
            else if (SwitchAnimProgress < holdEnd) {
                //悬停
                float t = (SwitchAnimProgress - fadeInEnd) / (holdEnd - fadeInEnd);
                cachedAlpha = 1f;
                cachedScale = 1.5f + MathF.Sin(t * MathHelper.TwoPi * 2f) * 0.1f;//轻微缩放
                cachedYOffset = MathF.Sin(t * MathHelper.TwoPi) * 3f;//轻微浮动
                
                //名称动画 - 悬停并波动
                cachedNameAlpha = 1f;
                cachedNameScale = 1f + MathF.Sin(t * MathHelper.TwoPi * 1.5f) * 0.05f;
                cachedNameYOffset = MathF.Sin(t * MathHelper.TwoPi * 0.8f) * 2f;
                cachedNameWavePhase = t * MathHelper.TwoPi * 3f;
            }
            else {
                //淡出+上升
                float t = (SwitchAnimProgress - holdEnd) / (fadeOutEnd - holdEnd);
                cachedAlpha = 1f - t;
                cachedScale = MathHelper.Lerp(1.5f, 2f, CWRUtils.EaseInCubic(t));
                cachedYOffset = MathHelper.Lerp(0f, 20f, CWRUtils.EaseInCubic(t));
                
                //名称动画 - 淡出并上升
                cachedNameAlpha = MathHelper.Lerp(1f, 0f, CWRUtils.EaseInCubic(t));
                cachedNameScale = MathHelper.Lerp(1f, 1.3f, CWRUtils.EaseInCubic(t));
                cachedNameYOffset = MathHelper.Lerp(0f, -15f, CWRUtils.EaseInCubic(t));
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

            //绘制技能名称
            DrawSkillName(spriteBatch, screenPos);

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

        /// <summary>
        /// 绘制技能名称（带特效）
        /// </summary>
        private static void DrawSkillName(SpriteBatch spriteBatch, Vector2 iconScreenPos) {
            if (cachedNameAlpha <= 0.01f || SwitchingSkill?.DisplayName == null) {
                return;
            }

            string skillName = SwitchingSkill.DisplayName.Value;
            if (string.IsNullOrEmpty(skillName)) {
                return;
            }

            //计算名称位置
            Vector2 namePos = iconScreenPos + new Vector2(0, -55 + cachedNameYOffset);
            
            //使用MouseText字体
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 nameSize = font.MeasureString(skillName);
            Vector2 nameOrigin = nameSize / 2f;

            //绘制外发光（多层，营造深度）
            for (int i = 3; i > 0; i--) {
                float glowRadius = i * 2.5f;
                float glowAlpha = cachedNameAlpha * (0.4f - i * 0.1f);
                Color glowColor = Color.Lerp(Color.AliceBlue, Color.CadetBlue, i / 3f) * glowAlpha;
                
                for (int j = 0; j < 8; j++) {
                    float angle = MathHelper.TwoPi * j / 8f;
                    Vector2 offset = angle.ToRotationVector2() * glowRadius;
                    spriteBatch.DrawString(font, skillName, namePos + offset, 
                        glowColor, 0f, nameOrigin, cachedNameScale * 0.9f, SpriteEffects.None, 0);
                }
            }

            //绘制主文字（带波浪渐变效果）
            DrawWaveText(spriteBatch, font, skillName, namePos, nameOrigin);

            //绘制顶部高光
            float highlightAlpha = cachedNameAlpha * 0.6f * (0.5f + 0.5f * MathF.Sin(cachedNameWavePhase));
            Color highlightColor = Color.White * highlightAlpha;
            Vector2 highlightOffset = new Vector2(0, -1.5f);
            spriteBatch.DrawString(font, skillName, namePos + highlightOffset, 
                highlightColor, 0f, nameOrigin, cachedNameScale * 0.95f, SpriteEffects.None, 0);

            //绘制装饰性星星粒子
            if (cachedNameAlpha > 0.5f && Main.rand.NextBool(8)) {
                float starX = namePos.X + Main.rand.NextFloat(-nameSize.X / 2, nameSize.X / 2);
                float starY = namePos.Y + Main.rand.NextFloat(-8, 8);
                Vector2 starPos = new Vector2(starX, starY);
                
                int dust = Dust.NewDust(starPos, 1, 1, DustID.GoldCoin, 0, -1f, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.3f;
                Main.dust[dust].fadeIn = 0.8f;
            }
        }

        /// <summary>
        /// 绘制波浪渐变文字（逐字符渐变）
        /// </summary>
        private static void DrawWaveText(SpriteBatch spriteBatch, DynamicSpriteFont font, 
            string text, Vector2 position, Vector2 origin) {
            
            Vector2 currentPos = position - origin * cachedNameScale;
            
            for (int i = 0; i < text.Length; i++) {
                string character = text[i].ToString();
                Vector2 charSize = font.MeasureString(character);
                
                //计算每个字符的波浪偏移
                float waveOffset = MathF.Sin(cachedNameWavePhase + i * 0.3f) * 2f;
                Vector2 charPos = currentPos + new Vector2(0, waveOffset);
                
                //计算渐变色
                float colorPhase = (cachedNameWavePhase + i * 0.2f) % MathHelper.TwoPi;
                float colorLerp = (MathF.Sin(colorPhase) + 1f) / 2f;
                Color charColor = Color.Lerp(Color.AliceBlue, Color.White, colorLerp) * cachedNameAlpha;
                
                //绘制字符阴影
                spriteBatch.DrawString(font, character, charPos + new Vector2(1.5f, 1.5f), 
                    Color.Black * cachedNameAlpha * 0.7f, 0f, Vector2.Zero, cachedNameScale, SpriteEffects.None, 0);
                
                //绘制字符主体
                spriteBatch.DrawString(font, character, charPos, 
                    charColor, 0f, Vector2.Zero, cachedNameScale, SpriteEffects.None, 0);
                
                currentPos.X += charSize.X * cachedNameScale;
            }
        }
    }
}
