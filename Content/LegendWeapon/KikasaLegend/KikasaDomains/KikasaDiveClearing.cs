using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>
    /// 沉湖清圈：本机玩家在观看域的湖里确认没顶后，湖水在人周围让出一口清水
    /// （圈内倒影/血染/墨雾退去，可见度回到水面上的口径），站在湖面或半身入水不出圈。
    /// 纯本机表现量：只看本机玩家与 <see cref="KikasaDomain.Viewed"/>，无网络；
    /// 着色器侧见 KikasaGrade.fx 的 uClearRing
    /// </summary>
    internal static class KikasaDiveClearing
    {
        /// <summary>头顶（碰撞盒最高点）没入水线多深才开始计确认（世界像素）；
        /// 站湖面时头顶在水线上方约 42px，跳跃落线的亚像素抖动够不到</summary>
        private const float EnterDepthPx = 14f;

        /// <summary>头顶回到水线下多浅算出水（迟滞，水线噪声/行波不会让圈闪断）</summary>
        private const float ExitDepthPx = 3f;

        /// <summary>没顶持续帧数达到后才算"完成沉入"，穿面下潜的一瞬不出圈</summary>
        private const int ConfirmFrames = 12;

        /// <summary>清圈满径（世界像素，随缩放换算到屏幕）；150 实机判偏小,2026-09-04 放到 185</summary>
        private const float RadiusPx = 185f;

        /// <summary>张开：指数逼近，约 18 帧到 97%</summary>
        private const float OpenRate = 0.18f;

        /// <summary>收拢：线性快收，约 9 帧合回中心（出水不是淡出，是水合回来）</summary>
        private const float CloseStep = 0.11f;

        private static int submergedFrames;
        private static bool confirmed;

        /// <summary>清圈在场强度 0~1，同时驱动半径与清水量：圈从人身中心张开、合回</summary>
        public static float Strength { get; private set; }

        /// <summary>圈心世界坐标（本机玩家中心），绘制时按当前相机投影</summary>
        public static Vector2 CenterWorld { get; private set; }

        public static void Clear() {
            submergedFrames = 0;
            confirmed = false;
            Strength = 0f;
        }

        public static void Update() {
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            Player player = Main.LocalPlayer;
            bool submerged = kdp != null && player?.active == true && IsSubmerged(kdp, player);

            if (submerged) {
                if (submergedFrames < ConfirmFrames) {
                    submergedFrames++;
                }
            }
            else {
                submergedFrames = 0;
            }

            bool target = submerged && submergedFrames >= ConfirmFrames;
            if (target && !confirmed) {
                //确认没顶的一拍：让水时挤出的一串小泡自圈内升起
                BurstBubbles(kdp, player);
            }
            confirmed = target;

            if (target) {
                CenterWorld = player.Center;
                Strength += (1f - Strength) * OpenRate;
            }
            else {
                Strength = MathF.Max(Strength - CloseStep, 0f);
                //收拢期圈心继续跟人，圈不会钉在原地
                if (Strength > 0f && player?.active == true) {
                    CenterWorld = player.Center;
                }
            }
        }

        /// <summary>着色器 uClearRing 打包：xy=圈心 uv，z=半径像素（≥1），w=强度；闲置 w=0</summary>
        internal static void FillUniforms(Effect effect, Vector2 viewSize) {
            if (Strength <= 0.002f) {
                effect.Parameters["uClearRing"]?.SetValue(new Vector4(0.5f, 0.5f, 1f, 0f));
                return;
            }
            Vector2 centerUv = Vector2.Transform(
                CenterWorld - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix) / viewSize;
            float eased = 1f - MathF.Pow(1f - Strength, 2f);
            float radiusPx = MathF.Max(RadiusPx * Main.GameViewMatrix.Zoom.X * eased, 1f);
            effect.Parameters["uClearRing"]?.SetValue(new Vector4(centerUv.X, centerUv.Y, radiusPx, Strength));
        }

        /// <summary>
        /// 没顶判定：碰撞盒最高点在观感水线之下（重力翻转时最高点在脚，仍是"整个人在水下"）。
        /// 水线以屏幕 uv 度量，与 KikasaGrade.SetSharedParams 同公式，涨水/退水期水线不在 LakeWorldY；
        /// 施术者本人脚下的让位坑把当地水线再压低一截。
        /// 翻转/鬼梦各有全屏演出，湖面镜面被接管或不存在，一律不出圈
        /// </summary>
        private static bool IsSubmerged(KikasaDomainPlayer kdp, Player player) {
            if (!kdp.AnyActive || kdp.Phase == KikasaDomainPhase.Flipping || kdp.InDreamPhase
                || kdp.RiseT < 0.05f || player.dead || player.ghost) {
                return false;
            }

            float screenH = Main.screenHeight;
            float zoomY = Main.GameViewMatrix.Zoom.Y;
            float pivotUv = WorldToScreen(new Vector2(Main.screenPosition.X, kdp.LakeWorldY)).Y / screenH;
            float waterUv = MathHelper.Lerp(1.15f, pivotUv, kdp.RiseProgress);
            if (ReferenceEquals(kdp.Player, player) && kdp.TideTroughDepthPx > 0f) {
                waterUv += kdp.TideTroughDepthPx * zoomY / screenH;
            }

            float headUv = WorldToScreen(player.TopLeft).Y / screenH;
            float depthPx = (headUv - waterUv) * screenH / zoomY;
            float threshold = submergedFrames > 0 || Strength > 0f ? ExitDepthPx : EnterDepthPx;
            return depthPx > threshold;
        }

        private static void BurstBubbles(KikasaDomainPlayer kdp, Player player) {
            int count = Main.rand.Next(6, 10);
            for (int i = 0; i < count; i++) {
                Vector2 at = player.Center + Main.rand.NextVector2Circular(RadiusPx * 0.55f, RadiusPx * 0.4f);
                PRTLoader.NewParticle<PRT_KikasaLakeBubble>(at,
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.2f, 0.5f)), default,
                    Main.rand.NextFloat(0.35f, 0.65f))?.Configure(Main.rand.Next(40, 75), kdp.LakeWorldY);
            }
        }

        private static Vector2 WorldToScreen(Vector2 worldPos)
            => Vector2.Transform(worldPos - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
    }
}
