using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon
{
    /// <summary>
    /// 霜月近战填充怪行为层（与 <see cref="FrmSiegeNPC"/> 攻城矩阵同包互补，类型表互斥）：
    /// 僵尸精灵三型=弯腰团雪→抛物线雪球齐投（基础 3 发标准弧/胡子 2 发重球挂寒颤/女孩 4 发快小球最大散布留中央缺口）；
    /// 姜饼人=搓手蓄力→包络突进沿途滴糖霜减速斑；雪花怪=贴脸膨胀自爆放射冰晶（恒缺口槽）；
    /// 雪怪=抡臂震地锥形冰刺（地面预告实体）。只叠加行为不动数值，原版 AI 全程继续跑；
    /// 决策全在权威端（客户端 PostAI 早退），客户端可见状态一律来自已同步的弹幕实体
    /// </summary>
    internal class FrmMeleeNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private enum MeleeFamily : byte
        {
            None,
            /// <summary>僵尸精灵三型：雪球齐投</summary>
            SnowThrow,
            /// <summary>姜饼人：糖霜突进</summary>
            GingerDash,
            /// <summary>雪花怪：飘雪自爆</summary>
            FlockoBurst,
            /// <summary>雪怪：冰拳震地</summary>
            YetiSlam,
        }

        //==== 通用节奏 ====
        /// <summary>条件未满足的重试间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>出生首攻等待窗（60~180 帧，随机错开避免同屏齐动）</summary>
        private const int FirstCooldownMin = 60;
        private const int FirstCooldownMax = 180;
        private const int CooldownJitter = 50;

        //==== 僵尸精灵·雪球齐投 ====
        /// <summary>雪球伤害 = npc.damage（已缩放值）× 此比例（重球取重比例）</summary>
        private const float SnowballDamageFrac = 0.5f;
        private const float HeavySnowballDamageFrac = 0.62f;
        private const float ElfMinRangeX = 120f;
        private const float ElfMaxRangeX = 520f;
        private const float ElfMaxRangeY = 320f;
        /// <summary>投掷收势帧</summary>
        private const int SnowRecoverFrames = 14;
        /// <summary>同屏雪球并发上限（触发时点算）</summary>
        private const int SnowballCap = 6;
        /// <summary>女孩型扇面槽位数</summary>
        private const int GirlFanSlots = 5;
        /// <summary>具名缺口：女孩型扇内恒跳过的中央落点槽（发射循环真正读取）</summary>
        private const int SnowSpreadGap = 2;
        /// <summary>弹道解算出膛速度封顶</summary>
        private const float MaxThrowSpeed = 16f;

        //==== 姜饼人·糖霜突进 ====
        /// <summary>前摇内的锁向帧（方向自此为承诺）</summary>
        private const int GingerLockFrames = 8;
        /// <summary>突进包络三段（合计=姿态实体的突进伴随帧，两处常量强制同源）</summary>
        private const int GingerDashRise = 8;
        private const int GingerDashDecay = 12;
        private const int GingerDashHold = FrmPoseTelegraphProj.RubDashFrames - GingerDashRise - GingerDashDecay;
        /// <summary>力竭后摇帧</summary>
        private const int GingerRecoverFrames = 16;
        /// <summary>突进名义峰速（档位 1/2/3，注入前除回提速补偿）</summary>
        private static readonly float[] GingerDashSpeedByTier = [8.5f, 9.5f, 10.5f];
        private const float GingerMinRangeX = 110f;
        private const float GingerMaxRangeX = 480f;
        private const float GingerMaxRangeY = 140f;
        private const int GingerDashCooldown = 430;
        /// <summary>糖霜贴片全局并发上限（滴落时点算，具名闸）</summary>
        private const int IcingPatchCap = 4;
        /// <summary>贴片滴落帧（突进时间轴上的固定点；第二滴档位 2+）</summary>
        private const int PatchDropFrameA = 12;
        private const int PatchDropFrameB = 26;
        private const int PatchGroundScanTiles = 6;

        //==== 雪花怪·飘雪自爆 ====
        /// <summary>触发距离（贴近玩家即入膨胀）</summary>
        private const float FlockoTriggerRange = 120f;
        private const float FlockoShardDamageFrac = 0.55f;
        /// <summary>放射冰晶速度（档位 1/2/3；弹幕不吃提速层，无需补偿）</summary>
        private static readonly float[] FlockoShardSpeedByTier = [6f, 6.8f, 7.6f];
        /// <summary>放射冰晶微坠（近直线）</summary>
        private const float FlockoShardGravity = 0.02f;
        /// <summary>同屏膨胀预告并发上限</summary>
        private const int FlockoBurstCap = 4;
        /// <summary>膨胀作废（预告体缺位等）后的复查间隔</summary>
        private const int FlockoAbortCooldown = 90;

        //==== 雪怪·冰拳震地 ====
        private const float YetiShardDamageFrac = 0.6f;
        private const float YetiMinRangeX = 90f;
        private const float YetiMaxRangeX = 430f;
        private const float YetiMaxRangeY = 200f;
        /// <summary>收势后摇帧</summary>
        private const int YetiRecoverFrames = 24;
        private const int YetiSlamCooldown = 470;
        /// <summary>同屏震地预告并发上限</summary>
        private const int YetiConeCap = 3;
        /// <summary>震源相对身位的前置距离（像素）</summary>
        private const float YetiOriginAhead = 34f;
        private const int YetiGroundScanTiles = 8;

        private const byte PhaseIdle = 0;
        private const byte PhaseTelegraph = 1;
        private const byte PhaseStrike = 2;
        private const byte PhaseRecover = 3;

        /// <summary>出生绑定档位，0=未绑定（中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private MeleeFamily family;
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>姜饼突进锁向（±1，承诺后不再改写）</summary>
        private float lockDirX;
        /// <summary>雪花怪放射环基准角（膨胀起始锁定）</summary>
        private float lockedAngle;
        /// <summary>本次攻击的预兆槽位（权威端私产，回读时索引+类型双校验）</summary>
        private int omenIndex = -1;

        private static MeleeFamily ResolveFamily(int type) {
            if (type == NPCID.ZombieElf || type == NPCID.ZombieElfBeard || type == NPCID.ZombieElfGirl) {
                return MeleeFamily.SnowThrow;
            }
            if (type == NPCID.GingerbreadMan) {
                return MeleeFamily.GingerDash;
            }
            if (type == NPCID.Flocko) {
                return MeleeFamily.FlockoBurst;
            }
            if (type == NPCID.Yeti) {
                return MeleeFamily.YetiSlam;
            }
            return MeleeFamily.None;
        }

        /// <summary>
        /// 僵尸精灵型号表（M6 同族差异）：槽位数/雪球风味/落点间距/飞行帧/基准冷却/缺口槽（-1=无缺口）。
        /// 基础型 3 发标准弧；胡子型 2 发重雪球（大慢弧挂寒颤）；
        /// 女孩型 5 槽 4 发快小球（散布最大，中央槽走 <see cref="SnowSpreadGap"/> 恒空）
        /// </summary>
        private static (int slots, int flavor, float step, int flight, int cooldown, int gapSlot) ElfProfile(int type) {
            if (type == NPCID.ZombieElfBeard) {
                return (2, FrmSnowballProj.FlavorHeavy, 84f, 60, 390, -1);
            }
            if (type == NPCID.ZombieElfGirl) {
                return (GirlFanSlots, FrmSnowballProj.FlavorSmall, 66f, 42, 330, SnowSpreadGap);
            }
            return (3, FrmSnowballProj.FlavorStandard, 56f, 50, 340, -1);
        }

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && ResolveFamily(entity.type) != MeleeFamily.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            family = MeleeFamily.None;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            family = ResolveFamily(npc.type);
            if (family == MeleeFamily.None) {
                return;
            }
            boundTier = tier;
            //冷却是权威端决策私产（无同步语义），Main.rand 播种合法；不读此刻恒为 0 的 whoAmI
            cooldown = FirstCooldownMin + Main.rand.Next(FirstCooldownMax - FirstCooldownMin + 1);
        }

        /// <summary>机制入口资格：友方/无敌/Boss/小动物载体/雕像怪/共享血池体节逐项排除（每次触发复查）</summary>
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
        /// 本层注入的承诺性速度一律除回该系数（位移项除、重力项不除），运行时读旗标
        /// </summary>
        private float MoveGain(NPC npc)
            => !npc.boss && npc.realLife < 0 ? 1f + GameModeTuning.SpeedBonus(boundTier) : 1f;

        /// <summary>档位冷却系数（机制只调强度不换形态）</summary>
        private int TierCooldown(int baseCooldown)
            => (int)(baseCooldown * (boundTier >= 3 ? 0.7f : boundTier >= 2 ? 0.85f : 1f))
               + Main.rand.Next(CooldownJitter + 1);

        /// <summary>校验自己名下的预兆弹幕仍有效（索引+类型双校验，防槽位复用；ai[0]=锚索引口径）</summary>
        private bool TryGetOmen(int projType, int npcIndex, out Projectile proj) {
            proj = null;
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile p = Main.projectile[omenIndex];
            if (!p.active || p.type != projType || (int)p.ai[0] != npcIndex) {
                return false;
            }
            proj = p;
            return true;
        }

        /// <summary>雪怪震地预告的回读校验（来源打包在 ai[2]：NPC+1|类型&lt;&lt;8）</summary>
        private bool TryGetYetiOmen(NPC npc, out Projectile proj) {
            proj = null;
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile p = Main.projectile[omenIndex];
            if (!p.active || p.type != ModContent.ProjectileType<FrmYetiConeOmen>()
                || ((int)p.ai[2] & 255) - 1 != npc.whoAmI) {
                return false;
            }
            proj = p;
            return true;
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (VaultUtils.isClient) {
                //决策只在权威端；客户端画面全部来自已同步的弹幕实体
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
                case MeleeFamily.SnowThrow:
                    SnowActive(npc);
                    break;
                case MeleeFamily.GingerDash:
                    GingerActive(npc);
                    break;
                case MeleeFamily.FlockoBurst:
                    FlockoActive(npc);
                    break;
                case MeleeFamily.YetiSlam:
                    YetiActive(npc);
                    break;
                default:
                    phase = PhaseIdle;
                    break;
            }
        }

        private void TryStart(NPC npc) {
            if (!Eligible(npc)) {
                cooldown = IneligibleDelay;
                return;
            }
            if (npc.target < 0 || npc.target >= Main.maxPlayers) {
                cooldown = RetryDelay;
                return;
            }
            Player player = Main.player[npc.target];
            if (!player.Alives()) {
                cooldown = RetryDelay;
                return;
            }

            switch (family) {
                case MeleeFamily.SnowThrow:
                    TryStartSnowVolley(npc, player);
                    break;
                case MeleeFamily.GingerDash:
                    TryStartGingerDash(npc, player);
                    break;
                case MeleeFamily.FlockoBurst:
                    TryStartFlockoSwell(npc, player);
                    break;
                case MeleeFamily.YetiSlam:
                    TryStartYetiSlam(npc, player);
                    break;
            }
        }

        private static bool CanSee(NPC npc, Player player)
            => Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height);

        #region 僵尸精灵·雪球齐投
        private void TryStartSnowVolley(NPC npc, Player player) {
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Center.Y - npc.Center.Y);
            if (npc.velocity.Y != 0f || dx < ElfMinRangeX || dx > ElfMaxRangeX || dy > ElfMaxRangeY
                || !CanSee(npc, player)) {
                cooldown = RetryDelay;
                return;
            }
            if (FrmSiegeUtils.CountProjOfType(ModContent.ProjectileType<FrmSnowballProj>()) >= SnowballCap) {
                cooldown = RetryDelay + 15;
                return;
            }
            //姿态实体承载前摇可见性；生成失败则整次投掷作废（无预告不出手）
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<FrmPoseTelegraphProj>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, FrmPoseTelegraphProj.StyleSnowGather * 1000 + npc.type, 0f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                cooldown = RetryDelay;
                return;
            }
            //弯腰团雪：压速蓄势
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = FrmPoseTelegraphProj.SnowWindupFrames;
        }

        private void SnowActive(NPC npc) {
            if (phase == PhaseTelegraph) {
                timer--;
                //中段再刹一次，压住走位漂移
                if (timer == 16) {
                    npc.velocity.X *= 0.3f;
                    npc.netUpdate = true;
                }
                if (!TryGetOmen(ModContent.ProjectileType<FrmPoseTelegraphProj>(), npc.whoAmI, out _)) {
                    //姿态实体缺位：无预告不出手（失败方向=安全方向）
                    phase = PhaseIdle;
                    omenIndex = -1;
                    cooldown = TierCooldown(300);
                    return;
                }
                if (timer <= 0) {
                    ThrowVolley(npc);
                    phase = PhaseRecover;
                    timer = SnowRecoverFrames;
                }
                return;
            }

            //收势
            if (--timer <= 0) {
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = TierCooldown(ElfProfile(npc.type).cooldown);
            }
        }

        /// <summary>投掷帧：落点=此刻目标位置 ± 型号落点间距（女孩型中央槽恒空），定时长抛物线解算</summary>
        private void ThrowVolley(NPC npc) {
            if (npc.target < 0 || npc.target >= Main.maxPlayers || !Main.player[npc.target].Alives()) {
                return;//目标没了：不出手（收势照走）
            }
            Player player = Main.player[npc.target];
            (int slots, int flavor, float step, int flight, int _, int gapSlot) = ElfProfile(npc.type);
            int damage = Math.Max(1, (int)(npc.damage
                * (flavor == FrmSnowballProj.FlavorHeavy ? HeavySnowballDamageFrac : SnowballDamageFrac)));
            Vector2 origin = npc.Center + new Vector2(npc.spriteDirection * 8f, -10f);
            float gravity = FrmSnowballProj.GravityFor(flavor);

            for (int i = 0; i < slots; i++) {
                if (gapSlot >= 0 && i == gapSlot) {
                    continue;//具名缺口 SnowSpreadGap：中央落点恒空（逃生位）
                }
                float targetX = player.Center.X + (i - (slots - 1) * 0.5f) * step;
                Vector2 d = new Vector2(targetX, player.Center.Y) - origin;
                //定时长弹道解算：位移项 v=d/T，重力项回扣 g(T+1)/2（AI 每帧先加重力后位移）；
                //弹幕不吃 GameModeNPC 提速层，无需补偿
                Vector2 vel = new Vector2(d.X / flight, d.Y / flight - gravity * (flight + 1) * 0.5f);
                if (vel.Length() > MaxThrowSpeed) {
                    vel = vel.SafeNormalize(Vector2.UnitX) * MaxThrowSpeed;
                }
                Projectile.NewProjectile(npc.GetSource_FromAI(), origin, vel,
                    ModContent.ProjectileType<FrmSnowballProj>(), damage, 0.6f, Main.myPlayer, flavor);
            }
            //出手顿挫（投掷帧跟同步）
            npc.velocity.X *= 0.5f;
            npc.netUpdate = true;
        }
        #endregion

        #region 姜饼人·糖霜突进
        private void TryStartGingerDash(NPC npc, Player player) {
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Center.Y - npc.Center.Y);
            if (npc.velocity.Y != 0f || dx < GingerMinRangeX || dx > GingerMaxRangeX || dy > GingerMaxRangeY
                || !CanSee(npc, player)) {
                cooldown = RetryDelay;
                return;
            }
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<FrmPoseTelegraphProj>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, FrmPoseTelegraphProj.StyleHandRub * 1000 + npc.type, 0f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                cooldown = RetryDelay;
                return;
            }
            //搓手蓄势：急停
            npc.velocity.X *= 0.15f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = FrmPoseTelegraphProj.RubWindupFrames;
        }

        private void GingerActive(NPC npc) {
            int dashTotal = GingerDashRise + GingerDashHold + GingerDashDecay;
            if (phase == PhaseTelegraph) {
                timer--;
                if (timer == 14) {
                    npc.velocity.X *= 0.3f;
                    npc.netUpdate = true;
                }
                if (!TryGetOmen(ModContent.ProjectileType<FrmPoseTelegraphProj>(), npc.whoAmI, out Projectile omen)) {
                    phase = PhaseIdle;
                    omenIndex = -1;
                    cooldown = TierCooldown(GingerDashCooldown);
                    return;
                }
                if (timer == GingerLockFrames) {
                    //锁向帧：方向自此为承诺，写回姿态实体亮出方向楔
                    lockDirX = npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()
                        ? (Main.player[npc.target].Center.X >= npc.Center.X ? 1f : -1f)
                        : npc.direction >= 0 ? 1f : -1f;
                    omen.ai[2] = (lockDirX > 0f ? 0f : MathHelper.Pi) + 10f;
                    omen.netUpdate = true;
                }
                if (timer <= 0) {
                    //突进帧：速度注入自此走包络（相位沿跟同步）
                    phase = PhaseStrike;
                    timer = dashTotal;
                    npc.netUpdate = true;
                }
                return;
            }

            if (phase == PhaseStrike) {
                timer--;
                int t = dashTotal - timer;
                //包络塑形（缓入→峰值→力竭），承诺性速度除回提速补偿；纵向留给重力
                float envelope = MobDash.Envelope(t, GingerDashRise, GingerDashHold, GingerDashDecay);
                npc.velocity.X = lockDirX * GingerDashSpeedByTier[boundTier - 1] * envelope / MoveGain(npc);
                if (timer % 6 == 0) {
                    npc.netUpdate = true;
                }
                //撞墙即入力竭段（冲撞落空=反制有效）
                if (npc.collideX && timer > GingerDashDecay) {
                    timer = GingerDashDecay;
                    npc.netUpdate = true;
                }
                //沿途滴落糖霜减速斑（全局并发闸内）
                if (t == PatchDropFrameA || (boundTier >= 2 && t == PatchDropFrameB)) {
                    TryDropIcingPatch(npc);
                }
                if (timer <= 0) {
                    //力竭：清残速把控制权还给原版 AI
                    npc.velocity.X *= 0.2f;
                    npc.netUpdate = true;
                    phase = PhaseRecover;
                    timer = GingerRecoverFrames;
                }
                return;
            }

            if (--timer <= 0) {
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = TierCooldown(GingerDashCooldown);
            }
        }

        /// <summary>滴落一片糖霜贴片：全局并发 ≤<see cref="IcingPatchCap"/>，无地表不滴（失败方向=安全方向）</summary>
        private void TryDropIcingPatch(NPC npc) {
            if (FrmSiegeUtils.CountProjOfType(ModContent.ProjectileType<FrmIcingPatchProj>()) >= IcingPatchCap) {
                return;
            }
            if (!FrmSiegeUtils.TryFindGroundY(npc.Bottom - Vector2.UnitY * 8f, PatchGroundScanTiles, out float groundY)) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), new Vector2(npc.Center.X, groundY - 6f),
                Vector2.Zero, ModContent.ProjectileType<FrmIcingPatchProj>(), 0, 0f, Main.myPlayer);
        }
        #endregion

        #region 雪花怪·飘雪自爆
        private void TryStartFlockoSwell(NPC npc, Player player) {
            if (Vector2.Distance(npc.Center, player.Center) > FlockoTriggerRange) {
                cooldown = RetryDelay;
                return;
            }
            if (FrmSiegeUtils.CountProjOfType(ModContent.ProjectileType<FrmFlockoBurstOmen>()) >= FlockoBurstCap) {
                cooldown = RetryDelay + 15;
                return;
            }
            //放射环基准角=此刻指向玩家（自此锁死，预告即承诺；缺口槽即此方向）
            lockedAngle = (player.Center - npc.Center).ToRotation();
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<FrmFlockoBurstOmen>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, lockedAngle, npc.type);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                cooldown = RetryDelay;
                return;
            }
            //膨胀起步：压住漂移
            npc.velocity *= 0.4f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = FrmFlockoBurstOmen.SwellFrames;
        }

        private void FlockoActive(NPC npc) {
            timer--;
            //低频压速脉冲：爆点尽量贴住预告实体（脉冲帧才跟同步）
            if (timer % 8 == 0) {
                npc.velocity *= 0.8f;
                npc.netUpdate = true;
            }
            if (!TryGetOmen(ModContent.ProjectileType<FrmFlockoBurstOmen>(), npc.whoAmI, out _)) {
                //预告体缺位：整次自爆作废（无预告不爆，失败方向=安全方向）
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = FlockoAbortCooldown;
                return;
            }
            if (timer > 0) {
                return;
            }

            //提交帧（权威端）：放射冰晶——发射循环真正跳过 BurstGapSlot 缺口槽
            int damage = Math.Max(1, (int)(npc.damage * FlockoShardDamageFrac));
            float speed = FlockoShardSpeedByTier[boundTier - 1];
            int shardType = ModContent.ProjectileType<FrmIceShardProj>();
            for (int slot = 0; slot < FrmFlockoBurstOmen.BurstSlots; slot++) {
                if (!FrmFlockoBurstOmen.SlotArmed(slot)) {
                    continue;//具名缺口槽：逃生方向
                }
                float ang = FrmFlockoBurstOmen.SlotAngle(lockedAngle, slot);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                    ang.ToRotationVector2() * speed, shardType, damage, 0.5f, Main.myPlayer,
                    FlockoShardGravity);
            }
            //自爆消亡：权威端走原版死亡（掉落/尸块/同步由 checkDead 承担）
            npc.life = 0;
            npc.HitEffect();
            npc.checkDead();
            npc.netUpdate = true;
            //防御性回正（正常情况下实体已消亡，不会再被读取）
            phase = PhaseIdle;
            omenIndex = -1;
            cooldown = FlockoAbortCooldown;
        }
        #endregion

        #region 雪怪·冰拳震地
        private void TryStartYetiSlam(NPC npc, Player player) {
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Center.Y - npc.Center.Y);
            if (npc.velocity.Y != 0f || dx < YetiMinRangeX || dx > YetiMaxRangeX || dy > YetiMaxRangeY
                || !CanSee(npc, player)) {
                cooldown = RetryDelay;
                return;
            }
            if (FrmSiegeUtils.CountProjOfType(ModContent.ProjectileType<FrmYetiConeOmen>()) >= YetiConeCap) {
                cooldown = RetryDelay + 15;
                return;
            }
            //震源=身前地表（生成帧锁死，预告即承诺）；无地表则不出拳
            float aheadX = npc.Center.X + (player.Center.X >= npc.Center.X ? 1f : -1f) * YetiOriginAhead;
            if (!FrmSiegeUtils.TryFindGroundY(new Vector2(aheadX, npc.Bottom.Y - 8f), YetiGroundScanTiles, out float groundY)) {
                cooldown = RetryDelay;
                return;
            }
            Vector2 origin = new Vector2(aheadX, groundY - 6f);
            float aim = (player.Center - origin).ToRotation();
            int damage = Math.Max(1, (int)(npc.damage * YetiShardDamageFrac));
            int bonus = boundTier >= 3 ? 2 : boundTier >= 2 ? 1 : 0;
            //缺口偏向侧权威掷定，随生成包同步
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), origin, Vector2.Zero,
                ModContent.ProjectileType<FrmYetiConeOmen>(), damage, 1f, Main.myPlayer,
                aim, FrmYetiConeOmen.Pack(bonus, Main.rand.NextBool()),
                (npc.whoAmI + 1) | (npc.type << 8));
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                cooldown = RetryDelay;
                return;
            }
            //抡臂蓄势：急停
            npc.velocity.X *= 0.15f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = FrmYetiConeOmen.TelegraphFrames;
        }

        private void YetiActive(NPC npc) {
            if (phase == PhaseTelegraph) {
                timer--;
                //离散刹车脉冲压住走位漂移，让震源贴住预告实体
                if (timer == 24 || timer == 12) {
                    npc.velocity.X *= 0.3f;
                    npc.netUpdate = true;
                }
                if (!TryGetYetiOmen(npc, out _)) {
                    //预告体缺位：发射由实体承担，实体没了攻击不会发生，直接回冷却
                    phase = PhaseIdle;
                    omenIndex = -1;
                    cooldown = TierCooldown(YetiSlamCooldown);
                    return;
                }
                if (timer <= 0) {
                    //提交帧由预告实体自治发射（各端同一时间轴）；本体入收势后摇
                    npc.velocity.X *= 0.2f;
                    npc.netUpdate = true;
                    phase = PhaseRecover;
                    timer = YetiRecoverFrames;
                }
                return;
            }

            if (--timer <= 0) {
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = TierCooldown(YetiSlamCooldown);
            }
        }
        #endregion
    }
}
