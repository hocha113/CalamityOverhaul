using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Mireheart
{
    /// <summary>
    /// 「幽蕨」：残酷模式地下丛林的常态氛围层（纯客户端演出）。
    /// 荧光孢子与萤火光点弥漫、湿滴/虫鸣/蛙声底噪、荧蕨花丛被经过时亮起涟漪。
    /// 蜂巢与神庙两个微区各有自己的基调，主区孢子在微区内减密让位；
    /// 蜂巢嗡鸣随驻留升调是「蜂域警戒」的听觉预告通道。
    /// 进出群系走 Presence 缓升缓降（镜像 OldNetAmbience），不硬切
    /// </summary>
    internal class MireheartAmbience : ModSystem
    {
        //==== 在场强度（本地屏幕演出量，随本地玩家旗标走，非逐玩家游戏状态）====
        /// <summary>地下丛林主区在场强度 0~1</summary>
        internal static float Presence { get; private set; }
        /// <summary>蜂巢微区在场强度（离开蜂巢快速平息）</summary>
        internal static float HivePresence { get; private set; }
        /// <summary>神庙微区在场强度</summary>
        internal static float TemplePresence { get; private set; }

        //==== 粉尘预算（常态合计约 25/s，低于 40/s 公约）====
        /// <summary>孢子平均补充间隔（帧），满在场约 15 粒/s</summary>
        private const int SporeChance = 4;
        /// <summary>萤火平均补充间隔（帧），约 10 粒/s</summary>
        private const int FireflyChance = 6;
        /// <summary>微区内主区粉尘让位系数</summary>
        private const float MicroZoneDim = 0.35f;
        /// <summary>Boss 在场时纯视觉整体减弱系数</summary>
        private const float BossDim = 0.45f;

        //==== 荧蕨涟漪 ====
        /// <summary>涟漪扫描间隔（帧）</summary>
        private const int RippleScanGap = 14;
        /// <summary>触发涟漪的最低移速（像素/帧）</summary>
        private const float RippleMinSpeed = 1.5f;
        /// <summary>同一格重触发冷却（帧）</summary>
        private const int RippleCellCooldown = 240;
        private const int RippleLife = 34;
        private const int MaxRipples = 10;
        private const int MaxRecentCells = 16;

        private struct Ripple
        {
            internal bool Active;
            internal Vector2 Pos;
            internal int Life;
        }

        private struct RecentCell
        {
            internal Point Cell;
            internal int Expire;
        }

        private static readonly Ripple[] ripples = new Ripple[MaxRipples];
        private static readonly RecentCell[] recentCells = new RecentCell[MaxRecentCells];
        private static int rippleScanTimer;

        //==== 环境声（镜像 OldNetAmbience 的 SlotId+回调惯例）====
        private static SlotId wetLoopSlot;
        private static SlotId hiveLoopSlot;
        private static readonly SoundStyle WetLoopStyle =
            SoundID.LiquidsWaterLava with { IsLooped = true, MaxInstances = 1 };
        private static readonly SoundStyle HiveDroneStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };

        private static int dripTimer;
        private static int critterTimer;
        private static int frogTimer;

        /// <summary>本地玩家是否处于地下丛林主区（含蜂巢/神庙微区在内的总开关另看各自旗标）</summary>
        internal static bool LocalInUndergroundJungle {
            get {
                Player player = Main.LocalPlayer;
                return player != null && player.active
                    && player.ZoneJungle
                    && (player.ZoneDirtLayerHeight || player.ZoneRockLayerHeight);
            }
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            bool gate = GameModeSystem.BrutalActive && !Main.gameMenu;
            Player player = Main.LocalPlayer;
            bool inMain = gate && LocalInUndergroundJungle;
            bool inHive = gate && player.active && player.ZoneHive;
            bool inTemple = gate && player.active && player.ZoneLihzhardTemple;

            Presence = Approach(Presence, inMain ? 1f : 0f, inMain ? 0.03f : 0.05f);
            //离开蜂巢立即平息：出场斜率远陡于入场
            HivePresence = Approach(HivePresence, inHive ? 1f : 0f, inHive ? 0.05f : 0.2f);
            TemplePresence = Approach(TemplePresence, inTemple ? 1f : 0f, inTemple ? 0.04f : 0.08f);

            if (Main.gameMenu) {
                return;
            }

            UpdateRipples();

            if (Presence > 0.02f || HivePresence > 0.02f || TemplePresence > 0.02f) {
                UpdateAmbientLoops();
            }
            if (Presence <= 0.05f) {
                return;
            }

            float dim = CWRWorld.HasBoss ? BossDim : 1f;
            float micro = (HivePresence > 0.5f || TemplePresence > 0.5f) ? MicroZoneDim : 1f;
            float density = Presence * dim * micro;

            SpawnDriftMotes(density);
            TryScanRipples(player);
            UpdateOneShots(density);
        }

        private static float Approach(float value, float target, float rate) {
            value = MathHelper.Lerp(value, target, rate);
            return Math.Abs(value - target) < 0.004f ? target : value;
        }

        /// <summary>荧光孢子与萤火光点：屏幕内空气处低速补充</summary>
        private static void SpawnDriftMotes(float density) {
            if (density <= 0.05f) {
                return;
            }
            //孢子：原版孢尘自带漫游滞留（带重力档），绿黄荧光
            if (Main.rand.NextFloat() < density / SporeChance) {
                Vector2 pos = RandomAirSpotOnScreen();
                if (pos != Vector2.Zero) {
                    Dust dust = Dust.NewDustPerfect(pos, DustID.JungleSpore,
                        new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-0.1f, 0.25f)),
                        160, default, Main.rand.NextFloat(0.7f, 1.1f));
                    dust.fadeIn = 0.8f;
                }
            }
            //萤火：无重力慢飘的暖光点
            if (Main.rand.NextFloat() < density / FireflyChance) {
                Vector2 pos = RandomAirSpotOnScreen();
                if (pos != Vector2.Zero) {
                    Dust dust = Dust.NewDustPerfect(pos, DustID.Firefly,
                        new Vector2(Main.rand.NextFloat(-0.45f, 0.45f), Main.rand.NextFloat(-0.3f, 0.15f)),
                        0, default, Main.rand.NextFloat(0.6f, 0.95f));
                    dust.noGravity = true;
                }
            }
        }

        /// <summary>在屏幕范围内找一个非实心格（少量尝试，找不到就放弃本帧）</summary>
        private static Vector2 RandomAirSpotOnScreen() {
            for (int i = 0; i < 3; i++) {
                Vector2 pos = Main.screenPosition + new Vector2(
                    Main.rand.NextFloat(Main.screenWidth), Main.rand.NextFloat(Main.screenHeight));
                Point cell = pos.ToTileCoordinates();
                if (!WorldGen.InWorld(cell.X, cell.Y, 10)) {
                    continue;
                }
                if (!WorldGen.SolidTile(cell.X, cell.Y)) {
                    return pos;
                }
            }
            return Vector2.Zero;
        }

        /// <summary>荧蕨涟漪：玩家移动经过丛林花草藤蔓时，植株亮起一圈短促荧光</summary>
        private void TryScanRipples(Player player) {
            if (--rippleScanTimer > 0 || !player.active || player.dead) {
                return;
            }
            rippleScanTimer = RippleScanGap;
            if (player.velocity.Length() < RippleMinSpeed || HivePresence > 0.5f || TemplePresence > 0.5f) {
                return;
            }

            Point center = player.Center.ToTileCoordinates();
            int found = 0;
            for (int attempt = 0; attempt < 14 && found < 2; attempt++) {
                int x = center.X + Main.rand.Next(-6, 7);
                int y = center.Y + Main.rand.Next(-4, 5);
                if (!WorldGen.InWorld(x, y, 10)) {
                    continue;
                }
                Tile tile = Main.tile[x, y];
                bool foliage = tile.HasTile
                    && (tile.TileType == TileID.JunglePlants || tile.TileType == TileID.JunglePlants2
                    || tile.TileType == TileID.JungleVines || tile.TileType == TileID.PlantDetritus);
                if (!foliage) {
                    continue;
                }
                if (!TryClaimCell(new Point(x, y))) {
                    continue;
                }
                SpawnRipple(new Vector2(x * 16f + 8f, y * 16f + 8f));
                found++;
            }
        }

        /// <summary>同格冷却登记；满表时覆写最先到期的槽</summary>
        private static bool TryClaimCell(Point cell) {
            int now = (int)Main.GameUpdateCount;
            int freeSlot = 0;
            int earliest = int.MaxValue;
            for (int i = 0; i < recentCells.Length; i++) {
                if (recentCells[i].Expire > now && recentCells[i].Cell == cell) {
                    return false;
                }
                if (recentCells[i].Expire < earliest) {
                    earliest = recentCells[i].Expire;
                    freeSlot = i;
                }
            }
            recentCells[freeSlot] = new RecentCell { Cell = cell, Expire = now + RippleCellCooldown };
            return true;
        }

        private static void SpawnRipple(Vector2 pos) {
            for (int i = 0; i < ripples.Length; i++) {
                if (ripples[i].Active) {
                    continue;
                }
                ripples[i] = new Ripple { Active = true, Pos = pos, Life = RippleLife };
                break;
            }
            //亮起瞬间的荧光尘（事件驱动，不吃常态预算）
            for (int k = 0; k < 3; k++) {
                Dust dust = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(10f, 8f),
                    DustID.JungleSpore, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.3f, 0.9f)),
                    120, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
            Dust torch = Dust.NewDustPerfect(pos, DustID.JungleTorch,
                new Vector2(0f, -0.5f), 100, default, 1.1f);
            torch.noGravity = true;
        }

        private static void UpdateRipples() {
            if (Main.gamePaused) {
                return;
            }
            for (int i = 0; i < ripples.Length; i++) {
                if (!ripples[i].Active) {
                    continue;
                }
                if (--ripples[i].Life <= 0) {
                    ripples[i].Active = false;
                    continue;
                }
                //脉冲光：正弦包络，起振快收尾缓
                float t = ripples[i].Life / (float)RippleLife;
                float env = MathF.Sin(t * MathHelper.Pi);
                Lighting.AddLight(ripples[i].Pos, new Vector3(0.10f, 0.34f, 0.12f) * env);
            }
        }

        //==== 环境声 ====

        /// <summary>循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走</summary>
        private static void UpdateAmbientLoops() {
            if (Main.gameMenu) {
                return;
            }
            if (Presence > 0.05f && !SoundEngine.TryGetActiveSound(wetLoopSlot, out _)) {
                wetLoopSlot = SoundEngine.PlaySound(WetLoopStyle, null, UpdateWetLoop);
            }
            if (HivePresence > 0.05f && !SoundEngine.TryGetActiveSound(hiveLoopSlot, out _)) {
                hiveLoopSlot = SoundEngine.PlaySound(HiveDroneStyle, null, UpdateHiveDrone);
            }
        }

        /// <summary>湿洞底噪：水声极低音量常驻，只当空气里的潮气</summary>
        private static bool UpdateWetLoop(ActiveSound sound) {
            if (Main.gameMenu || Presence <= 0.02f) {
                return false;
            }
            sound.Volume = 0.11f * Presence;
            sound.Pitch = -0.35f;
            sound.Position = null;
            return true;
        }

        /// <summary>蜂巢低鸣：音调随本地玩家驻留升高，是蜂云的听觉预告通道</summary>
        private static bool UpdateHiveDrone(ActiveSound sound) {
            if (Main.gameMenu || HivePresence <= 0.02f) {
                return false;
            }
            float dwell = Main.LocalPlayer?.GetModPlayer<MireheartPlayer>()?.HiveDwellFrac ?? 0f;
            sound.Volume = (0.16f + 0.22f * dwell) * HivePresence;
            sound.Pitch = -0.25f + 0.8f * dwell;
            sound.Position = null;
            return true;
        }

        /// <summary>湿滴/虫鸣/蛙声散点（有空间位置，音源落在玩家四周）</summary>
        private static void UpdateOneShots(float density) {
            Player player = Main.LocalPlayer;
            Vector2 around = player.Center + new Vector2(
                Main.rand.NextFloat(-320f, 320f), Main.rand.NextFloat(-180f, 180f));

            if (--dripTimer <= 0) {
                dripTimer = Main.rand.Next(90, 210);
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.65f * density }, around);
            }
            if (--critterTimer <= 0) {
                critterTimer = Main.rand.Next(200, 460);
                SoundEngine.PlaySound(SoundID.Critter with { Volume = 0.55f * density }, around);
            }
            if (--frogTimer <= 0) {
                frogTimer = Main.rand.Next(480, 1050);
                SoundEngine.PlaySound(SoundID.Frog with { Volume = 0.4f * density }, around);
            }
        }

        public override void ClearWorld() {
            Presence = 0f;
            HivePresence = 0f;
            TemplePresence = 0f;
            for (int i = 0; i < ripples.Length; i++) {
                ripples[i].Active = false;
            }
            for (int i = 0; i < recentCells.Length; i++) {
                recentCells[i] = default;
            }
        }
    }
}
