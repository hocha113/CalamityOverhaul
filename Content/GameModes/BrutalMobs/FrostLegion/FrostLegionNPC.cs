using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.FrostLegion.Projectiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostLegion
{
    /// <summary>雪人军团三型（Main.invasionType==InvasionID.SnowLegion 的全部阵容）</summary>
    internal enum FrostLegionRole
    {
        None,
        /// <summary>雪球点射手</summary>
        Gangsta,
        /// <summary>曲射雪炮手</summary>
        Balla,
        /// <summary>刺客</summary>
        Stabby,
    }

    /// <summary>
    /// 军操错拍调度（镜像 NightPackScheduler 的 lastGrant 思路的独立小型实现，不跨包引用）：
    /// 同型并发出手 ≤2（令牌硬上限），三型之间任意两次出手至少相隔全局节拍
    /// <see cref="GlobalBeatFrames"/>——一板一眼的玩具兵团节奏既是机制卖点也是公平阀门。
    /// 世界级静态，只由权威端决策路径读写；进出世界由 <see cref="FrostLegionDrillReset"/> 清零
    /// </summary>
    internal static class FrostLegionDrill
    {
        /// <summary>全局军操节拍：三型之间两次出手的最小间隔帧（任务口径 ≥30）</summary>
        internal const int GlobalBeatFrames = 30;
        /// <summary>同型同时出手数硬上限</summary>
        internal const int MaxConcurrentPerType = 2;

        private struct Token
        {
            public int NpcIndex;
            public int NpcType;
            public uint ExpireTick;
        }

        /// <summary>存活令牌，容量恒小（≤2×三型）</summary>
        private static readonly List<Token> live = new(8);

        /// <summary>上次放行出手的时刻（三型共用的节拍基准）</summary>
        private static uint lastGrant;

        /// <summary>申请出手令牌：同型并发与全局节拍双闸</summary>
        internal static bool TryAcquire(NPC npc, int leaseTicks) {
            Prune();
            int sameType = 0;
            for (int i = 0; i < live.Count; i++) {
                if (live[i].NpcType == npc.type) {
                    sameType++;
                }
            }
            if (sameType >= MaxConcurrentPerType) {
                return false;
            }
            if (Main.GameUpdateCount - lastGrant < (uint)GlobalBeatFrames) {
                return false;
            }
            live.Add(new Token {
                NpcIndex = npc.whoAmI,
                NpcType = npc.type,
                ExpireTick = Main.GameUpdateCount + (uint)leaseTicks,
            });
            lastGrant = Main.GameUpdateCount;
            return true;
        }

        /// <summary>归还令牌。正常收招时调用；死亡与丢失由租期到期和槽位类型校验兜底</summary>
        internal static void Release(NPC npc) {
            for (int i = live.Count - 1; i >= 0; i--) {
                if (live[i].NpcIndex == npc.whoAmI && live[i].NpcType == npc.type) {
                    live.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>清理过期、死亡与槽位易主的令牌（槽位复用靠类型校验识破）</summary>
        private static void Prune() {
            uint now = Main.GameUpdateCount;
            for (int i = live.Count - 1; i >= 0; i--) {
                Token token = live[i];
                NPC npc = Main.npc[token.NpcIndex];
                if (now >= token.ExpireTick || !npc.active || npc.type != token.NpcType) {
                    live.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 清空令牌与节拍基准。GameUpdateCount 每次进世界归零，跨世界残留的 ExpireTick
        /// 会伪装成远未过期并占死并发位，故进出世界必须清零（镜像 NightPackScheduler 的教训）
        /// </summary>
        internal static void ClearAll() {
            live.Clear();
            lastGrant = 0;
        }
    }

    /// <summary>世界清理钩子：调度器是世界级 static，进出世界统一清零（服务端与单人都会走到）</summary>
    internal class FrostLegionDrillReset : ModSystem
    {
        public override void ClearWorld() => FrostLegionDrill.ClearAll();
    }

    /// <summary>
    /// 残酷模式雪人军团组行为机制层，主题：玩具兵团——一板一眼的军操节奏。
    /// 叠加在原版 AI 之上，不接管：雪球手立定瞄准两连点射（短标线锁向）、
    /// 雪炮手蹲身装填曲射（落点警示环渐亮→抛物线大雪球→落点固定 4 向迸雪片）、
    /// 刺客压身刀光快突（长力竭惩罚窗）；三型共用军操错拍调度 <see cref="FrostLegionDrill"/>。
    /// 阵容声明：三型即雪人军团入侵全部阵容（与 FrostMoon 霜月事件无关，互不重叠）；
    /// 决策与生成只在权威端跑，客户端可见状态一律来自同步弹幕实体；数值层归 GameModeNPC
    /// </summary>
    internal class FrostLegionNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==== 通用节奏 ====
        /// <summary>条件未满足/令牌被拒的重试间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>出生首攻错拍窗（M7 密度预算：60~180 帧）</summary>
        private const int FirstCooldownMin = 60;
        private const int FirstCooldownMax = 180;
        /// <summary>冷却随机抖动上限</summary>
        private const int CooldownJitter = 60;
        /// <summary>向下寻找地表的最大瓦格数（超出视为目标悬空，放弃落点）</summary>
        private const int GroundSearchTiles = 12;

        //==== 雪球手·两连点射 ====
        private static readonly int[] GangstaCooldownByTier = [340, 300, 260];
        private const float GangstaMinRangeX = 160f;
        private const float GangstaMaxRangeX = 540f;
        private const float GangstaMaxRangeY = 320f;
        /// <summary>雪球伤害 = 已缩放 npc.damage × 此值</summary>
        private const float SnowballDamageFrac = 0.45f;
        /// <summary>收势帧（覆盖两连间隔+站桩）</summary>
        private const int GangstaHoldFrames = FlgAimLineOmen.DoubleTapGapFrames + 14;
        /// <summary>令牌租期（预告+两连+收势+余量）</summary>
        private const int GangstaLease = 90;

        //==== 雪炮手·曲射雪炮 ====
        private static readonly int[] BallaCooldownByTier = [460, 420, 380];
        private const float BallaMinRange = 200f;
        private const float BallaMaxRange = 640f;
        /// <summary>雪片伤害 = 已缩放 npc.damage × 此值</summary>
        private const float ShardDamageFrac = 0.45f;
        /// <summary>装填后收势帧（炮弹飞行与迸裂由警示环全权持有）</summary>
        private const int BallaRecoverFrames = 16;
        /// <summary>落点警示环全局并发上限（令牌之外的弹幕密度阀）</summary>
        private const int MortarRingCap = 4;
        private const int BallaLease = 90;

        //==== 刺客·快突 ====
        private static readonly int[] StabbyCooldownByTier = [420, 380, 340];
        private const float StabbyMinRangeX = 100f;
        private const float StabbyMaxRangeX = 320f;
        private const float StabbyMaxRangeY = 140f;
        /// <summary>快突名义峰速（注入前除回 MoveGain；玻璃刺客：快但力竭窗给足）</summary>
        private static readonly float[] StabbyPeakByTier = [13f, 14f, 15f];
        /// <summary>快突包络三段：之和须等于 FlgStabGlintOmen.StrikeFrames</summary>
        private const int StabbyRise = 4;
        private const int StabbyHold = 9;
        private const int StabbyDecay = 9;
        /// <summary>长力竭帧（任务口径 30：惩罚窗给足）</summary>
        private const int StabbyFatigueFrames = 30;
        private const int StabbyLease = 110;

        private const byte PhaseIdle = 0;
        private const byte PhaseTelegraph = 1;
        private const byte PhaseStrike = 2;
        private const byte PhaseRecover = 3;

        /// <summary>本个体出生时绑定的档位，0=未绑定（中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private FrostLegionRole role;
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>锁定方向（弧度；刺客只用 0/π 表示横向朝向。锁定后不再改写，预告即承诺）</summary>
        private float lockDir;
        /// <summary>本次出手的预告体槽位（权威端私产）</summary>
        private int omenIndex = -1;

        private static FrostLegionRole ResolveRole(int type) => type switch {
            NPCID.SnowmanGangsta => FrostLegionRole.Gangsta,
            NPCID.SnowBalla => FrostLegionRole.Balla,
            NPCID.MisterStabby => FrostLegionRole.Stabby,
            _ => FrostLegionRole.None,
        };

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && ResolveRole(entity.type) != FrostLegionRole.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            role = FrostLegionRole.None;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            FrostLegionRole resolved = ResolveRole(npc.type);
            if (resolved == FrostLegionRole.None) {
                return;
            }
            role = resolved;
            boundTier = tier;
            //首攻错拍：此刻 npc.whoAmI 恒为 0（NewNPC 之后才赋值），不可用作错拍源；
            //冷却是权威端决策私产，Main.rand 无同步语义
            cooldown = FirstCooldownMin + Main.rand.Next(FirstCooldownMax - FirstCooldownMin + 1);
        }

        /// <summary>机制入口资格：友方/无敌/Boss/雕像怪/共享血池体节逐项排除（每个机制入口都要过）</summary>
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
        /// 提速位移补偿：GameModeNPC.PostAI 对非 Boss 怪按 velocity×SpeedBonus 追加位置推进，
        /// 本层注入的承诺性速度一律除回该系数（位移项除回、重力项不除）
        /// </summary>
        private float MoveGain(NPC npc) => !npc.boss && npc.realLife < 0 ? 1f + GameModeTuning.SpeedBonus(boundTier) : 1f;

        /// <summary>统计某类弹幕的活动实例数（只在触发时调用，非每帧；自愈无漂移）</summary>
        private static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        private static bool CanSee(NPC npc, Player player)
            => Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height);

        /// <summary>来源打包：槽位+1 与类型合写，预告实体的取消检查与 NPC 侧回读共用此值</summary>
        private static int PackSource(NPC npc) => (npc.whoAmI + 1) | (npc.type << 8);

        /// <summary>NPC 侧回读校验绑定的预告实体（索引+类型+归属），缺位=不打无预告的招</summary>
        private bool OmenBound(NPC npc, int projType) {
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile proj = Main.projectile[omenIndex];
            return proj.active && proj.type == projType && (int)proj.ai[0] == PackSource(npc);
        }

        /// <summary>从目标脚下向下找可站立地表，返回落点锚点（找不到视为悬空，放弃曲射）</summary>
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

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (VaultUtils.isClient) {
                return;//决策只在权威端，客户端画面全部来自同步弹幕实体与 NPC 速度原生同步
            }

            if (phase == PhaseIdle) {
                if (--cooldown > 0) {
                    return;
                }
                TryStart(npc);
                return;
            }

            switch (role) {
                case FrostLegionRole.Gangsta:
                    if (phase == PhaseTelegraph) {
                        TickGangstaAim(npc);
                    }
                    else {
                        TickGangstaHold(npc);
                    }
                    return;
                case FrostLegionRole.Balla:
                    if (phase == PhaseTelegraph) {
                        TickBallaLoad(npc);
                    }
                    else {
                        TickBallaRecover(npc);
                    }
                    return;
                case FrostLegionRole.Stabby:
                    if (phase == PhaseTelegraph) {
                        TickStabbyCrouch(npc);
                    }
                    else if (phase == PhaseStrike) {
                        TickStabbyDash(npc);
                    }
                    else {
                        TickStabbyFatigue(npc);
                    }
                    return;
            }
        }

        /// <summary>预告体缺位/生成失败的回退：还令牌、退回待机短冷却（失败方向=安全方向）</summary>
        private void AbortToCooldown(NPC npc) {
            FrostLegionDrill.Release(npc);
            phase = PhaseIdle;
            omenIndex = -1;
            cooldown = RetryDelay;
        }

        /// <summary>正常收招：还令牌、进正式冷却</summary>
        private void Finish(NPC npc, int[] cooldownByTier) {
            FrostLegionDrill.Release(npc);
            phase = PhaseIdle;
            omenIndex = -1;
            cooldown = cooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
        }

        private void TryStart(NPC npc) {
            if (!Eligible(npc)) {
                cooldown = IneligibleDelay;
                return;
            }
            if (!npc.HasValidTarget) {
                cooldown = RetryDelay;
                return;
            }
            Player target = Main.player[npc.target];
            if (!target.Alives()) {
                cooldown = RetryDelay;
                return;
            }
            switch (role) {
                case FrostLegionRole.Gangsta:
                    TryStartGangsta(npc, target);
                    return;
                case FrostLegionRole.Balla:
                    TryStartBalla(npc, target);
                    return;
                case FrostLegionRole.Stabby:
                    TryStartStabby(npc, target);
                    return;
            }
        }

        //==== 雪球手：立定两连点射 ====

        private void TryStartGangsta(NPC npc, Player target) {
            if (npc.velocity.Y != 0f) {
                cooldown = RetryDelay;
                return;
            }
            float dx = Math.Abs(target.Center.X - npc.Center.X);
            float dy = Math.Abs(target.Center.Y - npc.Center.Y);
            if (dx < GangstaMinRangeX || dx > GangstaMaxRangeX || dy > GangstaMaxRangeY || !CanSee(npc, target)) {
                cooldown = RetryDelay;
                return;
            }
            if (!FrostLegionDrill.TryAcquire(npc, GangstaLease)) {
                cooldown = RetryDelay;
                return;
            }
            int damage = (int)(npc.damage * SnowballDamageFrac);
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<FlgAimLineOmen>(), damage, 1f, Main.myPlayer,
                PackSource(npc), boundTier);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                AbortToCooldown(npc);
                return;
            }
            omenIndex = omen;
            lockDir = (target.Center - npc.Center).ToRotation();
            //立定瞄准：军操点射从站定开始
            npc.velocity.X *= 0.15f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = FlgAimLineOmen.TelegraphFrames;
        }

        private void TickGangstaAim(NPC npc) {
            timer--;
            if (!OmenBound(npc, ModContent.ProjectileType<FlgAimLineOmen>())) {
                AbortToCooldown(npc);
                return;
            }
            //立定：离散刹车脉冲压住走位（脉冲帧才跟同步）
            if (timer == 22 || timer == 10) {
                npc.velocity.X *= 0.3f;
                npc.netUpdate = true;
            }
            if (timer == FlgAimLineOmen.LockFrames) {
                //锁向帧：方向自此为承诺，写回标线实体做各端权威纠偏
                if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                    lockDir = (Main.player[npc.target].Center - npc.Center).ToRotation();
                }
                Projectile omen = Main.projectile[omenIndex];
                omen.ai[2] = lockDir + 10f;
                omen.netUpdate = true;
            }
            if (timer <= 0) {
                //两连由标线实体在锁定线上发射，雪人只站桩收势
                phase = PhaseStrike;
                timer = GangstaHoldFrames;
                npc.netUpdate = true;
            }
        }

        private void TickGangstaHold(NPC npc) {
            timer--;
            if (timer % 6 == 0) {
                npc.velocity.X *= 0.5f;
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                Finish(npc, GangstaCooldownByTier);
            }
        }

        //==== 雪炮手：蹲身装填曲射 ====

        private void TryStartBalla(NPC npc, Player target) {
            if (npc.velocity.Y != 0f) {
                cooldown = RetryDelay;
                return;
            }
            float dist = npc.Distance(target.Center);
            if (dist < BallaMinRange || dist > BallaMaxRange) {
                cooldown = RetryDelay;
                return;
            }
            //曲射越障语义：不查视线，落点由警示环诚实宣告
            if (CountActive(ModContent.ProjectileType<FlgMortarRingOmen>()) >= MortarRingCap) {
                cooldown = RetryDelay;
                return;
            }
            if (!TryFindGround(target, out Vector2 basePos)) {
                cooldown = RetryDelay;
                return;
            }
            if (!FrostLegionDrill.TryAcquire(npc, BallaLease)) {
                cooldown = RetryDelay;
                return;
            }
            //预告即承诺：落点在生成帧锁死（环的位置即锁定值）
            int damage = (int)(npc.damage * ShardDamageFrac);
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), basePos, Vector2.Zero,
                ModContent.ProjectileType<FlgMortarRingOmen>(), damage, 1f, Main.myPlayer,
                PackSource(npc), boundTier);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                AbortToCooldown(npc);
                return;
            }
            omenIndex = omen;
            //蹲身装填
            npc.velocity.X *= 0.15f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = FlgMortarRingOmen.LoadFrames;
        }

        private void TickBallaLoad(NPC npc) {
            timer--;
            if (!OmenBound(npc, ModContent.ProjectileType<FlgMortarRingOmen>())) {
                AbortToCooldown(npc);
                return;
            }
            //蹲身：离散刹车脉冲定桩（装填期与警示环渐亮同步）
            if (timer == 24 || timer == 12) {
                npc.velocity.X *= 0.3f;
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                //发射与迸裂由警示环全权持有，炮手只收势
                phase = PhaseStrike;
                timer = BallaRecoverFrames;
                npc.netUpdate = true;
            }
        }

        private void TickBallaRecover(NPC npc) {
            timer--;
            if (timer % 6 == 0) {
                npc.velocity.X *= 0.5f;
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                Finish(npc, BallaCooldownByTier);
            }
        }

        //==== 刺客：压身刀光快突 ====

        private void TryStartStabby(NPC npc, Player target) {
            if (npc.velocity.Y != 0f) {
                cooldown = RetryDelay;
                return;
            }
            float dx = Math.Abs(target.Center.X - npc.Center.X);
            float dy = Math.Abs(target.Bottom.Y - npc.Bottom.Y);
            if (dx < StabbyMinRangeX || dx > StabbyMaxRangeX || dy > StabbyMaxRangeY || !CanSee(npc, target)) {
                cooldown = RetryDelay;
                return;
            }
            if (!FrostLegionDrill.TryAcquire(npc, StabbyLease)) {
                cooldown = RetryDelay;
                return;
            }
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<FlgStabGlintOmen>(), 0, 0f, Main.myPlayer,
                PackSource(npc), boundTier);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                AbortToCooldown(npc);
                return;
            }
            omenIndex = omen;
            //横向朝向初值（锁向帧再定案）
            lockDir = target.Center.X >= npc.Center.X ? 0f : MathHelper.Pi;
            //压低身位
            npc.velocity.X *= 0.1f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = FlgStabGlintOmen.TelegraphFrames;
        }

        private void TickStabbyCrouch(NPC npc) {
            timer--;
            if (!OmenBound(npc, ModContent.ProjectileType<FlgStabGlintOmen>())) {
                AbortToCooldown(npc);
                return;
            }
            if (timer == 20 || timer == FlgStabGlintOmen.LockFrames) {
                npc.velocity.X *= 0.3f;
                npc.netUpdate = true;
            }
            if (timer == FlgStabGlintOmen.LockFrames) {
                //锁向帧：横向朝向自此为承诺
                if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                    lockDir = Main.player[npc.target].Center.X >= npc.Center.X ? 0f : MathHelper.Pi;
                }
                Projectile omen = Main.projectile[omenIndex];
                omen.ai[2] = lockDir + 10f;
                omen.netUpdate = true;
            }
            if (timer <= 0) {
                phase = PhaseStrike;
                timer = FlgStabGlintOmen.StrikeFrames;
                npc.netUpdate = true;
            }
        }

        private void TickStabbyDash(NPC npc) {
            int t = FlgStabGlintOmen.StrikeFrames - timer + 1;
            //横向包络快突：竖直方向交给原版重力（重力项不除提速系数）
            npc.velocity.X = MathF.Cos(lockDir) * (StabbyPeakByTier[boundTier - 1]
                * MobDash.Envelope(t, StabbyRise, StabbyHold, StabbyDecay) / MoveGain(npc));
            if (t % 6 == 0) {
                npc.netUpdate = true;//长保持段低频重推
            }
            timer--;
            if (timer <= 0) {
                phase = PhaseRecover;
                timer = StabbyFatigueFrames;
                npc.netUpdate = true;
            }
        }

        /// <summary>玻璃刺客的长力竭惩罚窗：压住走位，站桩挨打</summary>
        private void TickStabbyFatigue(NPC npc) {
            timer--;
            if (timer % 6 == 0) {
                npc.velocity.X *= 0.4f;
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                Finish(npc, StabbyCooldownByTier);
            }
        }
    }
}
