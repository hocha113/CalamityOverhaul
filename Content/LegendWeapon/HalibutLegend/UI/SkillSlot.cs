using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria.Audio;
using Terraria.ID;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    internal class SkillSlot : UIHandle
    {
        public static SkillSlot Instance => UIHandleLoader.GetUIHandleOfType<SkillSlot>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public FishSkill FishSkill;
        public float hoverSengs;
        public float RelativeIndex; // 相对于可见范围的位置
        public float DrawAlpha = 1f; // 绘制透明度

        // 出现动画相关
        public float appearProgress = 0f; // 出现进度（0到1）
        public bool isAppearing = false; // 是否正在播放出现动画
        private const float AppearDuration = 20f; // 出现动画持续帧数

        public override void Update() {
            Size = new Vector2(Skillcon.Width, Skillcon.Height / 5);
            UIHitBox = DrawPosition.GetRectangle((int)Size.X, (int)(Size.Y));

            // 更新出现动画
            if (isAppearing) {
                appearProgress += 1f / AppearDuration;
                if (appearProgress >= 1f) {
                    appearProgress = 1f;
                    isAppearing = false;
                }
            }

            // 只有完全出现后才响应交互
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox) && DrawAlpha > 0.5f && appearProgress >= 1f;

            if (hoverInMainPage) {
                if (hoverSengs < 1f) {
                    hoverSengs += 0.1f;
                }
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    HalibutUIHead.Instance.FishSkill = FishSkill;
                }
            }
            else {
                if (hoverSengs > 0f) {
                    hoverSengs -= 0.1f;
                }
            }

            hoverSengs = Math.Clamp(hoverSengs, 0f, 1f);
        }

        /// <summary>
        /// 缓动函数：EaseOutBack - 带有回弹效果的缓出
        /// </summary>
        private float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (FishSkill == null) {
                return;
            }

            // 如果正在出现动画中，应用特殊效果
            float finalAlpha = DrawAlpha;
            float scale = 1f;
            float rotation = 0f;

            if (appearProgress < 1f) {
                // 使用EaseOutBack缓动，产生弹性效果
                float easedProgress = EaseOutBack(appearProgress);

                // 缩放从0.3开始，带有超调效果（可能超过1.0）
                scale = 0.3f + easedProgress * 0.7f;

                // 透明度渐入
                finalAlpha *= appearProgress;

                // 轻微的旋转效果
                rotation = (1f - appearProgress) * 0.5f;
            }

            Color baseColor = Color.White * finalAlpha;
            Color glowColor = Color.Gold with { A = 0 } * hoverSengs * finalAlpha;

            Vector2 center = DrawPosition + Size / 2;
            Vector2 origin = Size / 2;

            // 绘制悬停发光效果
            spriteBatch.Draw(FishSkill.Icon, center, null, glowColor, rotation, origin, scale * 1.2f, SpriteEffects.None, 0);

            // 绘制主图标
            spriteBatch.Draw(FishSkill.Icon, center, null, baseColor, rotation, origin, scale, SpriteEffects.None, 0);

            // 如果正在出现，绘制额外的光圈效果
            if (appearProgress < 1f && appearProgress > 0.2f) {
                float ringProgress = (appearProgress - 0.2f) / 0.8f;
                float ringScale = 0.5f + ringProgress * 1.5f;
                float ringAlpha = (1f - ringProgress) * 0.6f;
                Color ringColor = Color.Gold with { A = 0 } * ringAlpha * finalAlpha;

                spriteBatch.Draw(FishSkill.Icon, center, null, ringColor, rotation, origin, scale * ringScale, SpriteEffects.None, 0);
            }
        }
    }
}
