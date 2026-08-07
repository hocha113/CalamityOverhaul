using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Cyberwares.Victors.UIs;
using InnoVault.Cinematics;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Cyberwares.Victors
{
    /// <summary>
    /// 手术流程 诊所关→过场→眼睑→帧86请求→重开诊所；兼眼睑全屏
    /// </summary>
    internal class VictorSurgery : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        /// <summary>眼睑全黑时居中的提示文字</summary>
        public static LocalizedText SurgeryInProgressText { get; private set; }

        public override void SetStaticDefaults() {
            SurgeryInProgressText = this.GetLocalization(nameof(SurgeryInProgressText), () => "SURGERY IN PROGRESS");
        }

        /// <summary>过场进行中</summary>
        public static bool Active { get; private set; }

        /// <summary>眼睑 0睁→1黑，Lerp→EyelidTarget</summary>
        public static float EyelidValue;
        /// <summary>眼睑目标</summary>
        public static float EyelidTarget;
        /// <summary>睁眼眩光 0~1</summary>
        public static float GlowValue;

        private const int KindNone = 0;
        private const int KindInstall = 1;
        private const int KindUninstall = 2;

        private static int pendingKind;
        private static int pendingInvIndex;
        private static int pendingSlot;
        private static int pendingVictorWho = -1;
        private static bool applied;
        private static bool awaitingAuthority;
        private static uint operationSerial;
        private static int reopenSlot = -1;

        public static bool Busy => Active || awaitingAuthority;

        #region 对外触发

        /// <summary>安装/更换，invIndex→slot</summary>
        public static void BeginInstall(int invIndex, int slot) {
            if (Busy) {
                return;
            }
            pendingKind = KindInstall;
            pendingInvIndex = invIndex;
            pendingSlot = slot;
            Start();
        }

        /// <summary>卸载 slot</summary>
        public static void BeginUninstall(int slot) {
            if (Busy) {
                return;
            }
            pendingKind = KindUninstall;
            pendingSlot = slot;
            Start();
        }

        private static void Start() {
            applied = false;
            awaitingAuthority = false;
            pendingVictorWho = VictorSession.BoundWhoAmI;
            reopenSlot = pendingSlot;

            int who = pendingVictorWho;
            bool victorOk = who >= 0 && who < Main.maxNPCs && Main.npc[who].active
                && Main.npc[who].type == ModContent.NPCType<Victor>();

            if (victorOk && CutsceneDirector.Play<VictorSurgeryCutscene, int>(who)) {
                VictorClinicUI.Instance.CloseSilent();
                VictorTalkUI.Instance.Close();
                EyelidValue = 0f;
                EyelidTarget = 0f;
                GlowValue = 0f;
                Active = true;
            }
            else {
                ApplyPendingOp();
                if (!awaitingAuthority && pendingKind != KindNone) {
                    pendingKind = KindNone;
                    VictorClinicUI.Instance.OpenAtSlot(reopenSlot);
                }
            }
        }

        #endregion

        #region 过场回调

        /// <summary>过场全黑关键帧提交请求</summary>
        public static void ApplyPendingOp() {
            if (applied || pendingKind == KindNone) {
                return;
            }
            applied = true;

            Player player = Main.LocalPlayer;
            uint serial = ++operationSerial;
            awaitingAuthority = true;
            VictorRequestKind kind = pendingKind == KindInstall
                ? VictorRequestKind.Install
                : VictorRequestKind.Uninstall;
            int inventorySlot = kind == VictorRequestKind.Install
                ? pendingInvIndex
                : -1;
            bool sent = CyberwareNet.SendSurgeryRequest(player,
                pendingVictorWho, kind, inventorySlot, pendingSlot,
                result => HandleAuthorityResult(serial, result));
            if (!sent) {
                awaitingAuthority = false;
                HandleAuthorityResult(serial, default);
            }
        }

        private static void HandleAuthorityResult(uint serial,
            VictorRequestResult result) {
            if (serial != operationSerial) {
                return;
            }
            awaitingAuthority = false;
            bool success = result.IsSuccess;
            if (success && !VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.ChipSet, Main.LocalPlayer.Center);
                //下次开对话时优先吐一句术后台词
                VictorDialogue.NoteSurgeryDone();
            }
            if (!Active && pendingKind != KindNone) {
                pendingKind = KindNone;
                VictorClinicUI.Instance.OpenAtSlot(reopenSlot);
                reopenSlot = -1;
            }
            else if (success && !Active) {
                pendingKind = KindNone;
            }
        }

        private static void Finish() {
            Active = false;
            pendingKind = KindNone;
            EyelidTarget = 0f;
            if (reopenSlot >= 0) {
                VictorClinicUI.Instance.OpenAtSlot(reopenSlot);
            }
            reopenSlot = -1;
        }

        #endregion

        #region 每帧推进 / 绘制

        public override void PostUpdateEverything() {
            if (VaultUtils.isServer) {
                return;
            }

            EyelidValue = MathHelper.Lerp(EyelidValue, EyelidTarget, 0.16f);
            if (EyelidTarget <= 0.001f && EyelidValue < 0.004f) {
                EyelidValue = 0f;
            }
            GlowValue = MathHelper.Clamp(GlowValue - 0.02f, 0f, 1f);

            if (Active) {
                Player p = Main.LocalPlayer;
                if (p != null && p.active) {
                    //immune + immuneNoBlink 防猝死
                    p.immune = true;
                    p.immuneNoBlink = true;
                    if (p.immuneTime < 2) {
                        p.immuneTime = 2;
                    }
                }
                //过场结束或打断→Finish 重开诊所
                if (!CutsceneDirector.IsPlaying) {
                    Finish();
                }
            }

            //UI/手术都结束→Clear VictorSession
            if (!VictorSession.IsUIActive && !Busy && VictorSession.BoundWhoAmI != -1) {
                VictorSession.Clear();
            }
        }

        public override void OnWorldUnload() {
            Active = false;
            pendingKind = KindNone;
            applied = false;
            awaitingAuthority = false;
            pendingVictorWho = -1;
            operationSerial++;
            reopenSlot = -1;
            EyelidValue = EyelidTarget = GlowValue = 0f;
            VictorSession.Clear();
            VictorDialogue.ResetSession();
            VictorMood.Invalidate();
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (EyelidValue <= 0.001f && GlowValue <= 0.001f) {
                return;
            }
            //Interface 层最末，盖住世界+UI+指针
            layers.Add(new LegacyGameInterfaceLayer(
                "CWRMod: Victor Surgery Eyelid",
                delegate {
                    DrawEyelid(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.UI));
        }

        private static void DrawEyelid(SpriteBatch sb) {
            GraphicsDevice gd = sb.GraphicsDevice;
            //Viewport 尺寸全屏覆盖；screenWidth 随变焦缩小，UI 层铺整窗
            int w = gd.Viewport.Width;
            int h = gd.Viewport.Height;
            float close = MathHelper.Clamp(EyelidValue, 0f, 1f);
            float glow = MathHelper.Clamp(GlowValue, 0f, 1f);

            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) {
                return;
            }
            Effect shader = EffectLoader.VictorEyelidTransition?.Value;

            //像素矩阵 Matrix.Identity 铺后备缓冲
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            if (shader != null) {
                shader.Parameters["uClose"]?.SetValue(close);
                shader.Parameters["uGlow"]?.SetValue(glow);
                shader.Parameters["uTime"]?.SetValue((float)Main.GameUpdateCount / 60f);
                shader.Parameters["uResolution"]?.SetValue(new Vector2(w, h));
                shader.CurrentTechnique.Passes[0].Apply();
                sb.Draw(px, new Rectangle(0, 0, w, h), Color.White);
            }
            else {
                //无着色器上下黑幕
                int lid = (int)(close * (h * 0.5f + 2f));
                sb.Draw(px, new Rectangle(0, 0, w, lid), Color.Black);
                sb.Draw(px, new Rectangle(0, h - lid, w, lid), Color.Black);
            }

            sb.End();

            //SURGERY 提示同像素矩阵居中
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            if (close > 0.9f) {
                string txt = SurgeryInProgressText.Value;
                const float scale = 1f;
                Vector2 size = FontAssets.MouseText.Value.MeasureString(txt) * scale;
                float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GameUpdateCount * 0.12);
                Color c = new Color(255, 70, 70) * (0.55f + 0.45f * pulse);
                Utils.DrawBorderString(sb, txt, new Vector2(w / 2f - size.X / 2f, h * 0.6f), c, scale);
                sb.Draw(px, new Rectangle((int)(w / 2f - 170), (int)(h * 0.6f + size.Y + 8), 340, 2),
                    new Color(255, 70, 70) * (pulse * 0.5f));
            }
            sb.End();

            //还原 UIScaleMatrix 给后续层/鼠标
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        #endregion
    }
}
