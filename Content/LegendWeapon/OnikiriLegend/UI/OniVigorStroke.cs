using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 气力墨脉计,<see cref="OniTalismanHud"/> 驱动;
    /// 读 <see cref="OniVigor.Get"/>,动画在本类推导
    /// </summary>
    internal sealed class OniVigorStroke
    {
        //====显示状态(由数值变化自行推导,数据层只给 Value/Max)====
        //-1 = 未初始化:首帧直接吸附,重新出现不重播旧动画
        private float targetFill = -1f;
        private float displayFill;
        private float trailFill;
        private float flow;
        private float spendPulse;
        private float gainPulse;
        private float fullPulse;
        private float denyPulse;
        private bool wasFull;
        private float hoverEase;
        private OniVigorSnapshot snap;
        private Vector2 quadTopLeft;
        private Vector2 lastMouse;
        //====铭刻读数(纯展示)====
        //上限占比:倶利伽罗压缩后墨脉末段留焦黑断口
        private float capRatio = 1f;
        //友切咎层:笔道尾部的错位缺口数
        private int guiltLayers;
        //潮樋潮相:0..1(窗心 0.5),未装 -1;游标沿笔道涨落,合潮纸白涨亮
        private float tidePhase01 = -1f;
        private bool tideOnBeat;

        /// <summary>本帧悬浮在笔道核心带上(纯读数,不捕获点击)</summary>
        public bool Hovering { get; private set; }
        public bool TooltipVisible => hoverEase > 0.02f;

        /// <summary>朱印中心:笔画起端外侧,视觉锚点</summary>
        private Vector2 SealCenter => quadTopLeft + new Vector2(-4f, OnikiriUITheme.HudVigorQuadH * 0.5f);

        /// <summary>隐藏期间调用:清空瞬态,下次出现直接吸附到当前值</summary>
        public void Reset() {
            targetFill = -1f;
            spendPulse = gainPulse = fullPulse = denyPulse = 0f;
            flow = 0f;
            hoverEase = 0f;
            Hovering = false;
        }

        /// <summary>气力不足的拒绝反馈:干笔一颤 + 一声哑响(玩法层经 OniTalismanHud 转发调用,本地客户端)</summary>
        public void NotifyDenied() {
            denyPulse = 1f;
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.7f, Volume = 0.4f });
        }

        public void Update(Player player, Vector2 anchor, bool interactive, Vector2 mouse) {
            quadTopLeft = anchor + OnikiriUITheme.HudVigorOffset;
            lastMouse = mouse;
            snap = OniVigor.Get(player);
            capRatio = snap.CapRatio <= 0f ? 1f : snap.CapRatio;
            //咎层/潮相直读本地 ModPlayer(纯展示,本地 HUD 不进网络)
            if (player != null && player.TryGetModPlayer(out OnikiriPlayer okp)) {
                guiltLayers = okp.GuiltLayers;
                tidePhase01 = okp.TidePhase01;
                tideOnBeat = okp.IsTideOnBeatNow;
            }
            else {
                guiltLayers = 0;
                tidePhase01 = -1f;
                tideOnBeat = false;
            }
            float newTarget = snap.Ratio;
            if (targetFill < 0f) {
                targetFill = displayFill = trailFill = newTarget;
                wasFull = newTarget >= 0.995f;
            }

            //事件检测:目标值跳变推导消耗/补气脉冲
            float delta = newTarget - targetFill;
            if (delta < -0.005f) {
                //消耗:残痕自旧显示位起
                trailFill = Math.Max(trailFill, displayFill);
                spendPulse = Math.Min(1f, spendPulse + Math.Min(1f, 0.35f - delta * 4f));
            }
            else if (delta > 0.04f) {
                gainPulse = Math.Min(1f, gainPulse + delta * 3f);
            }
            targetFill = newTarget;

            //显示值:消耗利落回切,恢复缓慢洇进
            float step = targetFill - displayFill;
            displayFill += step * (step < 0f ? 0.34f : 0.065f);
            if (Math.Abs(targetFill - displayFill) < 0.0008f) {
                displayFill = targetFill;
            }
            //残痕蒸散,收拢回墨锋
            trailFill = Math.Max(displayFill, trailFill - 0.009f);
            //流速平滑,喂给墨锋的爬动与湿亮
            flow = MathHelper.Lerp(flow, MathHelper.Clamp(step * 14f, -1f, 1f), 0.16f);
            //回满收笔
            bool full = displayFill >= 0.995f;
            if (full && !wasFull) {
                fullPulse = 1f;
            }
            wasFull = full;
            spendPulse *= 0.90f;
            gainPulse *= 0.88f;
            fullPulse *= 0.95f;
            denyPulse *= 0.87f;

            //悬浮:只吃笔道核心带
            Rectangle core = new((int)(quadTopLeft.X + OnikiriUITheme.HudVigorPad - 16f),
                (int)(quadTopLeft.Y + OnikiriUITheme.HudVigorQuadH * 0.5f - 12f),
                (int)(OnikiriUITheme.HudVigorQuadW - OnikiriUITheme.HudVigorPad * 2f + 20f), 24);
            Hovering = interactive && core.Contains(mouse.ToPoint());
            hoverEase += ((Hovering ? 1f : 0f) - hoverEase) * 0.2f;
            if (Tutorial.OnikiriTutorialLead.IsActive)
                Tutorial.OnikiriTutorialTargets.Publish(Tutorial.OnikiriTutorialTargets.Tag_VigorStroke, core);
        }

        /// <summary>绘墨丝/朱印/墨痕,stripAttach=札缘挂点</summary>
        public void Draw(SpriteBatch sb, float alpha, Vector2 stripAttach, float time) {
            if (alpha <= 0.01f) {
                return;
            }

            //墨丝,札边垂至朱印
            Vector2 seal = SealCenter;
            OniBrush.DrawGradientLine(sb, stripAttach, seal,
                OnikiriUITheme.Deep * (alpha * 0.28f), OnikiriUITheme.Deep * (alpha * 0.70f), 1.1f);

            //朱印:气力将尽时呼吸并裂开
            float lowT = 1f - MathHelper.Clamp((displayFill - 0.12f) / 0.16f, 0f, 1f);
            float breath = 1f + (float)Math.Sin(time * 4.3f) * 0.10f * lowT;
            OniBrush.DrawSealGlyph(sb, seal, 9.5f * breath, alpha * 0.95f,
                (float)Math.Sin(time * 1.2f) * 0.03f, 1f - lowT * 0.45f);

            //拒绝反馈:干笔横向一颤(只抖墨痕,朱印与墨丝按住不动)
            float denyShake = denyPulse > 0.02f ? MathF.Sin(time * 43f) * 2.2f * denyPulse : 0f;

            //墨痕主体:shader 缺席退回 CPU 简笔;上限压缩时填充只写到断口
            Rectangle dest = new((int)(quadTopLeft.X + denyShake), (int)quadTopLeft.Y,
                (int)OnikiriUITheme.HudVigorQuadW, (int)OnikiriUITheme.HudVigorQuadH);
            if (OniVigorInkDraw.Available) {
                OniVigorInkDraw.Draw(sb, dest, new OniVigorInkParams {
                    Fill = displayFill * capRatio,
                    TrailFill = trailFill * capRatio,
                    Flow = flow,
                    SpendPulse = spendPulse,
                    GainPulse = gainPulse,
                    FullPulse = fullPulse,
                    Alpha = alpha,
                    Time = time,
                });
            }
            else {
                DrawFallback(sb, alpha, denyShake);
            }

            //倶利伽罗:上限压缩,末段一截不可书写的焦黑断口
            if (capRatio < 0.995f) {
                DrawCharredCap(sb, alpha, denyShake, time);
            }
            //友切:尾部 1~3 个错位缺口,残心命中偿清即消
            if (guiltLayers > 0) {
                DrawGuiltNotches(sb, alpha, denyShake);
            }
            //潮樋:笔道下方潮头游标涨落,合潮窗纸白涨亮，节拍从此看得见
            if (tidePhase01 >= 0f) {
                DrawTideCrest(sb, alpha, denyShake, time);
            }

        }

        /// <summary>潮头游标:相位三角波沿笔道往返(窗心=最右),合潮时纸白横浪涨亮</summary>
        private void DrawTideCrest(SpriteBatch sb, float alpha, float shakeX, float time) {
            (Vector2 s, Vector2 e) = StrokeSpan(shakeX);
            //三角波:0→窗心(0.5)潮涨到头,再退回;可写段内往返
            float tri = 1f - Math.Abs(tidePhase01 - 0.5f) * 2f;
            Vector2 pos = Vector2.Lerp(s, e, MathHelper.Lerp(0.06f, 0.94f, tri) * capRatio);
            pos.Y += 7f;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Color crest = tideOnBeat ? new Color(255, 243, 226) : OnikiriUITheme.Deep;
            float size = tideOnBeat ? 3.4f + (float)Math.Sin(time * 9f) * 0.5f : 2.1f;
            //潮位基线:极淡,只给游标一个"水面"参照
            OniBrush.DrawGradientLine(sb, new Vector2(s.X, pos.Y), new Vector2(e.X, pos.Y),
                OnikiriUITheme.TextDim * (alpha * 0.14f), OnikiriUITheme.TextDim * (alpha * 0.07f), 1f);
            //潮头
            sb.Draw(pixel, pos, src, crest * (alpha * (tideOnBeat ? 0.95f : 0.55f)),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(size), SpriteEffects.None, 0f);
            if (tideOnBeat) {
                //合潮横浪:一线纸白拉开
                sb.Draw(pixel, pos, src, OnikiriUITheme.Bright * (alpha * 0.45f),
                    0f, new Vector2(0.5f), new Vector2(size * 4.2f, 1.1f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>笔道基线两端(受 denyShake)</summary>
        private (Vector2 start, Vector2 end) StrokeSpan(float shakeX) {
            float y = quadTopLeft.Y + OnikiriUITheme.HudVigorQuadH * 0.5f;
            return (new Vector2(quadTopLeft.X + shakeX + OnikiriUITheme.HudVigorPad, y),
                new Vector2(quadTopLeft.X + shakeX + OnikiriUITheme.HudVigorQuadW - OnikiriUITheme.HudVigorPad, y));
        }

        /// <summary>焦黑断口:断口处一粒余烬呼吸,其后炭黑残段,不映射满长隐瞒代价</summary>
        private void DrawCharredCap(SpriteBatch sb, float alpha, float shakeX, float time) {
            (Vector2 s, Vector2 e) = StrokeSpan(shakeX);
            Vector2 capPos = Vector2.Lerp(s, e, capRatio);
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            //炭黑残段:粗糙碎节而非整线
            int chips = 4;
            for (int i = 0; i < chips; i++) {
                float t0 = (i + 0.12f) / chips;
                float t1 = (i + 0.82f) / chips;
                Vector2 a = Vector2.Lerp(capPos, e, t0);
                Vector2 b = Vector2.Lerp(capPos, e, t1);
                float wobble = ((i * 37 % 5) - 2) * 0.7f;
                OniBrush.DrawGradientLine(sb, a + new Vector2(0f, wobble), b + new Vector2(0f, wobble),
                    new Color(26, 13, 12) * (alpha * 0.9f), new Color(14, 7, 8) * (alpha * 0.75f), 2.4f);
            }
            //断口一粒余烬,低频呼吸
            float breath = 0.55f + 0.25f * (float)Math.Sin(time * 2.6f);
            sb.Draw(pixel, capPos, src, OnikiriUITheme.BurnDim * (alpha * breath),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(3.4f), SpriteEffects.None, 0f);
            sb.Draw(pixel, capPos, src, OnikiriUITheme.BurnHot * (alpha * breath * 0.5f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(1.7f), SpriteEffects.None, 0f);
        }

        /// <summary>咎缺口:可写段尾部的错位断栏(上下半各偏一侧),与友切字形的断口同语义</summary>
        private void DrawGuiltNotches(SpriteBatch sb, float alpha, float shakeX) {
            (Vector2 s, Vector2 e) = StrokeSpan(shakeX);
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            int count = Math.Min(guiltLayers, 3);
            for (int i = 0; i < count; i++) {
                Vector2 pos = Vector2.Lerp(s, e, capRatio * (0.72f + 0.10f * i));
                //错位两半:黑缺口错开半格,当中留发丝
                sb.Draw(pixel, pos + new Vector2(-1.4f, -3.4f), src, OnikiriUITheme.Ink * (alpha * 0.96f),
                    0.10f, new Vector2(0.5f), new Vector2(3.4f, 5.2f), SpriteEffects.None, 0f);
                sb.Draw(pixel, pos + new Vector2(1.4f, 3.4f), src, OnikiriUITheme.Ink * (alpha * 0.96f),
                    0.10f, new Vector2(0.5f), new Vector2(3.4f, 5.2f), SpriteEffects.None, 0f);
                sb.Draw(pixel, pos, src, OnikiriUITheme.Bright * (alpha * 0.5f),
                    0.10f, new Vector2(0.5f), new Vector2(0.9f, 9f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>CPU 降级:上限底痕 + 残痕红线 + 已填充段一笔刀痕(sweep 即截断,同样只写到断口)</summary>
        private void DrawFallback(SpriteBatch sb, float alpha, float shakeX = 0f) {
            float y = quadTopLeft.Y + OnikiriUITheme.HudVigorQuadH * 0.5f;
            float x0 = quadTopLeft.X + shakeX + OnikiriUITheme.HudVigorPad;
            float x1 = quadTopLeft.X + shakeX + OnikiriUITheme.HudVigorQuadW - OnikiriUITheme.HudVigorPad;
            OniBrush.DrawGradientLine(sb, new Vector2(x0, y), new Vector2(x1, y),
                OnikiriUITheme.TextDim * (alpha * 0.30f), OnikiriUITheme.TextDim * (alpha * 0.16f), 1.2f);
            float fillX = MathHelper.Lerp(x0, x1, displayFill * capRatio);
            float trailX = MathHelper.Lerp(x0, x1, trailFill * capRatio);
            if (trailX - fillX > 1.5f) {
                OniBrush.DrawGradientLine(sb, new Vector2(fillX, y), new Vector2(trailX, y),
                    OnikiriUITheme.Bright * (alpha * 0.55f), OnikiriUITheme.Bright * (alpha * 0.10f), 2.2f);
            }
            if (displayFill > 0.01f) {
                OniBrush.DrawTaperedSlash(sb, new Vector2(x0, y + 1f), new Vector2(x1, y - 1f),
                    5.8f, 1.3f, alpha * 0.95f, displayFill * capRatio);
            }
        }

        /// <summary>悬浮读数</summary>
        public void DrawTooltip(SpriteBatch sb, float alpha) {
            alpha *= hoverEase;
            if (alpha <= 0.02f) {
                return;
            }
            string title = OniTalismanHud.VigorTitle.Value;
            string line = string.Format(OniTalismanHud.VigorValueFormat.Value,
                (int)MathF.Round(snap.Value), (int)MathF.Round(snap.MaxValue));
            OniTooltipPanel.Draw(sb, lastMouse, title, 0.78f, alpha,
                new OniTooltipLine(line, OnikiriUITheme.TextDim));
        }
    }
}
