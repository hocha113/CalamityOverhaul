using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Draedon;
using CalamityOverhaul.Content.Scenarios.Draedon.ExoMechdusaSums;
using CalamityOverhaul.Content.Scenarios.Draedon.PQCDs.DraedonShops;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.PQCDs
{
    /// <summary>呼叫框禁用接口</summary>
    public interface IDraedonCallDisabledProvider
    {
        bool IsCallDisabled { get; }
        string DisabledReason { get; }
    }

    /// <summary>嘉登已在场则禁用</summary>
    internal class DraedonCallDisabledProvider : IDraedonCallDisabledProvider
    {
        public bool IsCallDisabled {
            get {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.active && npc.type == CWRID.NPC_Draedon) {
                        return true;
                    }
                }
                return false;
            }
        }

        public string DisabledReason => DraedonCallUI.DisabledReasonText?.Value ?? "UNAVAILABLE";
    }

    /// <summary>商店左侧呼叫面板</summary>
    internal class DraedonCallUI : UIHandle, ILocalizedModType
    {
        public static DraedonCallUI Instance => UIHandleLoader.GetUIHandleOfType<DraedonCallUI>();
        public static IDraedonCallDisabledProvider DisabledProvider = new DraedonCallDisabledProvider();

        public override bool Active => DraedonShopUI.Instance.Active;
        public override float RenderPriority => 0.9f;
        public override Vector2 MousePosition => DraedonShopTheme.UIMouse;
        public string LocalizationCategory => "UI";

        #region 本地化
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText TitleTextDisabled { get; private set; }
        public static LocalizedText CallButtonText { get; private set; }
        public static LocalizedText CallingText { get; private set; }
        public static LocalizedText DisabledButtonText { get; private set; }
        public static LocalizedText ConnectingText { get; private set; }
        public static LocalizedText ConnectedText { get; private set; }
        public static LocalizedText DisabledReasonText { get; private set; }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "呼叫嘉登");
            TitleTextDisabled = this.GetLocalization(nameof(TitleTextDisabled), () => "呼叫禁用");
            CallButtonText = this.GetLocalization(nameof(CallButtonText), () => "启动呼叫");
            CallingText = this.GetLocalization(nameof(CallingText), () => "正在呼叫...");
            DisabledButtonText = this.GetLocalization(nameof(DisabledButtonText), () => "禁用中");
            ConnectingText = this.GetLocalization(nameof(ConnectingText), () => "正在连接...");
            ConnectedText = this.GetLocalization(nameof(ConnectedText), () => "已连接");
            DisabledReasonText = this.GetLocalization(nameof(DisabledReasonText), () => "嘉登已被呼叫");
        }
        #endregion

        #region 状态
        private readonly DraedonPanelState state = new() {
            TechSideMargin = 18f,
            DataSpawnInterval = 26,
            MaxDataParticles = 8,
            CircuitSpawnInterval = 40,
            MaxCircuitNodes = 4,
            ParticleInsetY = 36f
        };

        private float alpha;
        private float eased;
        private Rectangle panelRect;
        private Rectangle portraitRect;
        private Rectangle buttonRect;

        private bool isDisabled;
        private float disabledTransition;
        private bool isCalling;
        private float callProgress;

        private bool hoverButton;
        private float buttonHover;
        private string statusText = string.Empty;
        private float statusAlpha;
        private bool wasOpen;
        #endregion

        public override void Update() {
            bool shopOpen = DraedonShopUI.Instance.IsOpen;
            //重开清呼叫进度
            if (shopOpen && !wasOpen) {
                isCalling = false;
                callProgress = 0f;
                statusText = string.Empty;
                statusAlpha = 0f;
            }
            wasOpen = shopOpen;

            alpha = MathHelper.Lerp(alpha, shopOpen ? 1f : 0f, 0.12f);
            if (alpha <= 0.002f && !shopOpen) {
                Cleanup();
                return;
            }
            eased = VaultUtils.EaseOutCubic(MathHelper.Clamp(alpha, 0f, 1f));

            bool nowDisabled = DisabledProvider?.IsCallDisabled ?? false;
            if (nowDisabled != isDisabled) {
                isDisabled = nowDisabled;
                if (isDisabled) {
                    isCalling = false;
                    callProgress = 0f;
                    statusText = DisabledProvider?.DisabledReason ?? string.Empty;
                }
                else {
                    statusText = string.Empty;
                }
            }
            disabledTransition = MathHelper.Lerp(disabledTransition, isDisabled ? 1f : 0f, 0.1f);

            //贴靠商店左侧
            Rectangle shop = DraedonShopUI.Instance.PanelRect;
            int x = shop.X - DraedonShopTheme.PanelGap - DraedonShopTheme.CallPanelWidth - (int)((1f - eased) * 32f);
            int y = shop.Y + (shop.Height - DraedonShopTheme.CallPanelHeight) / 2;
            panelRect = new Rectangle(x, y, DraedonShopTheme.CallPanelWidth, DraedonShopTheme.CallPanelHeight);

            int portraitSize = 150;
            portraitRect = new Rectangle(panelRect.Center.X - portraitSize / 2, panelRect.Y + 66, portraitSize, portraitSize);
            buttonRect = new Rectangle(panelRect.Center.X - 94, panelRect.Bottom - 66, 188, 46);

            state.Update(panelRect, shopOpen);

            if (shopOpen && eased > 0.85f) {
                UpdateInteraction();
            }
            else {
                hoverButton = false;
            }
            buttonHover = MathHelper.Lerp(buttonHover, hoverButton ? 1f : 0f, 0.18f);

            if (isCalling && !isDisabled) {
                callProgress += 0.015f;
                if (callProgress >= 1f) {
                    callProgress = 1f;
                    OnCallComplete();
                }
            }

            statusAlpha = MathHelper.Clamp(statusAlpha + (string.IsNullOrEmpty(statusText) ? -0.05f : 0.05f), 0f, 1f);
        }

        private void UpdateInteraction() {
            hoverInMainPage = panelRect.Contains(MousePosition.ToPoint());
            hoverButton = buttonRect.Contains(MousePosition.ToPoint());

            if (hoverInMainPage) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
            }

            if (hoverButton && keyLeftPressState == KeyPressState.Pressed) {
                if (isDisabled) {
                    SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f, Pitch = -0.5f });
                }
                else if (!isCalling) {
                    StartCall();
                }
            }
        }

        private void StartCall() {
            isCalling = true;
            callProgress = 0f;
            statusText = ConnectingText.Value;
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = 0.3f });
            SoundEngine.PlaySound("CalamityMod/Sounds/Custom/CodebreakerBeam".GetSound() with { Volume = 0.7f });
        }

        private void OnCallComplete() {
            statusText = ConnectedText.Value;
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.8f, Pitch = 0.5f });

            ExoMechdusaSum.SimpleMode = true;
            NPC.NewNPC(Main.LocalPlayer.FromObjectGetParent(), (int)Main.LocalPlayer.Center.X,
                (int)Main.LocalPlayer.Center.Y - 260, CWRID.NPC_Draedon);

            isCalling = false;
            callProgress = 0f;
            DraedonShopUI.Instance.Close();
        }

        private void Cleanup() {
            isCalling = false;
            callProgress = 0f;
            buttonHover = 0f;
            hoverButton = false;
            statusText = string.Empty;
            statusAlpha = 0f;
            state.Reset();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (alpha <= 0.002f) {
                return;
            }

            DraedonPanelDraw.DrawPanel(spriteBatch, panelRect, eased, state, DraedonPanelDetail.Full, shadowLayers: 7);
            state.DrawParticles(spriteBatch, eased, 0.7f, 0.6f);

            DrawTitle(spriteBatch);
            DrawPortrait(spriteBatch);
            DrawButton(spriteBatch);
            DrawStatus(spriteBatch);

            if (disabledTransition > 0.01f) {
                DrawDisabledOverlay(spriteBatch);
            }
        }

        private void DrawTitle(SpriteBatch sb) {
            string title = isDisabled ? TitleTextDisabled.Value : TitleText.Value;
            float w = FontAssets.MouseText.Value.MeasureString(title).X * 0.85f;
            Vector2 pos = new(panelRect.Center.X - w / 2f + 8f, panelRect.Y + 18f);
            DraedonPanelDraw.DrawSpeakerGlow(sb, pos, title, eased, 0.85f);
            Color col = Color.Lerp(DraedonShopTheme.EdgeBright, DraedonShopTheme.Danger, disabledTransition) * eased;
            Utils.DrawBorderString(sb, title, pos, col, 0.85f);

            DraedonPanelDraw.DrawDashDivider(sb,
                new Vector2(panelRect.X + 18, panelRect.Y + 48),
                new Vector2(panelRect.Right - 18, panelRect.Y + 48), eased, state.DataStreamTimer);
        }

        private void DrawPortrait(SpriteBatch sb) {
            DraedonPanelDraw.DrawPortraitFrame(sb, portraitRect, eased, state.CircuitPulseTimer);

            Texture2D tex = isCalling || isDisabled ? ADVAsset.DraedonRedADV : ADVAsset.DraedonADV;
            if (tex == null) {
                return;
            }

            if (isCalling && !isDisabled) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float pulse = MathF.Sin(state.CircuitPulseTimer * 4f) * 0.5f + 0.5f;
                Color gc = new Color(0, 210, 205, 0) * (eased * (0.3f + 0.4f * callProgress) * (0.5f + 0.5f * pulse));
                sb.Draw(glow, portraitRect.Center.ToVector2(), null, gc, 0f, glow.Size() / 2f,
                    portraitRect.Width / (float)glow.Width * 1.6f, SpriteEffects.None, 0f);
            }

            Rectangle inner = portraitRect;
            inner.Inflate(-10, -10);
            float scale = Math.Min(inner.Width / (float)tex.Width, inner.Height / (float)tex.Height);
            Color tint = Color.Lerp(Color.White, new Color(255, 150, 150), disabledTransition) * (eased * (isDisabled ? 0.65f : 1f));
            sb.Draw(tex, portraitRect.Center.ToVector2(), null, tint, 0f, tex.Size() / 2f, scale, SpriteEffects.None, 0f);
        }

        private void DrawButton(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            float pulse = MathF.Sin(state.CircuitPulseTimer * 1.4f) * 0.5f + 0.5f;
            Color accent = isCalling
                ? Color.Lerp(DraedonShopTheme.Edge, DraedonShopTheme.EdgeBright, pulse)
                : Color.Lerp(DraedonShopTheme.Edge, DraedonShopTheme.EdgeBright, 0.3f + 0.5f * buttonHover);
            accent = Color.Lerp(accent, DraedonShopTheme.Danger, disabledTransition);

            sb.Draw(px, buttonRect, new Rectangle(0, 0, 1, 1), DraedonShopTheme.Void * (eased * 0.85f));
            if (buttonHover > 0.01f && !isDisabled) {
                sb.Draw(px, buttonRect, new Rectangle(0, 0, 1, 1), accent * (eased * 0.16f * buttonHover));
            }

            //非对称框,上粗左半亮
            sb.Draw(px, new Rectangle(buttonRect.X, buttonRect.Y, buttonRect.Width, 2), new Rectangle(0, 0, 1, 1), accent * (eased * 0.95f));
            sb.Draw(px, new Rectangle(buttonRect.X, buttonRect.Y, 3, buttonRect.Height), new Rectangle(0, 0, 1, 1), accent * (eased * 0.7f));
            sb.Draw(px, new Rectangle(buttonRect.X, buttonRect.Bottom - 1, buttonRect.Width, 1), new Rectangle(0, 0, 1, 1), accent * (eased * 0.35f));
            sb.Draw(px, new Rectangle(buttonRect.Right - 1, buttonRect.Y, 1, buttonRect.Height), new Rectangle(0, 0, 1, 1), accent * (eased * 0.4f));

            if (buttonHover > 0.01f && !isDisabled) {
                DraedonPanelDraw.DrawChoiceDashIndicator(sb, buttonRect, accent, buttonHover, eased, state.DataStreamTimer);
            }

            if (isCalling && !isDisabled) {
                int pw = (int)((buttonRect.Width - 6) * callProgress);
                sb.Draw(px, new Rectangle(buttonRect.X + 3, buttonRect.Bottom - 4, pw, 2), new Rectangle(0, 0, 1, 1),
                    DraedonShopTheme.EdgeBright * (eased * 0.9f));
            }

            string text = isDisabled ? DisabledButtonText.Value : isCalling ? CallingText.Value : CallButtonText.Value;
            float ts = 0.85f;
            Vector2 size = font.MeasureString(text) * ts;
            Vector2 pos = new(buttonRect.Center.X - size.X / 2f, buttonRect.Center.Y - size.Y / 2f);
            if ((buttonHover > 0.01f || isCalling) && !isDisabled) {
                Color g = accent * (eased * 0.5f);
                for (int i = 0; i < 4; i++) {
                    Utils.DrawBorderString(sb, text, pos + (MathHelper.TwoPi * i / 4f).ToRotationVector2() * 1.5f, g * 0.45f, ts);
                }
            }
            Color textColor = Color.Lerp(DraedonShopTheme.TextBright, DraedonShopTheme.Danger, disabledTransition) * eased;
            Utils.DrawBorderString(sb, text, pos, textColor, ts);
        }

        private void DrawStatus(SpriteBatch sb) {
            if (statusAlpha <= 0f || string.IsNullOrEmpty(statusText)) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float ts = 0.72f;
            Vector2 size = font.MeasureString(statusText) * ts;
            Vector2 pos = new(panelRect.Center.X - size.X / 2f, buttonRect.Y - 26f);
            Color col = (isDisabled ? DraedonShopTheme.Danger : isCalling ? DraedonShopTheme.Gold : DraedonShopTheme.EdgeBright)
                * (eased * statusAlpha);
            Color g = col * 0.5f;
            for (int i = 0; i < 4; i++) {
                Utils.DrawBorderString(sb, statusText, pos + (MathHelper.TwoPi * i / 4f).ToRotationVector2() * 1.2f, g * 0.45f, ts);
            }
            Utils.DrawBorderString(sb, statusText, pos, col, ts);
        }

        private void DrawDisabledOverlay(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float t = disabledTransition * eased;

            float warn = MathF.Sin(state.GlitchTimer * 2f) * 0.5f + 0.5f;
            sb.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), DraedonShopTheme.Danger * (t * 0.08f * warn));

            Vector2 c = portraitRect.Center.ToVector2();
            float half = portraitRect.Width * 0.34f;
            Color ban = DraedonShopTheme.Danger * (t * 0.9f);
            DrawLine(sb, c + new Vector2(-half, -half), c + new Vector2(half, half), ban, 3f);
            DrawLine(sb, c + new Vector2(half, -half), c + new Vector2(-half, half), ban, 3f);

            if (MathF.Sin(state.GlitchTimer * 3f) > 0.7f && Main.rand.NextBool(3)) {
                int count = Main.rand.Next(2, 4);
                for (int i = 0; i < count; i++) {
                    int gy = panelRect.Y + Main.rand.Next(panelRect.Height);
                    int h = Main.rand.Next(2, 6);
                    Color gc = (Main.rand.NextBool() ? DraedonShopTheme.Danger : DraedonShopTheme.EdgeBright) * (t * 0.3f);
                    sb.Draw(px, new Rectangle(panelRect.X, gy, panelRect.Width, h), new Rectangle(0, 0, 1, 1), gc);
                }
            }
        }

        private static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float thickness) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float len = edge.Length();
            if (len < 0.1f) {
                return;
            }
            sb.Draw(px, start, new Rectangle(0, 0, 1, 1), color, edge.ToRotation(), new Vector2(0f, 0.5f),
                new Vector2(len, thickness), SpriteEffects.None, 0f);
        }
    }
}
