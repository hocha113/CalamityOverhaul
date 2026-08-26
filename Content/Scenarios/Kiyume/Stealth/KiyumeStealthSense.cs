using CalamityOverhaul.Content.Scenarios.Kiyume.Fog;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using CalamityOverhaul.Content.Scenarios.Kiyume.NPCs;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Stealth
{
    /// <summary>视线参数档案：每种敌人一份档案，即得与恶犬同一套物理的暴露度</summary>
    public struct SightProfile
    {
        /// <summary>基础视距（px）</summary>
        public float RangePx;
        /// <summary>视锥点积门（朝向与目标方向的 dot 低于此=身后盲区）</summary>
        public float ConeDot;
        /// <summary>满雾砍掉的视距比例</summary>
        public float FogCut;
        /// <summary>目标静止时的暴露折减</summary>
        public float StillMul;
        /// <summary>目标藏身时的暴露折减</summary>
        public float ShelterMul;
        /// <summary>目标满档光源的视距增幅</summary>
        public float LightBoost;
    }

    /// <summary>听觉参数档案</summary>
    public struct HearingProfile
    {
        /// <summary>基础听距（px）</summary>
        public float RangePx;
        /// <summary>行走响度档</summary>
        public float WalkLevel;
        /// <summary>奔跑响度档</summary>
        public float RunLevel;
        /// <summary>落地脉冲响度倍率</summary>
        public float LandImpulse;
        /// <summary>开火脉冲响度倍率</summary>
        public float WeaponImpulse;
        /// <summary>隔实心的闷响折减</summary>
        public float OcclusionMul;
    }

    /// <summary>警觉档位（阈值见 KiyumeHoundMetrics：起疑 25 / 搜索 60 / 追击 100）</summary>
    public enum AwarenessTier
    {
        Calm = 0,
        Alert = 1,
        Search = 2,
        Chase = 3,
    }

    /// <summary>
    /// 可选警觉计：通道暴露度加性积分（视觉权重高于听觉），三档阈值。
    /// 薄工具 struct 不绑架构，敌人侧自持（恶犬存 ai[2] 原版同步）
    /// </summary>
    public struct AwarenessMeter
    {
        public float Value;

        public readonly AwarenessTier Tier =>
            Value >= KiyumeHoundMetrics.ChaseThreshold ? AwarenessTier.Chase
            : Value >= KiyumeHoundMetrics.SearchThreshold ? AwarenessTier.Search
            : Value >= KiyumeHoundMetrics.AlertThreshold ? AwarenessTier.Alert
            : AwarenessTier.Calm;

        /// <summary>单帧积分：gain=Σ(通道暴露×通道增益)，decay=当前态衰减；返回更新后档位</summary>
        public AwarenessTier Update(float gain, float decay) {
            Value = MathHelper.Clamp(Value + gain - decay, 0f, KiyumeHoundMetrics.ChaseThreshold);
            return Tier;
        }
    }

    /// <summary>
    /// 鬼梦潜行的全部物理（全鬼梦唯一潜行框架，公开面按装配令 §2 冻结）：
    /// 视线/听觉/雾浓度三通道无状态函数 + 参数档案 + 噪声场 + 反向观测。<br/>
    /// 服务器权威端调用（建议 6t 节流），客户端可用同式做 HUD 表现；
    /// 通道零上行零下行——输入（位置/速度/HeldItem/buff）全部原版同步，裁决结果经 NPC ai[] 散布
    /// </summary>
    internal static class KiyumeStealthSense
    {
        /// <summary>
        /// 名义雾浓度 0..1（侦测掩体口径，裁决 1）：解析式（潮汐钟+几何的纯函数，两端一致）加贴地残雾项。<br/>
        /// 显式绕开 KiyumeFogDebug.DensityMul——客户端调试倍率不得改变侦测裁决；
        /// 解析项与 KiyumeFogSim.TargetAt 同源（TideFill×LakeFalloff×带表倍率，湖区与 SteamFill 取大），改一处必改两处
        /// </summary>
        internal static float FogConcealmentAt(Vector2 worldPx) {
            KiyumeFogTheme.Sample(worldPx.X / 16f, out _, out float mul);
            float surfaceY = KiyumeFogTide.SurfaceAt(worldPx.X);
            //W4 镜像修正：沉没=雾线之下（worldY 更大），与 KiyumeFogSim.TargetAt 翻正后同向
            float raw = KiyumeFogSim.TideFill(worldPx.Y - surfaceY)
                * KiyumeFogSim.LakeFalloff(worldPx.X) * mul;
            if (KiyumeWorld.Active) {
                float steamGate = MathHelper.Clamp(
                    (KiyumeMetrics.WaterRightPx - worldPx.X) / KiyumeMetrics.SteamFadeSpanPx, 0f, 1f);
                if (steamGate > 0f) {
                    raw = MathHelper.Max(raw, KiyumeFogSim.SteamFill(worldPx.Y) * steamGate);
                }
            }
            //贴地残雾：退潮后雾面沉得越低，残雾越足；只在贴地带内与解析项取大
            float groundY = GroundWorldY(worldPx);
            if (worldPx.Y >= groundY - KiyumeHoundMetrics.GroundFogBandPx && worldPx.Y <= groundY) {
                float expose = MathHelper.Clamp(
                    (surfaceY - groundY) / KiyumeHoundMetrics.GroundExposeSpanPx, 0f, 1f);
                raw = MathHelper.Max(raw, KiyumeHoundMetrics.GroundConcealBase * expose);
            }
            return MathHelper.Clamp(raw, 0f, 1f);
        }

        //贴地残雾的地面基准：服务器优先生成端规划（FloorTop 仅生成端非空），客户端 tile 探针；
        //探针范围内无实心=不在贴地带（悬空/高塔），返回哨兵让带判定落空
        private static float GroundWorldY(Vector2 worldPx) {
            int tileX = Math.Clamp((int)(worldPx.X / 16f), 0, Main.maxTilesX - 1);
            if (KiyumePlans.FloorTop != null) {
                return KiyumePlans.FloorTopAt(tileX) * 16f;
            }
            int fromRow = Math.Clamp((int)(worldPx.Y / 16f) - 2, 1, Main.maxTilesY - 2);
            int maxRow = Math.Min(fromRow + KiyumeHoundMetrics.GroundProbeRows, Main.maxTilesY - 2);
            for (int y = fromRow; y <= maxRow; y++) {
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return y * 16f;
                }
            }
            return float.MaxValue;
        }

        /// <summary>
        /// 视线通道单帧暴露度 0..1：视锥 × 距离衰减平方 × 静止/藏身折减 × 光源增距，tile 遮挡即零。<br/>
        /// 雾以名义浓度乘性砍视距（雾是视觉掩体不是听觉掩体）；
        /// 持光者视距被放大、雾衰减减半、身后盲区失效——火光不驱它，火光引它
        /// </summary>
        internal static float SightExposure(NPC seeker, Player prey, in SightProfile p) {
            if (seeker == null || prey == null || !prey.active || prey.dead || prey.ghost) {
                return 0f;
            }
            KiyumeStealthPlayer state = prey.GetModPlayer<KiyumeStealthPlayer>();
            float light = state.LightTier;
            float fogT = MathHelper.Clamp(
                (FogConcealmentAt(prey.Center) - KiyumeHoundMetrics.FogFloor) / KiyumeHoundMetrics.FogSpan,
                0f, 1f);
            float range = p.RangePx * (1f + light * p.LightBoost)
                * (1f - p.FogCut * fogT * (1f - 0.5f * light));
            Vector2 to = prey.Center - seeker.Center;
            float dist = to.Length();
            if (range <= 1f || dist >= range) {
                return 0f;
            }
            //身后盲区：视锥外唯有火光漏得出去
            float coneGate = Vector2.Dot(to.SafeNormalize(Vector2.Zero),
                new Vector2(seeker.direction >= 0 ? 1f : -1f, 0f)) > p.ConeDot ? 1f : light;
            if (coneGate <= 0f) {
                return 0f;
            }
            if (!Collision.CanHitLine(seeker.position, seeker.width, seeker.height,
                prey.position, prey.width, prey.height)) {
                return 0f;
            }
            float near = 1f - dist / range;
            float exposure = near * near * coneGate;
            if (state.IsStill) {
                exposure *= p.StillMul;
            }
            if (ShelterFactor(prey) < 1f) {
                exposure *= p.ShelterMul;
            }
            return MathHelper.Clamp(exposure, 0f, 1f);
        }

        /// <summary>
        /// 听觉通道单帧暴露度（0..1 基线，落地/开火脉冲可破 1）：速度分档半径无向，
        /// 实心遮挡闷响折减，雾不衰减听觉；噪声场采样一并计入（裁决 11，枪杀稻草人会引狗）
        /// </summary>
        internal static float SoundExposure(NPC seeker, Player prey, in HearingProfile p) {
            if (seeker == null) {
                return 0f;
            }
            float total = 0f;
            if (prey != null && prey.active && !prey.dead && !prey.ghost && p.RangePx > 1f) {
                float dist = Vector2.Distance(prey.Center, seeker.Center);
                if (dist < p.RangePx) {
                    KiyumeStealthPlayer state = prey.GetModPlayer<KiyumeStealthPlayer>();
                    float speed = prey.velocity.Length();
                    float level = speed >= KiyumeHoundMetrics.RunSpeedGate ? p.RunLevel
                        : speed >= KiyumeHoundMetrics.WalkSpeedGate ? p.WalkLevel : 0f;
                    level += state.LandPulse * p.LandImpulse + state.FirePulse * p.WeaponImpulse;
                    if (level > 0f) {
                        if (!Collision.CanHitLine(seeker.position, seeker.width, seeker.height,
                            prey.position, prey.width, prey.height)) {
                            level *= p.OcclusionMul;
                        }
                        total = level * (1f - dist / p.RangePx);
                    }
                }
            }
            return total + NoiseAt(seeker.Center, p.RangePx);
        }

        /// <summary>
        /// 藏身因子：1=露天，0.3=藏身中（裁决 9：自身几何 ∨ KiyumeStructures.IsHideVolumeAt，取强）。
        /// 结果在 KiyumeStealthPlayer 按 6t 缓存，这里是查询口
        /// </summary>
        internal static float ShelterFactor(Player prey) =>
            prey.GetModPlayer<KiyumeStealthPlayer>().Shelter;

        //藏身几何真算（缓存方 KiyumeStealthPlayer 调）：头顶 4 tile 内实心 + 身处墙皮覆盖，
        //或命中 P3 注册的藏身位形（KiyumeStructures 归同波 P3-A 新建，冻结签名见装配令 §2）
        internal static float ComputeShelterRaw(Player prey) {
            Point tp = prey.Center.ToTileCoordinates();
            bool geo = false;
            if (Framing.GetTileSafely(tp.X, tp.Y).WallType > 0) {
                int topRow = (int)(prey.position.Y / 16f);
                for (int dy = 1; dy <= KiyumeHoundMetrics.ShelterRoofRows; dy++) {
                    Tile tile = Framing.GetTileSafely(tp.X, topRow - dy);
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                        geo = true;
                        break;
                    }
                }
            }
            bool hide = KiyumeStructures.IsHideVolumeAt(tp);
            return geo || hide ? KiyumeHoundMetrics.ShelterSightMul : 1f;
        }

        /// <summary>
        /// 玩家自发光档 0/0.5/1：held 光源=满档，矿工头盔/照明宠物=半档。
        /// 服务器自查 HeldItem/头盔/buff，零信任问题零新包；结果在 KiyumeStealthPlayer 按 6t 缓存
        /// </summary>
        internal static float LightEmission(Player prey) =>
            prey.GetModPlayer<KiyumeStealthPlayer>().LightTier;

        //光源档真算（缓存方 KiyumeStealthPlayer 调）
        internal static float ComputeLightRaw(Player prey) {
            Item held = prey.HeldItem;
            if (held != null && !held.IsAir
                && (held.flame || held.createTile == TileID.Torches || held.createTile == TileID.Candles
                    || KiyumeHoundMetrics.HeldLightItems.Contains(held.type))) {
                return 1f;
            }
            if (prey.head == ArmorIDs.Head.MiningHelmet) {
                return 0.5f;
            }
            for (int i = 0; i < Player.MaxBuffs; i++) {
                int buff = prey.buffType[i];
                if (buff > 0 && prey.buffTime[i] > 0 && Main.lightPet[buff]) {
                    return 0.5f;
                }
            }
            return 0f;
        }

        /// <summary>
        /// 反向观测（裁决 10）：任一活跃玩家的保守视窗（±1010×640px）碰到该框，
        /// 且框中心解析浓度低于雾盲阈值，即视为被看见。守田人/无面者「只在无人注视时动」的判定口
        /// </summary>
        internal static bool ObservedByAnyPlayer(Rectangle hitboxWorldPx, float fogBlind) {
            float conceal = -1f;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                var view = new Rectangle(
                    (int)player.Center.X - KiyumeHoundMetrics.ObserveHalfWidthPx,
                    (int)player.Center.Y - KiyumeHoundMetrics.ObserveHalfHeightPx,
                    KiyumeHoundMetrics.ObserveHalfWidthPx * 2,
                    KiyumeHoundMetrics.ObserveHalfHeightPx * 2);
                if (!view.Intersects(hitboxWorldPx)) {
                    continue;
                }
                if (conceal < 0f) {
                    conceal = FogConcealmentAt(hitboxWorldPx.Center.ToVector2());
                }
                if (conceal < fogBlind) {
                    return true;
                }
            }
            return false;
        }

        //════════ 噪声场（裁决 11）：服务器侧环形缓冲，世界级会话状态 ════════

        private struct NoiseEvent
        {
            internal Vector2 Pos;
            internal float Amount;
            internal uint Tick;
        }

        private static readonly NoiseEvent[] noiseRing = new NoiseEvent[KiyumeHoundMetrics.NoiseRingCapacity];
        private static int noiseCursor;

        /// <summary>
        /// 噪声上报：权威端（服务器/单人）记录，客户端调用为无害空转。
        /// P4 各怪在自己规格标注的上报点调用（稻草人挨枪、井绳绷断……）
        /// </summary>
        internal static void ReportNoise(Vector2 worldPos, float amount) {
            if (VaultUtils.isClient || amount <= 0f) {
                return;
            }
            noiseRing[noiseCursor] = new NoiseEvent {
                Pos = worldPos, Amount = amount, Tick = Main.GameUpdateCount,
            };
            noiseCursor = (noiseCursor + 1) % noiseRing.Length;
        }

        /// <summary>
        /// 噪声场采样：radiusPx 内事件按线性距离衰减 × 半衰期时间衰减求和。
        /// 只有权威端有数据（客户端恒 0）；井手与恶犬听觉通道共用此口
        /// </summary>
        internal static float NoiseAt(Vector2 worldPos, float radiusPx) {
            if (radiusPx <= 1f) {
                return 0f;
            }
            float total = 0f;
            for (int i = 0; i < noiseRing.Length; i++) {
                ref NoiseEvent e = ref noiseRing[i];
                if (e.Amount <= 0f) {
                    continue;
                }
                float age = Main.GameUpdateCount - e.Tick;
                float decay = MathF.Pow(2f, -age / KiyumeHoundMetrics.NoiseHalfLifeTicks);
                if (decay < 0.02f) {
                    //衰减殆尽顺手清坟，缩短后续扫描
                    e.Amount = 0f;
                    continue;
                }
                float dist = Vector2.Distance(e.Pos, worldPos);
                if (dist < radiusPx) {
                    total += e.Amount * decay * (1f - dist / radiusPx);
                }
            }
            return total;
        }

        /// <summary>会话复位：ShouldSave=false 每次进梦全新，静态噪声残留=幽灵警报</summary>
        internal static void ResetSession() {
            Array.Clear(noiseRing);
            noiseCursor = 0;
        }
    }

    //会话复位挂线（镜像 OldNetICEDirector.ResetSession 纪律）
    internal class KiyumeStealthSenseSystem : ModSystem
    {
        public override void OnWorldLoad() => KiyumeStealthSense.ResetSession();
        public override void OnWorldUnload() => KiyumeStealthSense.ResetSession();
    }
}
