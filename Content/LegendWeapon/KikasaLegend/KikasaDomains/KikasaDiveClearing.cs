using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>
    /// 沉湖清圈与潜水视角：本机玩家在观看域的湖里确认没顶后，湖水先在人周围让出一口清水
    /// （圈内倒影/血染/墨雾退去，可见度回到水面上的口径），随后"眼睛适应了这片水"：
    /// 第二包络 <see cref="Clarity"/> 起爬，清圈半径外扩成一道扫向整湖的清明前沿、圈强度同步让位，
    /// 整湖按 uDive 淡化（倒影退去、墨雾变薄、血水转成透光的血光）。站在湖面或半身入水两者都不出。
    /// 纯本机表现量：只看本机玩家与 <see cref="KikasaDomain.Viewed"/>，无网络；
    /// 着色器侧见 KikasaGrade.fx 的 uClearRing / uDive
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

        /// <summary>出水合回的总帧数（满值起步）：清圈强度与整湖淡化同走一条 S 曲线（慢起慢收）从出水时的值回零。
        /// 旧版 9~10 帧线性快收实机判"略快、突兀"（2026-09-09），改为半秒余的水合回来</summary>
        private const int ExitFrames = 32;

        /// <summary>刚沾水就出来（包络还很小）时的合回帧数下限：小值不拖半秒长尾</summary>
        private const int ExitFramesMin = 10;

        //==================== 潜水视角包络 ====================

        /// <summary>清圈张到这么开之后再等一段，眼睛才开始适应</summary>
        private const float ClarityArmStrength = 0.9f;

        /// <summary>清圈张开后到整湖开始淡化的延迟帧数</summary>
        private const int ClarityDelayFrames = 8;

        /// <summary>整湖淡化的指数逼近速率：约 55 帧到 90%，与"眼睛适应"的体感同量级</summary>
        private const float ClarityRate = 0.04f;

        /// <summary>清圈半径随 Clarity 外扩的倍数：圈缘那一线血沫水膜就是清明扫过整湖的前沿</summary>
        private const float FrontGrow = 5f;

        /// <summary>潜水视角期水下环境血泡的生成间隔（帧）</summary>
        private const int AmbientBubbleMin = 10;
        private const int AmbientBubbleMax = 16;

        private static int submergedFrames;
        private static bool confirmed;
        private static int clarityDelay;
        private static int ambientBubbleTimer;

        //出水合回：出水一帧锁下当时的两包络值与前沿位置，之后按 S 曲线回零。
        //前沿位置冻结是关键：若让半径继续跟着 Clarity 缩，整湖清明退场时圈会从屏外一路冲回人身，
        //读成"可视圈猛地收拢"；冻住后整湖只是均匀合回血镜，圈缘不出场
        private static bool exiting;
        private static int exitTimer;
        private static int exitFrames;
        private static float exitStrength0;
        private static float exitClarity0;

        /// <summary>清圈在场强度 0~1，同时驱动半径与清水量：圈从人身中心张开、合回</summary>
        public static float Strength { get; private set; }

        /// <summary>潜水视角 0~1：整湖淡化程度，着色器 uDive；清圈开满后延迟起爬，出水按 S 曲线合回</summary>
        public static float Clarity { get; private set; }

        /// <summary>圈心世界坐标（本机玩家中心），绘制时按当前相机投影</summary>
        public static Vector2 CenterWorld { get; private set; }

        /// <summary>驱动清圈半径外扩与让位的那份 Clarity：入水期就是 Clarity，出水期冻结在出水一帧的值</summary>
        private static float FrontClarity => exiting ? exitClarity0 : Clarity;

        public static void Clear() {
            submergedFrames = 0;
            confirmed = false;
            clarityDelay = 0;
            ambientBubbleTimer = 0;
            exiting = false;
            exitTimer = 0;
            exitFrames = ExitFrames;
            exitStrength0 = 0f;
            exitClarity0 = 0f;
            Strength = 0f;
            Clarity = 0f;
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
                //合回半途又沉回去：从当前值继续张开，不跳变
                exiting = false;
                CenterWorld = player.Center;
                Strength += (1f - Strength) * OpenRate;
                //清圈张开后再等一拍，眼睛才开始适应这片水：整湖淡化起爬
                if (Strength >= ClarityArmStrength) {
                    if (clarityDelay < ClarityDelayFrames) {
                        clarityDelay++;
                    }
                    else {
                        Clarity += (1f - Clarity) * ClarityRate;
                    }
                }
            }
            else {
                UpdateExit();
                clarityDelay = 0;
                //收拢期圈心继续跟人，圈不会钉在原地
                if (Strength > 0f && player?.active == true) {
                    CenterWorld = player.Center;
                }
            }

            //潜水视角期水下静场要有东西在动才像在水里：屏幕内随机点缓升的小血泡
            if (Clarity > 0.5f && kdp != null && player?.active == true) {
                AmbientBubbles(kdp);
            }
            else {
                ambientBubbleTimer = 0;
            }
        }

        //出水合回：两包络沿同一条 smoothstep 从出水时的值回零，慢起慢收，
        //既不是旧版的线性快收，也不是淡出（水是合回来的，只是合得从容）

        private static void UpdateExit() {
            if (Strength <= 0f && Clarity <= 0f) {
                exiting = false;
                return;
            }
            if (!exiting) {
                exiting = true;
                exitTimer = 0;
                exitStrength0 = Strength;
                exitClarity0 = Clarity;
                //合回时长随出水时的包络量缩放：满值半秒余，刚沾水就出来只走十来帧
                exitFrames = (int)MathHelper.Lerp(ExitFramesMin, ExitFrames,
                    MathHelper.Clamp(MathF.Max(exitStrength0, exitClarity0), 0f, 1f));
            }
            exitTimer++;
            float s = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(exitTimer / (float)exitFrames, 0f, 1f));
            float keep = 1f - s;
            Strength = exitStrength0 * keep;
            Clarity = exitClarity0 * keep;
            if (exitTimer >= exitFrames) {
                Strength = 0f;
                Clarity = 0f;
                exiting = false;
            }
        }

        /// <summary>
        /// 着色器 uClearRing 打包：xy=圈心 uv，z=半径像素（≥1），w=强度；闲置 w=0。
        /// 潜水视角起爬后半径按 Clarity 外扩成前沿、强度同步让位，Clarity 到 1 时圈完全消失只剩整湖淡化；
        /// 出水期前沿位置与让位量冻结在出水一帧（<see cref="FrontClarity"/>），圈不会从屏外冲回人身；
        /// uDive 即 Clarity
        /// </summary>
        internal static void FillUniforms(Effect effect, Vector2 viewSize) {
            effect.Parameters["uDive"]?.SetValue(Clarity);
            if (Strength <= 0.002f) {
                effect.Parameters["uClearRing"]?.SetValue(new Vector4(0.5f, 0.5f, 1f, 0f));
                return;
            }
            Vector2 centerUv = Vector2.Transform(
                CenterWorld - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix) / viewSize;
            float eased = 1f - MathF.Pow(1f - Strength, 2f);
            float front = FrontClarity;
            float radiusPx = MathF.Max(
                RadiusPx * Main.GameViewMatrix.Zoom.X * eased * (1f + FrontGrow * front), 1f);
            effect.Parameters["uClearRing"]?.SetValue(new Vector4(
                centerUv.X, centerUv.Y, radiusPx, Strength * (1f - front)));
        }

        //屏幕内水线下随机点生一颗小血泡缓升，节流间隔随机；只在潜水视角成立时跑

        private static void AmbientBubbles(KikasaDomainPlayer kdp) {
            if (--ambientBubbleTimer > 0) {
                return;
            }
            ambientBubbleTimer = Main.rand.Next(AmbientBubbleMin, AmbientBubbleMax + 1);
            float lakeY = kdp.LakeWorldY;
            float top = MathF.Max(Main.screenPosition.Y, lakeY + 24f);
            float bottom = Main.screenPosition.Y + Main.screenHeight;
            if (bottom <= top) {
                return;
            }
            Vector2 at = new(
                Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth),
                Main.rand.NextFloat(top, bottom));
            PRTLoader.NewParticle<PRT_KikasaLakeBubble>(at,
                new Vector2(Main.rand.NextFloat(-0.15f, 0.15f), -Main.rand.NextFloat(0.25f, 0.55f)), default,
                Main.rand.NextFloat(0.3f, 0.6f))?.Configure(Main.rand.Next(45, 90), lakeY);
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
