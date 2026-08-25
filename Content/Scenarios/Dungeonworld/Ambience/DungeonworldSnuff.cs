using CalamityOverhaul.Content.Scenarios.Dungeonworld.Fog;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Ambience
{
    /// <summary>
    /// 熄灯惊魂（WAVE2-ATMOSPHERE E-3）：一次性 jump-scare 光照事件，只投 L2/L5，
    /// 每层每次进入至多一次。五拍时间轴：预兆烬闪 → 硬切黑 → 黑暗里两声接近 →
    /// 三拍哆嗦回亮 → 余韵。<br/>
    /// 权威端+同步字段：无。刻意的每客户端本地事件——各人被吓的时机不同，
    /// 正是"独自遇鬼"的叙事（与雾同款客户端口径）。本类 static 状态只是本地演出进度，
    /// 非 per-player 游戏状态，netcode 静态禁令不适用。<br/>
    /// 不改任何灯 tile 帧（那是世界状态，且是 L3 灭灯玩法的地盘）；不生成任何实体。
    /// </summary>
    internal class DungeonworldSnuff : ModSystem
    {
        //==== Debug 静态口 ====
        /// <summary>总开关：关掉零残留（光敏感降级口，直连未来客户端配置位）</summary>
        internal static bool Disable;

        /// <summary>立即武装：跳过入带计时/抽签，仍要求 L2/L5、站立、亮廊、每带一次</summary>
        internal static void DebugArm() => debugArm = true;

        //====================================================================
        //防痉挛红线（写死为常量，改动前先读 WAVE2-ATMOSPHERE E-3 风险节）：
        //  ·全程无白闪：包络恒 ≤1.0，回亮抖动只向下抖；
        //  ·回亮阶跃 ≤0.4/步（实取 0.13/0.25/0.40）；
        //  ·相邻阶跃间隔 ≥9f=150ms（实取 30f/32f）；
        //  ·黑→全亮总时长 90f=1.5s——是"哆嗦回亮"不是频闪；
        //  ·熄灭拍是单次向下硬切（非高频交替），不属频闪范畴。
        //====================================================================
        private const float CutFloor = 0.06f;    //黑暗下限：保 UI 与自身轮廓可读（纯 0 会被读成显卡故障）
        private const float DarkRise = 0.22f;    //黑暗保持期"眼睛适应"的终点
        private const int OmenF = 48;
        private const int CutF = 2;
        private const int DarkF = 72;
        private const int RelightF = 90;
        private const int TailF = 60;

        //军备条件常量
        private const int ArmBandTicks = 5400;       //入带 ≥90s
        private const int ArmNoHurtTicks = 180;      //3s 内未受伤
        private const float ArmMinBright = 0.45f;    //黑暗中熄灯无意义
        private const int LotteryOneIn = 3600;       //合格状态下平均 60s 触发一次
        private const int MaxLamps = 12;             //预兆灯采集上限
        private const int MaxEmbers = 20;            //演出烬闪总量硬帽

        private enum Phase { Idle, Omen, Cut, Dark, Relight, Tail }

        private static Phase phase = Phase.Idle;
        private static int timer;
        private static int eventBand = -1;           //触发时所在带（L5 换骨声变体）
        private static bool debugArm;

        //每带一次（回放制：OnWorldLoad 清空 = 每次进塔每层一次）
        private static readonly HashSet<int> firedBands = [];

        //带内驻留与受伤追踪（全部 LocalPlayer 本地）
        private static int lastBand = -1;
        private static int bandTicks;
        private static int sinceHurt = int.MaxValue / 2;
        private static int lastLife = -1;

        //怨灵在场扫描（镜像 FogSystem）
        private static int wraithType = -2;
        private static int wraithScanTimer;
        private static bool wraithAlive;

        //预兆采集的点亮灯具（烬闪喷溅位）
        private static readonly List<Point> lamps = new(MaxLamps);
        private static int emberCount;

        public override void OnWorldLoad() => HardReset();
        public override void ClearWorld() => HardReset();

        public override void Unload() {
            HardReset();
            Disable = false;
            wraithType = -2;
        }

        private static void HardReset() {
            phase = Phase.Idle;
            timer = 0;
            eventBand = -1;
            debugArm = false;
            firedBands.Clear();
            lastBand = -1;
            bandTicks = 0;
            sinceHurt = int.MaxValue / 2;
            lastLife = -1;
            wraithScanTimer = 0;
            wraithAlive = false;
            lamps.Clear();
            emberCount = 0;
        }

        //==================== 驱动 ====================

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            if (!Dungeonworld.Active || Main.gameMenu) {
                if (phase != Phase.Idle) {
                    phase = Phase.Idle;
                }
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            TrackHurt(player);
            TrackBand(player);
            ScanWraith();

            if (phase == Phase.Idle) {
                TryArm(player);
                return;
            }
            Advance(player);
        }

        private static void TrackHurt(Player player) {
            if (lastLife < 0) {
                lastLife = player.statLife;
            }
            if (player.statLife < lastLife) {
                sinceHurt = 0;
            }
            else if (sinceHurt < int.MaxValue / 2) {
                sinceHurt++;
            }
            lastLife = player.statLife;
        }

        private static void TrackBand(Player player) {
            int band = AmbientEmitters.BandIndexForRow((int)(player.Center.Y / 16f));
            if (band != lastBand) {
                lastBand = band;
                bandTicks = 0;
            }
            else if (bandTicks < int.MaxValue / 2) {
                bandTicks++;
            }
        }

        private static void ScanWraith() {
            if (wraithType == -1) {
                return;
            }
            if (wraithType == -2) {
                if (!NPCs.DeepGaolWraithGate.Enabled) {
                    wraithType = -1;
                    return;
                }
                wraithType = ModContent.NPCType<NPCs.DeepGaolWraith>();
            }
            if (++wraithScanTimer < 4) {
                return;
            }
            wraithScanTimer = 0;
            wraithAlive = false;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == wraithType) {
                    wraithAlive = true;
                    return;
                }
            }
        }

        //==================== 军备与抽签 ====================

        private static void TryArm(Player player) {
            if (Disable || wraithAlive) {
                return;
            }
            //只投 L2(带序1) 与 L5(带序4)：教学层与骨窖，气质最合
            if (lastBand != 1 && lastBand != 4) {
                return;
            }
            if (firedBands.Contains(lastBand)) {
                return;
            }
            if (!debugArm) {
                if (bandTicks < ArmBandTicks || sinceHurt < ArmNoHurtTicks) {
                    return;
                }
            }
            //脚踏实地（电梯/坠落中不触发）
            if (player.velocity.Y != 0f) {
                return;
            }
            //亮廊里才有熄灯可言
            if (Lighting.Brightness((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f)) < ArmMinBright) {
                return;
            }
            if (!debugArm && Main.rand.Next(LotteryOneIn) != 0) {
                return;
            }

            //触发：从武装集合移除，本次进入永不复发
            debugArm = false;
            firedBands.Add(lastBand);
            eventBand = lastBand;
            phase = Phase.Omen;
            timer = 0;
            emberCount = 0;
            CollectLamps(player);
            //一声薄响：不是玩家动的
            SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.18f, Pitch = 0.45f, MaxInstances = 1 });
        }

        //预兆烬闪的喷溅位：玩家 20 tile 内的点亮灯具（一次性扫描）
        private static void CollectLamps(Player player) {
            lamps.Clear();
            int cx = (int)(player.Center.X / 16f);
            int cy = (int)(player.Center.Y / 16f);
            for (int x = cx - 20; x <= cx + 20 && lamps.Count < MaxLamps; x++) {
                if (x < 1 || x >= Main.maxTilesX - 1) {
                    continue;
                }
                for (int y = cy - 20; y <= cy + 20 && lamps.Count < MaxLamps; y++) {
                    if (y < 1 || y >= Main.maxTilesY - 1) {
                        continue;
                    }
                    Tile t = Framing.GetTileSafely(x, y);
                    if (!t.HasTile) {
                        continue;
                    }
                    bool lit = t.TileType switch {
                        TileID.Chandeliers => t.TileFrameX % 108 < 54,
                        TileID.HangingLanterns => t.TileFrameX < 18,
                        TileID.Candles => t.TileFrameX < 18,
                        TileID.Torches => true,
                        _ => false
                    };
                    if (lit) {
                        lamps.Add(new Point(x, y));
                        //同一盏灯的多格只记一次：跳过右邻两列
                        y += 2;
                    }
                }
            }
        }

        //==================== 五拍推进 ====================

        private static void Advance(Player player) {
            switch (phase) {
                case Phase.Omen:
                    //烛火窜动：预兆期灯具烬粒喷溅（总量 ≤20 含余韵，不走 E-1 探针帽，事件量自钉死；
                    //预兆只用 20-6=14，给余韵补闪留 6 粒额度）
                    if (timer % 6 == 0 && emberCount < MaxEmbers - 6 && lamps.Count > 0) {
                        Point lamp = lamps[Main.rand.Next(lamps.Count)];
                        int n = Main.rand.Next(1, 3);
                        for (int i = 0; i < n && emberCount < MaxEmbers - 6; i++) {
                            SpawnEmber(lamp);
                        }
                    }
                    if (++timer >= OmenF) {
                        phase = Phase.Cut;
                        timer = 0;
                        //"啪"：干涩铁响 + 闷震同帧
                        SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = -0.9f, MaxInstances = 1 });
                        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = -0.9f, MaxInstances = 1 });
                    }
                    break;

                case Phase.Cut:
                    if (++timer >= CutF) {
                        phase = Phase.Dark;
                        timer = 0;
                    }
                    break;

                case Phase.Dark:
                    //黑暗里两声接近的闷响：背向 400px → 250px（L5 变体换骨声）
                    if (timer == 18) {
                        PlayApproach(player, 400f, 0.30f);
                    }
                    else if (timer == 48) {
                        PlayApproach(player, 250f, 0.38f);
                    }
                    //黑暗不吃操作：保持期挨打立即跳复明
                    if (sinceHurt == 0) {
                        phase = Phase.Relight;
                        timer = 0;
                        OnRelightStart(player);
                        break;
                    }
                    if (++timer >= DarkF) {
                        phase = Phase.Relight;
                        timer = 0;
                        OnRelightStart(player);
                    }
                    break;

                case Phase.Relight:
                    //每拍一声 Tink 音调递升（回亮"咔、咔、咔"）
                    if (timer == 30) {
                        SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.3f, Pitch = 0f, MaxInstances = 2 });
                    }
                    else if (timer == 62) {
                        SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.3f, Pitch = 0.2f, MaxInstances = 2 });
                    }
                    if (++timer >= RelightF) {
                        phase = Phase.Tail;
                        timer = 0;
                    }
                    break;

                case Phase.Tail:
                    //烛火惊魂未定：预兆喷过的灯具再补一次小烬闪（额度在 MaxEmbers 内预留）
                    if (timer == 10) {
                        int n = Math.Min(lamps.Count, 6);
                        for (int i = 0; i < n && emberCount < MaxEmbers; i++) {
                            SpawnEmber(lamps[i]);
                        }
                    }
                    if (++timer >= TailF) {
                        phase = Phase.Idle;
                        timer = 0;
                        lamps.Clear();
                    }
                    break;
            }
        }

        private static void OnRelightStart(Player player) {
            //复明瞬间雾圈退开："有什么走了"，随后按雾系统节奏合拢
            FogSuppression.RequestCircle(player.Center, 600f, 40, 500f);
            SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.3f, Pitch = -0.2f, MaxInstances = 2 });
        }

        //黑暗里的接近声：定位在玩家背向；L5 变体换骨声双连
        private static void PlayApproach(Player player, float distPx, float vol) {
            Vector2 pos = player.Center + new Vector2(-player.direction * distPx, 0f);
            if (eventBand == 4) {
                SoundEngine.PlaySound(SoundID.Tink with { Volume = vol, Pitch = -0.85f, MaxInstances = 2 }, pos);
                SoundEngine.PlaySound(SoundID.Tink with { Volume = vol * 0.8f, Pitch = -0.8f, MaxInstances = 2 },
                    pos + new Vector2(-player.direction * 20f, 0f));
            }
            else {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Volume = vol, Pitch = -0.8f, MaxInstances = 2
                }, pos);
            }
        }

        private static void SpawnEmber(Point lampTile) {
            emberCount++;
            Vector2 px = new(lampTile.X * 16f + 8f, lampTile.Y * 16f + 8f);
            InnoVault.PRT.PRTLoader.NewParticle<PRT_DwSpark>(px + Main.rand.NextVector2Circular(6f, 4f),
                new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-1.6f, -0.6f)),
                default, Main.rand.NextFloat(0.1f, 0.16f))?.Configure(Main.rand.Next(22, 38));
        }

        //==================== 亮度包络（与 E-2 的钩子乘法组合，互不知晓）====================

        public override void ModifyLightingBrightness(ref float scale) {
            if (Main.dedServ || phase == Phase.Idle) {
                return;
            }
            scale *= EnvelopeBrightness();
        }

        private static float EnvelopeBrightness() {
            switch (phase) {
                case Phase.Omen: {
                    //烛火先窜两下：两次 0.95 微抖，幅度与频率都远离频闪域
                    bool dip = (timer >= 12 && timer < 18) || (timer >= 30 && timer < 36);
                    return dip ? 0.95f : 1f;
                }
                case Phase.Cut:
                    return CutFloor;
                case Phase.Dark: {
                    float t = timer / (float)DarkF;
                    float ease = 1f - (1f - t) * (1f - t);
                    return MathHelper.Lerp(CutFloor, DarkRise, ease);
                }
                case Phase.Relight: {
                    //三拍阶梯：0.35 / 0.60 / 1.00，各拍前 6f 只向下荧光抖（恒 ≤1，无白闪）
                    float target;
                    int local;
                    if (timer < 30) {
                        target = 0.35f;
                        local = timer;
                    }
                    else if (timer < 62) {
                        target = 0.60f;
                        local = timer - 30;
                    }
                    else {
                        target = 1.00f;
                        local = timer - 62;
                    }
                    if (local < 6) {
                        float k = 1f - local / 6f;
                        return target - MathF.Abs(MathF.Sin(local * 1.9f)) * 0.06f * k;
                    }
                    return target;
                }
                default:
                    return 1f;
            }
        }

        /// <summary>一行状态摘要（TestItem 验收用）</summary>
        internal static string StatusLine()
            => $"[熄灯惊魂] 相位{phase} t{timer} 带{lastBand} 驻留{bandTicks / 60}s"
            + $" 未伤{Math.Min(sinceHurt, 9999)}f 已触发[{string.Join(",", firedBands)}]"
            + $" 怨灵{(wraithAlive ? "在场" : "无")}{(Disable ? " 已禁用" : "")}";
    }
}
