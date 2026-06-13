using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Cyberwares.Victors.UIs;
using InnoVault.Cinematics;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Cyberwares.Victors
{
    /// <summary>
    /// 义体"手术"流程控制器：把诊所的安装/卸载从即时改为一段过场。
    /// <br/>诊所主动关闭 → InnoVault 过场聚焦 Victor 与玩家中点并拉近 → 眼睑合拢渐黑（全黑瞬间真正换装）→ 睁眼手术灯 → 收尾重开诊所。
    /// <br/>本类同时是驱动眼睑全屏覆盖与每帧推进的 <see cref="ModSystem"/>
    /// </summary>
    internal class VictorSurgery : ModSystem
    {
        /// <summary>手术过场是否进行中</summary>
        public static bool Active { get; private set; }

        /// <summary>眼睑闭合当前值 0(睁)→1(全黑)，由 <see cref="EyelidTarget"/> 平滑逼近</summary>
        public static float EyelidValue;
        /// <summary>眼睑闭合目标值</summary>
        public static float EyelidTarget;
        /// <summary>睁眼瞬间手术灯眩光强度 0~1，逐帧衰减</summary>
        public static float GlowValue;

        private const int KindNone = 0;
        private const int KindInstall = 1;
        private const int KindUninstall = 2;

        private static int pendingKind;
        private static int pendingInvIndex;
        private static int pendingSlot;
        private static bool applied;
        private static int reopenSlot = -1;

        #region 对外触发

        /// <summary>请求一台"安装/更换"手术：把背包 <paramref name="invIndex"/> 的义体装入 <paramref name="slot"/></summary>
        public static void BeginInstall(int invIndex, int slot) {
            if (Active) {
                return;
            }
            pendingKind = KindInstall;
            pendingInvIndex = invIndex;
            pendingSlot = slot;
            Start();
        }

        /// <summary>请求一台"卸载"手术：取下 <paramref name="slot"/> 的义体</summary>
        public static void BeginUninstall(int slot) {
            if (Active) {
                return;
            }
            pendingKind = KindUninstall;
            pendingSlot = slot;
            Start();
        }

        private static void Start() {
            applied = false;
            reopenSlot = pendingSlot;

            int who = VictorSession.BoundWhoAmI;
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
                //无法播放过场（如 Victor 失效）时即时执行，保证功能不失效
                ApplyPendingOp();
                pendingKind = KindNone;
                VictorClinicUI.Instance.OpenAtSlot(reopenSlot);
            }
        }

        #endregion

        #region 过场回调

        /// <summary>在过场全黑关键帧由时间轴调用：真正执行义体换装</summary>
        public static void ApplyPendingOp() {
            if (applied || pendingKind == KindNone) {
                return;
            }
            applied = true;

            Player p = Main.LocalPlayer;
            CyberwarePlayer cp = p.GetModPlayer<CyberwarePlayer>();

            if (pendingKind == KindInstall) {
                Item item = pendingInvIndex >= 0 && pendingInvIndex < p.inventory.Length ? p.inventory[pendingInvIndex] : null;
                if (item != null && !item.IsAir && cp.CanEquip(item, pendingSlot)) {
                    Item old = cp.Unequip(pendingSlot);
                    if (old != null && !old.IsAir) {
                        p.QuickSpawnItem(p.GetSource_Misc("CyberwareUnequip"), old, old.stack);
                    }
                    cp.Equip(item, pendingSlot);
                    item.TurnToAir();
                }
            }
            else if (pendingKind == KindUninstall) {
                Item old = cp.Unequip(pendingSlot);
                if (old != null && !old.IsAir) {
                    p.QuickSpawnItem(p.GetSource_Misc("CyberwareUnequip"), old, old.stack);
                }
            }

            if (!VaultUtils.isServer) {
                //义体芯片植入音（与 RAM 升级芯片同源），契合赛博手术
                SoundEngine.PlaySound(CWRSound.ChipSet, p.Center);
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
                    //手术中防止被打死 + 不闪烁
                    p.immune = true;
                    p.immuneNoBlink = true;
                    if (p.immuneTime < 2) {
                        p.immuneTime = 2;
                    }
                }
                //过场自然结束或被打断 → 收尾并重开诊所
                if (!CutsceneDirector.IsPlaying) {
                    Finish();
                }
            }

            //会话清理：界面与手术都结束后解除 Victor 绑定
            if (!VictorSession.IsUIActive && !Active && VictorSession.BoundWhoAmI != -1) {
                VictorSession.Clear();
            }
        }

        public override void OnWorldUnload() {
            Active = false;
            pendingKind = KindNone;
            applied = false;
            reopenSlot = -1;
            EyelidValue = EyelidTarget = GlowValue = 0f;
            VictorSession.Clear();
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (EyelidValue <= 0.001f && GlowValue <= 0.001f) {
                return;
            }
            //放到最末尾 → 盖住一切（世界 + UI + 指针）
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
            //用真实后备缓冲尺寸：相机拉近时 Main.screenWidth 会随之缩小，但界面层绘制在整个窗口上，
            //必须以 Viewport 为准才能全屏覆盖（这是之前只盖住左上角的根因）
            int w = gd.Viewport.Width;
            int h = gd.Viewport.Height;
            float close = MathHelper.Clamp(EyelidValue, 0f, 1f);
            float glow = MathHelper.Clamp(GlowValue, 0f, 1f);

            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) {
                return;
            }
            Effect shader = EffectLoader.VictorEyelidTransition?.Value;

            //以像素空间（单位矩阵）接管，精确覆盖整个后备缓冲
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
                //着色器缺失时的纯黑回退：上下两道黑幕
                int lid = (int)(close * (h * 0.5f + 2f));
                sb.Draw(px, new Rectangle(0, 0, w, lid), Color.Black);
                sb.Draw(px, new Rectangle(0, h - lid, w, lid), Color.Black);
            }

            sb.End();

            //提示文字：同样在像素空间（单位矩阵）绘制，保证基于真实屏幕居中
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            if (close > 0.9f) {
                const string txt = "SURGERY IN PROGRESS";
                const float scale = 1f;
                Vector2 size = FontAssets.MouseText.Value.MeasureString(txt) * scale;
                float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GameUpdateCount * 0.12);
                Color c = new Color(255, 70, 70) * (0.55f + 0.45f * pulse);
                Utils.DrawBorderString(sb, txt, new Vector2(w / 2f - size.X / 2f, h * 0.6f), c, scale);
                sb.Draw(px, new Rectangle((int)(w / 2f - 170), (int)(h * 0.6f + size.Y + 8), 340, 2),
                    new Color(255, 70, 70) * (pulse * 0.5f));
            }
            sb.End();

            //还原到界面层默认状态，匹配后续层 / 鼠标绘制约定
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        #endregion
    }
}
