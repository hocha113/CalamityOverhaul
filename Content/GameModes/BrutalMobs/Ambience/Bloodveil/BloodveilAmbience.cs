using CalamityOverhaul.Common;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Bloodveil
{
    /// <summary>
    /// 「血帷」残酷模式血月氛围中枢（纯客户端演出，纯视觉声景）。
    /// 分工声明：血月的战斗层（潮汐军团机制）归 Legion 包（LegionNPC），
    /// 本包不做任何伤害、减益或生成，只有三个具名声景：
    /// 「赤雾」低空红雾漂带（<see cref="PRT_BloodveilMist"/>，幅度镜像 Woodsong 暮雾的克制级）；
    /// 「远嚎与心跳」远处兽嚎与无方位心跳双拍的低频定时器；
    /// 「猩红月色」夜色滤镜向猩红微调（幅度氛围级）。
    /// 档位只调雾密度；装饰粒子与声景吃 <see cref="CWRClientConfig.AmbienceDensity"/> 总闸
    /// </summary>
    internal static class BloodveilAmbience
    {
        /// <summary>本地在场强度 0~1（血月起落缓变，不硬切）</summary>
        public static float Presence { get; private set; }

        //==== 档位表：机制形状不变，只调密度（镜像 Woodsong 的 ByTier 写法）====
        /// <summary>红雾每秒生成预算，档位只调密度</summary>
        private static readonly float[] MistPerSecByTier = [3.0f, 4.0f, 5.0f];

        //==== 声景定时参数 ====
        /// <summary>心跳双拍的第二拍延迟（帧）</summary>
        private const int HeartSecondBeatDelay = 11;

        private static float mistAcc;
        private static int howlIn = 1500;
        private static int heartIn = 1100;
        private static int heartSecondIn;

        internal static void Reset() {
            Presence = 0f;
            mistAcc = 0f;
            howlIn = 1500;
            heartIn = 1100;
            heartSecondIn = 0;
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
                && Main.bloodMoon && player.ZoneOverworldHeight;
            //Boss 在场：纯视觉氛围保留但减弱（镜像 Woodsong）
            float target = inZone ? (CWRWorld.HasBoss ? 0.3f : 1f) : 0f;
            Presence = Math.Abs(target - Presence) < 0.004f
                ? target : MathHelper.Lerp(Presence, target, 0.03f);

            if (Presence <= 0.02f) {
                return;
            }
            SpawnMistBands(player);
            UpdateSoundscape(player);
        }

        //==================== 「赤雾」低空红雾漂带 ====================

        private static void SpawnMistBands(Player player) {
            int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);
            //氛围性能总闸：只缩装饰红雾密度，无任何机制路径经过此处
            float density = CWRClientConfig.Instance.AmbienceDensity;
            mistAcc += MistPerSecByTier[tier - 1] / 60f * Presence * density;
            while (mistAcc >= 1f) {
                mistAcc -= 1f;
                SpawnOneMist(player);
            }
        }

        private static void SpawnOneMist(Player player) {
            int tileX = (int)(player.Center.X / 16f) + Main.rand.Next(-46, 47);
            if (!TryFindOutdoorSurface(tileX, out int surfY)) {
                return;
            }
            //贴地低空：地表上方 4~46 像素的漂带层
            Vector2 pos = new(tileX * 16f + Main.rand.NextFloat(16f),
                surfY * 16f - Main.rand.NextFloat(4f, 46f));
            PRTLoader.NewParticle<PRT_BloodveilMist>(pos,
                new Vector2(Main.windSpeedCurrent * 0.5f + Main.rand.NextFloat(-0.12f, 0.12f),
                    Main.rand.NextFloat(-0.03f, 0.02f)),
                new Color(118, 26, 32) * Main.rand.NextFloat(0.40f, 0.55f),
                Main.rand.NextFloat(0.9f, 1.6f))
                ?.Configure(Main.rand.Next(300, 480));
        }

        //==================== 「远嚎与心跳」====================

        private static void UpdateSoundscape(Player player) {
            //声景密度同吃总闸：密度越低，定时器拉得越长
            float density = Math.Max(CWRClientConfig.Instance.AmbienceDensity, 0.25f);

            //心跳双拍：无方位低频闷响（咚-咚），血月压在心口的听感
            if (heartSecondIn > 0 && --heartSecondIn == 0) {
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.24f, Pitch = -0.95f, MaxInstances = 2
                });
            }
            if (--heartIn <= 0) {
                heartIn = (int)(Main.rand.Next(900, 1600) / density);
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.30f, Pitch = -0.85f, MaxInstances = 2
                });
                heartSecondIn = HeartSecondBeatDelay;
            }

            //远处兽嚎：远侧低吟，偶发一声更远的闷吼
            if (--howlIn <= 0) {
                howlIn = (int)(Main.rand.Next(1200, 2600) / density);
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 pos = player.Center + new Vector2(
                    side * Main.rand.NextFloat(850f, 1350f), Main.rand.NextFloat(-220f, 40f));
                SoundEngine.PlaySound(SoundID.ZombieMoan with {
                    Volume = 0.30f, Pitch = -0.55f + Main.rand.NextFloat(0.12f), MaxInstances = 2
                }, pos);
                if (Main.rand.NextBool(4)) {
                    SoundEngine.PlaySound(SoundID.Roar with {
                        Volume = 0.18f, Pitch = -0.72f, MaxInstances = 2
                    }, pos + new Vector2(side * 320f, -60f));
                }
            }
        }

        //==================== 地形采样 ====================

        /// <summary>找露天地表：自玩家高度向下走到首个实心格；上方那格必须无墙、无深液体（镜像 Woodsong）</summary>
        private static bool TryFindOutdoorSurface(int tileX, out int surfaceY) {
            surfaceY = 0;
            if (tileX < 20 || tileX >= Main.maxTilesX - 20) {
                return false;
            }
            Player anchor = Main.LocalPlayer;
            int yStart = Math.Max((int)(anchor.Center.Y / 16f) - 64, 24);
            int yEnd = Math.Min((int)Main.worldSurface + 26, Main.maxTilesY - 20);
            for (int y = yStart; y < yEnd; y++) {
                if (!WorldGen.SolidTile(tileX, y)) {
                    continue;
                }
                Tile above = Framing.GetTileSafely(tileX, y - 1);
                if (above.WallType != WallID.None || above.LiquidAmount > 64) {
                    return false;
                }
                surfaceY = y;
                return true;
            }
            return false;
        }
    }

    internal class BloodveilAmbienceSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                BloodveilAmbience.Update();
            }
        }

        public override void ClearWorld() {
            if (!Main.dedServ) {
                BloodveilAmbience.Reset();
            }
        }

        //「猩红月色」：夜色向猩红勒去，幅度压在氛围级（不盖过原版血月自身的调色）
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float k = BloodveilAmbience.Presence;
            if (k <= 0.01f) {
                return;
            }
            Color bloodTile = new(96, 30, 34);
            Color bloodBg = new(70, 18, 26);
            tileColor = Color.Lerp(tileColor, bloodTile, k * 0.16f);
            backgroundColor = Color.Lerp(backgroundColor, bloodBg, k * 0.24f);
        }
    }
}
