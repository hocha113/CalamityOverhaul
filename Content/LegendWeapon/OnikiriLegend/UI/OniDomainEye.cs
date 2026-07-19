using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 鬼域之眼：封印札 HUD 簇的挂点与领域控制面,整簇纸札"挂"在这只眼下。<br/>
    /// 与开域仪式共用 OniEye.fx(三勾玉写轮眼)——平时它栖在札上,开/收域时它离巢去天上干活,
    /// HUD 处只留一圈噪声消散的空窝;表世界绯红虹膜,里世界鬼火青,翻转时负片爆闪。<br/>
    /// 左键开阖领域,右键翻转表里(阖着先展到表);仪式进行中被拒时急促眨眼一次。<br/>
    /// 由 <see cref="OniTalismanHud"/> 驱动,共用其锚点;状态直读 <see cref="OniDomain.Local"/>,
    /// 动画全部本地缓动推导
    /// </summary>
    internal sealed class OniDomainEye
    {
        //====显示状态(直读域状态机,缓动本地推导)====
        private float open;
        //离巢:开/收域仪式期间眼去了天上,HUD 处噪声消散只剩空窝
        private float away;
        private float uraBlend;
        private float spin;
        private float flash;
        private float denyPulse;
        private float hoverEase;
        private bool wasHover;
        private OniDomainPhase lastPhase = OniDomainPhase.Closed;
        //失去悬停后的帧计数:边缘打滑不重复响纸声
        private int hoverOffTicks = 60;
        private Vector2 lastMouse;
        private float lastAlpha;

        /// <summary>本帧眼心(含呼吸浮动),Update 算好供 Draw/命中/系带共用</summary>
        public Vector2 Center { get; private set; }

        /// <summary>本帧悬浮在眼上</summary>
        public bool Hovering { get; private set; }

        /// <summary>挂点微移:眼的呼吸传给绳结,札随之轻晃——"挂在活物上"</summary>
        public Vector2 HangSway { get; private set; }

        /// <summary>系带上端:眼的下缘,绳结自此垂下</summary>
        public Vector2 TieTop => Center + new Vector2(0f, OnikiriUITheme.HudEyeHalf * 0.5f);

        /// <summary>隐藏期间调用:清空瞬态,下次出现直接吸附</summary>
        public void Reset() {
            denyPulse = 0f;
            flash = 0f;
            hoverEase = 0f;
            Hovering = wasHover = false;
            hoverOffTicks = 60;
            OniDomainPlayer odp = OniDomain.Local;
            OniDomainPhase phase = odp?.Phase ?? OniDomainPhase.Closed;
            open = phase is OniDomainPhase.Omote or OniDomainPhase.Ura or OniDomainPhase.Flipping ? 1f : 0f;
            away = phase is OniDomainPhase.Opening or OniDomainPhase.Closing ? 1f : 0f;
            uraBlend = (odp?.WorldIsUra ?? false) ? 1f : 0f;
            lastPhase = phase;
        }

        /// <summary>仪式进行中命令被拒:急促眨眼 + 一声哑响(本地客户端)</summary>
        public void NotifyDenied() {
            denyPulse = 1f;
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.75f, Volume = 0.38f });
        }

        /// <summary>
        /// 推进眼的动画与交互。clickToggle/clickFlip 为本帧鼠标按下沿
        /// (左键=开阖,右键或中键=翻转,由 HUD 用 UIHandle 按键状态判好传入)
        /// </summary>
        public void Update(Player player, Vector2 knot, bool interactive, Vector2 mouse, float time,
            bool clickToggle, bool clickFlip) {
            lastMouse = mouse;

            //呼吸浮动:离巢时空窝定住不动
            OniDomainPlayer odp = OniDomain.Local;
            OniDomainPhase phase = odp?.Phase ?? OniDomainPhase.Closed;
            float alive = 1f - away * 0.6f;
            Vector2 bob = new(
                MathF.Sin(time * 0.9f + 1.3f) * 0.7f * alive,
                MathF.Sin(time * 1.6f + 0.4f) * 1.7f * alive);
            Center = knot + OnikiriUITheme.HudEyeOffset + bob;
            HangSway = bob * 0.55f;

            //====状态缓动====
            //相位边沿:接令/落定的爆闪脉冲
            if (phase != lastPhase) {
                if (lastPhase == OniDomainPhase.Closed && phase == OniDomainPhase.Opening) {
                    flash = MathF.Max(flash, 0.55f);
                }
                else if (phase is OniDomainPhase.Omote or OniDomainPhase.Ura) {
                    flash = MathF.Max(flash, 0.4f);
                }
                else if (phase == OniDomainPhase.Closing) {
                    flash = MathF.Max(flash, 0.3f);
                }
                lastPhase = phase;
            }

            float openTarget = phase is OniDomainPhase.Omote or OniDomainPhase.Ura or OniDomainPhase.Flipping ? 1f : 0f;
            //睁快阖更快:眼睑是有弹性的
            open += (openTarget - open) * (openTarget > open ? 0.22f : 0.30f);
            float awayTarget = phase is OniDomainPhase.Opening or OniDomainPhase.Closing ? 1f : 0f;
            //离巢消散快,归巢重新凝实慢半拍
            away += (awayTarget - away) * (awayTarget > away ? 0.12f : 0.07f);
            uraBlend += (((odp?.WorldIsUra ?? false) ? 1f : 0f) - uraBlend) * 0.12f;

            //勾玉:表顺旋,里逆旋,翻转狂旋,阖眼时几不可察地蠕动——它在做梦
            float spinSpeed = phase switch {
                OniDomainPhase.Omote => 0.011f,
                OniDomainPhase.Ura => -0.017f,
                OniDomainPhase.Flipping => (odp != null && odp.FlipToUra ? 1f : -1f) * 0.085f,
                OniDomainPhase.Opening or OniDomainPhase.Closing => 0.03f,
                _ => 0.0028f,
            };
            spin += spinSpeed * (1f + hoverEase * 0.5f);

            //翻转负片帧直通爆闪
            flash = MathF.Max(flash * 0.90f, odp?.NegativeFlash ?? 0f);
            denyPulse *= 0.86f;

            //====命中与点击====
            Hovering = interactive && Vector2.Distance(mouse, Center) <= OnikiriUITheme.HudEyeHitRadius;
            hoverEase += ((Hovering ? 1f : 0f) - hoverEase) * 0.2f;

            if (Hovering && !wasHover && hoverOffTicks > 8) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f, Pitch = 0.5f });
            }
            hoverOffTicks = Hovering ? 0 : Math.Min(hoverOffTicks + 1, 600);
            wasHover = Hovering;

            if (!Hovering) {
                return;
            }
            player.mouseInterface = true;

            //左键开阖;右键翻转(阖着先展到表);中键与翻转键同义,悬停时在这里受理
            if (clickToggle) {
                if (OniDomain.TryToggle(player, out bool busy)) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.45f, Pitch = -0.15f });
                }
                else if (busy) {
                    NotifyDenied();
                }
            }
            else if (clickFlip) {
                if (OniDomain.TryFlip(player, out bool busy)) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.45f, Pitch = 0.1f });
                }
                else if (busy) {
                    NotifyDenied();
                }
            }
        }

        /// <summary>绘制眼本体(shader 缺席退回 CPU 简笔)。调用方保证当前批为 Deferred+UIScaleMatrix</summary>
        public void Draw(SpriteBatch sb, float alpha, float time) {
            lastAlpha = alpha;
            if (alpha <= 0.01f) {
                return;
            }

            //拒绝反馈:睁着急促眨眼,阖着烦躁地睁开一条缝
            float denyBlink = denyPulse > 0.03f ? denyPulse * (0.5f + 0.5f * MathF.Sin(time * 46f)) : 0f;
            float openDraw = MathHelper.Clamp(open * (1f - denyBlink * 0.65f) + (1f - open) * denyBlink * 0.2f, 0f, 1f);
            float presence = 1f - away * 0.92f;
            float intensity = alpha * presence;

            //离巢空窝:淡墨勾一圈眼眶,中心一点余烬——眼会回来的
            if (away > 0.05f) {
                DrawSocket(sb, alpha * away, time);
            }

            if (intensity <= 0.01f) {
                return;
            }

            float halfSize = OnikiriUITheme.HudEyeHalf * (0.93f + 0.07f * openDraw + 0.05f * hoverEase) + 2.5f * flash;

            Effect eye = EffectLoader.OniEye?.Value;
            Texture2D white = CWRAsset.Placeholder_White?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (eye == null || white == null || noise == null) {
                DrawFallback(sb, intensity, openDraw, halfSize, time);
                return;
            }

            eye.Parameters["uTime"]?.SetValue(time);
            eye.Parameters["uIntensity"]?.SetValue(intensity);
            eye.Parameters["uOpen"]?.SetValue(openDraw);
            eye.Parameters["uSpin"]?.SetValue(spin);
            eye.Parameters["uFlash"]?.SetValue(flash);
            eye.Parameters["uDissolve"]?.SetValue(away);
            eye.Parameters["uUra"]?.SetValue(uraBlend);

            var gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            Vector2 scale = new(halfSize * 2f / white.Width, halfSize * 2f / white.Height);
            Vector2 origin = white.Size() * 0.5f;

            //本体
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
            eye.CurrentTechnique = eye.Techniques["TechEyeBase"];
            eye.CurrentTechnique.Passes[0].Apply();
            sb.Draw(white, Center, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            sb.End();

            //辉光
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
            eye.CurrentTechnique = eye.Techniques["TechEyeGlow"];
            eye.CurrentTechnique.Passes[0].Apply();
            sb.Draw(white, Center, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            sb.End();

            //还原调用方的批
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        /// <summary>离巢空窝:上下睑淡墨弧线 + 窝心一点将熄的余烬(被拒时余烬惊闪)</summary>
        private void DrawSocket(SpriteBatch sb, float alpha, float time) {
            float w = OnikiriUITheme.HudEyeHalf * 0.9f;
            Vector2 l = Center - new Vector2(w, 0f);
            Vector2 r = Center + new Vector2(w, 0f);
            OniBrush.DrawTaperedSlash(sb, l, r, 1.5f, -4.2f, alpha * 0.5f);
            OniBrush.DrawTaperedSlash(sb, l, r, 1.3f, 3.4f, alpha * 0.42f);
            Color ember = Color.Lerp(OnikiriUITheme.Bright, OnikiriUITheme.GhostFire, uraBlend);
            float pulse = 0.55f + 0.45f * MathF.Sin(time * 2.7f) + denyPulse * 0.9f;
            OniBrush.DrawBacklight(sb, Center, 7f + denyPulse * 3f, ember, alpha * 0.5f * MathF.Min(pulse, 1.6f));
        }

        /// <summary>CPU 降级:睑线勾眶 + 虹膜辉斑 + 竖瞳 + 三勾玉点,读数不缺席</summary>
        private void DrawFallback(SpriteBatch sb, float intensity, float openDraw, float halfSize, float time) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            float w = halfSize * 0.92f;
            float h = MathF.Max(halfSize * 0.5f * openDraw, 1.2f);
            Color iris = Color.Lerp(OnikiriUITheme.Bright, OnikiriUITheme.GhostFire, uraBlend);

            //眼窝底
            sb.Draw(pixel, Center, src, OnikiriUITheme.Ink * (intensity * 0.9f), 0f, new Vector2(0.5f), new Vector2(w * 2f, h * 2f), SpriteEffects.None, 0f);
            //虹膜辉斑与竖瞳
            if (openDraw > 0.1f) {
                OniBrush.DrawBacklight(sb, Center, h * 0.95f, iris, intensity * (0.6f + 0.4f * flash));
                sb.Draw(pixel, Center, src, OnikiriUITheme.Ink * intensity, 0f, new Vector2(0.5f), new Vector2(2.2f, h * 1.5f), SpriteEffects.None, 0f);
                //三勾玉点位
                for (int i = 0; i < 3; i++) {
                    Vector2 dot = Center + (spin + i * MathHelper.TwoPi / 3f).ToRotationVector2() * h * 0.72f;
                    sb.Draw(pixel, dot, src, OnikiriUITheme.Ink * (intensity * 0.95f), 0f, new Vector2(0.5f), new Vector2(2.6f), SpriteEffects.None, 0f);
                }
            }
            //上下睑线
            Vector2 l = Center - new Vector2(w, 0f);
            Vector2 r = Center + new Vector2(w, 0f);
            OniBrush.DrawTaperedSlash(sb, l, r, 1.8f, -h - 1.5f, intensity * 0.85f);
            OniBrush.DrawTaperedSlash(sb, l, r, 1.5f, h + 1f, intensity * 0.7f);
        }

        /// <summary>悬浮说明:小裱墨牌,题名/当前世界/两行键位。由 HUD 在最后调用保证压在别的元素上</summary>
        public void DrawHoverTag(SpriteBatch sb) {
            float alpha = lastAlpha * hoverEase;
            if (alpha <= 0.05f) {
                return;
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            OniDomainPlayer odp = OniDomain.Local;
            OniDomainPhase phase = odp?.Phase ?? OniDomainPhase.Closed;
            string title = OniTalismanHud.DomainTitle.Value;
            string stateLine = phase switch {
                OniDomainPhase.Closed => OniTalismanHud.DomainStateClosed.Value,
                OniDomainPhase.Omote => OniTalismanHud.DomainStateOmote.Value,
                OniDomainPhase.Ura => OniTalismanHud.DomainStateUra.Value,
                _ => OniTalismanHud.DomainStateShifting.Value,
            };
            string toggleHint = string.Format(OniTalismanHud.DomainToggleHintFormat.Value,
                CWRKeySystem.Legend_Domain.ToTooltipString(CWRKeySystem.Notbound.Value));
            string flipHint = string.Format(OniTalismanHud.DomainFlipHintFormat.Value,
                CWRKeySystem.Onikiri_DomainFlip.ToTooltipString(CWRKeySystem.Notbound.Value));

            float w = MathF.Max(font.MeasureString(title).X * 0.82f, font.MeasureString(stateLine).X * 0.7f);
            w = MathF.Max(w, font.MeasureString(toggleHint).X * 0.7f);
            w = MathF.Max(w, font.MeasureString(flipHint).X * 0.7f);
            Rectangle panel = new((int)lastMouse.X + 18, (int)lastMouse.Y - 8, (int)w + 22, 80);
            //不出屏
            if (panel.Right > OnikiriUITheme.UIScreenW - 8f) {
                panel.X = (int)(lastMouse.X - panel.Width - 14f);
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            sb.Draw(pixel, new Rectangle(panel.X + 2, panel.Y + 3, panel.Width, panel.Height), src, new Color(8, 2, 5) * (alpha * 0.5f));
            sb.Draw(pixel, panel, src, OnikiriUITheme.Ink * (alpha * 0.95f));
            OniBrush.DrawTaperedSlash(sb, new Vector2(panel.X + 4f, panel.Y + 22f), new Vector2(panel.Right - 4f, panel.Y + 21f), 1.4f, 0.8f, alpha * 0.7f);

            //状态行的颜色跟世界走:表朱红,里鬼火青,过渡纸灰
            Color stateCol = phase switch {
                OniDomainPhase.Omote => OnikiriUITheme.Seal,
                OniDomainPhase.Ura => OnikiriUITheme.GhostFire,
                OniDomainPhase.Closed => OnikiriUITheme.TextDim,
                _ => OnikiriUITheme.Paper,
            };
            Utils.DrawBorderString(sb, title, new Vector2(panel.X + 10f, panel.Y + 4f), OnikiriUITheme.HotWhite * alpha, 0.82f);
            Utils.DrawBorderString(sb, stateLine, new Vector2(panel.X + 10f, panel.Y + 26f), stateCol * alpha, 0.7f);
            Utils.DrawBorderString(sb, toggleHint, new Vector2(panel.X + 10f, panel.Y + 44f), OnikiriUITheme.TextDim * alpha, 0.7f);
            Utils.DrawBorderString(sb, flipHint, new Vector2(panel.X + 10f, panel.Y + 62f), OnikiriUITheme.TextDim * alpha, 0.7f);
        }
    }
}
