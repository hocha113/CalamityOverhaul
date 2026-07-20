using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.BossBars
{
    /// <summary>
    /// 赛博朋克2077风格Boss血条
    /// </summary>
    public class CyberBossBarStyle : ModBossBarStyle
    {
        public const int MaxBars = 4;
        public const int BarWidth = 540;
        //薄条：核心信号线，不再是粗血槽
        public const int BarHeight = 12;
        //断续分段数量（对应2077的分段信号槽）
        public const int Segments = 5;
        public const int TopMargin = 40;
        public const int VerticalSpacing = 84;

        public static List<CyberBossHPUI> Bars;
        public static List<int> ExclusionList;

        public override void Load() {
            Bars = [];
            ExclusionList = [];
        }

        public override void SetStaticDefaults() {
            Bars = [];
            ExclusionList = [];
        }

        public override void Unload() {
            Bars = null;
            ExclusionList = null;
        }

        public override void Update(IBigProgressBar currentBar, ref BigProgressBarInfo info) {
            foreach (NPC n in Main.ActiveNPCs) {
                if (ExclusionList.Contains(n.type))
                    continue;
                //realLife>=0表示该NPC是某段蠕虫子节点，跳过避免重复添加
                if (n.boss && n.realLife < 0)
                    TryAddBar(n.whoAmI);
            }

            for (int i = 0; i < Bars.Count; i++) {
                Bars[i].Update();
                if (Bars[i].CloseTimer >= CyberBossHPUI.CloseTime) {
                    Bars.RemoveAt(i);
                    i--;
                }
            }
        }

        private static void TryAddBar(int index) {
            if (Bars.Count >= MaxBars) return;
            NPC npc = Main.npc[index];
            if (!npc.active || npc.life <= 0) return;
            if (Bars.Any(b => b.NPCIndex == index)) return;
            Bars.Add(new CyberBossHPUI(index));
        }

        public override bool PreventDraw => true;

        public override void Draw(SpriteBatch sb, IBigProgressBar currentBar, BigProgressBarInfo info) {
            float cx = Main.screenWidth / 2f;
            float y = TopMargin;
            foreach (var ui in Bars) {
                ui.Draw(sb, cx, y);
                y += VerticalSpacing;
            }
        }
    }

    /// <summary>
    /// 单个Boss血条的状态与绘制
    /// </summary>
    public class CyberBossHPUI
    {
        public const int OpenTime = 50;
        public const int CloseTime = 80;
        public const int HitFlashFrames = 22;

        public int NPCIndex;
        public int IntendedType;
        public int OpenTimer;
        public int CloseTimer;
        public long PrevLife;
        public long InitMaxLife;
        public float SmoothRatio = 1f;
        public float TrailRatio = 1f;
        public float HitFlash;

        //赛博朋克2077 HUD红色信号色系（满血珊瑚红→残血猩红，不用黄）
        private static readonly Color HpHigh = new(255, 88, 70);
        private static readonly Color HpMid = new(255, 55, 52);
        private static readonly Color HpLow = new(240, 35, 48);
        //文字：暖珊瑚（读数感），暗珊瑚（次要信息）
        private static readonly Color Coral = new(255, 150, 130);
        private static readonly Color CoralDim = new(205, 95, 88);
        //受击色散
        private static readonly Color ChromaCyan = new(60, 220, 255);
        private static readonly Color ChromaRed = new(255, 45, 70);

        public NPC Target => Main.npc.IndexInRange(NPCIndex) ? Main.npc[NPCIndex] : null;

        public float LifeRatio {
            get {
                NPC npc = Target;
                if (npc == null || !npc.active || InitMaxLife <= 0)
                    return 0f;
                return MathHelper.Clamp(npc.life / (float)InitMaxLife, 0f, 1f);
            }
        }

        public CyberBossHPUI(int index) {
            NPCIndex = index;
            NPC npc = Target;
            if (npc != null && npc.active) {
                IntendedType = npc.type;
                PrevLife = npc.life;
                InitMaxLife = npc.lifeMax;
            }
        }

        public void Update() {
            NPC npc = Target;
            bool dead = npc == null || !npc.active || npc.type != IntendedType;
            if (dead) {
                CloseTimer = Math.Min(CloseTimer + 1, CloseTime);
                return;
            }
            OpenTimer = Math.Min(OpenTimer + 1, OpenTime);
            if (npc.lifeMax > InitMaxLife)
                InitMaxLife = npc.lifeMax;

            if (npc.life < PrevLife)
                HitFlash = 1f;
            PrevLife = npc.life;

            float target = LifeRatio;
            SmoothRatio = MathHelper.Lerp(SmoothRatio, target, 0.12f);
            TrailRatio = MathHelper.Lerp(TrailRatio, target, 0.03f);
            if (Math.Abs(SmoothRatio - target) < 0.002f) SmoothRatio = target;
            if (Math.Abs(TrailRatio - target) < 0.002f) TrailRatio = target;

            if (HitFlash > 0f) HitFlash -= 1f / HitFlashFrames;
            if (HitFlash < 0f) HitFlash = 0f;
        }

        //威胁色插值(高=珊瑚红,中=红,低=猩红)，全程红色系
        private static Color ThreatColor(float r) {
            if (r > 0.6f) return Color.Lerp(HpMid, HpHigh, (r - 0.6f) / 0.4f);
            if (r > 0.3f) return Color.Lerp(HpLow, HpMid, (r - 0.3f) / 0.3f);
            return HpLow;
        }

        //lifeMax对数映射出威胁等级(仅视觉)
        private static int ComputeLevel(long maxLife) {
            if (maxLife <= 1) return 1;
            double lv = Math.Log10(maxLife) * 9.5;
            return Math.Clamp((int)lv, 1, 99);
        }

        private string StatusTag() {
            NPC npc = Target;
            if (npc == null || !npc.active) return "[ NEUTRALIZED ]";
            if (LifeRatio < 0.2f) return "[ CRITICAL ]";
            if (LifeRatio < 0.5f) return "[ WOUNDED ]";
            return "[ HOSTILE ]";
        }

        public void Draw(SpriteBatch sb, float cx, float y) {
            NPC npc = Target;
            if (npc == null) return;

            float alpha = MathHelper.Clamp(OpenTimer / (float)OpenTime, 0f, 1f);
            if (CloseTimer > 0)
                alpha = 1f - MathHelper.Clamp(CloseTimer / (float)CloseTime, 0f, 1f);
            if (alpha <= 0f) return;

            //开场赛博启动闪烁
            if (OpenTimer == 3 || OpenTimer == 7 || OpenTimer == 14)
                alpha *= Main.rand.NextFloat(0.35f, 0.55f);
            if (OpenTimer == 4 || OpenTimer == 8 || OpenTimer == 15)
                alpha *= Main.rand.NextFloat(0.65f, 0.8f);

            float barW = CyberBossBarStyle.BarWidth;
            int barH = CyberBossBarStyle.BarHeight;
            float left = cx - barW / 2f;
            Color primary = ThreatColor(LifeRatio);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            const float nameScale = 1.0f;
            const float smallScale = 0.74f;
            const float pctScale = 1.18f;
            const float tagScale = 0.74f;

            string name = npc.FullName.ToUpperInvariant();
            string lvText = $"LV.{ComputeLevel(InitMaxLife):00}";
            string hpText = $"{Math.Max(npc.life, 0):N0} / {InitMaxLife:N0}";
            int pct = (int)Math.Round(LifeRatio * 100f);
            string pctText = $"{pct}%";

            Vector2 nameSize = font.MeasureString(name) * nameScale;
            Vector2 lvSize = font.MeasureString(lvText) * smallScale;
            Vector2 hpSize = font.MeasureString(hpText) * smallScale;
            Vector2 pctSize = font.MeasureString(pctText) * pctScale;

            float rowTopH = MathF.Max(nameSize.Y, lvSize.Y);
            float barY = y + rowTopH + 7f;
            float row3Y = barY + barH + 7f;

            //暗雾背景
            DrawBacking(sb, cx, y - 6f, row3Y + pctSize.Y + 6f, barW, alpha);

            //顶行：LV+名称+HP读数
            float lvY = y + (rowTopH - lvSize.Y) / 2f;
            DrawHudText(sb, font, lvText, new Vector2(left, lvY), CoralDim * (alpha * 0.9f), smallScale);

            float nameX = left + lvSize.X + 10f;
            float chroma = HitFlash * 4f;
            if (chroma > 0.5f) {
                DrawHudText(sb, font, name, new Vector2(nameX - chroma, y), ChromaCyan * (alpha * 0.5f), nameScale);
                DrawHudText(sb, font, name, new Vector2(nameX + chroma, y), ChromaRed * (alpha * 0.5f), nameScale);
            }
            DrawHudText(sb, font, name, new Vector2(nameX, y), Coral * alpha, nameScale);

            float hpY = y + (rowTopH - hpSize.Y) / 2f;
            DrawHudText(sb, font, hpText, new Vector2(left + barW - hpSize.X, hpY), CoralDim * (alpha * 0.85f), smallScale);

            //：： 着色器主条（材质式 HUD 信号线）：：
            DrawShaderBar(sb, left, barY, barW, barH, alpha);

            //：： 附加辉光层（红色漏光 + 前沿亮点）：：
            DrawGlow(sb, left, barY, barW, barH, primary, alpha);

            //：： 第三行：百分比（左，醒目）+ ID/状态（右下，暗）：：
            DrawHudText(sb, font, pctText, new Vector2(left, row3Y), primary * alpha, pctScale);

            string idTag = $"TYPE:{npc.type:0000}";
            float idScale = tagScale * 0.85f;
            Vector2 idSize = font.MeasureString(idTag) * idScale;
            DrawHudText(sb, font, idTag,
                new Vector2(left + pctSize.X + 12f, row3Y + (pctSize.Y - idSize.Y) - 2f),
                CoralDim * (alpha * 0.7f), idScale);

            string tag = StatusTag();
            float tagAlpha = alpha;
            if (LifeRatio < 0.2f)
                tagAlpha *= 0.55f + 0.45f * (float)Math.Sin(Main.GameUpdateCount * 0.25f);
            Vector2 tagSize = font.MeasureString(tag) * tagScale;
            DrawHudText(sb, font, tag,
                new Vector2(left + barW - tagSize.X, row3Y + (pctSize.Y - tagSize.Y)),
                primary * tagAlpha, tagScale);
        }

        //羽化暗雾背景：用 Fog 蒙版拉伸成横向暗带，中心压暗、边缘透明，杜绝硬框
        private static void DrawBacking(SpriteBatch sb, float cx, float top, float bottom, float barW, float alpha) {
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog == null) return;

            float h = (bottom - top) + 34f;
            float w = barW + 170f;
            float cy = (top + bottom) / 2f;

            Color outer = new Color(10, 3, 4) * (alpha * 0.55f);
            sb.Draw(fog, new Rectangle((int)(cx - w / 2f), (int)(cy - h / 2f), (int)w, (int)h), outer);

            //中心再叠一层更深更窄，加强文字/条的可读性
            Color inner = new Color(6, 1, 2) * (alpha * 0.5f);
            sb.Draw(fog, new Rectangle((int)(cx - w * 0.36f), (int)(cy - h * 0.42f), (int)(w * 0.72f), (int)(h * 0.84f)), inner);
        }

        //HUD文字：1.5px暗影 + 主体，弃用粗黑四向描边以摆脱卡通感
        private static void DrawHudText(SpriteBatch sb, DynamicSpriteFont font, string text,
            Vector2 pos, Color color, float scale) {
            float sa = color.A / 255f;
            sb.DrawString(font, text, pos + new Vector2(1.5f, 1.5f),
                Color.Black * (sa * 0.8f), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        //红色辉光：沿填充宽度的宽幅底光 + 前沿亮点 + 受击暖白扩散（Additive）
        private void DrawGlow(SpriteBatch sb, float left, float barY, float barW, int barH, Color primary, float alpha) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            float fillW = barW * SmoothRatio;
            if (glow != null && fillW > 2f) {
                Color gc = primary;
                gc.A = 0;

                sb.Draw(glow, new Rectangle((int)left, (int)(barY - barH * 0.8f),
                    (int)fillW, (int)(barH * 2.6f)), gc * (alpha * 0.22f));

                float leadX = left + fillW;
                float dotScale = barH / (float)glow.Height * 2.4f;
                sb.Draw(glow, new Vector2(leadX, barY + barH / 2f), null, gc * (alpha * 0.55f),
                    0f, glow.Size() / 2f, dotScale, SpriteEffects.None, 0f);

                if (HitFlash > 0.01f) {
                    Color hf = new Color(255, 232, 210);
                    hf.A = 0;
                    sb.Draw(glow, new Rectangle((int)left, (int)(barY - barH),
                        (int)fillW, (int)(barH * 3f)), hf * (alpha * HitFlash * 0.5f));
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        private void DrawShaderBar(SpriteBatch sb, float x, float y, float w, float h, float alpha) {
            Effect effect = EffectLoader.CyberBossBar?.Value;
            Texture2D px = VaultAsset.placeholder2.Value;

            if (effect != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);

                effect.Parameters["uTime"]?.SetValue((float)Main.gameTimeCache.TotalGameTime.TotalSeconds);
                effect.Parameters["uAlpha"]?.SetValue(alpha);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(w, h));
                effect.Parameters["uLifeRatio"]?.SetValue(SmoothRatio);
                effect.Parameters["uTrailRatio"]?.SetValue(TrailRatio);
                effect.Parameters["uHitFlash"]?.SetValue(HitFlash);
                effect.Parameters["uSegments"]?.SetValue((float)CyberBossBarStyle.Segments);
                effect.CurrentTechnique.Passes[0].Apply();

                //预乘 alpha 由着色器处理，这里传 White 即可
                sb.Draw(px, new Rectangle((int)x, (int)y, (int)w, (int)h), Color.White);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
            }
            else {
                //降级绘制：红色分段填充
                Color fallback = ThreatColor(LifeRatio);
                int fillW = (int)(w * SmoothRatio);
                int segN = CyberBossBarStyle.Segments;
                float segW = w / segN;
                for (int i = 0; i < segN; i++) {
                    float segStart = i * segW;
                    float segEnd = (i + 1) * segW - 3;
                    if (segStart >= fillW) break;
                    float end = Math.Min(segEnd, fillW);
                    if (end <= segStart) continue;
                    sb.Draw(px, new Rectangle(
                        (int)(x + segStart), (int)y,
                        (int)(end - segStart), (int)h), fallback * alpha);
                }
            }
        }
    }
}
