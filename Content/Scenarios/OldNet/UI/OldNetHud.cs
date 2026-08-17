using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using CalamityOverhaul.Content.Scenarios.OldNet.NPCs;
using CalamityOverhaul.Content.UIs.HudStack;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Scenarios.OldNet.UI
{
    /// <summary>
    /// 旧网噪音计 HUD：横向分段噪音条（四档刻度）+ 账本读数 + 被追指示。
    /// 程序化零贴图，镜像 SandevistanHUD 的左下堆叠接法；纯展示不占 mouseInterface
    /// </summary>
    internal class OldNetHud : UIHandle, IBottomLeftHud
    {
        public static OldNetHud Instance => UIHandleLoader.GetUIHandleOfType<OldNetHud>();

        public override bool Active => OldNetWorld.Active && !Main.gameMenu;

        #region 左下角 HUD 队列接入
        //order 2：压在 Kikasa(0) 之上、Sandevistan(5) 之下
        bool IBottomLeftHud.HudStackActive => Active;
        int IBottomLeftHud.HudStackOrder => 2;
        Vector2 IBottomLeftHud.HudStackAnchor => NaturalAnchor;
        float IBottomLeftHud.HudStackTopExtent => 34f;
        //底沿含深度/底噪读数行（M2c）
        float IBottomLeftHud.HudStackBottomExtent => 46f;
        #endregion

        #region 布局与配色

        private const float BarW = 210f;
        private const float BarH = 8f;
        //渐变切片数：冷青→黑墙红的连续读数
        private const int FillSlices = 30;

        //沿用加载屏配色：旧网冷青 → 黑墙红
        private static readonly Color ColdCyan = new(140, 200, 210);
        private static readonly Color EmberRed = new(235, 64, 44);
        private static readonly Color TrackDim = new(30, 48, 54);
        private static readonly Color TextDim = new(150, 160, 175);

        //锚点=条中心，UI 空间防高 UIScale 漂移
        private static Vector2 NaturalAnchor => new(40f + BarW * 0.5f, BottomLeftHudStack.UIScreenH - 96f);

        #endregion

        #region 状态

        private float displayNoise;
        private float timer;
        //满载红闪倒数（拒收时外部点亮）
        private int ledgerFlash;
        //档位跃迁闪光
        private int lastTier;
        private float tierFlash;
        //分带横幅：跨带时中上淡入淡出（-2=首帧哨兵，进图不弹）
        private int lastBandIndex = -2;
        private int bannerTimer;
        private string bannerText;

        /// <summary>账本读数红闪（满载拒收时由 OldNetPlayer 调）</summary>
        internal static void FlashLedger() {
            OldNetHud inst = Instance;
            if (inst != null) {
                inst.ledgerFlash = 45;
            }
        }

        #endregion

        public override void Update() {
            OldNetPlayer session = OldNetPlayer.Get(Main.LocalPlayer);
            displayNoise += (session.Noise - displayNoise) * 0.15f;
            if (MathF.Abs(displayNoise - session.Noise) < 0.05f) {
                displayNoise = session.Noise;
            }
            timer += 0.016f;

            if (session.NoiseTier != lastTier) {
                if (session.NoiseTier > lastTier) {
                    tierFlash = 1f;
                }
                lastTier = session.NoiseTier;
            }
            tierFlash *= 0.92f;
            if (ledgerFlash > 0) {
                ledgerFlash--;
            }

            //──── 分带横幅：跨带触发（首帧只记不弹）────
            int band = OldNetMetrics.BandIndexForColumn((int)(Main.LocalPlayer.Center.X / 16f));
            if (band != lastBandIndex) {
                if (lastBandIndex != -2 && band >= 1) {
                    bannerText = band switch {
                        1 => OldNetTexts.OldNetBandFoot.Value,
                        2 => OldNetTexts.OldNetBandRuin.Value,
                        _ => OldNetTexts.OldNetBandFade.Value,
                    };
                    bannerTimer = 210;
                }
                lastBandIndex = band;
            }
            if (bannerTimer > 0) {
                bannerTimer--;
            }
        }

        public override void Draw(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            OldNetPlayer session = OldNetPlayer.Get(Main.LocalPlayer);
            Vector2 center = BottomLeftHudStack.ResolveAnchor(this);
            Vector2 barTopLeft = center - new Vector2(BarW * 0.5f, BarH * 0.5f);
            float frac = MathHelper.Clamp(displayNoise / 100f, 0f, 1f);
            Color noiseCol = Color.Lerp(ColdCyan, EmberRed, frac);

            //富层走 shader（暗钢切角底板 + 数据流噪音条），锐利前景仍归 CPU；缺编回退原绘制
            Effect hudFx = EffectLoader.OldNetHud?.Value;
            if (hudFx != null) {
                DrawShaderPlate(sb, px, hudFx, barTopLeft, frac, session);
            }
            else {
                DrawTrack(sb, px, barTopLeft);
                DrawFill(sb, px, barTopLeft, frac);
            }
            DrawTierTicks(sb, px, font, barTopLeft);
            DrawTipGlow(sb, barTopLeft, frac, noiseCol);
            DrawHeader(sb, px, font, barTopLeft, session, noiseCol);
            DrawLedgerLine(sb, font, barTopLeft, session);
            DrawDepthLine(sb, font, barTopLeft);
            DrawHunterPips(sb, px, font, barTopLeft);
            DrawBandBanner(sb, font);
        }

        //条下第二行：深度读数 + 回墙方向 + 当前距离底噪（2400 列单锚点世界的定向读数）
        private static void DrawDepthLine(SpriteBatch sb, DynamicSpriteFont font, Vector2 tl) {
            int tileX = (int)(Main.LocalPlayer.Center.X / 16f);
            int depthCols = Math.Max(tileX - OldNetMetrics.WallCols, 0);
            float drain = OldNetMetrics.DrainPerSecondAt(tileX);
            string text = drain > 0f
                ? $"<< DEPTH {depthCols:D4} // -{drain:0.00} RAM/s"
                : $"<< DEPTH {depthCols:D4} // LINK SAFE";
            //底噪越贵越向黑墙红靠
            float severity = MathHelper.Clamp(drain / 0.8f, 0f, 1f);
            Color col = drain > 0f
                ? Color.Lerp(TextDim, EmberRed, 0.25f + severity * 0.6f)
                : TextDim * 0.75f;
            Utils.DrawBorderString(sb, text, tl + new Vector2(0f, BarH + 30f), col, 0.58f);
        }

        //跨带横幅：中上淡入淡出
        private void DrawBandBanner(SpriteBatch sb, DynamicSpriteFont font) {
            if (bannerTimer <= 0 || string.IsNullOrEmpty(bannerText)) {
                return;
            }
            //前 30 帧淡入、后 60 帧淡出
            float fade = MathHelper.Clamp((210 - bannerTimer) / 30f, 0f, 1f)
                * MathHelper.Clamp(bannerTimer / 60f, 0f, 1f);
            float screenW = Terraria.GameInput.PlayerInput.RealScreenWidth / Main.UIScale;
            Vector2 size = font.MeasureString(bannerText) * 0.92f;
            Vector2 pos = new((screenW - size.X) * 0.5f, 110f);
            //横幅底衬线
            Texture2D px = VaultAsset.placeholder2.Value;
            sb.Draw(px, new Rectangle((int)(pos.X - 18f), (int)(pos.Y + size.Y + 6f),
                (int)(size.X + 36f), 1), ColdCyan * (0.45f * fade));
            Utils.DrawBorderString(sb, bannerText, pos, ColdCyan * fade, 0.92f);
        }

        //shader 富层：切角底板罩住 HUD 簇 + 数据流噪音条（Immediate 后恢复 Deferred/UIScaleMatrix）
        private void DrawShaderPlate(SpriteBatch sb, Texture2D px, Effect fx,
            Vector2 tl, float frac, OldNetPlayer session) {
            var panelRect = new Rectangle((int)(tl.X - 12f), (int)(tl.Y - 30f), (int)BarW + 24, 78);
            var barRect = new Rectangle((int)tl.X, (int)tl.Y, (int)BarW, (int)BarH);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            //共享参数化 shader：每个技法调用点全参数重设
            fx.CurrentTechnique = fx.Techniques["TechPanel"];
            fx.Parameters["uTime"]?.SetValue(timer);
            fx.Parameters["uPanelSize"]?.SetValue(new Vector2(panelRect.Width, panelRect.Height));
            fx.Parameters["uFrac"]?.SetValue(frac);
            fx.Parameters["uTier"]?.SetValue((float)session.NoiseTier);
            fx.Parameters["uAlpha"]?.SetValue(1f);
            fx.CurrentTechnique.Passes[0].Apply();
            sb.Draw(px, panelRect, Color.White);

            fx.CurrentTechnique = fx.Techniques["TechBar"];
            fx.Parameters["uTime"]?.SetValue(timer);
            fx.Parameters["uPanelSize"]?.SetValue(new Vector2(barRect.Width, barRect.Height));
            fx.Parameters["uFrac"]?.SetValue(frac);
            fx.Parameters["uTier"]?.SetValue((float)session.NoiseTier);
            fx.Parameters["uAlpha"]?.SetValue(1f);
            fx.CurrentTechnique.Passes[0].Apply();
            sb.Draw(px, barRect, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        //暗色轨道 + 两端封口刻线
        private static void DrawTrack(SpriteBatch sb, Texture2D px, Vector2 tl) {
            sb.Draw(px, tl, null, TrackDim * 0.8f, 0f, Vector2.Zero,
                new Vector2(BarW / px.Width, BarH / px.Height), SpriteEffects.None, 0f);
            //轨道上缘受光线
            sb.Draw(px, tl, null, ColdCyan * 0.18f, 0f, Vector2.Zero,
                new Vector2(BarW / px.Width, 1f / px.Height), SpriteEffects.None, 0f);
            //端帽
            sb.Draw(px, tl + new Vector2(-2f, -2f), null, ColdCyan * 0.55f, 0f, Vector2.Zero,
                new Vector2(1f / px.Width, (BarH + 4f) / px.Height), SpriteEffects.None, 0f);
            sb.Draw(px, tl + new Vector2(BarW + 1f, -2f), null, ColdCyan * 0.55f, 0f, Vector2.Zero,
                new Vector2(1f / px.Width, (BarH + 4f) / px.Height), SpriteEffects.None, 0f);
        }

        //冷青→黑墙红切片渐变填充 + 白芯线
        private void DrawFill(SpriteBatch sb, Texture2D px, Vector2 tl, float frac) {
            if (frac <= 0.001f) {
                return;
            }
            float fillW = BarW * frac;
            float sliceW = BarW / FillSlices;
            int lit = (int)MathF.Ceiling(fillW / sliceW);
            for (int i = 0; i < lit; i++) {
                float x0 = i * sliceW;
                float w = MathF.Min(sliceW, fillW - x0);
                if (w <= 0f) {
                    break;
                }
                Color c = Color.Lerp(ColdCyan, EmberRed, (x0 + w * 0.5f) / BarW);
                sb.Draw(px, tl + new Vector2(x0, 1f), null, c * 0.85f, 0f, Vector2.Zero,
                    new Vector2(w / px.Width, (BarH - 2f) / px.Height), SpriteEffects.None, 0f);
            }
            //白芯线：读数的骨
            sb.Draw(px, tl + new Vector2(0f, BarH * 0.5f - 0.5f), null, Color.White * 0.35f, 0f,
                Vector2.Zero, new Vector2(fillW / px.Width, 1f / px.Height), SpriteEffects.None, 0f);
        }

        //四档阈值刻度：过档点亮，档位跃迁白闪
        private void DrawTierTicks(SpriteBatch sb, Texture2D px, DynamicSpriteFont font, Vector2 tl) {
            for (int t = 1; t <= 4; t++) {
                float threshold = OldNetPlayer.TierThreshold(t);
                float x = tl.X + BarW * threshold / 100f;
                bool passed = displayNoise >= threshold;
                Color tickCol = passed
                    ? Color.Lerp(Color.Lerp(ColdCyan, EmberRed, threshold / 100f), Color.White, tierFlash * 0.7f)
                    : TextDim * 0.4f;
                sb.Draw(px, new Vector2(x, tl.Y - 3f), null, tickCol, 0f, Vector2.Zero,
                    new Vector2(1f / px.Width, (BarH + 6f) / px.Height), SpriteEffects.None, 0f);
                //刻度下角标 T1..T4
                string tag = "T" + t;
                Vector2 sz = font.MeasureString(tag) * 0.5f;
                Utils.DrawBorderString(sb, tag, new Vector2(x - sz.X * 0.5f, tl.Y + BarH + 4f),
                    passed ? tickCol * 0.9f : TextDim * 0.45f, 0.5f);
            }
        }

        //填充前沿脉冲光（SoftGlow A=0，亮层合法路径）
        private void DrawTipGlow(SpriteBatch sb, Vector2 tl, float frac, Color noiseCol) {
            if (frac <= 0.003f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float pulse = 0.7f + 0.3f * MathF.Sin(timer * 6f);
            Vector2 tip = tl + new Vector2(BarW * frac, BarH * 0.5f);
            Color c = noiseCol * (0.5f * pulse);
            c.A = 0;
            sb.Draw(glow, tip, null, c, 0f, glow.Size() * 0.5f, 0.22f, SpriteEffects.None, 0f);
            Color core = Color.White * (0.6f * pulse);
            core.A = 0;
            sb.Draw(glow, tip, null, core, 0f, glow.Size() * 0.5f, 0.08f, SpriteEffects.None, 0f);
        }

        //条上方：NOISE 标签 + 当前档位徽记
        private void DrawHeader(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            Vector2 tl, OldNetPlayer session, Color noiseCol) {
            Utils.DrawBorderString(sb, "NOISE", tl + new Vector2(0f, -22f), TextDim * 0.85f, 0.62f);

            if (session.NoiseTier > 0) {
                string tierTag = "T" + session.NoiseTier;
                float flick = session.NoiseTier >= 4 && MathF.Sin(timer * 20f) > 0f ? 1f : 0.85f;
                Color tierCol = Color.Lerp(noiseCol, Color.White, tierFlash) * flick;
                Vector2 sz = font.MeasureString(tierTag) * 0.72f;
                Utils.DrawBorderString(sb, tierTag,
                    tl + new Vector2(BarW - sz.X, -24f), tierCol, 0.72f);
                //档位徽记左侧警示斜杠
                sb.Draw(px, tl + new Vector2(BarW - sz.X - 10f, -18f), null, tierCol * 0.8f,
                    MathHelper.PiOver4 * 0.5f, Vector2.Zero,
                    new Vector2(2f / px.Width, 10f / px.Height), SpriteEffects.None, 0f);
            }
        }

        //条下方：账本读数，满载红闪
        private void DrawLedgerLine(SpriteBatch sb, DynamicSpriteFont font, Vector2 tl, OldNetPlayer session) {
            int total = session.PendingTotal;
            int cap = session.LedgerCapacity;
            string text = $"LEDGER {total}/{cap}";
            bool full = total >= cap;
            Color col = TextDim * 0.8f;
            if (ledgerFlash > 0 && ledgerFlash / 6 % 2 == 0) {
                col = EmberRed;
            }
            else if (full) {
                col = Color.Lerp(EmberRed, TextDim, 0.25f + 0.25f * MathF.Sin(timer * 5f));
            }
            Utils.DrawBorderString(sb, text, tl + new Vector2(0f, BarH + 14f), col, 0.58f);
        }

        //被追指示：场上猎杀者数（T2+ 才有意义，有则常显）
        private void DrawHunterPips(SpriteBatch sb, Texture2D px, DynamicSpriteFont font, Vector2 tl) {
            int hunters = OldNetICEDirector.ActiveHunterCount;
            if (hunters <= 0) {
                return;
            }
            float pulse = 0.75f + 0.25f * MathF.Sin(timer * 9f);
            Vector2 basePos = tl + new Vector2(BarW - 4f, BarH + 17f);
            //菱形警示片，一只一枚（上限 5 与清剿波补员对齐）
            for (int i = 0; i < Math.Min(hunters, OldNetMetrics.T4SustainCount); i++) {
                Vector2 p = basePos - new Vector2(i * 11f, 0f);
                sb.Draw(px, p, null, EmberRed * pulse, MathHelper.PiOver4,
                    new Vector2(0.5f), new Vector2(6f / px.Width, 6f / px.Height), SpriteEffects.None, 0f);
            }
            string tag = "×" + hunters;
            Vector2 sz = font.MeasureString(tag) * 0.55f;
            Utils.DrawBorderString(sb, tag, basePos - new Vector2(hunters * 11f + sz.X + 2f, sz.Y * 0.55f - 3f),
                EmberRed * (0.9f * pulse), 0.55f);
        }
    }
}
