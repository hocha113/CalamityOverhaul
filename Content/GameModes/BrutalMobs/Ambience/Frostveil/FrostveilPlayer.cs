using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Frostveil
{
    /// <summary>
    /// 白毛风·暴露度：残酷模式地表雪原的逐玩家寒冷计量（暴露度累积原型）。
    /// 夜间或暴雪时暴露在天空下累积；反馈渐进：呼出白气 → 屏边霜花渐浓 → 心跳变缓；
    /// 满值短暂施加原版寒颤后回落。靠近火把/篝火/熔炉、进入室内或城镇即快速消退。
    /// 全部状态为实例字段，逐端各自推演（输入均为同步世界态），寒颤只由本机玩家施加
    /// </summary>
    internal class FrostveilPlayer : ModPlayer
    {
        //==== 档位参数（只调累积速度，机制形状不变）====
        /// <summary>空值到满值的基准帧数（残酷/修罗/毁灭）</summary>
        private static readonly int[] RiseTicksByTier = [2700, 2100, 1650];
        /// <summary>暴雪中的累积倍率</summary>
        private const float BlizzardRiseMul = 1.5f;
        /// <summary>满风附加倍率上限</summary>
        private const float WindRiseBonus = 0.3f;

        //==== 消退速率（每帧）====
        private const float DecayWarm = 1f / 240f;
        private const float DecaySheltered = 1f / 750f;
        private const float DecayIdle = 1f / 1500f;
        private const float DecayOutside = 1f / 600f;

        //==== 阶段阈值 ====
        /// <summary>呼出白气起点</summary>
        private const float BreathStage = 0.12f;
        /// <summary>屏边霜花起点（渲染层同读）</summary>
        internal const float FrostStage = 0.45f;
        /// <summary>心跳变缓起点</summary>
        private const float HeartStage = 0.62f;
        /// <summary>满值寒颤时长</summary>
        private const int ChillFramesOnFull = 240;
        /// <summary>满值后回落到的暴露度</summary>
        private const float ExposureAfterFull = 0.4f;

        //==== 采样间隔 ====
        private const int ShelterScanGap = 8;
        private const int WarmthScanGap = 12;
        private const int TownScanGap = 30;
        /// <summary>头顶找遮蔽的最大瓦格数</summary>
        private const int RoofSearchTiles = 48;
        /// <summary>热源扫描半径（瓦格）</summary>
        private const int WarmthTiles = 7;

        /// <summary>当前暴露度 0~1（渲染层读取）</summary>
        internal float Exposure { get; private set; }
        /// <summary>满值瞬间的白闪包络（纯视觉，逐帧衰减）</summary>
        internal float ChillFlash { get; private set; }

        private bool sheltered;
        private bool nearWarmth;
        private bool nearTown;
        private int shelterTimer;
        private int warmthTimer;
        private int townTimer;
        private int breathTimer;
        private int heartTimer;

        /// <summary>本玩家当前是否处于本槽位辖区（地表雪原）</summary>
        internal static bool InZone(Player player)
            => GameModeSystem.BrutalActive && player.ZoneSnow && player.ZoneOverworldHeight;

        public override void PostUpdateMiscEffects() {
            if (ChillFlash > 0f) {
                ChillFlash = Math.Max(ChillFlash - 0.02f, 0f);
            }

            if (!InZone(Player)) {
                Exposure = Math.Max(Exposure - DecayOutside, 0f);
                return;
            }

            SampleEnvironment();

            bool blizzard = Main.raining;
            bool coldWindow = !Main.dayTime || blizzard;
            float windAbs = Math.Min(Math.Abs(Main.windSpeedCurrent), 1f);

            if (nearWarmth || nearTown) {
                Exposure = Math.Max(Exposure - DecayWarm, 0f);
            }
            else if (sheltered) {
                Exposure = Math.Max(Exposure - DecaySheltered, 0f);
            }
            else if (coldWindow && !CWRWorld.HasBoss) {
                //Boss 在场冻结累积（减益机制暂停），其余照常消退
                int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);
                float rate = 1f / RiseTicksByTier[tier - 1];
                if (blizzard) {
                    rate *= BlizzardRiseMul;
                }
                rate *= 1f + WindRiseBonus * windAbs;
                Exposure += rate;
                if (Exposure >= 1f) {
                    OnFullExposure();
                }
            }
            else {
                Exposure = Math.Max(Exposure - DecayIdle, 0f);
            }

            UpdateBreath(blizzard);
            UpdateHeartbeat();
        }

        public override void UpdateDead() {
            Exposure = Math.Max(Exposure - 1f / 300f, 0f);
            ChillFlash = 0f;
            breathTimer = 0;
            heartTimer = 0;
        }

        //==================== 环境采样（分频，热路径零分配）====================

        private void SampleEnvironment() {
            if (--shelterTimer <= 0) {
                shelterTimer = ShelterScanGap;
                sheltered = ScanRoof();
            }
            if (--warmthTimer <= 0) {
                warmthTimer = WarmthScanGap;
                nearWarmth = Player.HasBuff(BuffID.Campfire) || ScanWarmth();
            }
            if (--townTimer <= 0) {
                townTimer = TownScanGap;
                nearTown = FrostveilAmbience.NearTown(Player.Center);
            }
        }

        /// <summary>头顶三列向上找实体瓦：找到即视为有遮蔽（室内/檐下）</summary>
        private bool ScanRoof() {
            Point head = Player.Top.ToTileCoordinates();
            for (int dx = -1; dx <= 1; dx++) {
                int tileX = head.X + dx;
                bool covered = false;
                for (int dy = 1; dy <= RoofSearchTiles; dy++) {
                    int tileY = head.Y - dy;
                    if (!WorldGen.InWorld(tileX, tileY, 10)) {
                        break;
                    }
                    if (WorldGen.SolidTile(tileX, tileY)) {
                        covered = true;
                        break;
                    }
                }
                if (!covered) {
                    return false;//任一列直通天空即算暴露
                }
            }
            return true;
        }

        /// <summary>近身热源扫描：火把/篝火/熔炉/地狱熔炉</summary>
        private bool ScanWarmth() {
            Point center = Player.Center.ToTileCoordinates();
            for (int dx = -WarmthTiles; dx <= WarmthTiles; dx++) {
                for (int dy = -WarmthTiles; dy <= WarmthTiles; dy++) {
                    int tileX = center.X + dx;
                    int tileY = center.Y + dy;
                    if (!WorldGen.InWorld(tileX, tileY, 10)) {
                        continue;
                    }
                    Tile tile = Main.tile[tileX, tileY];
                    if (!tile.HasTile) {
                        continue;
                    }
                    if (tile.TileType == TileID.Torches || tile.TileType == TileID.Campfire
                        || tile.TileType == TileID.Furnaces || tile.TileType == TileID.Hellforge) {
                        return true;
                    }
                }
            }
            return false;
        }

        //==================== 阶段反馈 ====================

        /// <summary>呼出白气：所有客户端都为可见玩家播（联机同屏可读），服务端不跑</summary>
        private void UpdateBreath(bool blizzard) {
            if (Main.dedServ || Exposure < BreathStage) {
                return;
            }
            //只有寒窗内才呼白气；离屏玩家不花预算
            if (Main.dayTime && !blizzard && Exposure < FrostStage) {
                return;
            }
            if (Vector2.DistanceSquared(Player.Center, Main.LocalPlayer.Center) > 1600f * 1600f) {
                return;
            }
            if (--breathTimer > 0) {
                return;
            }
            breathTimer = (int)MathHelper.Lerp(96f, 44f, Exposure)
                + Main.rand.Next(-6, 7);

            Vector2 mouth = Player.MountedCenter
                + new Vector2(Player.direction * 7f, -7f + Player.gfxOffY);
            Vector2 drift = new(Player.direction * 0.5f + Main.windSpeedCurrent * 1.2f,
                -0.32f);
            PRTLoader.NewParticle<PRT_FrostveilBreath>(mouth, drift,
                new Color(232, 242, 250) * 0.6f,
                Main.rand.NextFloat(0.55f, 0.8f) * (0.8f + Exposure * 0.5f))
                ?.Configure(Main.rand.Next(38, 52));
        }

        /// <summary>心跳变缓：只给本机玩家自己听，暴露越深节拍越慢、声音越沉</summary>
        private void UpdateHeartbeat() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || Exposure < HeartStage) {
                return;
            }
            if (--heartTimer > 0) {
                return;
            }
            float k = (Exposure - HeartStage) / (1f - HeartStage);
            heartTimer = (int)MathHelper.Lerp(52f, 92f, k);
            float volume = MathHelper.Lerp(0.14f, 0.3f, k);
            //lub-dub 双音：闷重低频镜像克脑心跳的声选
            SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with {
                Volume = volume,
                Pitch = -0.86f,
                MaxInstances = 2,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            });
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = volume * 0.55f,
                Pitch = -0.6f,
                MaxInstances = 2,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            });
        }

        /// <summary>满值：短暂原版寒颤 + 白闪 + 冰晶迸闪，随后回落再攒</summary>
        private void OnFullExposure() {
            Exposure = ExposureAfterFull;
            ChillFlash = 1f;

            if (Player.whoAmI == Main.myPlayer && !Main.dedServ) {
                Player.AddBuff(BuffID.Chilled, ChillFramesOnFull);
            }
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with {
                Volume = 0.55f, Pitch = 0.3f, MaxInstances = 3
            }, Player.Center);
            for (int i = 0; i < 8; i++) {
                Vector2 pos = Player.Center + Main.rand.NextVector2Circular(26f, 34f);
                PRTLoader.NewParticle<PRT_DefFrostGlint>(pos, Vector2.Zero,
                    new Color(210, 240, 255), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(14, 22));
            }
        }
    }
}
