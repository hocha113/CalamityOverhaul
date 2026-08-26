using InnoVault.PRT;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Woodsong
{
    /// <summary>
    /// 纯净森林地表环境氛围中枢（残酷模式）。四个具名特色：<br/>
    /// 「林语」白日花粉柳絮蝶尘缓飘+鸟鸣加密，黄昏萤火渐起，风大时叶浪波次+叶涌声；<br/>
    /// 「暮雾」夜间贴地薄雾（绘制在 <see cref="WoodsongMistRender"/>），雾中远狼嚎与枝裂声，只有声与雾，绝不生成敌怪；<br/>
    /// 「引路鬼火」夜雾中低频亮起的中性冷白鬼火，缓缓飘向最近洞口或开阔地（<see cref="PRT_WoodsongWisp"/>）；<br/>
    /// 「惊鸦」浓雾夜树冠黑影掠动+鸦群惊飞（<see cref="PRT_WoodsongRaven"/>）。<br/>
    /// 全部为本地客户端演出量（镜像 GhostRainAmbience/OldNetAmbience 的生命周期管理），
    /// 无伤害机制、无网络包；档位（EffectiveTier）只调雾浓度上限与鬼火频率。
    /// </summary>
    internal static class WoodsongAmbience
    {
        /// <summary>本地在场强度 0~1（进出群系缓变，不硬切）</summary>
        public static float Presence { get; private set; }

        /// <summary>暮雾当前浓度 0~1（已乘 Presence，雾层与音效调度共读）</summary>
        public static float FogStrength { get; private set; }

        /// <summary>叶浪阵风包络 0~1</summary>
        internal static float GustEnv { get; private set; }

        /// <summary>黄昏萤火窗 0~1</summary>
        internal static float DuskGlow { get; private set; }

        /// <summary>风力绝对值 0~1</summary>
        internal static float WindAbs { get; private set; }

        //==== 档位表：机制形状不变，只调浓度与频率（镜像 Wastes 的 ByTier 写法）====
        /// <summary>暮雾浓度上限，档位只调浓度</summary>
        private static readonly float[] FogDensityByTier = [0.45f, 0.62f, 0.80f];
        /// <summary>鬼火生成基准间隔（tick），档位只调频率</summary>
        private static readonly int[] WispIntervalByTier = [5400, 4200, 3100];

        //==== 环境音循环（镜像 OldNetAmbience 的 SlotId+回调惯例）====
        private static SlotId windBedSlot;
        private static readonly SoundStyle WindBedStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        private static float fogRaw;
        private static int birdIn = 300;
        private static int critterIn = 420;
        private static int owlIn = 1600;
        private static int howlIn = 1400;
        private static int snapIn = 800;
        private static int gustIn = 420;
        private static int gustTimer;
        private static int gustLen = 1;
        private static int ravenIn = 3200;
        private static int ravenBurstIn;
        private static Vector2 ravenBurstPos;
        private static int wispIn = 1800;
        private static float moteAcc;
        private static float fireflyAcc;
        private static float leafAcc;

        //延迟音效队列：给惊鸦扑翼、枝裂余响这类需要错拍的一次性声
        private struct PendingSound
        {
            internal int Delay;
            internal SoundStyle Style;
            internal Vector2 Pos;
        }

        private static readonly PendingSound[] pending = new PendingSound[10];

        internal static void Reset() {
            Presence = 0f;
            FogStrength = 0f;
            GustEnv = 0f;
            DuskGlow = 0f;
            WindAbs = 0f;
            fogRaw = 0f;
            birdIn = 300;
            critterIn = 420;
            owlIn = 1600;
            howlIn = 1400;
            snapIn = 800;
            gustIn = 420;
            gustTimer = 0;
            gustLen = 1;
            ravenIn = 3200;
            ravenBurstIn = 0;
            wispIn = 1800;
            moteAcc = fireflyAcc = leafAcc = 0f;
            for (int i = 0; i < pending.Length; i++) {
                pending[i].Delay = 0;
            }
            PRT_WoodsongWisp.LastBeat = 0;
            WoodsongMistRender.ClearBanks();
        }

        /// <summary>
        /// 干净的纯净地表判定：地表高度，且不属于任何其他群系/事件旗标；
        /// 灾厄群系经 CWRRef 守门排除（星辉瘟疫与硫磺海可及地表）
        /// </summary>
        internal static bool LocalInPureForest(Player player) {
            if (!player.ZoneOverworldHeight) {
                return false;
            }
            if (player.ZoneDesert || player.ZoneSnow || player.ZoneJungle
                || player.ZoneCorrupt || player.ZoneCrimson || player.ZoneHallow
                || player.ZoneGlowshroom || player.ZoneMeteor || player.ZoneGraveyard
                || player.ZoneBeach || player.ZoneDungeon || player.ZoneUndergroundDesert
                || player.ZoneGranite || player.ZoneMarble || player.ZoneHive
                || player.ZoneLihzhardTemple || player.ZoneShimmer
                || player.ZoneTowerSolar || player.ZoneTowerVortex
                || player.ZoneTowerNebula || player.ZoneTowerStardust
                || player.ZoneOldOneArmy) {
                return false;
            }
            if (CWRRef.Has && (player.GetPlayerZoneAstral() || player.GetPlayerZoneSulphur())) {
                return false;
            }
            return true;
        }

        internal static void Update() {
            if (Main.gameMenu) {
                Presence = 0f;
                fogRaw = 0f;
                FogStrength = 0f;
                GustEnv = 0f;
                return;
            }
            if (Main.gamePaused) {
                return;
            }

            Player player = Main.LocalPlayer;
            bool inZone = player != null && player.active
                && GameModeSystem.BrutalActive && LocalInPureForest(player);
            //Boss 在场：纯视觉氛围保留但减弱
            float target = inZone ? (CWRWorld.HasBoss ? 0.3f : 1f) : 0f;
            Presence = Math.Abs(target - Presence) < 0.004f
                ? target : MathHelper.Lerp(Presence, target, 0.03f);
            WindAbs = Math.Min(Math.Abs(Main.windSpeedCurrent), 1f);

            //「暮雾」浓度：入夜一小时缓升，黎明前半小时散尽；档位只调上限
            int tier = GameModeSystem.EffectiveTier;
            float fogTarget = 0f;
            if (!Main.dayTime && tier > 0) {
                float t = (float)Main.time;
                float ramp = Math.Min(Math.Min(t / 3600f, 1f), MathHelper.Clamp((32400f - t) / 1800f, 0f, 1f));
                fogTarget = ramp * FogDensityByTier[tier - 1];
            }
            fogRaw = MathHelper.Lerp(fogRaw, fogTarget, 0.006f);
            FogStrength = fogRaw * Presence;

            //黄昏萤火窗：日末渐起，前半夜盛，后半夜困倦，黎明前收
            if (Main.dayTime) {
                DuskGlow = MathHelper.Clamp(((float)Main.time - 46800f) / 7200f, 0f, 1f);
            }
            else {
                float t = (float)Main.time;
                DuskGlow = t < 14400f ? 1f : Math.Max(0.35f, 1f - (t - 14400f) / 14000f);
                DuskGlow = Math.Min(DuskGlow, MathHelper.Clamp((32400f - t) / 2400f, 0f, 1f));
            }

            if (Presence <= 0.02f) {
                GustEnv = 0f;
                return;
            }

            UpdateWindBedLoop();
            UpdateGust(player);
            UpdateSoundSchedulers(player);
            SpawnAmbientVisuals(player);
            UpdateRavenScare(player);
            UpdateWispScheduler(player);
            PumpPending();
        }

        //==================== 环境音循环 ====================

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
        private static void UpdateWindBedLoop() {
            if (!SoundEngine.TryGetActiveSound(windBedSlot, out _)) {
                windBedSlot = SoundEngine.PlaySound(WindBedStyle, null, UpdateWindBed);
            }
        }

        //林间风床：常态低吟，风大与叶浪时抬起，夜雾里再垫一层潮闷
        private static bool UpdateWindBed(ActiveSound sound) {
            if (Main.gameMenu || Presence < 0.015f) {
                return false;
            }
            sound.Volume = Presence * Math.Min(
                0.10f + WindAbs * 0.28f + GustEnv * 0.16f + FogStrength * 0.08f, 0.5f);
            sound.Pitch = -0.45f + WindAbs * 0.18f;
            sound.Position = null;
            return true;
        }

        //==================== 「林语」叶浪阵风 ====================

        private static void UpdateGust(Player player) {
            if (gustTimer > 0) {
                gustTimer--;
                GustEnv = MathF.Sin(MathHelper.Pi * (1f - gustTimer / (float)gustLen));
                if (gustTimer == 0) {
                    GustEnv = 0f;
                }
            }
            else {
                GustEnv = 0f;
            }
            //设计意图：雨天叶片被打湿难以成浪，无论昼夜都要求更强的风（0.6）才起叶浪；晴时 0.42
            float gustGate = Main.raining ? 0.6f : 0.42f;
            if (WindAbs < gustGate) {
                return;
            }
            if (--gustIn > 0) {
                return;
            }
            gustIn = Main.rand.Next(330, 620);
            gustLen = gustTimer = Main.rand.Next(120, 190);
            //叶浪起势：两层草叶婆娑声自上风处压来
            Vector2 pos = player.Center + new Vector2(Main.windSpeedCurrent > 0f ? -300f : 300f, -140f);
            SoundEngine.PlaySound(SoundID.Grass with {
                Volume = 0.30f + WindAbs * 0.22f, Pitch = -0.28f, MaxInstances = 3
            }, pos);
            Enqueue(12, SoundID.Grass with {
                Volume = 0.24f + WindAbs * 0.16f, Pitch = 0.02f, MaxInstances = 3
            }, pos + new Vector2(120f, 30f));
        }

        //==================== 一次性环境声调度 ====================

        private static void UpdateSoundSchedulers(Player player) {
            if (Main.dayTime) {
                //「林语」鸟鸣加密：雨天三停其二
                if (--birdIn <= 0) {
                    birdIn = Main.rand.Next(300, 760);
                    if (!Main.raining || Main.rand.NextBool(3)) {
                        Vector2 pos = player.Center + new Vector2(
                            Main.rand.NextFloat(-640f, 640f), -Main.rand.NextFloat(90f, 320f));
                        SoundEngine.PlaySound(SoundID.Bird with { Volume = 0.34f }, pos);
                    }
                }
                return;
            }

            //夜虫低吟与枭鸣：夜的底噪
            if (--critterIn <= 0) {
                critterIn = Main.rand.Next(420, 900);
                Vector2 pos = player.Center + new Vector2(
                    (Main.rand.NextBool() ? 1f : -1f) * Main.rand.NextFloat(160f, 480f),
                    Main.rand.NextFloat(-30f, 20f));
                SoundEngine.PlaySound(SoundID.Critter with { Volume = 0.30f }, pos);
            }
            if (--owlIn <= 0) {
                owlIn = Main.rand.Next(1500, 4200);
                Vector2 pos = player.Center + new Vector2(
                    (Main.rand.NextBool() ? 1f : -1f) * Main.rand.NextFloat(280f, 820f),
                    -Main.rand.NextFloat(80f, 260f));
                SoundEngine.PlaySound(SoundID.Owl with { Volume = 0.40f, Pitch = -0.04f }, pos);
            }

            //「暮雾」雾中远嚎与枝裂：与夜袭狼群同一声部（ZombieMoan）遥相呼应，但这里只有声音
            if (FogStrength < 0.28f) {
                return;
            }
            if (--howlIn <= 0) {
                howlIn = (int)(Main.rand.Next(1080, 2300) / (0.5f + FogStrength));
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 pos = player.Center + new Vector2(
                    side * Main.rand.NextFloat(850f, 1350f), Main.rand.NextFloat(-240f, 40f));
                SoundEngine.PlaySound(SoundID.ZombieMoan with {
                    Volume = 0.30f + FogStrength * 0.18f,
                    Pitch = -0.62f + Main.rand.NextFloat(0.12f),
                    MaxInstances = 2
                }, pos);
            }
            if (--snapIn <= 0) {
                snapIn = Main.rand.Next(520, 1150);
                Vector2 pos = player.Center + new Vector2(
                    (Main.rand.NextBool() ? 1f : -1f) * Main.rand.NextFloat(380f, 880f),
                    Main.rand.NextFloat(-80f, 40f));
                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = 0.30f, Pitch = -0.55f + Main.rand.NextFloat(0.1f), MaxInstances = 2
                }, pos);
                //六成概率补一声枝叶簌簌的余响
                if (Main.rand.Next(5) < 3) {
                    Enqueue(Main.rand.Next(5, 12), SoundID.Grass with {
                        Volume = 0.20f, Pitch = -0.2f, MaxInstances = 3
                    }, pos);
                }
            }
        }

        //==================== 「林语」常态视觉粒子 ====================

        private static void SpawnAmbientVisuals(Player player) {
            //白日光尘：花粉/柳絮/蝶尘（雨天停）
            if (Main.dayTime && !Main.raining) {
                moteAcc += 0.10f * Presence;
                while (moteAcc >= 1f) {
                    moteAcc -= 1f;
                    SpawnAirMote();
                }
            }
            //黄昏萤火渐起
            if (DuskGlow > 0.05f && !Main.raining) {
                fireflyAcc += 0.05f * DuskGlow * Presence;
                while (fireflyAcc >= 1f) {
                    fireflyAcc -= 1f;
                    SpawnFirefly(player);
                }
            }
            //叶浪波次：只在阵风包络内成波成浪
            if (GustEnv > 0.1f) {
                leafAcc += 0.15f * GustEnv * Presence;
                while (leafAcc >= 1f) {
                    leafAcc -= 1f;
                    SpawnGustLeaf();
                }
            }
        }

        private static void SpawnAirMote() {
            Vector2 pos = Main.screenPosition + new Vector2(
                Main.rand.NextFloat(-80f, Main.screenWidth + 80f),
                Main.rand.NextFloat(Main.screenHeight));
            if (!AirAndOutdoor(pos)) {
                return;
            }
            int roll = Main.rand.Next(100);
            if (roll < 55) {
                //花粉：暖金细尘
                PRTLoader.NewParticle<PRT_WoodsongMote>(pos,
                    new Vector2(Main.windSpeedCurrent, 0.2f),
                    new Color(255, 232, 170) * 0.55f, Main.rand.NextFloat(0.05f, 0.08f))
                    ?.Configure(PRT_WoodsongMote.ModePollen, Main.rand.Next(200, 320));
            }
            else if (roll < 85) {
                //柳絮：白绒慢荡
                PRTLoader.NewParticle<PRT_WoodsongMote>(pos,
                    new Vector2(Main.windSpeedCurrent * 1.5f, 0.1f),
                    new Color(235, 240, 233) * 0.6f, Main.rand.NextFloat(0.08f, 0.12f))
                    ?.Configure(PRT_WoodsongMote.ModeCatkin, Main.rand.Next(260, 400));
            }
            else {
                //蝶尘：暖光小点盘卷
                PRTLoader.NewParticle<PRT_WoodsongMote>(pos,
                    Main.rand.NextVector2Circular(0.5f, 0.4f),
                    new Color(255, 210, 122) * 0.6f, Main.rand.NextFloat(0.06f, 0.08f))
                    ?.Configure(PRT_WoodsongMote.ModeButterfly, Main.rand.Next(220, 340));
            }
        }

        private static void SpawnFirefly(Player player) {
            int tileX = (int)(player.Center.X / 16f) + Main.rand.Next(-40, 41);
            if (!TryFindOutdoorSurface(tileX, out int surfY)) {
                return;
            }
            Vector2 pos = new(tileX * 16f + 8f, surfY * 16f - Main.rand.NextFloat(12f, 90f));
            PRTLoader.NewParticle<PRT_WoodsongMote>(pos, Vector2.Zero,
                new Color(186, 240, 120) * 0.9f, Main.rand.NextFloat(0.05f, 0.07f))
                ?.Configure(PRT_WoodsongMote.ModeFirefly, Main.rand.Next(420, 720));
        }

        private static void SpawnGustLeaf() {
            //从上风侧屏缘涌进来，横飞成浪
            bool windRight = Main.windSpeedCurrent > 0f;
            float x = windRight
                ? Main.screenPosition.X - Main.rand.NextFloat(40f, 160f)
                : Main.screenPosition.X + Main.screenWidth + Main.rand.NextFloat(40f, 160f);
            Vector2 pos = new(x, Main.screenPosition.Y + Main.rand.NextFloat(0.15f, 0.7f) * Main.screenHeight);
            float drive = Main.windSpeedCurrent * Main.rand.NextFloat(3.0f, 4.5f);
            PRTLoader.NewParticle<PRT_WoodsongLeaf>(pos,
                new Vector2(drive * 0.6f, Main.rand.NextFloat(0.2f, 0.8f)),
                Color.White, Main.rand.NextFloat(0.8f, 1.15f))
                ?.Configure(drive, Main.rand.Next(150, 240));
        }

        //==================== 「惊鸦」====================

        private static void UpdateRavenScare(Player player) {
            //第二拍：黑影掠过 20 tick 后鸦群自树冠惊飞
            if (ravenBurstIn > 0 && --ravenBurstIn == 0) {
                Main.instance.LoadNPC(NPCID.Raven);
                int birds = Main.rand.Next(3, 6);
                for (int i = 0; i < birds; i++) {
                    PRTLoader.NewParticle<PRT_WoodsongRaven>(
                        ravenBurstPos + Main.rand.NextVector2Circular(26f, 14f),
                        new Vector2((Main.rand.NextBool() ? 1f : -1f) * Main.rand.NextFloat(1.0f, 2.2f),
                            -Main.rand.NextFloat(1.6f, 2.6f)),
                        Color.White, Main.rand.NextFloat(0.72f, 1.02f))
                        ?.Configure(PRT_WoodsongRaven.ModeBird, Main.rand.Next(88, 132));
                }
                int shed = Main.rand.Next(7, 12);
                for (int i = 0; i < shed; i++) {
                    PRTLoader.NewParticle<PRT_WoodsongLeaf>(
                        ravenBurstPos + Main.rand.NextVector2Circular(34f, 18f),
                        new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), Main.rand.NextFloat(-0.5f, 0.8f)),
                        Color.White, Main.rand.NextFloat(0.8f, 1.1f))
                        ?.Configure(Main.windSpeedCurrent * 1.5f, Main.rand.Next(110, 170));
                }
                SoundEngine.PlaySound(SoundID.Grass with {
                    Volume = 0.40f, Pitch = -0.08f, MaxInstances = 3
                }, ravenBurstPos);
                for (int k = 0; k < 3; k++) {
                    Enqueue(6 + k * 9, SoundID.Item32 with {
                        Volume = 0.30f, Pitch = 0.22f + k * 0.14f, MaxInstances = 4
                    }, ravenBurstPos + new Vector2(k * 22f - 22f, -k * 16f));
                }
            }

            //触发门：夜里雾浓才有惊鸦
            if (Main.dayTime || FogStrength < 0.5f) {
                return;
            }
            if (--ravenIn > 0) {
                return;
            }
            if (!TryFindTreetop(player, out Vector2 top)) {
                ravenIn = 600;
                return;
            }
            ravenIn = Main.rand.Next(2700, 6000);
            ravenBurstPos = top;
            ravenBurstIn = 20;
            //第一拍：树影错动，黑影自树冠掠过+一声轻响
            float dir = Main.rand.NextBool() ? 1f : -1f;
            PRTLoader.NewParticle<PRT_WoodsongRaven>(top + new Vector2(-dir * 46f, -8f),
                new Vector2(dir * 2.8f, 0.2f), Color.White, 1f)
                ?.Configure(PRT_WoodsongRaven.ModeShade, 26);
            SoundEngine.PlaySound(SoundID.Grass with {
                Volume = 0.22f, Pitch = -0.35f, MaxInstances = 3
            }, top);
        }

        //==================== 「引路鬼火」====================

        private static void UpdateWispScheduler(Player player) {
            if (Main.dayTime || FogStrength < 0.32f) {
                return;
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            if (--wispIn > 0) {
                return;
            }
            int baseIv = WispIntervalByTier[tier - 1];
            wispIn = baseIv + Main.rand.Next(-baseIv / 3, baseIv / 3);
            //单只上限：场上已有活鬼火就跳过本轮
            if (PRT_WoodsongWisp.AliveRecently) {
                return;
            }
            int sx = (int)(player.Center.X / 16f)
                + (Main.rand.NextBool() ? 1 : -1) * Main.rand.Next(16, 29);
            if (!TryFindOutdoorSurface(sx, out int surfY)) {
                return;
            }
            Vector2 spawn = new(sx * 16f + 8f, surfY * 16f - Main.rand.NextFloat(50f, 120f));
            Vector2 target = FindWispTarget(player);
            var wisp = PRTLoader.NewParticle<PRT_WoodsongWisp>(spawn, new Vector2(0f, -0.2f),
                Color.White, 1f)?.Configure(target, Main.rand.Next(1050, 1500));
            if (wisp != null) {
                //幽幽亮起的一声软吟：可读性的听觉通道
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.22f, Pitch = -0.4f }, spawn);
            }
        }

        /// <summary>
        /// 鬼火去处：优先最近的洞口（地表相对基准线骤降 8 格以上的列），
        /// 其次远处贴近基准线的开阔地表，都找不到就顺风远点
        /// </summary>
        private static Vector2 FindWispTarget(Player player) {
            int px = (int)(player.Center.X / 16f);
            Span<int> xs = stackalloc int[29];
            Span<int> ys = stackalloc int[29];
            int n = 0;
            for (int i = -14; i <= 14; i++) {
                int x = px + i * 4;
                if (TryFindOutdoorSurface(x, out int sy)) {
                    xs[n] = x;
                    ys[n] = sy;
                    n++;
                }
            }
            Vector2 downwind = player.Center + new Vector2(
                Main.windSpeedCurrent >= 0f ? 520f : -520f, -40f);
            if (n < 6) {
                return downwind;
            }

            //中位地平线（插入排序小数组，零分配）
            Span<int> sorted = stackalloc int[29];
            ys[..n].CopyTo(sorted);
            for (int i = 1; i < n; i++) {
                int v = sorted[i];
                int j = i - 1;
                while (j >= 0 && sorted[j] > v) {
                    sorted[j + 1] = sorted[j];
                    j--;
                }
                sorted[j + 1] = v;
            }
            int median = sorted[n / 2];

            //洞口：取最近的骤降列，悬点压在开口上方
            int best = -1;
            int bestDist = int.MaxValue;
            for (int i = 0; i < n; i++) {
                if (ys[i] >= median + 8) {
                    int d = Math.Abs(xs[i] - px);
                    if (d >= 8 && d < bestDist) {
                        bestDist = d;
                        best = i;
                    }
                }
            }
            if (best >= 0) {
                return new Vector2(xs[best] * 16f + 8f, (median + 4) * 16f);
            }

            //开阔地表：取最远且贴近基准线的列
            best = -1;
            bestDist = -1;
            for (int i = 0; i < n; i++) {
                int d = Math.Abs(xs[i] - px);
                if (d >= 20 && Math.Abs(ys[i] - median) <= 3 && d > bestDist) {
                    bestDist = d;
                    best = i;
                }
            }
            if (best >= 0) {
                return new Vector2(xs[best] * 16f + 8f, ys[best] * 16f - 60f);
            }
            return downwind;
        }

        //==================== 地形采样与延迟音效 ====================

        /// <summary>
        /// 找露天地表：自玩家高度向下走到首个实心格；上方那格必须无墙、无深液体。
        /// 供雾团锚定、萤火与鬼火落位、树冠扫描共用
        /// </summary>
        internal static bool TryFindOutdoorSurface(int tileX, out int surfaceY) {
            surfaceY = 0;
            if (tileX < 20 || tileX >= Main.maxTilesX - 20) {
                return false;
            }
            Player player = Main.LocalPlayer;
            int yStart = Math.Max((int)(player.Center.Y / 16f) - 64, 24);
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

        /// <summary>找玩家附近一棵够高的树，返回树冠位置（自地面沿树干上爬）</summary>
        private static bool TryFindTreetop(Player player, out Vector2 top) {
            top = default;
            int px = (int)(player.Center.X / 16f);
            for (int attempt = 0; attempt < 14; attempt++) {
                int x = px + (Main.rand.NextBool() ? 1 : -1) * Main.rand.Next(8, 36);
                if (!TryFindOutdoorSurface(x, out int surfY)) {
                    continue;
                }
                int trunk = 0;
                while (trunk < 40) {
                    Tile t = Framing.GetTileSafely(x, surfY - 1 - trunk);
                    if (!t.HasTile || t.TileType != TileID.Trees) {
                        break;
                    }
                    trunk++;
                }
                if (trunk < 6) {
                    continue;
                }
                top = new Vector2(x * 16f + 8f, (surfY - trunk) * 16f - 12f);
                return true;
            }
            return false;
        }

        private static void Enqueue(int delay, SoundStyle style, Vector2 pos) {
            for (int i = 0; i < pending.Length; i++) {
                if (pending[i].Delay <= 0) {
                    pending[i] = new PendingSound { Delay = delay, Style = style, Pos = pos };
                    return;
                }
            }
        }

        private static void PumpPending() {
            for (int i = 0; i < pending.Length; i++) {
                if (pending[i].Delay <= 0) {
                    continue;
                }
                if (--pending[i].Delay == 0) {
                    SoundEngine.PlaySound(pending[i].Style, pending[i].Pos);
                }
            }
        }
    }

    internal class WoodsongAmbienceSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                WoodsongAmbience.Update();
            }
        }

        public override void ClearWorld() {
            if (!Main.dedServ) {
                WoodsongAmbience.Reset();
            }
        }

        //暮雾把夜色勒向冷灰青：幅度压在氛围级（远轻于鬼雨的压顶）
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float fog = WoodsongAmbience.FogStrength;
            if (fog <= 0.01f) {
                return;
            }
            Color mistTile = new(56, 64, 80);
            Color mistBg = new(38, 46, 62);
            tileColor = Color.Lerp(tileColor, mistTile, fog * 0.18f);
            backgroundColor = Color.Lerp(backgroundColor, mistBg, fog * 0.26f);
        }
    }
}
