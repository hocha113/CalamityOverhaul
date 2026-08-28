using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Tidecall.Projectiles;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Tidecall
{
    /// <summary>
    /// 「汐声」：残酷模式海洋沙滩的环境氛围中枢。
    /// 客户端持环境声层（海风+浪涌双循环+海鸥啼鸣，镜像 OldNetAmbience 的槽位管理），
    /// 浪涌包络 <see cref="Swell"/> 同时喂给岸线泡沫与碎金光斑（TidecallAmbientRender），音画同拍；
    /// 权威端低频调度「离岸流」与「疯狗浪」两个机制实体。
    /// 档位契约：只调离岸流频率与疯狗浪浪高，机制形状不随档位改变；
    /// 风雨联动：<see cref="StormSurge"/> 抬高浪涌包络下限并增益疯狗浪高度/频率，暴雨大风时浪更凶
    /// </summary>
    internal class TidecallAmbience : ModSystem
    {
        /// <summary>本地玩家的沙滩在场强度 0~1（进出 ~1.5s 缓升缓降，离开群系淡出不硬切）</summary>
        internal static float Presence { get; private set; }

        /// <summary>浪涌包络 0~1：主周期 ~5.6s 与慢周期 ~13.7s 叠加，声量与泡沫摆动同源；风雨抬底见 <see cref="StormSurge"/></summary>
        internal static float Swell { get; private set; }

        /// <summary>风雨联动强度 0~1：风速为主、降雨抬底（两者皆为全端同步的世界状态量，各端读值一致）</summary>
        internal static float StormSurge {
            get {
                float wind = MathHelper.Clamp(MathF.Abs(Main.windSpeedCurrent) / 0.8f, 0f, 1f);
                float rain = Main.raining ? 0.35f + 0.30f * Main.maxRaining : 0f;
                return MathHelper.Clamp(0.75f * wind + rain, 0f, 1f);
            }
        }

        /// <summary>Boss 在场时纯视觉氛围保留但减弱的统一系数</summary>
        internal static float BossDim => CWRWorld.HasBoss ? 0.55f : 1f;

        //==== 客户端表现量（本机屏幕级状态，非逐玩家数据） ====
        private static float swellClock;
        private static float ripFlow;
        private static float waveRoar;
        private static int gullTimer;
        private static SlotId seaWindSlot;
        private static SlotId surfSlot;
        private static SlotId roarSlot;

        private static readonly SoundStyle SeaWindStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };
        private static readonly SoundStyle SurfWashStyle =
            SoundID.Waterfall with { IsLooped = true, MaxInstances = 2 };
        private static readonly SoundStyle WaveRoarStyle =
            SoundID.BlizzardStrongLoop with { IsLooped = true, MaxInstances = 1 };

        //==== 权威端调度（世界级决策私产，ClearWorld 重置） ====
        /// <summary>离岸流间隔，档位只调频率</summary>
        private static readonly int[] RipIntervalByTier = [1140, 880, 660];
        /// <summary>疯狗浪浪高（像素），档位只调浪高</summary>
        internal static readonly int[] WaveHeightByTier = [96, 128, 160];
        /// <summary>疯狗浪间隔（不随档位变化，低频；风雨联动只在投放处做数值缩短）</summary>
        private const int WaveIntervalBase = 1900;
        /// <summary>疯狗浪伤害：锚定海洋原版粉水母接触伤害 20 × 0.5（DamageFrac 惯例）</summary>
        private const int WaveDamage = 10;
        private const int RipCap = 2;
        private const int WaveCap = 2;
        /// <summary>同域排斥距离：既有机制实体太近则不重复投放</summary>
        private const float SameSpotExclusion = 1800f;

        private static int ripTimer = 600;
        private static int waveTimer = 1500;

        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                ClientTick();
            }
            if (VaultUtils.isServer || VaultUtils.isSinglePlayer) {
                AuthorityTick();
            }
        }

        public override void ClearWorld() {
            Presence = 0f;
            Swell = 0f;
            swellClock = 0f;
            ripFlow = 0f;
            waveRoar = 0f;
            gullTimer = 0;
            ripTimer = 600;
            waveTimer = 1500;
        }

        //==================== 客户端：在场强度与声层 ====================

        private static void ClientTick() {
            if (Main.gameMenu) {
                Presence = 0f;
                ripFlow = 0f;
                waveRoar = 0f;
                return;
            }

            Player player = Main.LocalPlayer;
            float target = GameModeSystem.BrutalActive && player.active && player.ZoneBeach ? 1f : 0f;
            Presence = MathHelper.Lerp(Presence, target, 0.028f);
            if (Presence < 0.004f && target <= 0f) {
                Presence = 0f;
            }

            if (!Main.gamePaused) {
                swellClock += 1f / 60f;
                //机制实体每帧上报，此处消费后衰减，实体消失后声浪自然退潮
                ripFlow *= 0.9f;
                waveRoar *= 0.9f;
                if (ripFlow < 0.005f) {
                    ripFlow = 0f;
                }
                if (waveRoar < 0.005f) {
                    waveRoar = 0f;
                }
            }
            float main = 0.5f + 0.5f * MathF.Sin(swellClock * MathHelper.TwoPi / 5.6f);
            float slow = 0.5f + 0.5f * MathF.Sin(swellClock * MathHelper.TwoPi / 13.7f + 1.3f);
            float baseline = main * (0.45f + 0.55f * slow);
            //风雨抬底：暴雨大风时海面不再有真正的平静期（周期结构不动，声画同拍照旧走 Swell）
            Swell = baseline + (1f - baseline) * (0.42f * StormSurge);

            if (Presence <= 0.02f || Main.gamePaused) {
                return;
            }
            UpdateAmbientLoops();
            UpdateGullCalls(player);
        }

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
        private static void UpdateAmbientLoops() {
            if (!SoundEngine.TryGetActiveSound(seaWindSlot, out _)) {
                seaWindSlot = SoundEngine.PlaySound(SeaWindStyle, null, UpdateSeaWind);
            }
            if (!SoundEngine.TryGetActiveSound(surfSlot, out _)) {
                surfSlot = SoundEngine.PlaySound(SurfWashStyle, null, UpdateSurfWash);
            }
            //浪吼只在有疯狗浪逼近时在场
            if (waveRoar > 0.03f && !SoundEngine.TryGetActiveSound(roarSlot, out _)) {
                roarSlot = SoundEngine.PlaySound(WaveRoarStyle, null, UpdateWaveRoar);
            }
        }

        //海风：轻而稳的气声底，随浪涌微起伏
        private static bool UpdateSeaWind(ActiveSound sound) {
            if (Presence <= 0.01f || Main.gameMenu) {
                return false;
            }
            sound.Volume = (0.15f + 0.09f * Swell) * Presence * BossDim;
            sound.Pitch = 0.05f;
            sound.Position = null;
            return true;
        }

        //浪涌：水声冲刷随包络呼吸；离岸流在场时整体抬一档
        private static bool UpdateSurfWash(ActiveSound sound) {
            if (Presence <= 0.01f || Main.gameMenu) {
                return false;
            }
            sound.Volume = (0.14f + 0.40f * Swell + MathHelper.Clamp(ripFlow, 0f, 1f) * 0.26f)
                * Presence * BossDim;
            sound.Pitch = -0.30f + 0.08f * Swell;
            sound.Position = null;
            return true;
        }

        //浪吼：疯狗浪由远及近的低频轰鸣，靠机制实体逐帧上报驱动
        private static bool UpdateWaveRoar(ActiveSound sound) {
            if (waveRoar <= 0.02f || Main.gameMenu) {
                return false;
            }
            sound.Volume = MathHelper.Clamp(waveRoar, 0f, 1f) * 0.85f * (0.35f + 0.65f * Presence);
            sound.Pitch = -0.72f;
            sound.Position = null;
            return true;
        }

        //海鸥：白天低频啼鸣，从玩家向海一侧的空中传来
        private static void UpdateGullCalls(Player player) {
            if (!Main.dayTime || Main.raining || Presence < 0.35f) {
                return;
            }
            if (--gullTimer > 0) {
                return;
            }
            gullTimer = Main.rand.Next(360, 1080);
            Vector2 pos = player.Center + new Vector2(
                DeepDir(player.Center.X) * Main.rand.NextFloat(200f, 800f),
                -Main.rand.NextFloat(80f, 260f));
            SoundEngine.PlaySound(SoundID.Seagull with { Volume = 0.34f * BossDim }, pos);
        }

        /// <summary>离岸流在场时上报水声增益（客户端表现量，取峰值）</summary>
        internal static void ReportRipFlow(float value) => ripFlow = MathF.Max(ripFlow, value);

        /// <summary>疯狗浪逼近时上报浪吼强度（客户端表现量，取峰值）</summary>
        internal static void ReportWaveRoar(float value) => waveRoar = MathF.Max(waveRoar, value);

        //==================== 权威端：机制调度 ====================

        private static void AuthorityTick() {
            if (!GameModeSystem.BrutalActive) {
                return;
            }
            if (CWRWorld.HasBoss) {
                return;//Boss 在场机制暂停，计时冻结，战后续走
            }
            if (ripTimer > 0) {
                ripTimer--;
            }
            if (waveTimer > 0) {
                waveTimer--;
            }
            if (ripTimer <= 0) {
                TrySpawnRip();
            }
            if (waveTimer <= 0) {
                TrySpawnWave();
            }
        }

        /// <summary>离岸流投放：找一名泡在海水表层的沙滩玩家，在其向海一侧铺设暗流走廊</summary>
        private static void TrySpawnRip() {
            ripTimer = 90;//默认短重试
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            int ripType = ModContent.ProjectileType<TidecallRipCurrentProj>();
            if (CountActive(ripType) >= RipCap) {
                return;
            }

            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !player.ZoneBeach || !InSurfWater(player)) {
                    continue;
                }
                if (!TownCalm(player.Center)) {
                    continue;
                }
                if (AnyProjNear(ripType, player.Center.X)) {
                    continue;
                }
                int dir = DeepDir(player.Center.X);
                Point center = player.Center.ToTileCoordinates();
                //走廊中点在玩家向海一侧，要求那里是真海水（深≥4格）而非水洼
                int midX = center.X + dir * 18;
                if (!TryFindWaterSurface(midX, center.Y, out int midSurfaceY)
                    || WaterDepthTiles(midX, midSurfaceY) < 4) {
                    continue;
                }

                Vector2 spawn = new(midX * 16f + 8f, midSurfaceY * 16f);
                Projectile.NewProjectile(new EntitySource_Misc("CWRTidecallRip"), spawn, Vector2.Zero,
                    ripType, 0, 0f, Main.myPlayer, dir, midSurfaceY);
                ripTimer = RipIntervalByTier[tier - 1] + Main.rand.Next(-120, 121);
                return;
            }
        }

        /// <summary>疯狗浪投放：为一名沙滩玩家找到岸线，从远海压来一道拍岸浪</summary>
        private static void TrySpawnWave() {
            waveTimer = 150;//默认短重试
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            int waveType = ModContent.ProjectileType<TidecallRogueWaveProj>();
            if (CountActive(waveType) >= WaveCap) {
                return;
            }

            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !player.ZoneBeach) {
                    continue;
                }
                if (!TownCalm(player.Center)) {
                    continue;
                }
                if (!TryFindShoreline(player, out int shoreX, out int surfaceY, out int seaDir)) {
                    continue;
                }
                float shoreWorldX = shoreX * 16f + 8f;
                if (AnyProjNear(waveType, shoreWorldX)) {
                    continue;
                }

                //风雨调制：档位仍从 WaveHeightByTier 取基数，暴雨大风只做高度增益与间隔缩短（机制形状不变）
                float storm = StormSurge;
                float waveHeight = WaveHeightByTier[tier - 1] * (1f + 0.30f * storm);
                //出生在岸线向海约 55 格处的水面上，向陆压来
                Vector2 spawn = new(shoreWorldX + seaDir * TidecallRogueWaveProj.ApproachTiles * 16f, surfaceY * 16f);
                Projectile.NewProjectile(new EntitySource_Misc("CWRTidecallWave"), spawn, Vector2.Zero,
                    waveType, WaveDamage, 3f, Main.myPlayer, -seaDir, waveHeight, shoreX);
                waveTimer = WaveIntervalBase - (int)(560f * storm) + Main.rand.Next(-480, 481);
                return;
            }
        }

        /// <summary>统计某类弹幕的活动实例数（镜像 WastesBrutalNPC.CountActive）</summary>
        private static int CountActive(int projType, int stopAt = 8) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>指定类型的机制实体是否已存在于横向排斥距离内</summary>
        private static bool AnyProjNear(int projType, float worldX) {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && MathF.Abs(proj.Center.X - worldX) < SameSpotExclusion) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>城镇安宁：约 60 格内有存活城镇 NPC 则机制不触发</summary>
        internal static bool TownCalm(Vector2 pos) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(pos) < 960f) {
                    return false;
                }
            }
            return true;
        }

        //==================== 地形探针（机制实体与渲染层共用） ====================

        /// <summary>深海方向：沙滩总在世界两端，向最近的世界边缘为向海</summary>
        internal static int DeepDir(float worldX) => worldX < Main.maxTilesX * 8f ? -1 : 1;

        /// <summary>该格有可观水量的纯水（忽略残留薄水膜）</summary>
        internal static bool WaterAt(int x, int y) {
            if (!WorldGen.InWorld(x, y, 10)) {
                return false;
            }
            Tile tile = Framing.GetTileSafely(x, y);
            return tile.LiquidAmount > 32 && tile.LiquidType == LiquidID.Water;
        }

        internal static bool SolidAt(int x, int y)
            => WorldGen.InWorld(x, y, 10) && WorldGen.SolidTile(x, y);

        /// <summary>
        /// 找某列的水面行。参考行在水中则向上回溯；不在水中则向下探最多 12 格
        /// （先碰到实心地面视为此列无水）
        /// </summary>
        internal static bool TryFindWaterSurface(int tileX, int refTileY, out int surfaceTileY) {
            surfaceTileY = 0;
            if (!WaterAt(tileX, refTileY)) {
                for (int dy = 1; dy <= 12; dy++) {
                    if (SolidAt(tileX, refTileY + dy)) {
                        return false;
                    }
                    if (WaterAt(tileX, refTileY + dy)) {
                        surfaceTileY = refTileY + dy;
                        return true;
                    }
                }
                return false;
            }
            int y = refTileY;
            for (int i = 0; i < 60 && WaterAt(tileX, y - 1); i++) {
                y--;
            }
            surfaceTileY = y;
            return true;
        }

        /// <summary>自水面向下量水深（格），到实心或无水为止</summary>
        internal static int WaterDepthTiles(int tileX, int surfaceTileY, int maxProbe = 24) {
            int depth = 0;
            while (depth < maxProbe && WaterAt(tileX, surfaceTileY + depth)) {
                depth++;
            }
            return depth;
        }

        /// <summary>
        /// 玩家处于海水表层：胸部以上没入纯水，且距水面 ≤7 格。
        /// 岸上与浅滩（膝深涉水）不满足；深潜超过表层也不满足（离岸流只做海面近岸，防撞契约）
        /// </summary>
        internal static bool InSurfWater(Player player) {
            if (!player.wet || player.lavaWet || player.honeyWet || player.shimmerWet) {
                return false;
            }
            Point center = player.Center.ToTileCoordinates();
            if (!WaterAt(center.X, center.Y) || !WaterAt(center.X, center.Y - 1)) {
                return false;
            }
            if (!TryFindWaterSurface(center.X, center.Y, out int surfaceY)) {
                return false;
            }
            return player.Center.Y - surfaceY * 16f <= 7 * 16f;
        }

        /// <summary>
        /// 找玩家所在海滩的岸线列（向海方向第一列水深 ≥3 的海水列）与其水面行。
        /// 玩家在水中则向陆回退到最后一列海水
        /// </summary>
        internal static bool TryFindShoreline(Player player, out int shoreTileX, out int surfaceTileY, out int seaDir) {
            seaDir = DeepDir(player.Center.X);
            shoreTileX = 0;
            surfaceTileY = 0;
            Point pt = player.Center.ToTileCoordinates();

            if (ColumnHasSea(pt.X, pt.Y, out int sy)) {
                int x = pt.X;
                for (int i = 0; i < 220; i++) {
                    int nx = x - seaDir;
                    if (!ColumnHasSea(nx, pt.Y, out int nsy)) {
                        break;
                    }
                    x = nx;
                    sy = nsy;
                }
                shoreTileX = x;
                surfaceTileY = sy;
                return true;
            }

            for (int i = 1; i <= 220; i++) {
                int nx = pt.X + seaDir * i;
                if (ColumnHasSea(nx, pt.Y, out int nsy)) {
                    shoreTileX = nx;
                    surfaceTileY = nsy;
                    return true;
                }
            }
            return false;
        }

        /// <summary>该列在参考高度附近是否有海水（深 ≥3 格才算海，水洼不算）</summary>
        private static bool ColumnHasSea(int tileX, int refTileY, out int surfaceTileY) {
            surfaceTileY = 0;
            for (int y = refTileY - 40; y <= refTileY + 30; y++) {
                if (!WorldGen.InWorld(tileX, y, 10)) {
                    return false;
                }
                if (SolidAt(tileX, y)) {
                    return false;//先碰到实心：该列到此高度没有水面
                }
                if (WaterAt(tileX, y)) {
                    surfaceTileY = y;
                    return WaterDepthTiles(tileX, y) >= 3;
                }
            }
            return false;
        }
    }
}
