using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core
{
    /// <summary>
    /// 仪式帷幕屏效状态（各端本地推导，无网络）<br/>
    /// 状态每帧调用 <see cref="SetVeil"/> / <see cref="SetDim"/> / <see cref="SetWind"/> 声明目标；未声明则自然消散
    /// </summary>
    internal static class CultistScreenFX
    {
        /// <summary>帷幕当前强度 0~1，缓动逼近目标</summary>
        public static float VeilIntensity { get; private set; }
        /// <summary>帷幕目标强度，每帧自衰减</summary>
        private static float veilGoal;
        /// <summary>帷幕圆心（世界坐标）</summary>
        public static Vector2 VeilWorldCenter { get; private set; }
        /// <summary>元素染色</summary>
        public static Vector3 VeilTint { get; private set; } = new(1f, 0.7f, 0.35f);
        /// <summary>符环带半径（世界px）</summary>
        public static float BandRadiusPx { get; private set; } = 620f;
        /// <summary>白闪 0~1，快衰减</summary>
        public static float Flash { get; private set; }
        /// <summary>死亡去饱和 0~1</summary>
        public static float BreakDesat { get; set; }
        /// <summary>风暴涌激 0~1(星旋出场拉满),喂给分相天幕,缓衰减</summary>
        public static float StormSurge { get; set; }

        /// <summary>整屏压暗 0~1(全食的天黑),缓动逼近目标;与帷幕的外域压暗不同,这层均匀吃掉整屏亮度</summary>
        public static float Dim { get; private set; }
        private static float dimGoal;

        /// <summary>当前风速(px/帧,带符号,+X 向右):环境粒子按它漂,天幕按它流</summary>
        public static float Wind { get; private set; }
        /// <summary>风力目标(px/帧幅值),分相环境每帧声明,未声明自衰</summary>
        private static float windGoal;
        private static float windStrength;
        /// <summary>风位移积分(屏高归一),天幕云雾/极光帘随风漂移的相位</summary>
        public static float WindOffset { get; private set; }
        /// <summary>阵风包络 0~1,粒子生成密度跟它起伏</summary>
        public static float Gust { get; private set; }

        public static bool HasAny => VeilIntensity > 0.012f || Flash > 0.012f || BreakDesat > 0.012f || Dim > 0.012f;

        /// <summary>声明本帧帷幕目标（状态每帧调用保持）</summary>
        public static void SetVeil(float goal, Vector2 worldCenter, Color tint, float bandRadiusPx = 620f) {
            if (goal > veilGoal) {
                veilGoal = MathHelper.Clamp(goal, 0f, 1f);
            }
            VeilWorldCenter = worldCenter;
            VeilTint = tint.ToVector3();
            BandRadiusPx = bandRadiusPx;
        }

        /// <summary>声明本帧整屏压暗目标(每帧调用保持,取最大)</summary>
        public static void SetDim(float goal) {
            if (goal > dimGoal) {
                dimGoal = MathHelper.Clamp(goal, 0f, 1f);
            }
        }

        /// <summary>声明本帧风力幅值(px/帧),分相环境每帧调用保持</summary>
        public static void SetWind(float strength) {
            if (strength > windGoal) {
                windGoal = MathHelper.Max(strength, 0f);
            }
        }

        /// <summary>推白闪</summary>
        public static void PushFlash(float amount) {
            if (amount > Flash) {
                Flash = MathHelper.Clamp(amount, 0f, 1f);
            }
        }

        /// <summary>每帧推进：强度缓动，目标/白闪/去饱和自衰减</summary>
        public static void Update() {
            VeilIntensity = MathHelper.Lerp(VeilIntensity, veilGoal, 0.08f);
            veilGoal *= 0.93f;
            Dim = MathHelper.Lerp(Dim, dimGoal, 0.08f);
            dimGoal *= 0.93f;
            Flash *= 0.86f;
            StormSurge *= 0.988f;
            if (StormSurge < 0.004f) {
                StormSurge = 0f;
            }
            BreakDesat *= 0.975f;
            if (BreakDesat < 0.004f) {
                BreakDesat = 0f;
            }
            if (VeilIntensity < 0.004f) {
                VeilIntensity = 0f;
            }
            if (Dim < 0.004f) {
                Dim = 0f;
            }
            if (Flash < 0.004f) {
                Flash = 0f;
            }
            UpdateWind();
        }

        /// <summary>
        /// 风场:幅值缓动跟目标,方向由两支慢正弦叠出(约 40s 一换向,不会恒吹一边),<br/>
        /// 阵风=半周期尖脉冲,吹过时粒子密度与流线速度一起抬
        /// </summary>
        private static void UpdateWind() {
            windStrength = MathHelper.Lerp(windStrength, windGoal, 0.03f);
            windGoal *= 0.96f;
            if (windStrength < 0.01f) {
                windStrength = 0f;
                Wind = 0f;
                Gust = 0f;
                return;
            }
            float t = Main.GlobalTimeWrappedHourly;
            float pattern = MathF.Sin(t * 0.16f) * 0.7f + MathF.Sin(t * 0.41f + 1.3f) * 0.3f;
            float gustWave = MathF.Sin(t * 0.37f + 2.1f);
            Gust = gustWave > 0f ? MathF.Pow(gustWave, 6f) : 0f;
            Wind = windStrength * (pattern + MathF.Sign(pattern) * Gust * 0.8f);
            WindOffset += Wind / 1080f;
        }

        /// <summary>卸载/战斗结束清空</summary>
        public static void Clear() {
            VeilIntensity = 0f;
            veilGoal = 0f;
            Flash = 0f;
            BreakDesat = 0f;
            StormSurge = 0f;
            Dim = 0f;
            dimGoal = 0f;
            Wind = 0f;
            windGoal = 0f;
            windStrength = 0f;
            WindOffset = 0f;
            Gust = 0f;
        }
    }
}
