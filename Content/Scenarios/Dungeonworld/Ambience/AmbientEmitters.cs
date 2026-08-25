using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Ambience
{
    /// <summary>
    /// 氛围静默公开 API：圆/矩形区域 + TTL + 羽化，多请求取最小因子。<br/>
    /// 形状逐行镜像 <see cref="Fog.FogSuppression"/>（TTL 续订制、无注销即无泄漏），
    /// 但域不同：这里压的是环境粒子与光锥，不是雾。与 IMPL-D 的 SpawnQuietZone
    /// （刷怪静默）也是两个域，命名保持区分（裁决 §1-5）。<br/>
    /// 首版消费者：C 路 Boss 房清场（Wave-2.5 对齐，本波不接不阻塞）
    /// </summary>
    public static class AmbientQuiet
    {
        private struct Entry
        {
            internal bool IsRect;
            internal Vector2 Center;
            internal float Radius;
            internal Rectangle Rect;
            internal float Feather;
            internal uint ExpireTick;
        }

        private static readonly List<Entry> requests = new(8);
        private static uint tickNow;
        private const int MaxRequests = 256;

        /// <summary>圆形静默：<paramref name="radiusPx"/> 内环境发射概率乘 0，边缘羽化过渡</summary>
        public static void Request(Vector2 worldCenterPx, float radiusPx, int ttlTicks = 12, float featherPx = 200f) {
            if (requests.Count >= MaxRequests) {
                requests.RemoveAt(0);
            }
            requests.Add(new Entry {
                IsRect = false,
                Center = worldCenterPx,
                Radius = MathHelper.Max(radiusPx, 0f),
                Feather = MathHelper.Max(featherPx, 1f),
                ExpireTick = tickNow + (uint)Math.Max(ttlTicks, 1)
            });
        }

        /// <summary>矩形静默（Boss 房整间清场用）</summary>
        public static void Request(Rectangle worldRectPx, int ttlTicks = 12, float featherPx = 200f) {
            if (requests.Count >= MaxRequests) {
                requests.RemoveAt(0);
            }
            requests.Add(new Entry {
                IsRect = true,
                Rect = worldRectPx,
                Feather = MathHelper.Max(featherPx, 1f),
                ExpireTick = tickNow + (uint)Math.Max(ttlTicks, 1)
            });
        }

        public static void Clear() => requests.Clear();

        internal static void Update() {
            tickNow++;
            for (int i = requests.Count - 1; i >= 0; i--) {
                if (requests[i].ExpireTick <= tickNow) {
                    requests.RemoveAt(i);
                }
            }
        }

        /// <summary>静默因子：1=无静默，0=全压；多请求取最小，边缘 smoothstep</summary>
        public static float Evaluate(Vector2 worldPx) {
            float factor = 1f;
            for (int i = 0; i < requests.Count; i++) {
                Entry req = requests[i];
                float d;
                if (req.IsRect) {
                    float dx = MathHelper.Max(MathHelper.Max(req.Rect.Left - worldPx.X, worldPx.X - req.Rect.Right), 0f);
                    float dy = MathHelper.Max(MathHelper.Max(req.Rect.Top - worldPx.Y, worldPx.Y - req.Rect.Bottom), 0f);
                    d = MathF.Sqrt(dx * dx + dy * dy);
                }
                else {
                    d = MathHelper.Max(Vector2.Distance(worldPx, req.Center) - req.Radius, 0f);
                }
                float t = MathHelper.Clamp(d / req.Feather, 0f, 1f);
                factor = MathHelper.Min(factor, t * t * (3f - 2f * t));
            }
            return factor;
        }
    }

    /// <summary>
    /// 每 tick 生成预算（硬帽写死为常量，热调只能降不能破帽）：<br/>
    /// 探针帽 2 粒/tick——环境常驻发射的数学上界（2×寿命150f=存活≤300）；<br/>
    /// 事件帽 6 粒/tick——机器蒸汽/滴水溅射/扬尘这类突发，episodic 不常驻
    /// </summary>
    internal static class AmbientBudget
    {
        internal const int ProbeCapPerTick = 2;
        internal const int EventCapPerTick = 6;

        private static int probeSpent;
        private static int eventSpent;

        internal static void NewTick() {
            probeSpent = 0;
            eventSpent = 0;
        }

        internal static bool TryProbe(int n = 1) {
            if (probeSpent + n > ProbeCapPerTick) {
                return false;
            }
            probeSpent += n;
            return true;
        }

        internal static bool TryEvent(int n = 1) {
            if (eventSpent + n > EventCapPerTick) {
                return false;
            }
            eventSpent += n;
            return true;
        }

        internal static string Line => $"探针{probeSpent}/{ProbeCapPerTick} 事件{eventSpent}/{EventCapPerTick}";
    }

    /// <summary>
    /// 七层发射表：探针分类（空气/顶板下沿/液面/地缝）+ 层条目 + 墙面签名二级修饰。<br/>
    /// 墙签名对齐 F28 刷怪派系哲学：Slab{94,96,98}=潮湿、Tiled{95,97,99}=金属、
    /// 基础{7,8,9}=尘埃基线——B 路铺子地带墙面时空气差异零接口自动跟随。<br/>
    /// 概率常量为"每命中探针"口径（典型窗口空气探针≈16/tick），合计目标 0.4~1.2 粒/tick
    /// </summary>
    internal static class AmbientEmitters
    {
        internal enum WallSig
        {
            None,   //无墙/带外
            Dust,   //基础墙：尘埃基线
            Damp,   //Slab 墙：潮湿（滴水/霉雾概率↑）
            Metal   //Tiled 墙：金属（火花/锈尘概率↑）
        }

        //==== 层色板（由 DungeonworldLoadTheme 推导，运行时表现层专用）====
        private static readonly Color MoteGoldL1 = new(233, 185, 102);   //Candle 金尘
        private static readonly Color MoteGrayL2 = new(150, 130, 138);   //囚粉压灰
        private static readonly Color RustFlake = new(137, 81, 41);      //锈屑
        private static readonly Color MoteBookL3 = new(208, 196, 168);   //纸墨尘
        private static readonly Color ScrapPaper = new(217, 205, 178);   //Parchment
        private static readonly Color DripWater = new(205, 232, 218);    //湿沼绿偏白
        private static readonly Color MistWetL4 = new(110, 150, 132);
        private static readonly Color GlintWater = new(190, 230, 220);
        private static readonly Color AshBoneL5 = new(186, 178, 158);    //骨白去饱和
        private static readonly Color MistSteamL6 = new(152, 136, 120);  //暖灰蒸汽
        private static readonly Color MistMold = new(120, 128, 112);     //霉雾灰绿
        private static readonly Color MoteRust = new(170, 120, 80);
        internal static readonly Color AshVoidL7 = new(166, 158, 182);   //冥紫灰白

        //==== 层条目概率（每命中探针）====
        private const float L1MoteP = 0.035f;
        private const float L2MoteP = 0.011f;          //全塔最稀
        private const float L2RustCeilP = 0.025f;
        private const float L3MoteP = 0.072f;          //全塔最高（光照门控 ≥0.35 才生）
        private const float L3ScrapP = 0.008f;
        private const float L4DripCeilP = 0.06f;       //计划钉死值
        private const float L4MistSurfaceP = 0.05f;
        private const float L4GlintSurfaceP = 0.09f;
        private const float L5AshP = 0.053f;
        private const float L6SparkCeilP = 0.04f;      //计划钉死值
        private const float L6MistSeamP = 0.03f;
        private const float L7AshP = 0.05f;
        private const float L3SpawnLightGate = 0.35f;

        //==== 墙面签名二级修饰（独立加投，不改层条目本身）====
        private const float DampDripP = 0.02f;
        private const float DampMistP = 0.012f;
        private const float MetalSparkP = 0.015f;
        private const float MetalMoteP = 0.008f;

        /// <summary>行→带序：0..6 层带，7=深渊(≥5600)，-1=天空/隔离带（不发射）</summary>
        internal static int BandIndexForRow(int y) {
            if (y >= 5600) {
                return 7;
            }
            var bands = Gen.DungeonworldMetrics.Bands;
            for (int i = 0; i < bands.Length; i++) {
                if (y >= bands[i].Top && y < bands[i].Bottom) {
                    return i;
                }
            }
            return -1;
        }

        internal static WallSig ClassifyWall(ushort wall) {
            switch (wall) {
                case WallID.BlueDungeonSlabUnsafe:
                case WallID.PinkDungeonSlabUnsafe:
                case WallID.GreenDungeonSlabUnsafe:
                    return WallSig.Damp;
                case WallID.BlueDungeonTileUnsafe:
                case WallID.PinkDungeonTileUnsafe:
                case WallID.GreenDungeonTileUnsafe:
                    return WallSig.Metal;
                case WallID.BlueDungeonUnsafe:
                case WallID.GreenDungeonUnsafe:
                case WallID.PinkDungeonUnsafe:
                    return WallSig.Dust;
                default:
                    return WallSig.None;
            }
        }

        /// <summary>
        /// 单针入口：分类 tile 并按层带发射。<paramref name="mul"/>=presence×RateMul×Boss 因子，
        /// 静默区因子在本函数内按针位求值
        /// </summary>
        internal static void FireProbe(int x, int y, float mul) {
            int band = BandIndexForRow(y);
            if (band < 0) {
                return;
            }
            Vector2 px = new(x * 16f + 8f, y * 16f + 8f);
            mul *= AmbientQuiet.Evaluate(px);
            if (mul <= 0.01f) {
                return;
            }

            Tile tile = Framing.GetTileSafely(x, y);
            WallSig sig = ClassifyWall(tile.WallType);

            if (tile.HasTile) {
                if (!Main.tileSolid[tile.TileType]) {
                    return;    //家具/平台等非实心：不作发射源（灯具由探针端另行登记）
                }
                //顶板下沿：实心下邻空气
                Tile below = Framing.GetTileSafely(x, y + 1);
                if (!below.HasTile && below.LiquidAmount == 0) {
                    EmitCeiling(band, sig, new Vector2(px.X, px.Y + 12f), mul);
                }
                return;
            }

            if (tile.LiquidAmount > 32) {
                //液面：液体上邻空气
                Tile above = Framing.GetTileSafely(x, y - 1);
                if (!above.HasTile && above.LiquidAmount == 0) {
                    float surfaceY = y * 16f + (1f - tile.LiquidAmount / 255f) * 16f;
                    EmitLiquidSurface(band, new Vector2(px.X, surfaceY), mul);
                }
                return;
            }

            //空气针；顺带判地缝（下邻实心）
            Tile under = Framing.GetTileSafely(x, y + 1);
            bool floorSeam = under.HasTile && Main.tileSolid[under.TileType];
            EmitAir(band, sig, px, mul, floorSeam);
        }

        //==================== 空气 ====================

        private static void EmitAir(int band, WallSig sig, Vector2 px, float mul, bool floorSeam) {
            switch (band) {
                case 0:
                    //L1 教堂静空气金尘：缓沉，烛光边最亮（光照门控在粒子 AI 内）
                    if (Roll(L1MoteP * mul)) {
                        SpawnMote(px, MoteGoldL1, 0.16f, 110, 150, 0.05f, 0.09f);
                    }
                    break;
                case 1:
                    if (Roll(L2MoteP * mul)) {
                        SpawnMote(px, MoteGrayL2, 0.14f, 90, 130, 0.04f, 0.07f);
                    }
                    break;
                case 2:
                    //L3 书尘：出生点光照 ≥0.35 才生（暗处书海无尘），光锥柱内密度 ×2
                    float p = L3MoteP * (DungeonworldAmbientFX.IsInShaftLight(px) ? 2f : 1f);
                    if (Roll(p * mul) && AmbientPRTUtil.SafeBright(px) >= L3SpawnLightGate) {
                        SpawnMote(px, MoteBookL3, 0.12f, 100, 150, 0.05f, 0.08f);
                    }
                    if (Roll(L3ScrapP * mul) && AmbientBudget.TryProbe()) {
                        PRTLoader.NewParticle<PRT_DwScrap>(px, new Vector2(0f, 0.4f), ScrapPaper,
                            Main.rand.NextFloat(0.9f, 1.3f))?.Configure(Main.rand.Next(80, 120), 3f, 2f);
                    }
                    break;
                case 4:
                    //L5 骨灰似雪，无声飘降
                    if (Roll(L5AshP * mul)) {
                        SpawnAsh(px, AshBoneL5, Main.rand.NextFloat(0.45f, 0.65f));
                    }
                    break;
                case 5:
                    //L6 地缝蒸汽：贴地气孔嘶出的一口
                    if (floorSeam && Roll(L6MistSeamP * mul) && AmbientBudget.TryProbe()) {
                        PRTLoader.NewParticle<PRT_DwMist>(px + new Vector2(0f, 4f), new Vector2(0f, -0.3f),
                            MistSteamL6, Main.rand.NextFloat(0.14f, 0.22f))
                            ?.Configure(Main.rand.Next(70, 110), 0.35f, 0.0022f, 0.2f);
                    }
                    break;
                case 6:
                case 7:
                    //L7/深渊 灰烬逆升：这层的"重力"在视觉语言上是反的
                    if (Roll(L7AshP * mul)) {
                        SpawnAsh(px, AshVoidL7, -Main.rand.NextFloat(0.4f, 0.6f));
                    }
                    break;
            }

            //金属墙签名：空气里飘锈色微尘
            if (sig == WallSig.Metal && Roll(MetalMoteP * mul)) {
                SpawnMote(px, MoteRust, 0.2f, 80, 120, 0.04f, 0.07f);
            }
        }

        //==================== 顶板下沿 ====================

        private static void EmitCeiling(int band, WallSig sig, Vector2 px, float mul) {
            switch (band) {
                case 1:
                    //L2 顶板锈尘：偶发一小蓬锈屑坠落（受 2 粒/tick 硬帽约束，单蓬 ≤2）
                    if (Roll(L2RustCeilP * mul)) {
                        int n = Main.rand.Next(2, 4);
                        for (int i = 0; i < n && AmbientBudget.TryProbe(); i++) {
                            PRTLoader.NewParticle<PRT_DwScrap>(px + new Vector2(Main.rand.NextFloat(-6f, 6f), 0f),
                                new Vector2(0f, 0.6f), RustFlake, Main.rand.NextFloat(0.7f, 1f))
                                ?.Configure(Main.rand.Next(60, 90), 2f, 2f);
                        }
                    }
                    break;
                case 3:
                    //L4 顶板滴水（计划 6%）
                    if (Roll(L4DripCeilP * mul)) {
                        SpawnDrip(px);
                    }
                    break;
                case 5:
                    //L6 顶板焊火滴落（计划 4%）
                    if (Roll(L6SparkCeilP * mul) && AmbientBudget.TryProbe()) {
                        PRTLoader.NewParticle<PRT_DwSpark>(px, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), 0.8f),
                            default, Main.rand.NextFloat(0.1f, 0.16f))?.Configure(Main.rand.Next(28, 46));
                    }
                    break;
            }

            //墙面签名二级修饰（独立加投，任意层生效）
            if (sig == WallSig.Damp) {
                if (Roll(DampDripP * mul)) {
                    SpawnDrip(px);
                }
                else if (Roll(DampMistP * mul) && AmbientBudget.TryProbe()) {
                    PRTLoader.NewParticle<PRT_DwMist>(px, new Vector2(0f, 0.15f), MistMold,
                        Main.rand.NextFloat(0.1f, 0.16f))?.Configure(Main.rand.Next(60, 90), 0.1f, 0.0014f, 0.14f);
                }
            }
            else if (sig == WallSig.Metal && Roll(MetalSparkP * mul) && AmbientBudget.TryProbe()) {
                PRTLoader.NewParticle<PRT_DwSpark>(px, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), 0.7f),
                    default, Main.rand.NextFloat(0.08f, 0.14f))?.Configure(Main.rand.Next(24, 40));
            }
        }

        //==================== 液面 ====================

        private static void EmitLiquidSurface(int band, Vector2 surfacePx, float mul) {
            if (band != 3) {
                return;    //液面语言目前只属于水牢
            }
            if (Roll(L4MistSurfaceP * mul) && AmbientBudget.TryProbe()) {
                //贴水潮雾：几乎不升，横着懒散
                PRTLoader.NewParticle<PRT_DwMist>(surfacePx + new Vector2(0f, -6f),
                    new Vector2(Main.rand.NextFloat(-0.15f, 0.15f), 0f), MistWetL4,
                    Main.rand.NextFloat(0.16f, 0.26f))?.Configure(Main.rand.Next(90, 140), 0.04f, 0.0018f, 0.16f);
            }
            if (Roll(L4GlintSurfaceP * mul) && AmbientBudget.TryProbe()) {
                PRTLoader.NewParticle<PRT_DwGlint>(surfacePx + new Vector2(0f, -1f),
                    new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), 0f), GlintWater,
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(Main.rand.Next(36, 70));
            }
        }

        //==================== 事件溅射（滴水入水回调）====================

        /// <summary>水珠入水：涟漪 + 碎星 + 轻响。走事件预算，由 PRT_DwDrip 的死亡帧调用</summary>
        internal static void SplashAt(Vector2 px, Color dripColor) {
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.2f, Pitch = -0.3f, MaxInstances = 3 }, px);
            if (AmbientBudget.TryEvent()) {
                PRTLoader.NewParticle<PRT_DwRipple>(px, Vector2.Zero, GlintWater,
                    Main.rand.NextFloat(0.28f, 0.42f))?.Configure(Main.rand.Next(22, 30));
            }
            int glints = Main.rand.Next(1, 3);
            for (int i = 0; i < glints && AmbientBudget.TryEvent(); i++) {
                PRTLoader.NewParticle<PRT_DwGlint>(px + new Vector2(Main.rand.NextFloat(-8f, 8f), -2f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), 0f), GlintWater,
                    Main.rand.NextFloat(0.04f, 0.07f))?.Configure(Main.rand.Next(30, 55));
            }
        }

        /// <summary>L3 狂奔贴书架的扬尘一蓬（事件预算）</summary>
        internal static void DashPuffAt(Vector2 feetPx, Vector2 playerVel) {
            Vector2 back = -Vector2.Normalize(playerVel.X == 0f && playerVel.Y == 0f ? Vector2.UnitX : playerVel);
            for (int i = 0; i < 2 && AmbientBudget.TryEvent(); i++) {
                PRTLoader.NewParticle<PRT_DwScrap>(feetPx + new Vector2(Main.rand.NextFloat(-10f, 10f), -4f),
                    back * Main.rand.NextFloat(0.6f, 1.4f) + new Vector2(0f, -0.8f), ScrapPaper,
                    Main.rand.NextFloat(0.8f, 1.2f))?.Configure(Main.rand.Next(50, 80), 3f, 2f);
            }
            for (int i = 0; i < 2 && AmbientBudget.TryEvent(); i++) {
                PRTLoader.NewParticle<PRT_DwMote>(feetPx + new Vector2(Main.rand.NextFloat(-12f, 12f), -8f),
                    back * 0.5f + new Vector2(0f, -0.3f), MoteBookL3,
                    Main.rand.NextFloat(0.05f, 0.08f))?.Configure(Main.rand.Next(60, 90), 0.1f);
            }
        }

        /// <summary>L6 机器行程火花（事件预算，挂 L6MachineStrike）</summary>
        internal static void MachineSparkAt(Vector2 px) {
            if (!AmbientBudget.TryEvent()) {
                return;
            }
            PRTLoader.NewParticle<PRT_DwSpark>(px + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f),
                new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-1.8f, -0.5f)),
                default, Main.rand.NextFloat(0.12f, 0.2f))?.Configure(Main.rand.Next(26, 44));
        }

        /// <summary>L6 机器消亡帧蒸汽一蓬（事件预算，帽内取整）</summary>
        internal static void MachineSteamAt(Vector2 px) {
            for (int i = 0; i < 4 && AmbientBudget.TryEvent(); i++) {
                PRTLoader.NewParticle<PRT_DwMist>(px + Main.rand.NextVector2Circular(14f, 10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -0.5f), MistSteamL6,
                    Main.rand.NextFloat(0.16f, 0.26f))?.Configure(Main.rand.Next(60, 100), 0.5f, 0.003f, 0.22f);
            }
        }

        //==================== 私有小件 ====================

        private static bool Roll(float p) => p > 0f && Main.rand.NextFloat() < p;

        private static void SpawnMote(Vector2 px, Color color, float sink, int lifeMin, int lifeMax,
            float scaleMin, float scaleMax) {
            if (!AmbientBudget.TryProbe()) {
                return;
            }
            PRTLoader.NewParticle<PRT_DwMote>(px + Main.rand.NextVector2Circular(10f, 10f),
                Main.rand.NextVector2Circular(0.1f, 0.05f), color,
                Main.rand.NextFloat(scaleMin, scaleMax))
                ?.Configure(Main.rand.Next(lifeMin, lifeMax), sink * Main.rand.NextFloat(0.7f, 1.3f));
        }

        private static void SpawnAsh(Vector2 px, Color color, float fall) {
            if (!AmbientBudget.TryProbe()) {
                return;
            }
            PRTLoader.NewParticle<PRT_DwAsh>(px + Main.rand.NextVector2Circular(8f, 8f),
                new Vector2(0f, fall * 0.5f), color, Main.rand.NextFloat(0.06f, 0.1f))
                ?.Configure(Main.rand.Next(90, 140), fall);
        }

        private static void SpawnDrip(Vector2 px) {
            if (!AmbientBudget.TryProbe()) {
                return;
            }
            PRTLoader.NewParticle<PRT_DwDrip>(px, new Vector2(0f, 1.4f), DripWater,
                Main.rand.NextFloat(0.7f, 1f))?.Configure(90);
        }
    }
}
