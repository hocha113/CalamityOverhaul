using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rainveil
{
    /// <summary>
    /// 「雨帷」残酷模式降雨氛围中枢（纯客户端演出）。两个具名特色：
    /// 「雨幕加密」在原版雨之上叠一层近远景雨帘丝（密度随雨强 <see cref="Main.maxRaining"/>），
    /// 近玩家淡出保战斗可读；「风暴压顶」雷暴期（<see cref="Main.IsItStorming"/>）天色勒向
    /// 铅灰蓝（幅度镜像 Woodsong 暮雾的氛围级）。
    /// 危害（落雷）归 <see cref="RainveilStormSystem"/> 权威端；普通雨只有此处的纯氛围。
    /// 装饰粒子生成率吃 <see cref="CWRClientConfig.AmbienceDensity"/> 总闸
    /// </summary>
    internal static class RainveilAmbience
    {
        /// <summary>本地在场强度 0~1（雨起雨收缓变，不硬切）</summary>
        public static float Presence { get; private set; }

        /// <summary>风暴压迫强度 0~1（IsItStorming 平滑包络，天色与雨帘共读）</summary>
        public static float StormPressure { get; private set; }

        //==== 雨帘密度参数 ====
        /// <summary>雨帘丝每秒基准预算（满雨强时再乘雨强与风暴项）</summary>
        private const float CurtainPerSecBase = 8f;
        /// <summary>雨强项每秒增量上限</summary>
        private const float CurtainPerSecRain = 26f;
        /// <summary>风暴项每秒增量</summary>
        private const float CurtainPerSecStorm = 14f;

        private static float curtainAcc;

        internal static void Reset() {
            Presence = 0f;
            StormPressure = 0f;
            curtainAcc = 0f;
        }

        internal static void Update() {
            if (Main.gameMenu) {
                Presence = 0f;
                StormPressure = 0f;
                return;
            }
            if (Main.gamePaused) {
                return;
            }

            Player player = Main.LocalPlayer;
            bool inZone = player != null && player.active && GameModeSystem.BrutalActive
                && Main.raining && player.ZoneOverworldHeight;
            //Boss 在场：纯视觉氛围保留但减弱（镜像 Woodsong）
            float target = inZone ? (CWRWorld.HasBoss ? 0.3f : 1f) : 0f;
            Presence = Math.Abs(target - Presence) < 0.004f
                ? target : MathHelper.Lerp(Presence, target, 0.03f);

            //风暴包络：跟原版雷暴判定走，平滑避免硬跳
            float stormTarget = inZone && Main.IsItStorming ? 1f : 0f;
            StormPressure = Math.Abs(stormTarget - StormPressure) < 0.004f
                ? stormTarget : MathHelper.Lerp(StormPressure, stormTarget, 0.03f);

            if (Presence <= 0.02f) {
                return;
            }
            SpawnRainCurtain();
        }

        //==================== 「雨幕加密」雨帘丝 ====================

        private static void SpawnRainCurtain() {
            //氛围性能总闸：只缩装饰雨帘密度，不碰落雷预告与危害路径
            float density = CWRClientConfig.Instance.AmbienceDensity;
            float rain = MathHelper.Clamp(Main.maxRaining, 0f, 1f);
            float perSec = (CurtainPerSecBase + CurtainPerSecRain * rain
                + CurtainPerSecStorm * StormPressure) * Presence * density;
            curtainAcc += perSec / 60f;
            while (curtainAcc >= 1f) {
                curtainAcc -= 1f;
                SpawnCurtainDrop();
            }
        }

        private static void SpawnCurtainDrop() {
            //六成远幕（小而慢，读作远处雨墙），四成近幕
            float depth = Main.rand.NextFloat() < 0.6f ? Main.rand.NextFloat(0.5f, 0.8f) : 1f;
            float windX = Main.windSpeedCurrent * (9f + 4f * StormPressure) * depth;
            Vector2 pos = new(
                Main.screenPosition.X + Main.rand.NextFloat(-60f, Main.screenWidth + 60f),
                Main.screenPosition.Y - Main.rand.NextFloat(20f, 80f));
            PRTLoader.NewParticle<PRT_GhostRainDrop>(pos,
                new Vector2(windX, Main.rand.NextFloat(7f, 11f) * depth),
                new Color(126, 152, 192) * (0.32f + 0.20f * depth),
                Main.rand.NextFloat(0.55f, 0.95f) * depth)
                ?.Configure(Main.rand.Next(110, 170), windX).AsCurtain(depth);
        }
    }

    internal class RainveilAmbienceSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                RainveilAmbience.Update();
            }
        }

        public override void ClearWorld() {
            if (!Main.dedServ) {
                RainveilAmbience.Reset();
            }
        }

        //「风暴压顶」：雷暴期天色勒向铅灰蓝，幅度镜像 Woodsong 暮雾的氛围级（0.18/0.26）
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float k = RainveilAmbience.Presence * RainveilAmbience.StormPressure;
            if (k <= 0.01f) {
                return;
            }
            Color stormTile = new(58, 64, 84);
            Color stormBg = new(36, 42, 60);
            tileColor = Color.Lerp(tileColor, stormTile, k * 0.18f);
            backgroundColor = Color.Lerp(backgroundColor, stormBg, k * 0.26f);
        }
    }
}
