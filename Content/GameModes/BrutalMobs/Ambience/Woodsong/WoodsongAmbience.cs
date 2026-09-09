using CalamityOverhaul.Common;
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
    /// 「惊鸦」屏外树冠或地面落一群栖息的渡鸦（<see cref="PRT_WoodsongRaven"/>），玩家走近就惊飞；
    /// 屏外有敌怪锁定玩家时鸟群骚动后背离来敌惊飞，浓雾夜里也会被看不见的东西惊起。
    /// 鸟从不在屏内凭空出现或消失：屏外落位、屏外销毁，没有栖息群时报敌改为从威胁侧屏缘外横穿。<br/>
    /// 氛围层为本地客户端演出（镜像 GhostRainAmbience/OldNetAmbience 的生命周期管理，无网络包）；
    /// 战斗中的荆棘丛由 <see cref="WoodsongBrambleSystem"/> 权威端投放，不在本类判定。
    /// 档位（EffectiveTier）只调雾浓度上限与鬼火频率。<br/>
    /// Boss 战中视觉氛围以低 Presence 弱化保留，一次性环境声与新排演出全部冻结，
    /// 只留开场一次「惊鸦报敌」（见 <see cref="UpdateThreatWarning"/>）。
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
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 0 };

        private static float fogRaw;
        private static int birdIn = 300;
        private static int critterIn = 420;
        private static int owlIn = 1600;
        private static int howlIn = 1400;
        private static int snapIn = 800;
        private static int gustIn = 420;
        private static int gustTimer;
        private static int gustLen = 1;
        /// <summary>雾夜惊鸦日程（帧）</summary>
        private static int ravenIn = 3200;
        /// <summary>栖息群补位日程（帧）：无群时到点在屏外落一群</summary>
        private static int roostIn = 900;
        /// <summary>威胁预警冷却（独立于雾夜惊鸦的日程）</summary>
        private static int warnIn = 600;
        /// <summary>Boss 开场预警窗（帧）：惊鸦报敌只许落在开场这一小段内</summary>
        private const int BossWarnWindowFrames = 240;
        /// <summary>上一帧 Boss 是否在场（取上升沿用）</summary>
        private static bool bossWasUp;
        /// <summary>Boss 开场预警窗余量；归零后整场不再放送任何惊鸦</summary>
        private static int bossWarnGrace;
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
            roostIn = 900;
            warnIn = 600;
            bossWasUp = false;
            bossWarnGrace = 0;
            wispIn = 1800;
            moteAcc = fireflyAcc = leafAcc = 0f;
            for (int i = 0; i < pending.Length; i++) {
                pending[i].Delay = 0;
            }
            PRT_WoodsongWisp.LastBeat = 0;
            PRT_WoodsongRaven.ResetRegistry();
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
            bool bossUp = CWRWorld.HasBoss;
            //Boss 在场：纯视觉氛围保留但减弱
            float target = inZone ? (bossUp ? 0.3f : 1f) : 0f;
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
            UpdateGust(player, bossUp);
            if (!bossUp) {
                //Boss 战中冻结鸟鸣/虫吟/枭鸣/远嚎/枝裂的排程：计时器停摆，战后原地续拍
                UpdateSoundSchedulers(player);
            }
            SpawnAmbientVisuals(player);
            UpdateRoostScheduler(player, bossUp);
            UpdateRavenScare(player, bossUp);
            UpdateThreatWarning(player, bossUp);
            UpdateWispScheduler(player, bossUp);
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

        private static void UpdateGust(Player player, bool bossUp) {
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
            if (bossUp) {
                //Boss 战中叶浪只留画面，不出声
                return;
            }
            //叶浪起势：两层草叶婆娑声自上风处压来
            Vector2 pos = player.Center + new Vector2(Main.windSpeedCurrent > 0f ? -300f : 300f, -140f);
            SoundEngine.PlaySound(SoundID.Grass with {
                Volume = 0.30f + WindAbs * 0.22f,
                Pitch = -0.28f,
                MaxInstances = 3
            }, pos);
            Enqueue(12, SoundID.Grass with {
                Volume = 0.24f + WindAbs * 0.16f,
                Pitch = 0.02f,
                MaxInstances = 3
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
                    Volume = 0.30f,
                    Pitch = -0.55f + Main.rand.NextFloat(0.1f),
                    MaxInstances = 2
                }, pos);
                //六成概率补一声枝叶簌簌的余响
                if (Main.rand.Next(5) < 3) {
                    Enqueue(Main.rand.Next(5, 12), SoundID.Grass with {
                        Volume = 0.20f,
                        Pitch = -0.2f,
                        MaxInstances = 3
                    }, pos);
                }
            }
        }

        //==================== 「林语」常态视觉粒子 ====================

        private static void SpawnAmbientVisuals(Player player) {
            //氛围性能总闸：只缩装饰粒子密度，不碰任何机制/预告/危害路径
            float density = CWRClientConfig.Instance.AmbienceDensity;
            //白日光尘：花粉/柳絮/蝶尘（雨天停）
            if (Main.dayTime && !Main.raining) {
                moteAcc += 0.10f * Presence * density;
                while (moteAcc >= 1f) {
                    moteAcc -= 1f;
                    SpawnAirMote();
                }
            }
            //黄昏萤火渐起
            if (DuskGlow > 0.05f && !Main.raining) {
                fireflyAcc += 0.05f * DuskGlow * Presence * density;
                while (fireflyAcc >= 1f) {
                    fireflyAcc -= 1f;
                    SpawnFirefly(player);
                }
            }
            //叶浪波次：只在阵风包络内成波成浪
            if (GustEnv > 0.1f) {
                leafAcc += 0.15f * GustEnv * Presence * density;
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

        //==================== 「栖息鸦群」====================

        /// <summary>栖息群落位距屏缘的外扩下限/上限（像素）：玩家看不见落位，走过去才发现</summary>
        private const float RoostOffscreenMin = 200f;
        private const float RoostOffscreenMax = 600f;
        /// <summary>栖息时长范围（帧），到点后不在屏内就静默回收、在屏内则自行飞走</summary>
        private const int RoostLifeMin = 1800;
        private const int RoostLifeMax = 3600;
        /// <summary>报敌/雾夜能调用栖息群的最大距离（像素）：覆盖屏外落位带</summary>
        private const float FlockReachRange = 1800f;

        /// <summary>无栖息群时按日程在屏外落一群，一次只养一群</summary>
        private static void UpdateRoostScheduler(Player player, bool bossUp) {
            if (PRT_WoodsongRaven.HasPerchedFlock) {
                return;
            }
            if (--roostIn > 0) {
                return;
            }
            roostIn = Main.rand.Next(1200, 2400);
            if (bossUp) {
                //Boss 战中不补新群（新演出排程冻结）
                return;
            }
            //偏向玩家行进方向落位，静止时随机一侧
            float side = Math.Abs(player.velocity.X) > 0.5f
                ? Math.Sign(player.velocity.X) : (Main.rand.NextBool() ? 1f : -1f);
            float edgeX = side > 0f ? Main.screenPosition.X + Main.screenWidth : Main.screenPosition.X;
            int tileX = (int)((edgeX + side * Main.rand.NextFloat(RoostOffscreenMin, RoostOffscreenMax)) / 16f);
            if (!TryFindRoost(tileX, out Vector2 foot, out bool treetop)) {
                //这一侧没处落脚：很快再试
                roostIn = 240;
                return;
            }
            SeedFlock(foot, treetop, Main.rand.Next(2, 6));
        }

        /// <summary>
        /// 在脚点附近落 birds 只栖息鸟。鸟宽约 36px，落点按序号等距排开再加小抖动，不许叠成一团黑：
        /// 树冠只容 3 只（冠宽约 80px，间距 30px），地面每 3 格一只并逐列探地防生进坡里
        /// </summary>
        private static void SeedFlock(Vector2 foot, bool treetop, int birds) {
            int roostLife = Main.rand.Next(RoostLifeMin, RoostLifeMax);
            int centerTileX = (int)(foot.X / 16f);
            if (treetop) {
                birds = Math.Min(birds, 3);
            }
            for (int i = 0; i < birds; i++) {
                float slot = i - (birds - 1) * 0.5f;
                Vector2 spot;
                if (treetop) {
                    spot = foot + new Vector2(slot * 30f + Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-8f, 4f));
                }
                else {
                    int tx = centerTileX + (int)MathF.Round(slot * 3f);
                    if (!TryFindOutdoorSurface(tx, out int sy)) {
                        continue;
                    }
                    spot = new Vector2(tx * 16f + 8f + Main.rand.NextFloat(-4f, 4f), sy * 16f);
                }
                PRTLoader.NewParticle<PRT_WoodsongRaven>(spot, Vector2.Zero, Color.White,
                    Main.rand.NextFloat(0.72f, 1.02f))
                    ?.ConfigurePerch(spot, 0, roostLife + Main.rand.Next(-120, 121), treetop);
            }
        }

        /// <summary>没有栖息群时的报敌兜底：从威胁侧屏缘外飞入，横穿屏幕逃向安全侧</summary>
        private static void SpawnFlyThrough(Player player, float fromSide, int birds) {
            float edgeX = fromSide > 0f ? Main.screenPosition.X + Main.screenWidth : Main.screenPosition.X;
            for (int i = 0; i < birds; i++) {
                Vector2 pos = new(edgeX + fromSide * Main.rand.NextFloat(60f, 200f),
                    player.Center.Y - Main.rand.NextFloat(60f, 200f));
                Vector2 vel = new(-fromSide * Main.rand.NextFloat(3.0f, 3.8f), -Main.rand.NextFloat(0.2f, 0.6f));
                PRTLoader.NewParticle<PRT_WoodsongRaven>(pos, vel, Color.White,
                    Main.rand.NextFloat(0.72f, 1.02f))
                    ?.ConfigureFlight(Main.rand.Next(1, 12));
            }
        }

        //==================== 「惊鸦」（雾夜惊吓）====================

        private static void UpdateRavenScare(Player player, bool bossUp) {
            //触发门：夜里雾浓才有惊鸦；Boss 战中不排新惊吓
            if (Main.dayTime || bossUp || FogStrength < 0.5f) {
                return;
            }
            if (--ravenIn > 0) {
                return;
            }
            ravenIn = Main.rand.Next(2700, 6000);
            if (!PRT_WoodsongRaven.HasPerchedFlock) {
                //没有栖息群就先催一群落到屏外，惊吓留给下一轮
                roostIn = Math.Min(roostIn, 30);
                return;
            }
            Vector2 anchor = PRT_WoodsongRaven.PerchAnchor;
            if (Vector2.Distance(anchor, player.Center) > FlockReachRange) {
                return;
            }
            //雾里有什么东西经过：远处一声枝响作"来处"，鸟群骚动后随机一侧惊飞
            SoundEngine.PlaySound(SoundID.Grass with {
                Volume = 0.22f,
                Pitch = -0.35f,
                MaxInstances = 3
            }, anchor + new Vector2(Main.rand.NextFloat(-120f, 120f), 20f));
            PRT_WoodsongRaven.FlushFlock(Main.rand.NextBool() ? 1f : -1f, agitate: true);
        }

        //==================== 「惊鸦预警」（威胁方向情报） ====================

        /// <summary>预警感知半径（像素）</summary>
        private const float WarnRange = 1100f;
        /// <summary>预警触发后的冷却（帧）</summary>
        private const int WarnCooldown = 1080;
        /// <summary>屏内判定的外扩（像素）：屏内的敌人玩家自己看得见，不报</summary>
        private const int ThreatOnScreenFluff = 40;

        /// <summary>
        /// 敌讯惊鸦：有敌对个体锁定本地玩家、进入感知圈且不在屏内时，栖息鸦群骚动后
        /// 整齐背离来敌惊飞，鸟群逃离的反方向就是敌人来向；没有栖息群则从威胁侧屏缘外横穿。
        /// 屏内的敌人不报（用户裁定 2026-09-09），鸟群只报玩家看不见的那一个。
        /// 纯本地情报演出（读的都是已同步的 NPC 状态），不做任何判定改动。<br/>
        /// Boss 战只在开场预警窗内放送一次（<see cref="BossWarnWindowFrames"/>），战中不复读
        /// </summary>
        private static void UpdateThreatWarning(Player player, bool bossUp) {
            if (bossUp && !bossWasUp) {
                //Boss 刚出场：压掉残余冷却，让报敌落在开场几拍内，而不是战中某个随机时刻
                warnIn = Math.Min(warnIn, 20);
                bossWarnGrace = BossWarnWindowFrames;
            }
            bossWasUp = bossUp;
            if (bossUp) {
                //开场窗耗尽或已警过一次：整场 Boss 战不再有任何鸦群惊飞
                if (bossWarnGrace <= 0) {
                    return;
                }
                bossWarnGrace--;
            }
            if (--warnIn > 0) {
                return;
            }
            warnIn = 90;//未触发时低频重扫

            NPC threat = null;
            float best = WarnRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || npc.damage <= 0 || npc.lifeMax <= 5 || npc.SpawnedFromStatue) {
                    continue;
                }
                if (!npc.HasValidTarget || npc.target != player.whoAmI) {
                    continue;
                }
                //屏内的敌人不报：预警只对看不见的来敌才有情报价值
                if (VaultUtils.IsPointOnScreen(npc.Center - Main.screenPosition, ThreatOnScreenFluff)) {
                    continue;
                }
                float dist = npc.Distance(player.Center);
                if (dist < best) {
                    best = dist;
                    threat = npc;
                }
            }
            if (threat == null) {
                return;
            }
            if (bossUp) {
                //开场只警这一次
                bossWarnGrace = 0;
            }
            warnIn = WarnCooldown;

            float threatSide = threat.Center.X >= player.Center.X ? 1f : -1f;
            if (PRT_WoodsongRaven.HasPerchedFlock
                && Vector2.Distance(PRT_WoodsongRaven.PerchAnchor, player.Center) < FlockReachRange) {
                //有栖息群：骚动一拍（转头、小跳、一声鸦鸣）再背离来敌惊飞
                PRT_WoodsongRaven.FlushFlock(-threatSide, agitate: true);
                return;
            }
            //无栖息群：鸟群自威胁侧屏缘外飞入横穿，逃向安全侧
            SpawnFlyThrough(player, threatSide, Main.rand.Next(3, 6));
        }

        //==================== 「引路鬼火」====================

        private static void UpdateWispScheduler(Player player, bool bossUp) {
            //Boss 战中不排新鬼火（连带亮起的软吟一起静默）
            if (Main.dayTime || bossUp || FogStrength < 0.32f) {
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
        internal static bool TryFindOutdoorSurface(int tileX, out int surfaceY)
            => TryFindOutdoorSurfaceFor(Main.LocalPlayer, tileX, out surfaceY);

        /// <summary>锚定指定玩家的露天地表扫描：服务端荆棘布点复用，不读 LocalPlayer</summary>
        internal static bool TryFindOutdoorSurfaceFor(Player anchor, int tileX, out int surfaceY) {
            surfaceY = 0;
            if (tileX < 20 || tileX >= Main.maxTilesX - 20) {
                return false;
            }
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

        /// <summary>
        /// 在指定列附近找鸟群落脚点：优先 ±3 格内一棵够高的树（自地面沿树干上爬，脚点落在树冠上部），
        /// 其次该列的露天地面。全部找不到返回 false
        /// </summary>
        private static bool TryFindRoost(int tileX, out Vector2 foot, out bool treetop) {
            foot = default;
            treetop = false;
            for (int dx = 0; dx <= 3; dx++) {
                for (int s = -1; s <= 1; s += 2) {
                    if (dx == 0 && s > 0) {
                        continue;
                    }
                    int x = tileX + dx * s;
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
                    //树冠贴图在顶端干格上方展开，脚点抬到冠的上部
                    foot = new Vector2(x * 16f + 8f, (surfY - trunk) * 16f - 34f);
                    treetop = true;
                    return true;
                }
            }
            if (TryFindOutdoorSurface(tileX, out int groundY)) {
                foot = new Vector2(tileX * 16f + 8f, groundY * 16f);
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
