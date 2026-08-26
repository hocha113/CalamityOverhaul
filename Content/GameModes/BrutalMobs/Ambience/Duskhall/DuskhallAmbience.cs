using CalamityOverhaul.Content.Scenarios.Dungeonworld;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Duskhall
{
    /// <summary>
    /// 残酷模式地牢环境层，纯客户端表现：零网络包、零伤害判定、零 tile 写入。<br/>
    /// 「烛骨」常态氛围：幽蓝烛光浮尘（烛具探针登记，屏内低密度上飘）+
    /// 锁链吱呀/远处铁门闷响/幽魂低语的方位感稀疏底噪（全部原版音源，带世界坐标做声像）；<br/>
    /// 「烛熄之风」：世界时钟（Main.time，各端同拍）驱动的周期阴风，风前沿沿风向扫过屏幕，
    /// 烛火次第压熄作预告（烟+熄辉，音画双通道预告约 90 帧），过境期全局轻度压光
    /// （峰值 -16% 亮度，绝不黑屏）+ 衣袂尘屑横掠，风尾扫过烛火复明；<br/>
    /// 「骨语回廊」：低频、水平方位偏置的骨骼拖行三连声，纯氛围。<br/>
    /// 档位只调阴风频率（<see cref="WindCycleByTier"/>）与凝视累积速度（见 <see cref="DuskhallPlayer"/>），
    /// 机制形状不随档位改变。Boss 在场整体降密、新阴风整轮压掉。<br/>
    /// 与 Hollowdeep 暗涌的差异：那边是无光驱动的屏边黑雾常态压迫，
    /// 本层收暗是事件式短暂全局压光，且有烛火预告与复明收尾
    /// </summary>
    internal class DuskhallAmbience : ModSystem
    {
        /// <summary>本地在场强度 0~1（进出地牢缓升缓降，一切表现乘它）</summary>
        internal static float Presence { get; private set; }

        /// <summary>Boss 在场降密因子（1 平常，0.35 Boss 中；纯视觉保留但减弱）</summary>
        internal static float BossCalm { get; private set; } = 1f;

        /// <summary>本地玩家凝视值镜像（供声画回调读取；权威值在各自的 <see cref="DuskhallPlayer"/> 上）</summary>
        internal static float LocalGaze { get; private set; }

        /// <summary>presence 曾经抬起过，归零后需要一次硬复位</summary>
        private static bool dirty;

        //==================== 烛具登记（镜像 DungeonworldAmbientFX 的探针+槽位口径）====================

        private const int MaxCandles = 12;
        private const int ProbesPerTick = 20;
        private const int CandleTtl = 90;
        private const int CandleRecheck = 10;

        internal struct CandleSlot
        {
            internal bool Active;
            internal Point Tile;
            internal int Type;
            internal Vector2 FlamePx;
            internal int Ttl;
            internal int RecheckIn;
            /// <summary>呼吸相位</summary>
            internal float Phase;
            /// <summary>风前沿到达抖动 0~1（按格坐标散列，次第感来源之一）</summary>
            internal float Jitter01;
            /// <summary>本帧被阴风压熄</summary>
            internal bool Snuffed;
            /// <summary>吊挂灯具（链声的视觉锚点）</summary>
            internal bool Hanging;
        }

        internal static readonly CandleSlot[] Candles = new CandleSlot[MaxCandles];

        /// <summary>幽蓝烛光的加光底色（AddLight 只能加不能减，压暗一律走全局压光）</summary>
        private static readonly Vector3 CandleCold = new(0.30f, 0.42f, 0.66f);
        private static readonly Vector3 CandleColdGaze = new(0.24f, 0.50f, 0.86f);

        //==================== 烛熄之风 ====================

        /// <summary>阴风周期（帧），档位只调频率</summary>
        private static readonly int[] WindCycleByTier = [3600, 3000, 2460];
        /// <summary>事件总长：预告(烛熄+风声起)→过境(压光)→复明</summary>
        private const int WindEventLen = 390;
        /// <summary>风前沿出发位（相对起风时玩家 X 的带符号距离，像素）</summary>
        private const float WindFrontStart = -1100f;
        /// <summary>风前沿推进速度（像素/帧）：先声后至，首批烛熄约在压光前 75 帧</summary>
        private const float WindFrontSpeed = 9f;
        /// <summary>风带厚度：前沿压熄，风尾复明</summary>
        private const float WindTailLen = 940f;
        /// <summary>过境压光峰值（轻微收暗，不做黑屏）</summary>
        private const float WindDarkenMax = 0.16f;

        /// <summary>过境压光包络 0~1（平滑后）</summary>
        internal static float WindDarken { get; private set; }
        private static float windSoundEnv;
        private static int windDir = 1;
        private static float windOriginX;
        private static int lastWindKey = -1;
        private static bool windSuppressed;
        /// <summary>本帧事件内时刻，-1 = 无事件（或被 Boss 压掉）</summary>
        private static int windEventT = -1;

        //==================== 环境声 ====================

        private static SlotId whisperSlot;
        private static SlotId windSlot;
        /// <summary>幽魂低语声床：低沉门扉嗡鸣压低音调，随凝视值渐清晰</summary>
        private static readonly SoundStyle WhisperBedStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };
        /// <summary>穿廊阴风：室内闷风声</summary>
        private static readonly SoundStyle WindLoopStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        private static int cueTimer = 480;
        private static int boneTimer = 1800;

        //一次性音延迟队列（骨语三连、门响回声）
        private struct PendingCue
        {
            internal bool Active;
            internal SoundStyle Style;
            internal Vector2 Pos;
            internal int Delay;
        }

        private static readonly PendingCue[] pendingCues = new PendingCue[4];

        //==================== 生命周期 ====================

        public override void ClearWorld() => HardReset();

        public override void Unload() => HardReset();

        private static void HardReset() {
            Presence = 0f;
            BossCalm = 1f;
            LocalGaze = 0f;
            WindDarken = 0f;
            windSoundEnv = 0f;
            windEventT = -1;
            lastWindKey = -1;
            windSuppressed = false;
            cueTimer = 480;
            boneTimer = 1800;
            dirty = false;
            for (int i = 0; i < Candles.Length; i++) {
                Candles[i].Active = false;
            }
            for (int i = 0; i < pendingCues.Length; i++) {
                pendingCues[i].Active = false;
            }
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gamePaused) {
                return;//暂停不推进；循环声回调自会维持现值
            }

            Player player = Main.LocalPlayer;
            bool want = player != null && player.active && !Main.gameMenu
                && GameModeSystem.BrutalActive && player.ZoneDungeon
                && !Dungeonworld.Active;//地牢子世界有自己的空气签名，让位

            Presence = MathHelper.Lerp(Presence, want ? 1f : 0f, want ? 0.05f : 0.08f);
            if (!want && Presence < 0.004f) {
                if (dirty) {
                    HardReset();
                }
                return;
            }
            dirty = true;

            BossCalm = MathHelper.Lerp(BossCalm, CWRWorld.HasBoss ? 0.35f : 1f, 0.05f);
            LocalGaze = player.GetModPlayer<DuskhallPlayer>().Gaze;

            UpdateWind(player);
            RunProbes();
            UpdateCandles();
            UpdateWindStreaks(player);
            UpdateCues(player);
            UpdateLoops();
        }

        /// <summary>过境压光：全局亮度轻度下压，无黑屏（Hollowdeep 的屏边黑雾归他们，此处只动亮度）</summary>
        public override void ModifyLightingBrightness(ref float scale) {
            float dark = WindDarkenMax * WindDarken * Presence;
            if (dark > 0.001f) {
                scale *= 1f - dark;
            }
        }

        //==================== 阴风时间轴 ====================

        /// <summary>
        /// 世界时钟推进：Main.time 各端同步（镜像 LegionNPC 潮汐的零网络口径），
        /// 昼夜翻转时钟归零只会造成一次提前起风，属可接受的表现层抖动
        /// </summary>
        private static void UpdateWind(Player player) {
            int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);
            int cycle = WindCycleByTier[tier - 1];
            int cycleIdx = (int)(Main.time / cycle);
            int t = (int)(Main.time % cycle);
            bool active = t < WindEventLen;

            int key = cycleIdx * 2 + (Main.dayTime ? 1 : 0);
            if (active && key != lastWindKey) {
                //新一轮起风：锁定原点与风向；Boss 在场则整轮压掉（伤害为零，纯避免战中干扰）
                lastWindKey = key;
                windOriginX = player.Center.X;
                windSuppressed = CWRWorld.HasBoss;
                uint h = (uint)key * 2654435761u ^ (Main.dayTime ? 0x9E3779B9u : 0x85EBCA6Bu);
                windDir = (h & 2u) == 0u ? 1 : -1;
            }
            windEventT = active && !windSuppressed ? t : -1;

            float rawDark = 0f;
            float rawSound = 0f;
            if (windEventT >= 0) {
                float ft = windEventT;
                //压光：90 帧后起坡（此前已有 ≥45 帧的风声+烛熄双通道预告），255 帧起退坡
                rawDark = MathHelper.Clamp((ft - 90f) / 40f, 0f, 1f)
                    * (1f - MathHelper.Clamp((ft - 255f) / 45f, 0f, 1f));
                //风声：起风即闻，随压光加强，事件尾自然收
                rawSound = 0.30f * MathHelper.Clamp(ft / 50f, 0f, 1f)
                    * (1f - MathHelper.Clamp((ft - 300f) / 60f, 0f, 1f))
                    + 0.32f * rawDark;
            }
            WindDarken = MathHelper.Lerp(WindDarken, rawDark * BossCalm, 0.12f);
            windSoundEnv = MathHelper.Lerp(windSoundEnv, rawSound * BossCalm, 0.15f);
        }

        /// <summary>该烛此刻是否处于风带内（纯函数：可从任意时刻求值，进出沿由槽位缓存做边沿检测）</summary>
        private static bool WindSnuffedAt(float flameX, float jitter01) {
            if (windEventT < 0) {
                return false;
            }
            float r = (flameX - windOriginX) * windDir + jitter01 * 90f;
            float front = WindFrontStart + windEventT * WindFrontSpeed;
            return front > r && front - WindTailLen < r;
        }

        //==================== 烛具探针与槽位 ====================

        private static void RunProbes() {
            int left = (int)(Main.screenPosition.X / 16f) - 6;
            int top = (int)(Main.screenPosition.Y / 16f) - 6;
            int right = (int)((Main.screenPosition.X + Main.screenWidth) / 16f) + 6;
            int bottom = (int)((Main.screenPosition.Y + Main.screenHeight) / 16f) + 6;
            left = (int)MathHelper.Clamp(left, 1, Main.maxTilesX - 2);
            right = (int)MathHelper.Clamp(right, left + 1, Main.maxTilesX - 2);
            top = (int)MathHelper.Clamp(top, 1, Main.maxTilesY - 2);
            bottom = (int)MathHelper.Clamp(bottom, top + 1, Main.maxTilesY - 2);

            for (int i = 0; i < ProbesPerTick; i++) {
                int x = Main.rand.Next(left, right);
                int y = Main.rand.Next(top, bottom);
                Tile tile = Framing.GetTileSafely(x, y);
                if (tile.HasTile && FixtureLit(tile)) {
                    TryRegisterCandle(x, y, tile.TileType);
                }
            }
        }

        /// <summary>
        /// 灯具点亮判据（帧语义对齐 DungeonworldAmbientFX.LampLit，只读不写）。
        /// 烛台/灯柱的熄灭帧位按"整器宽度平移 frameX"的原版布线惯例推断，
        /// 误判只影响表现密度，无判定后果
        /// </summary>
        private static bool FixtureLit(Tile tile) {
            return tile.TileType switch {
                TileID.Chandeliers => tile.TileFrameX % 108 < 54,
                TileID.HangingLanterns => tile.TileFrameX < 18,
                TileID.Candles => tile.TileFrameX < 18,
                TileID.Candelabras => tile.TileFrameX % 72 < 36,
                TileID.Lamps => tile.TileFrameX < 18,
                TileID.WaterCandle => true,
                TileID.Torches => true,
                _ => false
            };
        }

        private static void TryRegisterCandle(int x, int y, int tileType) {
            //去重：多格灯具的任意格都算同一盏，命中既有槽只续订
            for (int i = 0; i < Candles.Length; i++) {
                if (!Candles[i].Active) {
                    continue;
                }
                if (Math.Abs(Candles[i].Tile.X - x) <= 2 && Math.Abs(Candles[i].Tile.Y - y) <= 2) {
                    Candles[i].Ttl = CandleTtl;
                    return;
                }
            }
            for (int i = 0; i < Candles.Length; i++) {
                if (Candles[i].Active) {
                    continue;
                }
                Vector2 flamePx = new(x * 16f + 8f, y * 16f + 4f);
                float jitter = Hash01(x, y);
                Candles[i] = new CandleSlot {
                    Active = true,
                    Tile = new Point(x, y),
                    Type = tileType,
                    FlamePx = flamePx,
                    Ttl = CandleTtl,
                    RecheckIn = CandleRecheck,
                    Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Jitter01 = jitter,
                    //风带内滚屏登记的烛按当前风况初始化，避免重播压熄拍
                    Snuffed = WindSnuffedAt(flamePx.X, jitter),
                    Hanging = tileType == TileID.Chandeliers || tileType == TileID.HangingLanterns
                };
                return;
            }
        }

        private static float Hash01(int x, int y) {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) % 1000u / 1000f;
        }

        private static void UpdateCandles() {
            float gazeBlue = MathHelper.Clamp((LocalGaze - 0.4f) / 0.6f, 0f, 1f);
            for (int i = 0; i < Candles.Length; i++) {
                if (!Candles[i].Active) {
                    continue;
                }
                Candles[i].Ttl--;
                Candles[i].RecheckIn--;

                if (Candles[i].RecheckIn <= 0) {
                    Candles[i].RecheckIn = CandleRecheck;
                    Tile t = Framing.GetTileSafely(Candles[i].Tile.X, Candles[i].Tile.Y);
                    if (!t.HasTile || t.TileType != Candles[i].Type || !FixtureLit(t)) {
                        Candles[i].Active = false;
                        continue;
                    }
                    if (OnScreenPad(Candles[i].FlamePx, 180f)) {
                        Candles[i].Ttl = CandleTtl;
                    }
                }
                if (Candles[i].Ttl <= 0) {
                    Candles[i].Active = false;
                    continue;
                }

                //阴风进出沿：前沿压熄出烟，风尾复明闪焰（纯函数求值+缓存边沿）
                bool snuffedNow = WindSnuffedAt(Candles[i].FlamePx.X, Candles[i].Jitter01);
                if (snuffedNow != Candles[i].Snuffed) {
                    Candles[i].Snuffed = snuffedNow;
                    if (snuffedNow) {
                        SnuffPuff(Candles[i].FlamePx);
                    }
                    else {
                        RelightFlash(Candles[i].FlamePx);
                    }
                }
                if (Candles[i].Snuffed) {
                    //熄灭余烟：低概率细烟上散
                    if (Main.rand.NextFloat() < 0.012f * Presence) {
                        Dust wisp = Dust.NewDustPerfect(Candles[i].FlamePx, DustID.Smoke,
                            new Vector2(windDir * 0.6f, -0.5f), 190, default, 0.7f);
                        wisp.noGravity = true;
                    }
                    continue;
                }

                //幽蓝呼吸加光（AddLight 只能加不能减，"摇曳"是加光抖动的幻觉，不改灯帧）
                float breath = 0.5f
                    + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12.6f + Candles[i].Phase)
                    + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6.1f + Candles[i].Phase * 1.7f);
                float k = (0.028f + 0.022f * breath) * (1f + 0.5f * LocalGaze) * Presence;
                Vector3 cold = Vector3.Lerp(CandleCold, CandleColdGaze, gazeBlue);
                Lighting.AddLight(Candles[i].FlamePx, cold.X * k, cold.Y * k, cold.Z * k);

                //幽蓝烛光浮尘（常态预算主项，约 0.24 粒/帧全屏合计）
                if (Main.rand.NextFloat() < 0.02f * Presence) {
                    bool icy = Main.rand.NextFloat() < 0.3f + 0.4f * gazeBlue;
                    Dust mote = Dust.NewDustPerfect(
                        Candles[i].FlamePx + Main.rand.NextVector2Circular(4f, 3f),
                        icy ? DustID.IceTorch : DustID.BlueTorch,
                        new Vector2(Main.rand.NextFloat(-0.15f, 0.15f), -Main.rand.NextFloat(0.2f, 0.5f)),
                        120, default, Main.rand.NextFloat(0.55f, 0.85f));
                    mote.noGravity = true;
                }
                //被凝视反馈：烛火转蓝的冷焰上蹿（随凝视值加密）
                if (gazeBlue > 0f && Main.rand.NextFloat() < 0.02f * gazeBlue * Presence) {
                    Dust flare = Dust.NewDustPerfect(Candles[i].FlamePx, DustID.IceTorch,
                        new Vector2(0f, -Main.rand.NextFloat(0.5f, 0.9f)),
                        90, default, Main.rand.NextFloat(0.75f, 1.05f));
                    flare.noGravity = true;
                }
            }
        }

        /// <summary>压熄拍：烟三缕顺风散 + 一点垂死蓝烬</summary>
        private static void SnuffPuff(Vector2 flamePx) {
            for (int j = 0; j < 3; j++) {
                Dust smoke = Dust.NewDustPerfect(flamePx + Main.rand.NextVector2Circular(3f, 2f),
                    DustID.Smoke, new Vector2(windDir * Main.rand.NextFloat(1.6f, 3f), -Main.rand.NextFloat(0.2f, 0.7f)),
                    170, default, Main.rand.NextFloat(0.8f, 1.1f));
                smoke.noGravity = true;
            }
            Dust ember = Dust.NewDustPerfect(flamePx, DustID.BlueTorch,
                new Vector2(windDir * 1.2f, -0.3f), 150, default, 0.6f);
            ember.noGravity = true;
        }

        /// <summary>复明拍：蓝焰四溅 + 一记加光脉冲</summary>
        private static void RelightFlash(Vector2 flamePx) {
            for (int j = 0; j < 4; j++) {
                Dust flame = Dust.NewDustPerfect(flamePx, DustID.BlueTorch,
                    Main.rand.NextVector2Circular(1.4f, 1.1f) - new Vector2(0f, 0.6f),
                    100, default, Main.rand.NextFloat(0.7f, 1f));
                flame.noGravity = true;
            }
            Lighting.AddLight(flamePx, 0.22f, 0.3f, 0.5f);
        }

        //==================== 阴风过境的衣袂尘屑 ====================

        private static void UpdateWindStreaks(Player player) {
            if (WindDarken < 0.12f) {
                return;
            }
            //过境瞬时预算 ≈33 粒/秒，事件外归零
            if (Main.rand.NextFloat() >= 0.55f * WindDarken * Presence) {
                return;
            }
            Vector2 pos;
            if (Main.rand.NextBool()) {
                //贴身衣袂：从玩家身侧掠过
                pos = player.Center + new Vector2(Main.rand.NextFloat(-160f, 160f), Main.rand.NextFloat(-100f, 100f));
            }
            else {
                pos = Main.screenPosition + new Vector2(
                    Main.rand.NextFloat(Main.screenWidth), Main.rand.NextFloat(Main.screenHeight));
            }
            Dust streak = Dust.NewDustPerfect(pos, DustID.Smoke,
                new Vector2(windDir * Main.rand.NextFloat(6.5f, 10.5f), Main.rand.NextFloat(-0.6f, 0.6f)),
                170, default, Main.rand.NextFloat(0.7f, 1.15f));
            streak.noGravity = true;
        }

        //==================== 方位感稀疏底噪 ====================

        private static void UpdateCues(Player player) {
            FlushPendingCues();
            if (Presence < 0.55f || BossCalm < 0.8f || player.dead) {
                return;
            }
            if (--cueTimer <= 0) {
                cueTimer = Main.rand.Next(480, 960);
                int roll = Main.rand.Next(100);
                if (roll < 40) {
                    ChainCreak(player);
                }
                else if (roll < 70) {
                    DoorThud(player);
                }
                else {
                    GhostMoan(player);
                }
            }
            if (--boneTimer <= 0) {
                boneTimer = Main.rand.Next(1500, 2700);
                BoneDrag(player);
            }
        }

        /// <summary>锁链摇曳吱呀：优先锚在屏内吊挂灯具上（音画同点），无吊具则中距随机方位</summary>
        private static void ChainCreak(Player player) {
            Vector2 pos = default;
            bool found = false;
            int start = Main.rand.Next(Candles.Length);
            for (int i = 0; i < Candles.Length; i++) {
                int idx = (start + i) % Candles.Length;
                if (Candles[idx].Active && Candles[idx].Hanging) {
                    pos = Candles[idx].FlamePx;
                    found = true;
                    break;
                }
            }
            if (!found) {
                pos = player.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(420f, 720f);
            }
            SoundEngine.PlaySound(SoundID.DoorOpen with {
                Volume = 0.2f, Pitch = -0.5f + Main.rand.NextFloat(0.15f), MaxInstances = 2
            }, pos);
            if (found) {
                //吊具轻晃的两点火星，给声音一个可见锚
                for (int j = 0; j < 2; j++) {
                    Dust sway = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(6f, 3f),
                        DustID.BlueTorch, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), 0.2f),
                        140, default, 0.6f);
                    sway.noGravity = true;
                }
            }
        }

        /// <summary>远处铁门闷响：低哑一记 + 更弱的回声</summary>
        private static void DoorThud(Player player) {
            Vector2 pos = player.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(750f, 1150f);
            SoundEngine.PlaySound(SoundID.DoorClosed with {
                Volume = 0.34f, Pitch = -0.72f, MaxInstances = 2
            }, pos);
            QueueCue(SoundID.DoorClosed with { Volume = 0.14f, Pitch = -0.9f, MaxInstances = 2 }, pos, 16);
        }

        /// <summary>幽魂低语：远处呻吟，方位随机</summary>
        private static void GhostMoan(Player player) {
            Vector2 pos = player.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(600f, 1000f);
            SoundEngine.PlaySound(SoundID.ZombieMoan with {
                Volume = 0.2f, Pitch = -0.55f, MaxInstances = 2
            }, pos);
        }

        /// <summary>骨语回廊：走廊尽头低频骨骼拖行三连（水平方位偏置，越拖越远越轻）</summary>
        private static void BoneDrag(Player player) {
            int dir = Main.rand.NextBool() ? 1 : -1;
            Vector2 pos = player.Center + new Vector2(dir * Main.rand.NextFloat(520f, 900f), Main.rand.NextFloat(-90f, 90f));
            SoundEngine.PlaySound(SoundID.WormDigQuiet with {
                Volume = 0.34f, Pitch = -0.55f, MaxInstances = 2
            }, pos);
            QueueCue(SoundID.WormDigQuiet with { Volume = 0.28f, Pitch = -0.5f, MaxInstances = 2 },
                pos + new Vector2(dir * 40f, 0f), 30);
            QueueCue(SoundID.WormDigQuiet with { Volume = 0.18f, Pitch = -0.62f, MaxInstances = 2 },
                pos + new Vector2(dir * 90f, 0f), 62);
        }

        private static void QueueCue(SoundStyle style, Vector2 pos, int delay) {
            for (int i = 0; i < pendingCues.Length; i++) {
                if (pendingCues[i].Active) {
                    continue;
                }
                pendingCues[i] = new PendingCue { Active = true, Style = style, Pos = pos, Delay = delay };
                return;
            }
        }

        private static void FlushPendingCues() {
            for (int i = 0; i < pendingCues.Length; i++) {
                if (!pendingCues[i].Active) {
                    continue;
                }
                if (--pendingCues[i].Delay > 0) {
                    continue;
                }
                SoundEngine.PlaySound(pendingCues[i].Style, pendingCues[i].Pos);
                pendingCues[i].Active = false;
            }
        }

        //==================== 循环声槽（镜像 GhostRainAmbience/OldNetAmbience 的生命周期管理）====================

        private static void UpdateLoops() {
            if (Main.gameMenu || Presence < 0.02f) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(whisperSlot, out _)) {
                whisperSlot = SoundEngine.PlaySound(WhisperBedStyle, null, UpdateWhisperBed);
            }
            if (!SoundEngine.TryGetActiveSound(windSlot, out _)) {
                windSlot = SoundEngine.PlaySound(WindLoopStyle, null, UpdateWindLoop);
            }
        }

        /// <summary>低语声床：随凝视值渐清晰（音量升、音调回正），阴风里被风声轻轻托起</summary>
        private static bool UpdateWhisperBed(ActiveSound sound) {
            if (Presence <= 0.01f || Main.gameMenu) {
                return false;
            }
            sound.Volume = (0.06f + 0.24f * LocalGaze + 0.04f * windSoundEnv) * Presence * BossCalm;
            sound.Pitch = -0.78f + 0.36f * LocalGaze;
            sound.Position = null;
            return true;
        }

        private static bool UpdateWindLoop(ActiveSound sound) {
            if (Presence <= 0.01f || Main.gameMenu) {
                return false;
            }
            sound.Volume = windSoundEnv * Presence;
            sound.Pitch = -0.25f + 0.2f * WindDarken;
            sound.Position = null;
            return true;
        }

        private static bool OnScreenPad(Vector2 worldPx, float pad) {
            return worldPx.X > Main.screenPosition.X - pad
                && worldPx.X < Main.screenPosition.X + Main.screenWidth + pad
                && worldPx.Y > Main.screenPosition.Y - pad
                && worldPx.Y < Main.screenPosition.Y + Main.screenHeight + pad;
        }
    }
}
