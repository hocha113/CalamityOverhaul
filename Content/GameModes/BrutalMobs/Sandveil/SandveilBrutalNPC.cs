using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.Sandveil.Projectiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Sandveil
{
    /// <summary>
    /// 残酷模式沙尘暴事件包，主题：风沙掠食者（沙暴是猎场，捕猎全程藏在沙里）。
    /// 分工：本包只管沙尘暴事件专属怪——沙鲨四型（原型/腐化噬骨/猩红割肉/神圣晶棱）、
    /// 愤怒滚草、沙元素；常驻沙漠怪（蚁狮/拉弥亚/食尸鬼/沙漠蝎等）归 Wastes 包，不入本表。
    /// 机制：沙鲨鳍迹钻沙突袭（地表鳍迹尘=天然预告→原地沙涌 omen→破沙跃咬→落沙后摇）、
    /// 滚草三连压制弹跳（压地蓄力→三次递增高度、每跳落点标记 omen→力竭滚停惩罚窗）、
    /// 沙元素沙龙卷阵（扇形三道涌沙、中央道具名空缺永远安全）。
    /// 叠加在原版 AI 之上不接管、不动数值（数值层归 <see cref="GameModeNPC"/>）；
    /// 决策只在权威端，客户端可见状态一律来自同步弹幕实体与 NPC 速度原生同步。
    /// 沙隐只读原版 <see cref="Sandstorm"/> 世界状态（全端确定性模拟，非氛围联动 API）；
    /// 隐身的公平回款：沙暴激烈期沙鲨 omen 加长、沙龙卷阵缺口加宽（思路镜像
    /// WastesSandConeTelegraph.CurrentGapHalfAngle，代码独立不跨包引用）
    /// </summary>
    internal class SandveilBrutalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==== 通用节奏 ====
        /// <summary>触发条件不满足时的复查间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>出生首攻错拍窗（M7 密度预算：60~180 帧）</summary>
        private const int FirstCooldownMin = 60;
        private const int FirstCooldownMax = 180;
        /// <summary>冷却随机抖动上限</summary>
        private const int CooldownJitter = 60;

        //==== 沙鲨·鳍迹钻沙突袭 ====
        /// <summary>跃咬冷却（档位 1/2/3，主力招 ≤600）</summary>
        private static readonly int[] SharkCooldownByTier = [420, 360, 300];
        private const float SharkMinRangeX = 110f;
        private const float SharkMaxRangeX = 300f;
        private const float SharkMaxRangeY = 220f;
        /// <summary>跃咬滞空帧（档位越高弧越快；omen 时长不随档位缩短）</summary>
        private static readonly int[] SharkFlightByTier = [38, 34, 30];
        /// <summary>跃咬横向包络的爬升/衰减帧（保持段=滞空-两者）</summary>
        private const int SharkRise = 7;
        private const int SharkDecay = 9;
        /// <summary>跃咬横向峰速上限（未含提速补偿）</summary>
        private const float SharkMaxVx = 14f;
        /// <summary>跃咬纵向初速钳制</summary>
        private const float SharkMaxUpVy = -13f;
        /// <summary>落沙后摇帧</summary>
        private const int SharkRecoverFrames = 20;
        /// <summary>跃咬 omen 全局并发上限</summary>
        private const int SharkOmenCap = 6;
        /// <summary>原版 UpdateNPC 重力常数，跳弧解算与之对齐</summary>
        private const float NpcGravity = 0.3f;

        //==== 沙鲨变体签名（ByType 表） ====
        /// <summary>腐化噬骨：尾迹诅咒沙珠的投放帧（跃咬计时轴上两点）与伤害系数</summary>
        private const int CorruptBeadFrameA = 10;
        private const int CorruptBeadFrameB = 18;
        private const float BeadDamageFrac = 0.4f;
        /// <summary>猩红割肉：跃咬命中流血时长（3 秒，不随档位增长）</summary>
        private const int CrimsonBleedTicks = 180;
        /// <summary>神圣晶棱：跃出帧的棱光小闪伤害系数与初速</summary>
        private const float PrismDamageFrac = 0.35f;
        private const float PrismSpeed = 5.5f;

        //==== 滚草·三连压制弹跳 ====
        /// <summary>弹跳序列冷却（档位 1/2/3，主力招 ≤600）</summary>
        private static readonly int[] TumbleCooldownByTier = [560, 500, 440];
        private const float TumbleMinRangeX = 120f;
        private const float TumbleMaxRangeX = 520f;
        private const float TumbleMaxRangeY = 260f;
        /// <summary>压地蓄力帧（≥24 姿态前摇，蓄力环 omen 承载可见性）</summary>
        private const int TumbleWindupFrames = 24;
        /// <summary>三跳滞空帧（递增=高度递增；落点标记可见时长=滞空，均 ≥30）</summary>
        private static readonly int[] HopFlightFrames = [40, 50, 60];
        /// <summary>弹跳横向速度钳制（未含提速补偿）</summary>
        private const float HopMaxVx = 9f;
        /// <summary>弹跳纵向初速钳制</summary>
        private const float HopMaxUpVy = -11.5f;
        /// <summary>落地判定的最短滞空帧（起跳瞬间 collideY 残留不误判）</summary>
        private const int HopMinAirFrames = 8;
        /// <summary>滞空超时余量（卡地形时强制收尾）</summary>
        private const int HopTimeoutPad = 40;
        /// <summary>力竭滚停帧（惩罚窗）</summary>
        private const int TumbleExhaustFrames = 30;
        /// <summary>落点标记 omen 全局并发上限</summary>
        private const int MarkCap = 6;

        //==== 沙元素·沙龙卷阵 ====
        /// <summary>沙龙卷阵冷却（档位 1/2/3，主力招 ≤600）</summary>
        private static readonly int[] VortexCooldownByTier = [560, 500, 440];
        private const float VortexMinRange = 130f;
        private const float VortexMaxRange = 700f;
        /// <summary>阵列 omen 全局并发上限</summary>
        private const int ArrayCap = 2;
        /// <summary>沙涌柱全局并发闸（触发时查，超限不起阵）</summary>
        private const int ColumnCap = 6;
        /// <summary>沙涌柱伤害 = 已缩放 npc.damage × 此值</summary>
        private const float ColumnDamageFrac = 0.5f;

        //==== 通用地形 ====
        /// <summary>向下寻找地表的最大瓦格数（超出视为悬空，放弃地面机制）</summary>
        private const int GroundSearchTiles = 12;
        /// <summary>鳍迹尘向上找沙面的最大瓦格数（更深则视为无迹可寻）</summary>
        private const int FinSurfaceSearchTiles = 14;

        //==== 相位 ====
        private const byte PhaseIdle = 0;
        private const byte PhaseSharkTelegraph = 1;
        private const byte PhaseSharkStrike = 2;
        private const byte PhaseSharkRecover = 3;
        private const byte PhaseTumbleWindup = 4;
        private const byte PhaseTumbleHop = 5;
        private const byte PhaseTumbleExhaust = 6;
        private const byte PhaseVortexRitual = 7;

        private enum SvFamily : byte
        {
            None,
            Shark,
            Tumble,
            Elemental,
        }

        /// <summary>沙鲨家族（四型同装备，每型 ≥1 条签名差异，见变体常量块）</summary>
        private static readonly HashSet<int> SharkTypes = [
            NPCID.SandShark, NPCID.SandsharkCorrupt, NPCID.SandsharkCrimson, NPCID.SandsharkHallow,
        ];

        private static SvFamily ResolveFamily(int type) {
            if (SharkTypes.Contains(type)) {
                return SvFamily.Shark;
            }
            if (type == NPCID.Tumbleweed) {
                return SvFamily.Tumble;
            }
            return type == NPCID.SandElemental ? SvFamily.Elemental : SvFamily.None;
        }

        /// <summary>本个体出生时绑定的档位，0=未绑定（镜像 GameModeNPC；中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private SvFamily family;
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>锁定落点（跃咬/弹跳），锁定后不再改写（预告即承诺）</summary>
        private Vector2 lockPoint;
        /// <summary>本次攻击绑定的预告体槽位（权威端私产）</summary>
        private int omenIndex = -1;
        /// <summary>跃咬横向包络峰速与方向（提交帧解算后不变）</summary>
        private float strikePeakVx;
        private float strikeDirX;
        /// <summary>跃咬滞空总帧（提交帧按档位定格）</summary>
        private int strikeFlight;
        /// <summary>弹跳持有横速（起跳帧解算后整段持有，抵住原版加速）</summary>
        private float hopVx;
        /// <summary>当前弹跳序号 1..3</summary>
        private int hopIndex;
        /// <summary>本次跃咬的破沙特效已放（神圣棱光只在真正出沙那一帧放一次）</summary>
        private bool strikeFxDone;
        /// <summary>本个体沙隐强度 0..1（各端从同步天气确定性推得，无需同步）</summary>
        private float veil;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && ResolveFamily(entity.type) != SvFamily.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            family = SvFamily.None;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            SvFamily resolved = ResolveFamily(npc.type);
            if (resolved == SvFamily.None) {
                return;
            }
            family = resolved;
            boundTier = tier;
            //首攻错拍：此刻 npc.whoAmI 恒为 0（NewNPC 之后才赋值），不可用作错拍源；
            //冷却是权威端决策私产，Main.rand 无同步语义
            cooldown = FirstCooldownMin + Main.rand.Next(FirstCooldownMax - FirstCooldownMin + 1);
        }

        /// <summary>机制入口资格：友方/无敌/Boss/雕像怪/共享血池体节逐项排除（每个入口都要过）</summary>
        private static bool Eligible(NPC npc) {
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage || npc.boss) {
                return false;
            }
            if (npc.lifeMax <= 5 || npc.damage <= 0) {
                return false;
            }
            if (npc.SpawnedFromStatue) {
                return false;
            }
            return npc.realLife < 0;
        }

        /// <summary>
        /// 提速位移补偿：GameModeNPC.PostAI 按 velocity×SpeedBonus 追加位置推进，
        /// 本层注入的承诺性速度一律除回该系数（位移项除回、重力项不除），
        /// 口径镜像 PumpkinMoonNPC.MoveGain：boss 旗标个体与体节不吃提速层，系数为 1
        /// </summary>
        private float MoveGain(NPC npc) => !npc.boss && npc.realLife < 0 ? 1f + GameModeTuning.SpeedBonus(boundTier) : 1f;

        /// <summary>来源打包（槽位+1|类型&lt;&lt;8），预兆实体与 NPC 侧回读共用（镜像沙锥）</summary>
        private static int SrcPack(NPC npc) => (npc.whoAmI + 1) | (npc.type << 8);

        /// <summary>统计某类弹幕的活动实例数（到 stopAt 提前退出；只在触发时调用，非每帧）</summary>
        private static int CountActive(int projType, int stopAt = 16) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>
        /// 沙隐条件：沙暴进行中且强度过阈、个体在地表高度。只读原版天气（全端同步的
        /// 世界状态，不算氛围联动 API）；本包自带实现，不跨包调用 Wastes 的私有成员
        /// </summary>
        internal static bool SandVeilActive(NPC npc)
            => GameModeSystem.EffectiveTier > 0 && Sandstorm.Happening && Sandstorm.Severity > 0.4f
            && npc.Center.Y < Main.worldSurface * 16f;

        /// <summary>沙暴激烈期（Severity&gt;0.7）：沙鲨 omen 加长 8 帧的触发条件（强度换公平）</summary>
        internal static bool StormSevere => Sandstorm.Happening && Sandstorm.Severity > 0.7f;

        /// <summary>从目标脚下向下找可站立地表，返回锚点（找不到视为悬空，放弃地面机制）</summary>
        private static bool TryFindGround(Player target, out Vector2 basePos) {
            basePos = default;
            Point feet = target.Bottom.ToTileCoordinates();
            for (int dy = 0; dy < GroundSearchTiles; dy++) {
                int tileY = feet.Y + dy;
                if (!WorldGen.InWorld(feet.X, tileY, 10)) {
                    return false;
                }
                if (WorldGen.SolidTile(feet.X, tileY)) {
                    basePos = new Vector2(feet.X * 16f + 8f, tileY * 16f);
                    return true;
                }
            }
            return false;
        }

        /// <summary>沙系地块（含硬化沙/沙岩，吃转换表覆盖邪化神圣变体；数值同 DuneStorm，代码独立）</summary>
        private static bool IsSandFamily(int tileType)
            => TileID.Sets.Conversion.Sand[tileType]
            || TileID.Sets.Conversion.HardenedSand[tileType]
            || TileID.Sets.Conversion.Sandstone[tileType];

        /// <summary>个体中心是否埋在实体块里（沙鲨钻沙态判定）</summary>
        private static bool Burrowed(NPC npc) {
            Point c = npc.Center.ToTileCoordinates();
            return WorldGen.InWorld(c.X, c.Y, 10) && WorldGen.SolidTile(c.X, c.Y);
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            //全端确定性表现（沙隐+鳍迹尘），须在客户端早退之前
            UpdateSandVeil(npc);
            if (family == SvFamily.Shark) {
                EmitFinWake(npc);
            }
            if (VaultUtils.isClient) {
                //决策只在权威端；客户端画面全部来自同步弹幕实体与 NPC 原生同步
                return;
            }

            if (phase == PhaseIdle) {
                if (--cooldown > 0) {
                    return;
                }
                TryStart(npc);
                return;
            }
            switch (family) {
                case SvFamily.Shark:
                    TickShark(npc);
                    break;
                case SvFamily.Tumble:
                    TickTumble(npc);
                    break;
                case SvFamily.Elemental:
                    TickVortex(npc);
                    break;
            }
        }

        /// <summary>
        /// 沙隐：沙暴里半透明并获得微幅推进（本包自实现，口径与 Wastes 试点一致）。
        /// 透明度只抬不压（与原版自管值取更大者），退隐期只回收本层抬上去的余量；
        /// 推进量过碰撞钳制。公平回款不在此处：见沙鲨 omen 加长与沙龙卷阵缺口加宽
        /// </summary>
        private void UpdateSandVeil(NPC npc) {
            bool active = SandVeilActive(npc);
            if (!active && veil <= 0f) {
                return;
            }
            veil = MathHelper.Clamp(veil + (active ? 0.03f : -0.05f), 0f, 1f);
            if (veil > 0.02f) {
                npc.alpha = Math.Max(npc.alpha, (int)(veil * 130f));
                Vector2 advance = npc.velocity * (veil * 0.10f);
                if (!npc.noTileCollide) {
                    advance = Collision.TileCollision(npc.position, advance, npc.width, npc.height);
                }
                npc.position += advance;
                if (!Main.dedServ && Main.rand.NextBool(6)) {
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.Sand, npc.velocity.X * 0.3f, 0f, 140, default, 0.9f);
                    dust.noGravity = true;
                    dust.velocity *= 0.4f;
                }
            }
            else if (!active && npc.alpha > 0 && npc.alpha <= 131) {
                //退隐余量回收：只收本层可能抬上去的区间，不碰原版更高的自管值
                npc.alpha = Math.Max(0, npc.alpha - 6);
            }
        }

        /// <summary>
        /// 鳍迹尘（天然预告）：钻沙且在游动的沙鲨，在正上方沙面持续掀起推进的沙痕。
        /// 从同步的 NPC 位置/速度确定性推得，各端自行绘制，无需额外同步
        /// </summary>
        private static void EmitFinWake(NPC npc) {
            if (Main.dedServ || Math.Abs(npc.velocity.X) < 1.2f || !Burrowed(npc) || !Main.rand.NextBool(2)) {
                return;
            }
            Point c = npc.Center.ToTileCoordinates();
            if (!IsSandFamily(Main.tile[c.X, c.Y].TileType)) {
                return;
            }
            //向上找第一格露天沙面；埋得太深则无迹可寻
            for (int dy = 0; dy < FinSurfaceSearchTiles; dy++) {
                int tileY = c.Y - dy;
                if (!WorldGen.InWorld(c.X, tileY, 10)) {
                    return;
                }
                if (!WorldGen.SolidTile(c.X, tileY)) {
                    Vector2 surface = new(npc.Center.X + npc.velocity.X * 3f, (tileY + 1) * 16f);
                    Dust fin = Dust.NewDustPerfect(surface, DustID.Sand,
                        new Vector2(npc.velocity.X * 0.4f, -Main.rand.NextFloat(1.2f, 2.6f)),
                        110, default, Main.rand.NextFloat(1f, 1.5f));
                    fin.noGravity = Main.rand.NextBool();
                    return;
                }
            }
        }

        private static bool CanSee(NPC npc, Player player)
            => Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height);

        private void TryStart(NPC npc) {
            if (!Eligible(npc)) {
                cooldown = IneligibleDelay;
                return;
            }
            if (!npc.HasValidTarget) {
                cooldown = RetryDelay;
                return;
            }
            Player player = Main.player[npc.target];
            switch (family) {
                case SvFamily.Shark:
                    TryStartShark(npc, player);
                    break;
                case SvFamily.Tumble:
                    TryStartTumble(npc, player);
                    break;
                case SvFamily.Elemental:
                    TryStartVortex(npc, player);
                    break;
            }
        }

        //======== 沙鲨·鳍迹钻沙突袭 ========

        /// <summary>钻沙就位时锁定目标脚下，起原地沙涌 omen（鳍迹停驻=预告开始）</summary>
        private void TryStartShark(NPC npc, Player player) {
            cooldown = RetryDelay;
            if (!Burrowed(npc)) {
                return;//浮出沙面时不伏击，先由原版 AI 游回沙里
            }
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Bottom.Y - npc.Center.Y);
            if (dx < SharkMinRangeX || dx > SharkMaxRangeX || dy > SharkMaxRangeY) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<SandveilFinSurgeOmen>()) >= SharkOmenCap) {
                return;
            }
            if (!TryFindGround(player, out Vector2 ground)) {
                return;
            }

            //预告即承诺：落点此帧锁死。激烈沙暴里 omen 加长 8 帧（强度换公平），
            //时长以生成帧的世界状态定格并打包进 ai[0]，NPC 与 omen 两侧同源不漂移
            bool severe = StormSevere;
            lockPoint = ground - Vector2.UnitY * 6f;
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), lockPoint, Vector2.Zero,
                ModContent.ProjectileType<SandveilFinSurgeOmen>(), 0, 0f, Main.myPlayer,
                SrcPack(npc) | (severe ? SandveilFinSurgeOmen.StormBit : 0));
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                omenIndex = -1;
                return;//预告体生成失败（弹幕位满）则整次进攻作废
            }
            //鳍迹停驻：刹住游动，蛰伏在沙里
            npc.velocity *= 0.3f;
            npc.netUpdate = true;
            timer = SandveilFinSurgeOmen.TelegraphOf(severe);
            phase = PhaseSharkTelegraph;
        }

        /// <summary>回读绑定 omen（索引+类型+来源三重校验），缺位=攻击作废（失败方向=安全方向）</summary>
        private bool BoundSharkOmenAlive(NPC npc) {
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile omen = Main.projectile[omenIndex];
            return omen.active && omen.type == ModContent.ProjectileType<SandveilFinSurgeOmen>()
                && ((int)omen.ai[0] & 255) == npc.whoAmI + 1;
        }

        private void TickShark(NPC npc) {
            if (phase == PhaseSharkTelegraph) {
                timer--;
                if (!BoundSharkOmenAlive(npc)) {
                    AbortToCooldown();
                    return;
                }
                //停驻脉冲：离散刹车压住游动漂移，让破沙点贴住 omen（脉冲帧才跟同步）
                if (timer % 8 == 0) {
                    npc.velocity *= 0.35f;
                    npc.netUpdate = true;
                }
                if (timer <= 0) {
                    CommitSharkLeap(npc);
                }
                return;
            }
            if (phase == PhaseSharkStrike) {
                TickSharkStrike(npc);
                return;
            }
            //落沙后摇：横速衰减清残速，把控制权干净还给原版 AI（惩罚窗）
            timer--;
            npc.velocity.X *= 0.72f;
            if (timer <= 0) {
                npc.velocity.X = 0f;
                npc.netUpdate = true;
                EndMove(SharkCooldownByTier);
            }
        }

        /// <summary>破沙跃咬提交：向锁定落点解算跳弧（横向包络塑形、纵向弹道+原版重力）</summary>
        private void CommitSharkLeap(NPC npc) {
            if (!BoundSharkOmenAlive(npc)) {
                AbortToCooldown();
                return;
            }
            float gain = MoveGain(npc);
            int flight = SharkFlightByTier[boundTier - 1];
            int hold = flight - SharkRise - SharkDecay;
            //包络面积（二次缓入 2/3、保持 1、二次衰减 1/3 的帧积分），横向位移=峰速×面积
            float envArea = SharkRise * (2f / 3f) + hold + SharkDecay / 3f;
            Vector2 to = lockPoint - npc.Bottom;
            strikeDirX = to.X >= 0f ? 1f : -1f;
            strikePeakVx = MathHelper.Clamp(Math.Abs(to.X) / (envArea * gain), 0f, SharkMaxVx);
            //纵向：位移项除提速补偿，重力项按原版常数补偿半程（镜像 NightPack 跳弧口径）
            npc.velocity.Y = MathHelper.Clamp(to.Y / (flight * gain) - NpcGravity * flight * 0.5f, SharkMaxUpVy, 2f);
            npc.velocity.X = 0f;
            npc.netUpdate = true;
            strikeFlight = flight;
            timer = 0;
            strikeFxDone = false;
            phase = PhaseSharkStrike;
        }

        private void TickSharkStrike(NPC npc) {
            timer++;
            //横向包络持有：抵住原版游泳 AI 的每帧改写，实际弧线兑现 omen 落点
            float env = MobDash.Envelope(timer, SharkRise, strikeFlight - SharkRise - SharkDecay, SharkDecay);
            npc.velocity.X = strikeDirX * strikePeakVx * env;
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            bool outOfSand = !Burrowed(npc);

            //神圣晶棱签名：真正跃出沙面那一帧放 4 向棱光小闪。
            //固定对角 4 向=非追踪保证（不读玩家位置）；出沙后生成避免弹体憋死在地块里
            if (npc.type == NPCID.SandsharkHallow && !strikeFxDone && outOfSand) {
                strikeFxDone = true;
                int damage = Math.Max(1, (int)(npc.damage * PrismDamageFrac));
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.PiOver4 + MathHelper.PiOver2 * i;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                        ang.ToRotationVector2() * PrismSpeed,
                        ModContent.ProjectileType<SandveilPrismFlashProj>(), damage, 0.5f, Main.myPlayer);
                }
            }

            //腐化噬骨签名：跃咬尾迹在固定两帧漏下诅咒沙珠（慢速下坠小弹；埋沙帧跳过）
            if (npc.type == NPCID.SandsharkCorrupt && outOfSand
                && (timer == CorruptBeadFrameA || timer == CorruptBeadFrameB)) {
                int damage = Math.Max(1, (int)(npc.damage * BeadDamageFrac));
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                    new Vector2(npc.velocity.X * 0.1f, 0.4f),
                    ModContent.ProjectileType<SandveilCursedBeadProj>(), damage, 0.5f, Main.myPlayer);
            }

            //撞墙或提前扎回沙里（滞空过半后）都提前收势：突进撞空=反制有效
            bool earlyEnd = npc.collideX || (timer > strikeFlight / 2 && !outOfSand);
            if (timer >= strikeFlight || earlyEnd) {
                timer = SharkRecoverFrames;
                phase = PhaseSharkRecover;
                npc.netUpdate = true;
            }
        }

        //======== 滚草·三连压制弹跳 ========

        /// <summary>着地且目标在带内时起手：压地蓄力（蓄力环 omen 承载全端可见性）</summary>
        private void TryStartTumble(NPC npc, Player player) {
            cooldown = RetryDelay;
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Center.Y - npc.Center.Y);
            if (dx < TumbleMinRangeX || dx > TumbleMaxRangeX || dy > TumbleMaxRangeY || !CanSee(npc, player)) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<SandveilBounceMarkOmen>()) >= MarkCap) {
                return;
            }
            if (npc.velocity.Y != 0f && !npc.collideY) {
                //原版滚草几乎一直在蹦：只差触地条件时贴帧复查，逮住下一次落地瞬间
                cooldown = 1;
                return;
            }

            //蓄力环：模式 1=压地蓄力（跟随语义无落点承诺），生成失败则整次作废
            int ring = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom, Vector2.Zero,
                ModContent.ProjectileType<SandveilBounceMarkOmen>(), 0, 0f, Main.myPlayer,
                SrcPack(npc), TumbleWindupFrames, 1f);
            if (ring < 0 || ring >= Main.maxProjectiles) {
                return;
            }
            npc.velocity.X *= 0.3f;
            npc.netUpdate = true;
            timer = TumbleWindupFrames;
            hopIndex = 0;
            phase = PhaseTumbleWindup;
        }

        private void TickTumble(NPC npc) {
            if (phase == PhaseTumbleWindup) {
                timer--;
                //压地蓄力：横速持续压死（可见的急停+蓄力环+压地尘=前摇信号）
                npc.velocity.X *= 0.8f;
                if (timer % 6 == 0) {
                    npc.netUpdate = true;
                }
                if (timer <= 0) {
                    LaunchHop(npc);
                }
                return;
            }
            if (phase == PhaseTumbleHop) {
                TickHop(npc);
                return;
            }
            //力竭滚停：30 帧惩罚窗，只衰减不进攻
            timer--;
            npc.velocity.X *= 0.82f;
            if (timer <= 0) {
                npc.velocity.X = 0f;
                npc.netUpdate = true;
                EndMove(TumbleCooldownByTier);
            }
        }

        /// <summary>
        /// 起跳：落点在起跳帧锁定（预告即承诺），先落标记 omen 再解算弹道。
        /// 三跳滞空递增=高度递增；目标悬空或标记生成失败则直接力竭（失败方向=安全方向）
        /// </summary>
        private void LaunchHop(NPC npc) {
            hopIndex++;
            int flight = HopFlightFrames[hopIndex - 1];
            Player player = npc.HasValidTarget ? Main.player[npc.target] : null;
            if (player == null || !player.Alives() || !TryFindGround(player, out Vector2 ground)) {
                BeginExhaust(npc);
                return;
            }
            lockPoint = ground - Vector2.UnitY * 4f;
            int mark = Projectile.NewProjectile(npc.GetSource_FromAI(), lockPoint, Vector2.Zero,
                ModContent.ProjectileType<SandveilBounceMarkOmen>(), 0, 0f, Main.myPlayer,
                SrcPack(npc), flight + SandveilBounceMarkOmen.LingerFrames, 0f);
            if (mark < 0 || mark >= Main.maxProjectiles) {
                BeginExhaust(npc);
                return;
            }

            //定时长跳弧解算：位移项除提速补偿，重力项不除（镜像 NightPack 跳弧口径）
            float gain = MoveGain(npc);
            Vector2 to = lockPoint - npc.Bottom;
            hopVx = MathHelper.Clamp(to.X / (flight * gain), -HopMaxVx, HopMaxVx);
            npc.velocity = new Vector2(hopVx,
                MathHelper.Clamp(to.Y / (flight * gain) - NpcGravity * flight * 0.5f, HopMaxUpVy, 2f));
            npc.netUpdate = true;
            timer = 0;
            phase = PhaseTumbleHop;
        }

        private void TickHop(NPC npc) {
            timer++;
            int flight = HopFlightFrames[hopIndex - 1];
            //横速持有：抵住原版冲锋 AI 的空中加速，弧线兑现落点标记；纵向交给原版重力
            npc.velocity.X = hopVx;
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            bool landed = timer > HopMinAirFrames && (npc.collideY || npc.velocity.Y == 0f);
            if (!landed && timer < flight + HopTimeoutPad) {
                return;
            }
            if (hopIndex >= 3 || !landed) {
                //三跳完成或卡地形超时：力竭滚停
                BeginExhaust(npc);
                return;
            }
            LaunchHop(npc);
        }

        private void BeginExhaust(NPC npc) {
            npc.velocity.X *= 0.5f;
            npc.netUpdate = true;
            timer = TumbleExhaustFrames;
            phase = PhaseTumbleExhaust;
        }

        //======== 沙元素·沙龙卷阵 ========

        /// <summary>脚下有地时起阵：扇形三道涌沙标记（中央道具名空缺），仪式与提交全由阵列 omen 承载</summary>
        private void TryStartVortex(NPC npc, Player player) {
            cooldown = RetryDelay;
            float dist = npc.Distance(player.Center);
            if (dist < VortexMinRange || dist > VortexMaxRange) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<SandveilVortexArrayOmen>()) >= ArrayCap
                || CountActive(ModContent.ProjectileType<SandveilSurgeColumnProj>()) >= ColumnCap) {
                return;
            }
            //阵锚：沙元素正下方地表（悬空过高则放弃）
            Point self = npc.Bottom.ToTileCoordinates();
            Vector2 anchor = default;
            bool found = false;
            for (int dy = 0; dy < GroundSearchTiles + 6; dy++) {
                int tileY = self.Y + dy;
                if (!WorldGen.InWorld(self.X, tileY, 10)) {
                    break;
                }
                if (WorldGen.SolidTile(self.X, tileY)) {
                    anchor = new Vector2(self.X * 16f + 8f, tileY * 16f);
                    found = true;
                    break;
                }
            }
            if (!found) {
                return;
            }

            //预告即承诺：阵向（朝目标一侧）在此帧锁死，写进 ai 随生成包同步
            bool dirNegative = player.Center.X < npc.Center.X;
            int damage = Math.Max(1, (int)(npc.damage * ColumnDamageFrac));
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), anchor, Vector2.Zero,
                ModContent.ProjectileType<SandveilVortexArrayOmen>(), damage, 1f, Main.myPlayer,
                SrcPack(npc), SandveilVortexArrayOmen.Pack(dirNegative, boundTier));
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                omenIndex = -1;
                return;
            }
            timer = SandveilVortexArrayOmen.RitualFrames;
            phase = PhaseVortexRitual;
        }

        private void TickVortex(NPC npc) {
            timer--;
            //仪式期回读阵列 omen（索引+类型+来源），实体缺位=本轮作废回冷却
            if (timer > 0) {
                if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                    AbortToCooldown();
                    return;
                }
                Projectile omen = Main.projectile[omenIndex];
                if (!omen.active || omen.type != ModContent.ProjectileType<SandveilVortexArrayOmen>()
                    || ((int)omen.ai[0] & 255) != npc.whoAmI + 1) {
                    AbortToCooldown();
                    return;
                }
                return;
            }
            //提交帧之后喷发由柱实体自走，沙元素直接回冷却
            EndMove(VortexCooldownByTier);
        }

        //======== 收尾与命中 ========

        /// <summary>预告体缺位/生成失败的统一回退：退回待机，短冷却复查</summary>
        private void AbortToCooldown() {
            phase = PhaseIdle;
            omenIndex = -1;
            timer = 0;
            cooldown = RetryDelay + Main.rand.Next(31);
        }

        private void EndMove(int[] cooldownByTier) {
            phase = PhaseIdle;
            omenIndex = -1;
            timer = 0;
            cooldown = cooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
        }

        /// <summary>猩红割肉签名：跃咬窗内命中挂流血 3 秒。命中方本机结算（原生同步），
        /// 突进窗由已同步的 omen 实体判定，不读权威端私产计时器</summary>
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) {
            if (boundTier <= 0 || npc.type != NPCID.SandsharkCrimson) {
                return;
            }
            if (SandveilFinSurgeOmen.IsStrikeWindowFor(npc.whoAmI)) {
                target.AddBuff(BuffID.Bleeding, CrimsonBleedTicks);
            }
        }
    }
}
