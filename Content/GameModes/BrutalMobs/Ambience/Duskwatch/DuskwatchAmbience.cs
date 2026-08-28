using CalamityOverhaul.Common;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Duskwatch
{
    /// <summary>
    /// 「昏卫」残酷模式日食氛围中枢（纯客户端演出，纯视觉声景）。
    /// 分工声明：日食的战斗层（日食怪机制）归 Eclipse 包（EclipseNPC/EclMothronNPC），
    /// 本包不做任何伤害、减益或生成，只有三个具名声景：
    /// 「日冕昏光」天色压暗偏琥珀（幅度镜像 Woodsong 暮雾的氛围级）；
    /// 「灰翳」飘散灰翳尘（<see cref="PRT_DuskwatchAsh"/>）；
    /// 「异响」恐怖片音景定时器（远尖啸与门板吱呀，原版 SoundID 低频错拍）。
    /// 档位只调灰翳密度；装饰粒子与声景吃 <see cref="CWRClientConfig.AmbienceDensity"/> 总闸
    /// </summary>
    internal static class DuskwatchAmbience
    {
        /// <summary>本地在场强度 0~1（日食起落缓变，不硬切）</summary>
        public static float Presence { get; private set; }

        //==== 档位表：机制形状不变，只调密度（镜像 Woodsong 的 ByTier 写法）====
        /// <summary>灰翳尘每秒生成预算，档位只调密度</summary>
        private static readonly float[] AshPerSecByTier = [6f, 8f, 10f];

        //==== 声景错拍参数 ====
        /// <summary>同帧防撞：尖啸响起后把吱呀推迟的帧数（错拍下限）</summary>
        private const int SoundStaggerGap = 60;

        private static float ashAcc;
        private static int screechIn = 1600;
        private static int creakIn = 1000;

        internal static void Reset() {
            Presence = 0f;
            ashAcc = 0f;
            screechIn = 1600;
            creakIn = 1000;
        }

        internal static void Update() {
            if (Main.gameMenu) {
                Presence = 0f;
                return;
            }
            if (Main.gamePaused) {
                return;
            }

            Player player = Main.LocalPlayer;
            bool inZone = player != null && player.active && GameModeSystem.BrutalActive
                && Main.eclipse && player.ZoneOverworldHeight;
            //Boss 在场：纯视觉氛围保留但减弱（镜像 Woodsong）
            float target = inZone ? (CWRWorld.HasBoss ? 0.3f : 1f) : 0f;
            Presence = Math.Abs(target - Presence) < 0.004f
                ? target : MathHelper.Lerp(Presence, target, 0.03f);

            if (Presence <= 0.02f) {
                return;
            }
            SpawnAshMotes();
            UpdateHorrorSoundscape(player);
        }

        //==================== 「灰翳」飘散灰翳尘 ====================

        private static void SpawnAshMotes() {
            int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);
            //氛围性能总闸：只缩装饰灰翳密度，无任何机制路径经过此处
            float density = CWRClientConfig.Instance.AmbienceDensity;
            ashAcc += AshPerSecByTier[tier - 1] / 60f * Presence * density;
            while (ashAcc >= 1f) {
                ashAcc -= 1f;
                SpawnOneAsh();
            }
        }

        private static void SpawnOneAsh() {
            Vector2 pos = Main.screenPosition + new Vector2(
                Main.rand.NextFloat(-60f, Main.screenWidth + 60f),
                Main.rand.NextFloat(-40f, Main.screenHeight * 0.8f));
            if (!AirAndOutdoor(pos)) {
                return;
            }
            //三成更暗的焦渣，七成暖灰细翳
            Color tint = Main.rand.NextBool(3)
                ? new Color(96, 86, 78) * 0.55f
                : new Color(152, 140, 126) * Main.rand.NextFloat(0.38f, 0.55f);
            PRTLoader.NewParticle<PRT_DuskwatchAsh>(pos,
                new Vector2(Main.windSpeedCurrent, Main.rand.NextFloat(0.2f, 0.6f)),
                tint, Main.rand.NextFloat(0.7f, 1.3f))
                ?.Configure(Main.rand.Next(180, 320));
        }

        private static bool AirAndOutdoor(Vector2 worldPos) {
            int tx = (int)(worldPos.X / 16f);
            int ty = (int)(worldPos.Y / 16f);
            if (tx < 20 || tx >= Main.maxTilesX - 20 || ty < 20 || ty >= Main.maxTilesY - 20) {
                return false;
            }
            Tile tile = Framing.GetTileSafely(tx, ty);
            if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                return false;
            }
            return tile.WallType == WallID.None && tile.LiquidAmount == 0;
        }

        //==================== 「异响」恐怖片音景 ====================

        private static void UpdateHorrorSoundscape(Player player) {
            //声景密度同吃总闸：密度越低，定时器拉得越长
            float density = Math.Max(CWRClientConfig.Instance.AmbienceDensity, 0.25f);

            //远尖啸：远侧高空一声拉长的嘶叫，日食片场的第一恐怖声部
            if (--screechIn <= 0) {
                screechIn = (int)(Main.rand.Next(1400, 3200) / density);
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 pos = player.Center + new Vector2(
                    side * Main.rand.NextFloat(900f, 1400f), -Main.rand.NextFloat(80f, 320f));
                SoundEngine.PlaySound(SoundID.NPCDeath2 with {
                    Volume = 0.22f,
                    Pitch = -0.45f + Main.rand.NextFloat(0.15f),
                    MaxInstances = 2
                }, pos);
                //错拍：尖啸落地后短时间内不许吱呀撞声
                if (creakIn < SoundStaggerGap) {
                    creakIn = SoundStaggerGap;
                }
            }

            //门板吱呀：中距一声木轴呻吟，来源无从查证才最瘆人
            if (--creakIn <= 0) {
                creakIn = (int)(Main.rand.Next(900, 2200) / density);
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 pos = player.Center + new Vector2(
                    side * Main.rand.NextFloat(300f, 700f), Main.rand.NextFloat(-60f, 30f));
                SoundStyle creak = Main.rand.NextBool()
                    ? SoundID.DoorOpen with { Volume = 0.26f, Pitch = -0.6f + Main.rand.NextFloat(0.1f), MaxInstances = 2 }
                    : SoundID.DoorClosed with { Volume = 0.24f, Pitch = -0.55f + Main.rand.NextFloat(0.1f), MaxInstances = 2 };
                SoundEngine.PlaySound(creak, pos);
            }
        }
    }

    internal class DuskwatchAmbienceSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                DuskwatchAmbience.Update();
            }
        }

        public override void ClearWorld() {
            if (!Main.dedServ) {
                DuskwatchAmbience.Reset();
            }
        }

        //「日冕昏光」：日色压暗偏琥珀，幅度镜像 Woodsong 暮雾的氛围级（0.18/0.26）
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float k = DuskwatchAmbience.Presence;
            if (k <= 0.01f) {
                return;
            }
            Color duskTile = new(128, 96, 54);
            Color duskBg = new(86, 62, 36);
            tileColor = Color.Lerp(tileColor, duskTile, k * 0.18f);
            backgroundColor = Color.Lerp(backgroundColor, duskBg, k * 0.26f);
        }
    }
}
