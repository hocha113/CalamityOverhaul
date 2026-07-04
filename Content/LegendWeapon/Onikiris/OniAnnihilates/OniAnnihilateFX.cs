using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniAnnihilates
{
    /// <summary>
    /// 鬼哭·灭世一闪屏幕级演出状态（仅客户端）：径向模糊 + 爆发全屏白闪 —— 本招的专属签名。<br/>
    /// 模糊：蓄力末段微量爬入（空间向极点塌陷的暗示），爆发后由巨斩持续推送十余帧再回落；<br/>
    /// 白闪：爆发帧一次推高、指数速落的整屏暖白覆盖（绯红裂空的冲击光刻意防整屏白爆，
    /// 大招的"白屏一瞬"由本类自己画）。<br/>
    /// 弹幕侧每帧 Push 推高目标值，渲染端 <see cref="Update"/> 自然衰减 —— 弹幕消失后画面自动回落。<br/>
    /// 压暗/负片复用 <see cref="OniFinaleSlashs.OniFinaleFX"/>，Bloom/冲击拉丝复用
    /// <see cref="CrimsonRendSlashs.CrimsonImpactFX"/>
    /// </summary>
    internal static class OniAnnihilateFX
    {
        /// <summary>径向模糊强度（shader 直入值，尖峰 ~0.22）</summary>
        public static float Blur { get; private set; }
        /// <summary>爆发全屏白闪 0..1，触发后指数速落</summary>
        public static float WhiteFlash { get; private set; }
        /// <summary>模糊中心（世界坐标）</summary>
        public static Vector2 BlurCenterWorld { get; private set; }

        public static bool HasAny => Blur > 0.0035f || WhiteFlash > 0.012f;

        /// <summary>演出焦点离本地视野中心过远时忽略推送，多人下远处玩家不承受全屏后效</summary>
        private static bool FocusNearLocalView(Vector2 focusWorld) {
            if (VaultUtils.isServer) {
                return false;
            }
            Vector2 viewCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            return Vector2.Distance(viewCenter, focusWorld) < 2800f;
        }

        /// <summary>推高模糊（蓄力爬入期每帧调用，爆发后由巨斩逐帧续推）</summary>
        public static void PushBlur(Vector2 focusWorld, float strength) {
            if (!FocusNearLocalView(focusWorld)) {
                return;
            }
            BlurCenterWorld = focusWorld;
            Blur = MathHelper.Clamp(MathF.Max(Blur, strength), 0f, 0.30f);
        }

        /// <summary>爆发白屏一瞬，一次触发自行速落</summary>
        public static void PushWhiteFlash(Vector2 focusWorld, float strength) {
            if (!FocusNearLocalView(focusWorld)) {
                return;
            }
            WhiteFlash = MathHelper.Clamp(MathF.Max(WhiteFlash, strength), 0f, 1f);
        }

        /// <summary>渲染端每帧衰减（由 <see cref="OniAnnihilateRender"/> 驱动）</summary>
        public static void Update() {
            Blur *= 0.86f;
            if (Blur < 0.0035f) {
                Blur = 0f;
            }
            WhiteFlash *= 0.72f;
            if (WhiteFlash < 0.012f) {
                WhiteFlash = 0f;
            }
        }

        /// <summary>世界切换/卸载兜底清空</summary>
        public static void Clear() => Blur = WhiteFlash = 0f;
    }

    /// <summary>世界卸载时清空屏幕演出状态</summary>
    internal sealed class OniAnnihilateFXSystem : ModSystem
    {
        public override void OnWorldUnload() => OniAnnihilateFX.Clear();
    }

    /// <summary>
    /// 鬼哭·灭世一闪全屏后效：径向模糊（<see cref="EffectLoader.RadialBlur"/>）+
    /// 爆发整屏暖白覆盖，单次 screenTarget ping-pong 内完成
    /// </summary>
    internal sealed class OniAnnihilateRender : RenderHandle
    {
        /// <summary>权重 1.11：晚于 OnikiriImpactRender(1.10) —— Bloom 辉光也被一起
        /// 径向拉丝，能量向极点塌陷的观感更完整</summary>
        public override float Weight => 1.11f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            OniAnnihilateFX.Update();

            if (!OniAnnihilateFX.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }

            Effect blurFx = EffectLoader.RadialBlur?.Value;
            bool doBlur = OniAnnihilateFX.Blur > 0.0035f && blurFx != null;
            if (doBlur) {
                blurFx.Parameters["center"]?.SetValue(WorldToScreenUV(OniAnnihilateFX.BlurCenterWorld));
                blurFx.Parameters["strength"]?.SetValue(OniAnnihilateFX.Blur);
            }

            //拷屏到 screenSwap（模糊在此 pass 完成）——screenTarget 是 DiscardContents，
            //重绑定即丢弃原画面，白闪覆盖也必须走这趟全帧往返
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            if (doBlur) {
                sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
                blurFx.CurrentTechnique.Passes[0].Apply();
            }
            else {
                sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            }
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            //写回 screenTarget，再叠爆发白屏（暖白加色，速落）
            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();

            if (OniAnnihilateFX.WhiteFlash > 0.012f && CWRAsset.Placeholder_White?.Value is Texture2D white) {
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive);
                sb.Draw(white, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight)
                    , new Color(255, 240, 222) * OniAnnihilateFX.WhiteFlash);
                sb.End();
            }
        }

        /// <summary>世界坐标 → 归一化 uv（含 GameViewMatrix.Zoom）</summary>
        private static Vector2 WorldToScreenUV(Vector2 worldPos) {
            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) {
                zoom.X = 1f;
            }
            if (zoom.Y <= 0f) {
                zoom.Y = 1f;
            }
            Vector2 screenCenterPx = new(screenW * 0.5f, screenH * 0.5f);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenterPx;
            Vector2 screenPx = screenCenterPx + (worldPos - viewWorldCenter) * zoom;
            return new Vector2(screenPx.X / screenW, screenPx.Y / screenH);
        }
    }
}
