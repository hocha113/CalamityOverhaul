using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Guides;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Scenarios.OldNet.UI
{
    /// <summary>
    /// 旧网首潜五步引导：噪音计 → 账本 → 节点分级 → 中继与登出 → 距离底噪。
    /// 信息卡制——每步都有"知道了"确认，玩家真实做出对应动作也会自动推进。
    /// 卡片底板走 OldNetHud.fx 的 TechPanel（域内同一张皮），缺编 CPU 回退。
    /// 经 <see cref="GuideLeadQueue"/> 排队，优先级 20（排在全部既有引导之后）
    /// </summary>
    internal class OldNetGuideLead : ModSystem, IGuideLead
    {
        private enum Phase { Inactive, Noise, Ledger, Nodes, Relay, Drain, Complete }

        private static readonly Phase[] StepOrder =
            [Phase.Noise, Phase.Ledger, Phase.Nodes, Phase.Relay, Phase.Drain];

        private const int CardW = 336;

        private static Phase currentPhase = Phase.Inactive;
        private static float animProgress;
        //Ledger 步基线：进相位时的账本量
        private static int ledgerBaseline;

        public override void SetStaticDefaults() => GuideLeadQueue.Register(this);

        #region 引导排队协议
        int IGuideLead.GuidePriority => 20;
        bool IGuideLead.GuideReserving => Reserving;
        bool IGuideLead.GuideReady => Ready;
        void IGuideLead.OnGuideAbandoned() => MarkSeen();
        #endregion

        private static OldNetGuideData Guide
            => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<OldNetGuideData>();

        //占位：在旧网内 + 未看过
        private static bool Reserving {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || !OldNetWorld.Active) {
                    return false;
                }
                return !Guide.GuideSeen;
            }
        }

        private static bool Ready {
            get {
                if (!Reserving) {
                    return false;
                }
                if (NarrativeTriggerGate.IsBusy || InnoVault.Cinematics.CutsceneDirector.IsPlaying) {
                    return false;
                }
                return SessionUsable();
            }
        }

        //会话可用：活着 + 无骇入时停 + 无全屏 UI；不满足只暂停不重置
        private static bool SessionUsable() {
            Player p = Main.LocalPlayer;
            if (p == null || !p.active || p.dead) {
                return false;
            }
            if (HackTime.Active || OldNetDebriefPanel.Visible) {
                return false;
            }
            if (QuestLog.Instance?.IsOpen == true || QuestManagerUI.Instance?.IsOpen == true) {
                return false;
            }
            return true;
        }

        private static void MarkSeen() {
            Guide.GuideSeen = true;
            currentPhase = Phase.Complete;
            animProgress = 0f;
        }

        private static void SetPhase(Phase phase) {
            currentPhase = phase;
            animProgress = 0f;
            if (phase == Phase.Ledger) {
                ledgerBaseline = OldNetPlayer.Get(Main.LocalPlayer).PendingTotal;
            }
        }

        private static void AdvanceStep() {
            int idx = Array.IndexOf(StepOrder, currentPhase);
            if (idx < 0 || idx >= StepOrder.Length - 1) {
                MarkSeen();
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f, Volume = 0.5f });
            SetPhase(StepOrder[idx + 1]);
        }

        public override void OnWorldUnload() {
            currentPhase = Phase.Inactive;
            animProgress = 0f;
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            if (!GuideLeadQueue.IsHolder(this)) {
                if (currentPhase != Phase.Inactive && currentPhase != Phase.Complete) {
                    currentPhase = Phase.Inactive;
                    animProgress = 0f;
                }
                return;
            }
            if (currentPhase == Phase.Inactive) {
                SetPhase(Phase.Noise);
            }
            if (!SessionUsable()) {
                return;
            }
            animProgress = MathHelper.Lerp(animProgress, 1f, 0.12f);

            //真实操作自动推进：卡片讲的事玩家做了就不用再点确认
            OldNetPlayer session = OldNetPlayer.Get(Main.LocalPlayer);
            switch (currentPhase) {
                case Phase.Noise:
                    if (session.Noise >= 12f) {
                        AdvanceStep();
                    }
                    break;
                case Phase.Ledger:
                    if (session.PendingTotal > ledgerBaseline) {
                        AdvanceStep();
                    }
                    break;
                case Phase.Nodes:
                    if (session.Channeling) {
                        AdvanceStep();
                    }
                    break;
                case Phase.Relay:
                    if (session.SettledTotal > 0) {
                        AdvanceStep();
                    }
                    break;
                case Phase.Drain:
                    int depthCols = (int)(Main.LocalPlayer.Center.X / 16f) - OldNetMetrics.WallCols;
                    if (depthCols > OldNetMetrics.DrainSafeCols) {
                        MarkSeen();
                    }
                    break;
            }
        }

        //==================== 绘制 ====================

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (Array.IndexOf(StepOrder, currentPhase) < 0) {
                return;
            }
            if (!GuideLeadQueue.IsHolder(this) || !SessionUsable() || animProgress < 0.02f) {
                return;
            }
            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) {
                return;
            }
            layers.Insert(idx, new LegacyGameInterfaceLayer("CWRMod: OldNet Guide",
                delegate {
                    DrawOverlay(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.UI));
        }

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        private static Point UIMouse => new((int)(PlayerInput.MouseX / Main.UIScale),
            (int)(PlayerInput.MouseY / Main.UIScale));

        private static readonly Color ColdCyan = new(140, 200, 210);
        private static readonly Color TextDim = new(150, 160, 175);
        private static readonly Color PanelBg = new(8, 12, 16);

        private static void DrawOverlay(SpriteBatch sb) {
            float alpha = MathHelper.Clamp(animProgress, 0f, 1f);
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            (string title, string body) = currentPhase switch {
                Phase.Noise => (OldNetTexts.GuideNoiseTitle.Value, OldNetTexts.GuideNoiseBody.Value),
                Phase.Ledger => (OldNetTexts.GuideLedgerTitle.Value, OldNetTexts.GuideLedgerBody.Value),
                Phase.Nodes => (OldNetTexts.GuideNodesTitle.Value, OldNetTexts.GuideNodesBody.Value),
                Phase.Relay => (OldNetTexts.GuideRelayTitle.Value, OldNetTexts.GuideRelayBody.Value),
                _ => (OldNetTexts.GuideDrainTitle.Value, OldNetTexts.GuideDrainBody.Value),
            };

            const float titleSc = 0.84f;
            const float bodySc = 0.68f;
            float lineT = font.MeasureString("A").Y * titleSc + 2f;
            float lineB = font.MeasureString("A").Y * bodySc + 1f;
            List<string> bodyLines = WrapLines(font, body, (int)((CardW - 28) / bodySc));
            float cardH = 12f + lineT + 9f + bodyLines.Count * lineB + 44f;

            //卡位：噪音计 HUD 簇上方（左下），越界钳制
            float cardX = MathHelper.Clamp(36f, 16f, Math.Max(16f, UIScreenW - CardW - 16f));
            float cardY = MathHelper.Clamp(UIScreenH - 190f - cardH,
                16f, Math.Max(16f, UIScreenH - cardH - 16f));
            float slideY = (1f - alpha) * 16f;
            Rectangle card = new((int)cardX, (int)(cardY + slideY), CardW, (int)cardH);

            DrawCardBg(sb, card, alpha);

            float px = card.X + 14f;
            float py = card.Y + 12f;

            //步数角标 + 标题 + 分隔线
            int stepIdx = Array.IndexOf(StepOrder, currentPhase);
            string counter = $"{stepIdx + 1:00} / {StepOrder.Length:00}";
            float counterW = font.MeasureString(counter).X * 0.58f;
            Utils.DrawBorderString(sb, counter,
                new Vector2(card.Right - 14f - counterW, py), TextDim * (0.7f * alpha), 0.58f);
            Utils.DrawBorderString(sb, title, new Vector2(px, py), ColdCyan * alpha, titleSc);
            py += lineT + 2f;
            sb.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)px, (int)py, CardW - 28, 1), ColdCyan * (0.4f * alpha));
            py += 7f;

            foreach (string wl in bodyLines) {
                Utils.DrawBorderString(sb, wl, new Vector2(px, py), TextDim * (0.92f * alpha), bodySc);
                py += lineB;
            }

            //信息卡制：确认键常驻（真实操作也会自动推进）
            if (DrawAckButton(sb, card, alpha)) {
                AdvanceStep();
            }
        }

        //卡底板：OldNetHud.fx TechPanel 暗钢切角；缺编回退实底+边线
        private static void DrawCardBg(SpriteBatch sb, Rectangle card, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Effect fx = EffectLoader.OldNetHud?.Value;
            if (fx != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
                fx.CurrentTechnique = fx.Techniques["TechPanel"];
                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                fx.Parameters["uPanelSize"]?.SetValue(new Vector2(card.Width, card.Height));
                fx.Parameters["uFrac"]?.SetValue(0f);
                fx.Parameters["uTier"]?.SetValue(0f);
                fx.Parameters["uAlpha"]?.SetValue(alpha);
                fx.CurrentTechnique.Passes[0].Apply();
                sb.Draw(px, card, Color.White);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
                return;
            }
            //CPU 回退：实底 + 1px 边框 + 顶缘受光（禁暗羽化）
            sb.Draw(px, card, PanelBg * (0.92f * alpha));
            Color edge = ColdCyan * (0.5f * alpha);
            sb.Draw(px, new Rectangle(card.X, card.Y, card.Width, 1), edge);
            sb.Draw(px, new Rectangle(card.X, card.Bottom - 1, card.Width, 1), edge * 0.7f);
            sb.Draw(px, new Rectangle(card.X, card.Y, 1, card.Height), edge * 0.85f);
            sb.Draw(px, new Rectangle(card.Right - 1, card.Y, 1, card.Height), edge * 0.85f);
            sb.Draw(px, new Rectangle(card.X + 1, card.Y + 1, card.Width - 2, 1), ColdCyan * (0.75f * alpha));
        }

        private static bool DrawAckButton(SpriteBatch sb, Rectangle card, float alpha) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string label = OldNetTexts.GuideSkip.Value;
            const float sc = 0.62f;
            Vector2 size = font.MeasureString(label) * sc;
            int btnW = (int)size.X + 22;
            const int btnH = 20, margin = 10;
            Rectangle btn = new(card.Right - btnW - margin, card.Bottom - btnH - margin, btnW, btnH);

            bool hovered = btn.Contains(UIMouse);
            Texture2D px = VaultAsset.placeholder2.Value;
            sb.Draw(px, btn, PanelBg * ((hovered ? 0.95f : 0.7f) * alpha));
            Color border = ColdCyan * ((hovered ? 0.85f : 0.45f) * alpha);
            sb.Draw(px, new Rectangle(btn.X, btn.Y, btn.Width, 1), border);
            sb.Draw(px, new Rectangle(btn.X, btn.Bottom - 1, btn.Width, 1), border * 0.7f);
            sb.Draw(px, new Rectangle(btn.X, btn.Y, 1, btn.Height), border * 0.85f);
            sb.Draw(px, new Rectangle(btn.Right - 1, btn.Y, 1, btn.Height), border * 0.85f);
            Utils.DrawBorderString(sb, label,
                btn.Center.ToVector2() - size * 0.5f,
                (hovered ? Color.White : TextDim) * alpha, sc);

            if (hovered) {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    Main.mouseLeftRelease = false;
                    return true;
                }
            }
            return false;
        }

        private static List<string> WrapLines(DynamicSpriteFont font, string text, int wrapW) {
            List<string> result = [];
            foreach (string line in text.Split('\n')) {
                string[] wrapped = VaultUtils.WrapTextArray(line, font, wrapW, 99, out _);
                foreach (string wl in wrapped) {
                    if (string.IsNullOrEmpty(wl)) {
                        continue;
                    }
                    result.Add(wl.TrimEnd('-', ' '));
                }
            }
            return result;
        }
    }
}
