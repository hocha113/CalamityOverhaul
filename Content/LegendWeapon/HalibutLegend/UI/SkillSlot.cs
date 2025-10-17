using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    internal class SkillSlot : UIHandle, ILocalizedModType
    {
        public static SkillSlot Instance => UIHandleLoader.GetUIHandleOfType<SkillSlot>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public string LocalizationCategory => "Legend.HalibutText";
        public FishSkill FishSkill;
        public float hoverSengs;
        public float RelativeIndex;//相对于可见范围的位置
        public float DrawAlpha = 1f;//绘制透明度

        //出现动画相关
        public float appearProgress = 0f;//出现进度（0到1）
        public bool isAppearing = false;//是否正在播放出现动画
        private const float AppearDuration = 20f;//出现动画持续帧数

        //悬停计时器（延迟显示介绍面板）
        private int hoverTimer = 0;
        private const int HoverDelay = 20;//悬停20帧（约0.33秒）后显示介绍

        //快捷提示栏相关
        private int hintTimer = 0;//提示计时
        private const int HintDelay = 10;//10帧后出现提示
        internal static SkillSlot HoveredSlot;//当前悬停的槽位(供面板调用绘制提示)

        //拖拽相关字段
        internal bool beingDragged;//是否正被拖拽(由面板设置)

        internal static LocalizedText Hover1;
        internal static LocalizedText Hover2;
        internal static LocalizedText Hover3;
        internal static LocalizedText Hover4;

        public override void SetStaticDefaults() {
            Hover1 = this.GetLocalization(nameof(Hover1), () => "左键: 选择");
            Hover2 = this.GetLocalization(nameof(Hover2), () => "右键: 置顶");
            Hover3 = this.GetLocalization(nameof(Hover3), () => "滚轮: 滚动");
            Hover4 = this.GetLocalization(nameof(Hover4), () => "长按: 拖拽");
        }

        public override void Update() {
            Size = new Vector2(Skillcon.Width, Skillcon.Height / 5);
            UIHitBox = DrawPosition.GetRectangle((int)Size.X, (int)(Size.Y));

            if (isAppearing) {
                appearProgress += 1f / AppearDuration;
                if (appearProgress >= 1f) {
                    appearProgress = 1f;
                    isAppearing = false;
                }
            }

            hoverInMainPage = UIHitBox.Intersects(MouseHitBox) && DrawAlpha > 0.5f && appearProgress >= 1f;

            if (hoverInMainPage) {
                HoveredSlot = this;
                if (hoverSengs < 1f) {
                    hoverSengs += 0.1f;
                }
                hoverTimer++;
                hintTimer++;
                if (hoverTimer >= HoverDelay && FishSkill != null) {
                    Vector2 mainPanelPos = HalibutUIPanel.Instance.DrawPosition;
                    Vector2 mainPanelSize = HalibutUIPanel.Instance.Size;
                    SkillTooltipPanel.Instance.Show(FishSkill, mainPanelPos, mainPanelSize);
                }
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    HalibutUIHead.FishSkill = FishSkill;
                }
                if (Main.mouseRight && Main.mouseRightRelease) {
                    if (FishSkill != null) {
                        HalibutUIPanel.Instance.MoveSlotToFront(this);
                    }
                }
            }
            else {
                if (hoverSengs > 0f) {
                    hoverSengs -= 0.1f;
                }
                hoverTimer = 0;
                hintTimer = 0;
                if (HoveredSlot == this) {
                    HoveredSlot = null;
                }
            }

            hoverSengs = Math.Clamp(hoverSengs, 0f, 1f);
        }

        ///<summary>
        ///缓动函数：EaseOutBack - 带有回弹效果的缓出
        ///</summary>
        private float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (FishSkill == null) {
                return;
            }
            float finalAlpha = DrawAlpha;
            float scale = 1f;
            float rotation = 0f;
            if (appearProgress < 1f) {
                float easedProgress = EaseOutBack(appearProgress);
                scale = 0.3f + easedProgress * 0.7f;
                finalAlpha *= appearProgress;
                rotation = (1f - appearProgress) * 0.5f;
            }
            if (beingDragged) {
                scale *= 1.15f;//拖拽时放大
            }
            Color baseColor = Color.White * finalAlpha;
            Color glowColor = Color.Gold with { A = 0 } * hoverSengs * finalAlpha;
            Vector2 center = DrawPosition + Size / 2;
            Vector2 origin = Size / 2;
            spriteBatch.Draw(FishSkill.Icon, center, null, glowColor, rotation, origin, scale * 1.2f, SpriteEffects.None, 0);
            spriteBatch.Draw(FishSkill.Icon, center, null, baseColor, rotation, origin, scale, SpriteEffects.None, 0);
            if (appearProgress < 1f && appearProgress > 0.2f) {
                float ringProgress = (appearProgress - 0.2f) / 0.8f;
                float ringScale = 0.5f + ringProgress * 1.5f;
                float ringAlpha = (1f - ringProgress) * 0.6f;
                Color ringColor = Color.Gold with { A = 0 } * ringAlpha * finalAlpha;
                spriteBatch.Draw(FishSkill.Icon, center, null, ringColor, rotation, origin, scale * ringScale, SpriteEffects.None, 0);
            }
        }

        internal void DrawHint(SpriteBatch spriteBatch) {
            if (this != HoveredSlot) {
                return;
            }
            if (hintTimer < HintDelay) {
                return;
            }
            string l1 = Hover1.Value;
            string l2 = Hover2.Value;
            string l3 = Hover3.Value;
            string l4 = Hover4.Value;
            var font = FontAssets.MouseText.Value;
            float w = Math.Max(font.MeasureString(l1).X, Math.Max(font.MeasureString(l2).X, font.MeasureString(l3).X));
            float lineH = 18f;
            Vector2 size = new Vector2(w + 20, lineH * 4 + 16);
            Vector2 pos = DrawPosition + new Vector2(Size.X / 2 - size.X / 2, -size.Y - 6);
            pos.X = Math.Clamp(pos.X, 16, Main.screenWidth - size.X - 16);
            pos.Y = Math.Max(16, pos.Y);
            Rectangle rect = new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y);
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float alpha = 0.92f;
            spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), new Color(20, 30, 50) * alpha);
            Color edge = Color.Gold * 0.65f;
            //边框
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Rectangle(0, 0, 1, 1), edge * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.9f);
            Vector2 tPos = pos + new Vector2(10, 8);
            Utils.DrawBorderString(spriteBatch, l1, tPos, Color.White, 0.75f);
            Utils.DrawBorderString(spriteBatch, l2, tPos + new Vector2(0, lineH), Color.White, 0.75f);
            Utils.DrawBorderString(spriteBatch, l3, tPos + new Vector2(0, lineH * 2), Color.White, 0.75f);
            Utils.DrawBorderString(spriteBatch, l4, tPos + new Vector2(0, lineH * 3), Color.White, 0.75f);
        }
    }
}
