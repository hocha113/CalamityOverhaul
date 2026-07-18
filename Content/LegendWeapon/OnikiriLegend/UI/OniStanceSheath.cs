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
    /// 架势鞘刀：封印札 HUD 簇内的架势计,墨脉之下横悬一柄微倾的鞘中打刀。<br/>
    /// 架势=拔刀进度:钢自鲤口向锋尖淹没黑漆,分界的拔刀线即读数,柄随蓄势轻轻后撤;
    /// 满架势刃线白热呼吸、流光巡刃并响一声鲤口切;释放时拔刀闪沿刃扫出、
    /// 读数急落,归座时"咔"地一声。<br/>
    /// 由 <see cref="OniTalismanHud"/> 驱动,共用其锚点;数据经 <see cref="OniStance.Get"/>
    /// 取原始快照,拔刀/回鞘/点火等演出全部在本类推导
    /// </summary>
    internal sealed class OniStanceSheath
    {
        //====显示状态(由数值变化自行推导,数据层只给 Value/Max)====
        //-1 = 未初始化:首帧直接吸附,重新出现不重播旧动画
        private float targetFill = -1f;
        private float displayFill;
        private float flow;
        private float fullGlow;
        private float releaseFlash;
        private float seatPulse;
        private bool wasFull;
        //释放/大幅泄势后等待读数落底,落底瞬间放归座反馈
        private bool seating;
        private float hoverEase;
        private OniStanceSnapshot snap;
        private Vector2 pommelBase;
        private Vector2 lastMouse;

        /// <summary>本帧悬浮在刀身上(纯读数,不捕获点击)</summary>
        public bool Hovering { get; private set; }

        /// <summary>隐藏期间调用:清空瞬态,下次出现直接吸附到当前值</summary>
        public void Reset() {
            targetFill = -1f;
            releaseFlash = seatPulse = 0f;
            flow = fullGlow = 0f;
            seating = false;
            hoverEase = 0f;
            Hovering = false;
        }

        public void Update(Player player, Vector2 anchor, bool interactive, Vector2 mouse) {
            pommelBase = anchor + OnikiriUITheme.HudStanceOffset;
            lastMouse = mouse;
            snap = OniStance.Get(player);
            float newTarget = snap.Ratio;
            if (targetFill < 0f) {
                targetFill = displayFill = newTarget;
                wasFull = newTarget >= 0.995f;
            }

            //事件检测:大幅泄势——满位泄出按拔刀演,余者按短促回鞘演
            float delta = newTarget - targetFill;
            if (delta < -0.20f) {
                releaseFlash = Math.Max(releaseFlash, targetFill >= 0.85f ? 1f : 0.45f);
                seating = true;
            }
            targetFill = newTarget;

            //显示值:泄势急落,蓄势稳涨
            float step = targetFill - displayFill;
            displayFill += step * (step < 0f ? 0.30f : 0.08f);
            if (Math.Abs(targetFill - displayFill) < 0.0008f) {
                displayFill = targetFill;
                if (seating) {
                    //纳刀归座:残心的那半拍落定
                    seating = false;
                    seatPulse = 1f;
                    SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.30f, Volume = 0.40f });
                }
            }
            flow = MathHelper.Lerp(flow, MathHelper.Clamp(step * 14f, -1f, 1f), 0.16f);

            //满架势:鲤口切的一声轻响,刃口点火渐入
            bool full = displayFill >= 0.995f;
            if (full && !wasFull) {
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.35f, Volume = 0.35f });
            }
            wasFull = full;
            fullGlow += ((targetFill >= 0.995f ? 1f : 0f) - fullGlow) * 0.07f;
            releaseFlash *= 0.88f;
            seatPulse *= 0.86f;

            //悬浮:整刀(含后撤余量)的轴对齐外包
            float totalLen = OnikiriUITheme.HudStanceTsukaLen + 3f + OnikiriUITheme.HudStanceBladeW;
            Vector2 dir = OnikiriUITheme.HudStanceCant.ToRotationVector2();
            float tipY = pommelBase.Y + dir.Y * totalLen;
            Rectangle box = new(
                (int)(pommelBase.X - OnikiriUITheme.HudStanceTsukaRecede - 4f),
                (int)(Math.Min(tipY, pommelBase.Y) - 11f),
                (int)(totalLen + OnikiriUITheme.HudStanceTsukaRecede + 8f),
                (int)(Math.Abs(tipY - pommelBase.Y) + 22f));
            Hovering = interactive && box.Contains(mouse.ToPoint());
            hoverEase += ((Hovering ? 1f : 0f) - hoverEase) * 0.2f;
        }

        /// <summary>绘制柄/镡/刃鞘/归座反馈/悬浮读数。suppressTag 为真时不出悬浮读数</summary>
        public void Draw(SpriteBatch sb, float alpha, float time, bool suppressTag) {
            if (alpha <= 0.01f) {
                return;
            }
            float cant = OnikiriUITheme.HudStanceCant;
            Vector2 dir = cant.ToRotationVector2();
            Vector2 perp = (cant + MathHelper.PiOver2).ToRotationVector2();

            //柄随蓄势后撤:刀正在被抽出的第二动势(缓动曲线,不追噪声)
            float ease = displayFill * displayFill * (3f - 2f * displayFill);
            Vector2 pommel = pommelBase - dir * (OnikiriUITheme.HudStanceTsukaRecede * ease);
            Vector2 tsubaC = pommel + dir * OnikiriUITheme.HudStanceTsukaLen;
            Vector2 quadLC = tsubaC + dir * 3f;

            DrawTsuka(sb, pommel, tsubaC, dir, perp, cant, alpha);

            Vector2 size = new(OnikiriUITheme.HudStanceBladeW, OnikiriUITheme.HudStanceBladeH);
            if (OniStanceBladeDraw.Available) {
                OniStanceBladeDraw.Draw(sb, quadLC, cant, size, new OniStanceBladeParams {
                    Reveal = displayFill,
                    Flow = flow,
                    FullGlow = fullGlow,
                    ReleaseFlash = releaseFlash,
                    Alpha = alpha,
                    Time = time,
                });
            }
            else {
                DrawFallback(sb, quadLC, dir, perp, cant, alpha);
            }

            //归座反馈:鲤口处一线短促白光
            if (seatPulse > 0.03f) {
                sb.Draw(VaultAsset.placeholder2.Value, quadLC + dir * 4f, new Rectangle(0, 0, 1, 1),
                    OnikiriUITheme.HotWhite * (alpha * seatPulse * 0.9f), cant + MathHelper.PiOver2,
                    new Vector2(0.5f), new Vector2(13f, 1.6f), SpriteEffects.None, 0f);
            }

            if (!suppressTag && hoverEase > 0.05f) {
                DrawHoverTag(sb, alpha * hoverEase);
            }
        }

        /// <summary>柄与镡:漆木柄身 + 交错菱巻 + 柄头铜口 + 铁镡,全 CPU,shader 缺席也在</summary>
        private static void DrawTsuka(SpriteBatch sb, Vector2 pommel, Vector2 tsubaC,
            Vector2 dir, Vector2 perp, float cant, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            float len = OnikiriUITheme.HudStanceTsukaLen;

            //柄身:阴影 + 深漆木条
            sb.Draw(pixel, pommel + new Vector2(1f, 1.6f), src, new Color(8, 2, 5) * (alpha * 0.5f),
                cant, new Vector2(0f, 0.5f), new Vector2(len, 7f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pommel, src, OnikiriUITheme.Dark * (alpha * 0.96f),
                cant, new Vector2(0f, 0.5f), new Vector2(len, 7f), SpriteEffects.None, 0f);

            //菱巻:缠柄的交错小朱菱
            for (int i = 0; i < 5; i++) {
                Vector2 p = pommel + dir * (5.5f + i * 6.2f) + perp * ((i % 2 == 0 ? 1f : -1f) * 1.1f);
                sb.Draw(pixel, p, src, OnikiriUITheme.Deep * (alpha * 0.9f),
                    cant + MathHelper.PiOver4, new Vector2(0.5f), new Vector2(3.6f), SpriteEffects.None, 0f);
            }

            //柄头:铜口 + 一粒高光
            sb.Draw(pixel, pommel + dir * 1f, src, OnikiriUITheme.Deep * (alpha * 0.95f),
                cant, new Vector2(0.5f), new Vector2(3.4f, 8f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pommel + dir * 1f - perp * 1.6f, src, OnikiriUITheme.Bright * (alpha * 0.40f),
                cant, new Vector2(0.5f), new Vector2(1.4f), SpriteEffects.None, 0f);

            //镡:铁菱板,上缘接住一点光
            sb.Draw(pixel, tsubaC, src, OnikiriUITheme.Ink * (alpha * 0.98f),
                cant, new Vector2(0.5f), new Vector2(3.6f, 14f), SpriteEffects.None, 0f);
            sb.Draw(pixel, tsubaC, src, OnikiriUITheme.Deep * (alpha * 0.85f),
                cant, new Vector2(0.5f), new Vector2(2.2f, 11f), SpriteEffects.None, 0f);
            sb.Draw(pixel, tsubaC - perp * 5f, src, OnikiriUITheme.Bright * (alpha * 0.35f),
                cant, new Vector2(0.5f), new Vector2(1.6f), SpriteEffects.None, 0f);
        }

        /// <summary>CPU 降级:黑漆鞘条 + 露刃段素钢 + 拔刀线,能读数即可</summary>
        private void DrawFallback(SpriteBatch sb, Vector2 quadLC, Vector2 dir, Vector2 perp, float cant, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            float w = OnikiriUITheme.HudStanceBladeW - 6f;
            //鞘:黑漆条 + 上缘深红压线
            sb.Draw(pixel, quadLC, src, OnikiriUITheme.Ink * (alpha * 0.95f),
                cant, new Vector2(0f, 0.5f), new Vector2(w, 12f), SpriteEffects.None, 0f);
            sb.Draw(pixel, quadLC - perp * 6f, src, OnikiriUITheme.Deep * (alpha * 0.6f),
                cant, new Vector2(0f, 0.5f), new Vector2(w, 1.3f), SpriteEffects.None, 0f);
            //露刃:素钢 + 刃线
            float steelLen = w * displayFill;
            if (steelLen > 1.5f) {
                sb.Draw(pixel, quadLC, src, OnikiriUITheme.Paper * (alpha * 0.80f),
                    cant, new Vector2(0f, 0.5f), new Vector2(steelLen, 8f), SpriteEffects.None, 0f);
                sb.Draw(pixel, quadLC - perp * 4f, src, OnikiriUITheme.HotWhite * (alpha * (0.5f + fullGlow * 0.5f)),
                    cant, new Vector2(0f, 0.5f), new Vector2(steelLen, 1.2f), SpriteEffects.None, 0f);
                //拔刀线
                sb.Draw(pixel, quadLC + dir * steelLen, src, OnikiriUITheme.Bright * (alpha * 0.9f),
                    cant + MathHelper.PiOver2, new Vector2(0.5f), new Vector2(12f, 1.6f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>悬浮读数:小裱墨牌,题名 + 当前/上限;满架势多题一行"只欠一拔"</summary>
        private void DrawHoverTag(SpriteBatch sb, float alpha) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string title = OniTalismanHud.StanceTitle.Value;
            string line = string.Format(OniTalismanHud.StanceValueFormat.Value,
                (int)MathF.Round(snap.Value), (int)MathF.Round(snap.MaxValue));
            string readyLine = displayFill >= 0.995f ? OniTalismanHud.StanceReadyLine.Value : null;

            float w = Math.Max(font.MeasureString(title).X * 0.78f, font.MeasureString(line).X * 0.7f);
            if (readyLine != null) {
                w = Math.Max(w, font.MeasureString(readyLine).X * 0.7f);
            }
            float h = 42f + (readyLine != null ? 18f : 0f);
            Rectangle panel = new((int)lastMouse.X + 16, (int)lastMouse.Y - 6, (int)w + 20, (int)h);
            //不出屏
            if (panel.Right > OnikiriUITheme.UIScreenW - 8f) {
                panel.X = (int)(lastMouse.X - panel.Width - 12f);
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            sb.Draw(pixel, new Rectangle(panel.X + 2, panel.Y + 3, panel.Width, panel.Height), src, new Color(8, 2, 5) * (alpha * 0.5f));
            sb.Draw(pixel, panel, src, OnikiriUITheme.Ink * (alpha * 0.95f));
            OniBrush.DrawTaperedSlash(sb, new Vector2(panel.X + 4f, panel.Y + 20f),
                new Vector2(panel.Right - 4f, panel.Y + 19f), 1.3f, 0.7f, alpha * 0.7f);

            Utils.DrawBorderString(sb, title, new Vector2(panel.X + 9f, panel.Y + 3f), OnikiriUITheme.HotWhite * alpha, 0.78f);
            Utils.DrawBorderString(sb, line, new Vector2(panel.X + 9f, panel.Y + 23f), OnikiriUITheme.TextDim * alpha, 0.7f);
            if (readyLine != null) {
                Utils.DrawBorderString(sb, readyLine, new Vector2(panel.X + 9f, panel.Y + 41f), OnikiriUITheme.Bright * (alpha * 0.95f), 0.7f);
            }
        }
    }
}
