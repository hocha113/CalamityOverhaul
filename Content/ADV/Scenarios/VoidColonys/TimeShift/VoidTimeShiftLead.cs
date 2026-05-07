using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.EntrustManager;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.TimeShift
{
    //虚空时间入侵引导卡片
    //玩家首次进入虚空聚落时显示，提示其按下时间入侵键切换时代
    //引导完成条件：按下该键、点击确认按钮或超时自动消失
    internal class VoidTimeShiftLead : ModSystem, ILocalizedModType
    {
        private enum LeadPhase { Inactive, Active, Complete }

        public string LocalizationCategory => "ADV.VoidColony";

        public static LocalizedText TextTitle { get; private set; }
        public static LocalizedText TextBound { get; private set; }
        public static LocalizedText TextUnboundWarn { get; private set; }
        public static LocalizedText TextUnboundDefault { get; private set; }
        public static LocalizedText TextBindHint { get; private set; }
        public static LocalizedText TextConfirm { get; private set; }

        public override void SetStaticDefaults() {
            TextTitle = this.GetLocalization(nameof(TextTitle), () => "虚空时间入侵");
            TextBound = this.GetLocalization(nameof(TextBound), () => "按 [{0}] 可在当下与过去之间切换时间线");
            TextUnboundWarn = this.GetLocalization(nameof(TextUnboundWarn), () => "⚠  时间入侵按键尚未绑定！");
            TextUnboundDefault = this.GetLocalization(nameof(TextUnboundDefault), () => "当前按 [{0}]（默认键）可执行时间入侵");
            TextBindHint = this.GetLocalization(nameof(TextBindHint), () => "建议前往  设置 → 控制  中绑定自定义按键");
            TextConfirm = this.GetLocalization(nameof(TextConfirm), () => "明白了");
        }

        private static LeadPhase _phase = LeadPhase.Inactive;
        private static float _animProgress = 0f;
        private static float _shaderTimer = 0f;
        private static int _tickTimer = 0;

        private const float AnimSpeed = 0.12f;
        private const int EdgePad = 8;
        //60秒后自动消失
        private const int SoftTimeout = 60 * 60;
        private const int CardW = 326;
        private const int CardH_Bound = 102;
        private const int CardH_Unbound = 152;

        public override void OnWorldUnload() {
            _phase = LeadPhase.Inactive;
            _animProgress = 0f;
            _tickTimer = 0;
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.gameMenu) return;
            if (_phase == LeadPhase.Complete) return;

            _shaderTimer += 0.004f;
            if (_shaderTimer > 100f) _shaderTimer -= 100f;

            if (_phase == LeadPhase.Inactive) {
                if (VoidColony.Active && Main.LocalPlayer.TryGetADVSave(out var save)
                    && !save.Get<VoidColonyADVData>().TimeShiftGuideSeen) {
                    _phase = LeadPhase.Active;
                    _animProgress = 0f;
                    _tickTimer = 0;
                }
                return;
            }

            if (_phase == LeadPhase.Active) {
                _animProgress = MathHelper.Lerp(_animProgress, 1f, AnimSpeed);
                _tickTimer++;

                //玩家实际按下了入侵键，立即标记完成
                if (CWRKeySystem.VoidTimeShift_Key?.JustPressed == true) {
                    MarkSeen();
                    return;
                }

                //超时自动消失
                if (_tickTimer > SoftTimeout)
                    MarkSeen();

                //离开虚空聚落时重置，下次进入重新触发
                if (!VoidColony.Active) {
                    _phase = LeadPhase.Inactive;
                    _animProgress = 0f;
                    _tickTimer = 0;
                }
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (_phase != LeadPhase.Active) return;

            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) return;

            layers.Insert(idx, new LegacyGameInterfaceLayer(
                "CWRMod: VoidTimeShift Guide Lead",
                delegate {
                    DrawGuideCard(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.UI
            ));
        }

        private static void MarkSeen() {
            if (Main.LocalPlayer.TryGetADVSave(out var save))
                save.Get<VoidColonyADVData>().TimeShiftGuideSeen = true;
            _phase = LeadPhase.Complete;
        }

        private static string GetBoundKeyName() {
            if (CWRKeySystem.VoidTimeShift_Key == null) return null;
            var keys = CWRKeySystem.VoidTimeShift_Key.GetAssignedKeys();
            return keys.Count > 0 ? keys[0] : null;
        }

        private static void DrawGuideCard(SpriteBatch sb) {
            string boundKey = GetBoundKeyName();
            bool hasBind = boundKey != null;
            string displayKey = hasBind ? boundKey : "K";
            int cardH = hasBind ? CardH_Bound : CardH_Unbound;

            float slideY = (1f - _animProgress) * 50f;
            float x = (Main.screenWidth - CardW) * 0.5f;
            float y = Main.screenHeight - cardH - 26f + slideY;
            float alpha = _animProgress;
            var card = new Rectangle((int)x, (int)y, CardW, cardH);

            DrawCardBackground(sb, card, alpha);

            var font = FontAssets.MouseText.Value;
            float titleScale = 0.82f;
            float bodyScale = 0.72f;
            float subScale = 0.63f;
            float px = x + 14f, py = y + 10f;
            float lineH_t = font.MeasureString("A").Y * titleScale + 2f;
            float lineH_b = font.MeasureString("A").Y * bodyScale + 2f;
            float lineH_s = font.MeasureString("A").Y * subScale + 2f;

            //标题
            Utils.DrawBorderString(sb, TextTitle.Value,
                new Vector2(px, py),
                new Color(100, 220, 255, (int)(255 * alpha)), titleScale);
            py += lineH_t + 2f;

            //分割线
            BaseManagerStyle.FillRect(sb,
                new Rectangle((int)px, (int)py, CardW - 28, 1),
                new Color(60, 160, 200, (int)(110 * alpha)));
            py += 6f;

            if (hasBind) {
                string line = TextBound.Format(displayKey);
                int wrapW = (int)((CardW - 28) / bodyScale);
                string[] wrapped = Utils.WordwrapString(line, font, wrapW, 99, out _);
                foreach (string wl in wrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, py),
                        new Color(200, 240, 255, (int)(240 * alpha)), bodyScale);
                    py += lineH_b;
                }
            }
            else {
                //警告标题（琥珀色脉动）
                float blink = 0.84f + MathF.Sin(_shaderTimer * 52f) * 0.16f;
                var warnColor = new Color(
                    (int)(255 * blink),
                    (int)(175 * blink),
                    (int)(25 * blink),
                    (int)(255 * alpha));
                Utils.DrawBorderString(sb, TextUnboundWarn.Value, new Vector2(px, py), warnColor, 0.82f);
                py += font.MeasureString("A").Y * 0.82f + 4f;

                string defaultLine = TextUnboundDefault.Format(displayKey);
                int wrapW = (int)((CardW - 28) / bodyScale);
                string[] dWrapped = Utils.WordwrapString(defaultLine, font, wrapW, 99, out _);
                foreach (string wl in dWrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, py),
                        new Color(215, 225, 200, (int)(230 * alpha)), bodyScale);
                    py += lineH_b;
                }
                py += 1f;

                int hintW = (int)((CardW - 28) / subScale);
                string[] hintWrapped = Utils.WordwrapString(TextBindHint.Value, font, hintW, 99, out _);
                foreach (string wl in hintWrapped) {
                    if (string.IsNullOrEmpty(wl)) continue;
                    Utils.DrawBorderString(sb, wl.TrimEnd('-', ' '), new Vector2(px, py),
                        new Color(130, 150, 175, (int)(190 * alpha)), subScale);
                    py += lineH_s;
                }
            }

            if (DrawConfirmButton(sb, card, alpha))
                MarkSeen();
        }

        private static void DrawCardBackground(SpriteBatch sb, Rectangle card, float alpha) {
            Effect effect = EffectLoader.EntrustGuideCard?.Value;
            if (effect != null) {
                Rectangle ext = card;
                ext.Inflate(EdgePad, EdgePad);

                effect.Parameters["uTime"]?.SetValue(_shaderTimer);
                effect.Parameters["uAlpha"]?.SetValue(alpha * 0.96f);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(ext.Width, ext.Height));
                effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);
                //冷青色风格，契合虚空维度氛围
                effect.Parameters["uVariant"]?.SetValue(1.0f);

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);
                sb.Draw(VaultAsset.placeholder2.Value, ext, Color.White);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, null, Main.UIScaleMatrix);
            }
            else {
                //降级：纯色背景加边框
                BaseManagerStyle.FillRect(sb, card, new Color(0, 0, 0, (int)(200 * alpha)));
                BaseManagerStyle.StrokeRect(sb, card, 1, new Color(60, 180, 220, (int)(120 * alpha)));
            }
        }

        private static bool DrawConfirmButton(SpriteBatch sb, Rectangle card, float alpha) {
            const int btnW = 78, btnH = 20, margin = 8;
            var rect = new Rectangle(card.Right - btnW - margin, card.Bottom - btnH - margin, btnW, btnH);

            //分隔线将正文与按钮区视觉分离
            int sepY = rect.Y - 6;
            BaseManagerStyle.FillRect(sb,
                new Rectangle(card.X + 12, sepY, card.Width - 24, 1),
                new Color(60, 140, 170, (int)(80 * alpha)));

            bool hovered = rect.Contains(Main.mouseX, Main.mouseY);
            BaseManagerStyle.FillRect(sb, rect, new Color(10, 40, 60, (int)((hovered ? 220 : 145) * alpha)));
            BaseManagerStyle.StrokeRect(sb, rect, 1, new Color(60, 170, 210, (int)(145 * alpha)));

            string buttonText = TextConfirm.Value;
            var textColor = new Color(130, 220, 255, (int)(255 * alpha));
            Vector2 ts = FontAssets.MouseText.Value.MeasureString(buttonText) * 0.62f;
            Utils.DrawBorderString(sb, buttonText,
                new Vector2(rect.X + (rect.Width - ts.X) * 0.5f, rect.Y + (rect.Height - ts.Y) * 0.5f),
                textColor, 0.62f);

            if (hovered) Main.LocalPlayer.mouseInterface = true;
            return hovered && Main.mouseLeft && !Main.mouseLeftRelease;
        }
    }
}
