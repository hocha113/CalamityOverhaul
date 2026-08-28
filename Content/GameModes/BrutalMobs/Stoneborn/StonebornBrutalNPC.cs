using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.Stoneborn.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Stoneborn
{
    /// <summary>
    /// 残酷模式石灵组（花岗岩洞+大理石洞）行为机制层，主题：石之仪式
    /// （花岗岩=能量共振，大理石=希腊神话决斗礼）。叠加在原版 AI 之上，不接管、不动数值。
    /// 名单（两座石厅全员覆盖，无排除项）：
    /// 花岗岩魔像=共振震地（立定蓄能→双向地表共振波）+ 入壳反震（近战压制触发的环脉冲）、
    /// 花岗岩精=弧光俯冲（追踪→锁定→冻结标线→包络俯冲，轨迹留 30 帧残电）、
    /// 美杜莎=凝视重做——本层不改石化机制本身，只给它补预告与惩罚窗
    /// （前奏束 ≥40 帧 + 凝视后 60 帧疲劳演出，判定完全归原版）、
    /// 豪杰骷髅=标枪三连（后撤瞄准→锁线三投，第 2 枪固定抬高=可读的节奏缺口）。
    /// 原版蹲缩防御保留；蹲缩期原版置 dontTakeDamage，命中钩子物理上不触发，
    /// 反震因此挂在入壳沿上：入壳前窗口内近战命中 ≥3 次即触发（语义=被打出来的壳会放电）。
    /// 石醒氛围联动暂不接（后续统一做）。
    /// 决策与生成只在权威端跑，客户端可见状态一律来自同步弹幕实体与原版同步原语；
    /// 数值增强由 GameModeNPC 统一负责，此处只加行为
    /// </summary>
    internal class StonebornBrutalNPC : GlobalNPC
    {
        //==== 通用节奏（M7 密度预算） ====
        /// <summary>出生首攻错拍窗（遭遇 ≤3 秒可见首个机制）</summary>
        private const int FirstCooldownMin = 60;
        private const int FirstCooldownMax = 180;
        /// <summary>触发条件不满足时的复查间隔</summary>
        private const int TriggerRetryFrames = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>预告体缺位/被打断的回退冷却（失败方向=安全方向）</summary>
        private const int AbortCooldown = 60;
        /// <summary>主力招冷却抖动上限</summary>
        private const int CooldownJitter = 60;

        //==== 花岗岩魔像·共振震地 ====
        /// <summary>震地冷却（档位只调频率不换机制）</summary>
        private static readonly int[] StompCooldownByTier = [520, 470, 420];
        /// <summary>共振波存续帧=射程预算（档位只加射程，波速与波高不变）</summary>
        private static readonly int[] WaveLifeByTier = [96, 108, 120];
        private const float StompMinRangeX = 90f;
        private const float StompMaxRangeX = 560f;
        private const float StompMaxRangeY = 220f;
        /// <summary>震地收势帧（任务口径 10 帧）</summary>
        private const int StompRecoverFrames = 10;
        /// <summary>共振波伤害 = 已缩放 npc.damage × 此值</summary>
        private const float WaveDamageFrac = 0.5f;
        /// <summary>蓄能载体/共振波全局并发上限</summary>
        private const int ChargeCap = 6;
        private const int WaveCap = 6;

        //==== 花岗岩魔像·入壳反震 ====
        /// <summary>触发阈值：入壳前记忆窗内的近战命中数</summary>
        private const int CounterHitThreshold = 3;
        /// <summary>近战命中记忆窗（帧）：窗内无新命中则计数清零</summary>
        private const int CounterHitMemoryFrames = 90;
        /// <summary>反震独立冷却（与震地互不占用）</summary>
        private const int CounterCooldownFrames = 480;
        /// <summary>反震脉冲半径（档位只加强度）</summary>
        private static readonly int[] CounterRadiusByTier = [150, 165, 180];
        /// <summary>反震伤害 = 已缩放 npc.damage × 此值（伤害低，击退为主）</summary>
        private const float CounterDamageFrac = 0.3f;
        /// <summary>联机近战猜测半径：服务端收不到命中钩子，以掉血+近身玩家近似近战</summary>
        private const float MeleeGuessRange = 150f;
        private const int RingCap = 4;

        //==== 花岗岩精·弧光俯冲 ====
        private static readonly int[] DiveCooldownByTier = [430, 380, 330];
        /// <summary>俯冲名义峰速（未含提速补偿，注入时除回 MoveGain）</summary>
        private static readonly float[] DivePeakByTier = [10f, 11f, 12f];
        /// <summary>俯冲包络三段（合计 = StonebornDiveLane.StrikeFrames，衰减段即惯性滑出）</summary>
        private const int DiveRiseFrames = 6;
        private const int DiveHoldFrames = 9;
        private const int DiveDecayFrames = 13;
        /// <summary>俯冲后收势帧（清残速，把控制权还给原版悬浮 AI）</summary>
        private const int DiveRecoverFrames = 8;
        private const float DiveMinRange = 120f;
        private const float DiveMaxRange = 620f;
        /// <summary>残电投放间隔（帧）与微伤系数（视觉为主）</summary>
        private const int ResidueIntervalFrames = 5;
        private const float ResidueDamageFrac = 0.12f;
        private const int LaneCap = 6;
        private const int ResidueCap = 30;

        //==== 美杜莎·凝视可读性层 ====
        /// <summary>凝视层重扫间隔（一体一实体，丢失兜底重建）</summary>
        private const int MedusaRescanFrames = 300;
        private const int GazeOmenCap = 8;

        //==== 豪杰骷髅·标枪三连 ====
        private static readonly int[] VolleyCooldownByTier = [400, 350, 300];
        private const float VolleyMinRangeX = 160f;
        private const float VolleyMaxRangeX = 520f;
        private const float VolleyMaxRangeY = 260f;
        /// <summary>后撤步冲量（名义值，注入时除回 MoveGain）</summary>
        private const float BackstepSpeed = 3.2f;
        /// <summary>齐射后收势帧</summary>
        private const int VolleyRecoverFrames = 12;
        /// <summary>标枪伤害 = 已缩放 npc.damage × 此值</summary>
        private const float JavelinDamageFrac = 0.55f;
        private const int JavelinOmenCap = 6;

        private const byte PhaseIdle = 0;
        private const byte PhaseTelegraph = 1;
        private const byte PhaseStrike = 2;
        private const byte PhaseRecover = 3;

        private enum StoneFamily : byte
        {
            None,
            /// <summary>花岗岩魔像：共振震地+入壳反震</summary>
            Golem,
            /// <summary>花岗岩精：弧光俯冲</summary>
            Flyer,
            /// <summary>美杜莎：凝视可读性层</summary>
            Medusa,
            /// <summary>豪杰骷髅：标枪三连</summary>
            Hoplite,
        }

        private static StoneFamily ResolveFamily(int type) => type switch {
            NPCID.GraniteGolem => StoneFamily.Golem,
            NPCID.GraniteFlyer => StoneFamily.Flyer,
            NPCID.Medusa => StoneFamily.Medusa,
            NPCID.GreekSkeleton => StoneFamily.Hoplite,
            _ => StoneFamily.None,
        };

        public override bool InstancePerEntity => true;

        /// <summary>本个体出生时绑定的档位，0=未绑定（中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private StoneFamily family;
        private byte phase;
        private int timer;
        /// <summary>决策冷却；权威端私产，客户端不得用它驱动画面</summary>
        private int cooldown;
        /// <summary>锁定俯冲方向（锁定帧后不再改写，预告即承诺）</summary>
        private float lockDir;
        /// <summary>本次攻击的预告体槽位（权威端私产）</summary>
        private int boundProjIndex = -1;

        //——魔像反震计数（权威端私产）——
        /// <summary>上一帧是否处于原版蹲缩（ai[2]&lt;0，1.4.0.5 反编译核实）</summary>
        private bool prevCrouched;
        private int recentMeleeHits;
        private int meleeMemoryLeft;
        private int counterCooldown;
        /// <summary>掉血追踪（联机侧近战判断的伤害证据）</summary>
        private int lifeTracker;
        /// <summary>单人侧精确近战受击戳记（联机服务端命中钩子不到场，以掉血+近身玩家为准）</summary>
        private uint hookMeleeTick;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && ResolveFamily(entity.type) != StoneFamily.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            family = StoneFamily.None;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            StoneFamily resolved = ResolveFamily(npc.type);
            if (resolved == StoneFamily.None) {
                return;
            }
            family = resolved;
            boundTier = tier;
            //首攻错拍：此刻 npc.whoAmI 恒为 0（NewNPC 之后才赋值），不可用作错拍源；
            //冷却是权威端决策私产，Main.rand 无同步语义（M7/M8）
            cooldown = FirstCooldownMin + Main.rand.Next(FirstCooldownMax - FirstCooldownMin + 1);
        }

        /// <summary>机制入口资格：友方/无敌/Boss/雕像怪/共享血池体节逐项排除（每个入口都要过）</summary>
        private static bool MechEligible(NPC npc) {
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
        /// 提速位移补偿：<see cref="GameModeNPC.PostAI"/> 对非 Boss 怪按 velocity×SpeedBonus 追加位置推进，
        /// 本层注入的承诺性速度一律除回该系数（位移项除回、重力项不除）。运行时读旗标：体节与 boss 旗标个体系数为 1
        /// </summary>
        private float MoveGain(NPC npc) => !npc.boss && npc.realLife < 0 ? 1f + GameModeTuning.SpeedBonus(boundTier) : 1f;

        /// <summary>统计某类弹幕的活动实例数（到 stopAt 提前退出；只在触发时调用，非每帧）</summary>
        private static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>回读 ai[0] 锚定型预告体（索引+类型+归属三重校验；缺位=进攻作废）</summary>
        private static bool ValidAnchoredOmen(int index, int projType, NPC npc) {
            if (index < 0 || index >= Main.maxProjectiles) {
                return false;
            }
            Projectile proj = Main.projectile[index];
            return proj.active && proj.type == projType && (int)proj.ai[0] == npc.whoAmI;
        }

        /// <summary>回读标枪预告体（来源打包在 ai[1] 低八位）</summary>
        private static bool ValidJavelinOmen(int index, NPC npc) {
            if (index < 0 || index >= Main.maxProjectiles) {
                return false;
            }
            Projectile proj = Main.projectile[index];
            return proj.active && proj.type == ModContent.ProjectileType<StonebornJavelinOmen>()
                && ((int)proj.ai[1] & 255) - 1 == npc.whoAmI;
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (VaultUtils.isClient) {
                return;//决策只在权威端；客户端画面全部来自同步弹幕实体
            }

            if (family == StoneFamily.Golem) {
                TickGolemCounterWatch(npc);
            }
            if (family == StoneFamily.Medusa) {
                TickMedusaUpkeep(npc);
                return;
            }

            switch (phase) {
                case PhaseIdle:
                    if (--cooldown > 0) {
                        return;
                    }
                    TryStart(npc);
                    return;
                case PhaseTelegraph:
                    TickTelegraph(npc);
                    return;
                case PhaseStrike:
                    TickStrike(npc);
                    return;
                default:
                    TickRecover(npc);
                    return;
            }
        }

        #region 触发
        private static bool GroundReady(NPC npc, Player player, float minX, float maxX, float maxY) {
            if (npc.velocity.Y != 0f) {
                return false;
            }
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Center.Y - npc.Center.Y);
            return dx >= minX && dx <= maxX && dy <= maxY;
        }

        private void TryStart(NPC npc) {
            if (!MechEligible(npc)) {
                cooldown = IneligibleDelay;
                return;
            }
            if (npc.target < 0 || npc.target >= Main.maxPlayers) {
                cooldown = TriggerRetryFrames;
                return;
            }
            Player player = Main.player[npc.target];
            if (!player.Alives()) {
                cooldown = TriggerRetryFrames;
                return;
            }
            cooldown = TriggerRetryFrames;

            switch (family) {
                case StoneFamily.Golem:
                    TryStartStomp(npc, player);
                    return;
                case StoneFamily.Flyer:
                    TryStartDive(npc, player);
                    return;
                case StoneFamily.Hoplite:
                    TryStartVolley(npc, player);
                    return;
            }
        }

        /// <summary>魔像·共振震地：落地立定蓄能，蓄能可见性由锚定载体承担</summary>
        private void TryStartStomp(NPC npc, Player player) {
            if (npc.ai[2] < 0f) {
                return;//原版蹲缩中不起震地（壳里不跺脚）
            }
            if (!GroundReady(npc, player, StompMinRangeX, StompMaxRangeX, StompMaxRangeY)) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<StonebornQuakeCharge>()) >= ChargeCap
                || CountActive(ModContent.ProjectileType<StonebornQuakeWave>()) >= WaveCap) {
                return;
            }
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<StonebornQuakeCharge>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, npc.type);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                cooldown = AbortCooldown;
                return;
            }
            boundProjIndex = omen;
            //刹车脉冲：立定蓄势
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = StonebornQuakeCharge.WindupFrames;
        }

        /// <summary>花岗岩精·弧光俯冲：悬停划线，标线实体独立承载锁定语义</summary>
        private void TryStartDive(NPC npc, Player player) {
            float dist = npc.Distance(player.Center);
            if (dist < DiveMinRange || dist > DiveMaxRange) {
                return;
            }
            if (!Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height)) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<StonebornDiveLane>()) >= LaneCap) {
                return;
            }
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<StonebornDiveLane>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, npc.type);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                cooldown = AbortCooldown;
                return;
            }
            boundProjIndex = omen;
            lockDir = (player.Center - npc.Center).ToRotation();
            //刹车脉冲：悬停蓄势
            npc.velocity *= 0.35f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = StonebornDiveLane.TelegraphFrames;
        }

        /// <summary>豪杰骷髅·标枪三连：后撤步瞄准，锁向即承诺（瞄角在预告生成帧锁死）</summary>
        private void TryStartVolley(NPC npc, Player player) {
            if (!GroundReady(npc, player, VolleyMinRangeX, VolleyMaxRangeX, VolleyMaxRangeY)) {
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<StonebornJavelinOmen>()) >= JavelinOmenCap) {
                return;
            }
            Vector2 muzzle = npc.Center + new Vector2(0f, -14f);
            float aim = (player.Center - muzzle).ToRotation();
            int damage = Math.Max(1, (int)(npc.damage * JavelinDamageFrac));
            //ai[1] 低位=来源槽+1、高位=来源类型：槽位被新怪复用时取消检查不被骗过
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, Vector2.Zero,
                ModContent.ProjectileType<StonebornJavelinOmen>(), damage, 1f, Main.myPlayer,
                aim, (npc.whoAmI + 1) | (npc.type << 8));
            if (omen < 0 || omen >= Main.maxProjectiles) {
                cooldown = AbortCooldown;
                return;
            }
            boundProjIndex = omen;
            //后撤步：小幅后滑冲量（位移项除回提速补偿）
            float away = player.Center.X > npc.Center.X ? -1f : 1f;
            npc.velocity.X = away * (BackstepSpeed / MoveGain(npc));
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = StonebornJavelinOmen.AimFrames;
        }
        #endregion

        #region 相位推进
        /// <summary>预告体缺位/被打断的统一回退：不出招，回冷却（失败方向=安全方向）</summary>
        private void Abort() {
            boundProjIndex = -1;
            phase = PhaseIdle;
            cooldown = AbortCooldown;
        }

        private void TickTelegraph(NPC npc) {
            timer--;
            switch (family) {
                case StoneFamily.Golem: {
                    //蓄能期回读载体；载体消散（锚死/提前入壳）→ 整次进攻作废。
                    //入壳判据与载体同读 npc.ai[2]，两端结论一致
                    if (!ValidAnchoredOmen(boundProjIndex, ModContent.ProjectileType<StonebornQuakeCharge>(), npc)
                        || npc.ai[2] < 0f) {
                        Abort();
                        return;
                    }
                    //离散刹车脉冲压住走位漂移，让波从立定点发出（非每帧，脉冲帧才跟同步）
                    if (timer == 24 || timer == 12) {
                        npc.velocity.X *= 0.25f;
                        npc.netUpdate = true;
                    }
                    if (timer <= 0) {
                        CommitStomp(npc);
                    }
                    return;
                }
                case StoneFamily.Flyer: {
                    if (!ValidAnchoredOmen(boundProjIndex, ModContent.ProjectileType<StonebornDiveLane>(), npc)) {
                        Abort();
                        return;
                    }
                    //离散刹车脉冲：悬停压漂移
                    if (timer == 24 || timer == 16 || timer == 8) {
                        npc.velocity *= 0.5f;
                        npc.netUpdate = true;
                    }
                    if (timer == StonebornDiveLane.LockFrames) {
                        //锁定帧：方向自此为承诺，写回标线实体做各端权威纠偏
                        if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                            lockDir = (Main.player[npc.target].Center - npc.Center).ToRotation();
                        }
                        Projectile omen = Main.projectile[boundProjIndex];
                        omen.ai[2] = lockDir + 10f;
                        omen.netUpdate = true;
                    }
                    if (timer <= 0) {
                        phase = PhaseStrike;
                        timer = StonebornDiveLane.StrikeFrames;
                    }
                    return;
                }
                case StoneFamily.Hoplite: {
                    if (!ValidJavelinOmen(boundProjIndex, npc)) {
                        Abort();
                        return;
                    }
                    //后撤步余速衰减（脉冲帧跟同步）
                    if (timer == 20 || timer == 10) {
                        npc.velocity.X *= 0.5f;
                        npc.netUpdate = true;
                    }
                    if (timer <= 0) {
                        //齐射窗：投掷由预告体按拍执行（来源死亡即中止），骷髅本体只持姿态
                        phase = PhaseStrike;
                        timer = StonebornJavelinOmen.ShotIntervalFrames * (StonebornJavelinOmen.ShotCount - 1)
                            + VolleyRecoverFrames;
                    }
                    return;
                }
            }
        }

        /// <summary>震地提交：立定点向两侧各放一道共振波，随后进入收势</summary>
        private void CommitStomp(NPC npc) {
            //提交帧再验一次载体（M3：实体缺位→回冷却，绝不无预告出招）
            if (!ValidAnchoredOmen(boundProjIndex, ModContent.ProjectileType<StonebornQuakeCharge>(), npc)) {
                Abort();
                return;
            }
            int damage = Math.Max(1, (int)(npc.damage * WaveDamageFrac));
            int waveType = ModContent.ProjectileType<StonebornQuakeWave>();
            int life = WaveLifeByTier[boundTier - 1];
            for (int dir = -1; dir <= 1; dir += 2) {
                //波从脚边起步，贴地实体自行吸附地形
                Vector2 origin = npc.Bottom + new Vector2(dir * 22f, -StonebornQuakeWave.WaveCrestHeightPx * 0.5f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), origin, Vector2.Zero,
                    waveType, damage, 3f, Main.myPlayer, dir, life);
            }
            boundProjIndex = -1;
            phase = PhaseRecover;
            timer = StompRecoverFrames;
        }

        private void TickStrike(NPC npc) {
            timer--;
            if (family == StoneFamily.Flyer) {
                //被打进宝石防御态（dontTakeDamage）→ 俯冲中断，读作原版反制生效
                if (npc.dontTakeDamage) {
                    npc.velocity *= 0.3f;
                    npc.netUpdate = true;
                    phase = PhaseRecover;
                    timer = DiveRecoverFrames;
                    boundProjIndex = -1;
                    return;
                }
                int t = StonebornDiveLane.StrikeFrames - timer;
                float envelope = MobDash.Envelope(t, DiveRiseFrames, DiveHoldFrames, DiveDecayFrames);
                //包络塑形的承诺性速度：位移项除回提速补偿（M2）
                npc.velocity = lockDir.ToRotationVector2() * (DivePeakByTier[boundTier - 1] / MoveGain(npc)) * envelope;
                if (t == 1 || t % 6 == 0) {
                    npc.netUpdate = true;
                }
                //残电投放：只铺在包络的高速段（视觉为主，接触微伤）
                if (t % ResidueIntervalFrames == 0 && envelope > 0.5f
                    && CountActive(ModContent.ProjectileType<StonebornArcResidue>()) < ResidueCap) {
                    int residueDamage = Math.Max(1, (int)(npc.damage * ResidueDamageFrac));
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<StonebornArcResidue>(), residueDamage, 0f, Main.myPlayer);
                }
            }
            //豪杰骷髅齐射窗：预告体自主按拍投掷，本体只计时收势

            if (timer <= 0) {
                if (family == StoneFamily.Flyer) {
                    phase = PhaseRecover;
                    timer = DiveRecoverFrames;
                    boundProjIndex = -1;
                    return;
                }
                FinishMove(npc);
            }
        }

        /// <summary>收势：衰减清残速，把控制权干净还给原版 AI（M2）</summary>
        private void TickRecover(NPC npc) {
            timer--;
            if (family == StoneFamily.Flyer) {
                npc.velocity *= 0.72f;
                if (timer == DiveRecoverFrames - 1) {
                    npc.netUpdate = true;
                }
            }
            if (timer <= 0) {
                FinishMove(npc);
            }
        }

        private void FinishMove(NPC npc) {
            phase = PhaseIdle;
            boundProjIndex = -1;
            int[] table = family switch {
                StoneFamily.Golem => StompCooldownByTier,
                StoneFamily.Flyer => DiveCooldownByTier,
                _ => VolleyCooldownByTier,
            };
            cooldown = table[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
        }
        #endregion

        #region 魔像反震（入壳沿触发）
        /// <summary>
        /// 反震监视（每权威帧，独立于相位机）：
        /// 记录记忆窗内的近战命中，在原版蹲缩的入壳沿检查阈值——
        /// 蹲缩期 dontTakeDamage=true 使命中钩子物理上不触发，故反震语义定为「被打出来的壳会放电」。
        /// 近战判断双源：单人=命中钩子精确戳记；联机服务端钩子不到场，以掉血+近身玩家近似
        /// </summary>
        private void TickGolemCounterWatch(NPC npc) {
            if (lifeTracker == 0) {
                lifeTracker = npc.life;
            }
            bool hurtThisFrame = npc.life < lifeTracker;
            lifeTracker = npc.life;
            if (hurtThisFrame) {
                bool hookFresh = Main.GameUpdateCount - hookMeleeTick <= 2;
                bool meleeGuess = hookFresh || (!VaultUtils.isSinglePlayer && AnyPlayerInMeleeRange(npc));
                if (meleeGuess) {
                    recentMeleeHits++;
                    meleeMemoryLeft = CounterHitMemoryFrames;
                }
            }
            if (meleeMemoryLeft > 0 && --meleeMemoryLeft == 0) {
                recentMeleeHits = 0;
            }
            if (counterCooldown > 0) {
                counterCooldown--;
            }

            bool crouched = npc.ai[2] < 0f;
            //入壳沿：该帧原版尚未置 dontTakeDamage（蹲缩分支下一帧才跑），资格照常复查
            if (crouched && !prevCrouched && counterCooldown <= 0
                && recentMeleeHits >= CounterHitThreshold && MechEligible(npc)
                && CountActive(ModContent.ProjectileType<StonebornCounterRing>()) < RingCap) {
                int damage = Math.Max(1, (int)(npc.damage * CounterDamageFrac));
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<StonebornCounterRing>(), damage, 8f, Main.myPlayer,
                    npc.whoAmI, npc.type, CounterRadiusByTier[boundTier - 1]);
                counterCooldown = CounterCooldownFrames;
                recentMeleeHits = 0;
                meleeMemoryLeft = 0;
            }
            prevCrouched = crouched;
        }

        /// <summary>联机近战近似：有存活玩家贴身即视为近战压制（服务端收不到命中钩子的兜底口径）</summary>
        private static bool AnyPlayerInMeleeRange(NPC npc) {
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && player.Distance(npc.Center) < MeleeGuessRange) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) {
            if (boundTier > 0 && family == StoneFamily.Golem) {
                hookMeleeTick = Main.GameUpdateCount;//单人侧精确近战戳记
            }
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) {
            if (boundTier > 0 && family == StoneFamily.Golem
                && projectile.CountsAsClass(DamageClass.Melee)) {
                hookMeleeTick = Main.GameUpdateCount;
            }
        }
        #endregion

        #region 美杜莎凝视层
        /// <summary>
        /// 一体一凝视层实体，低频兜底重建。石化判定不动：实体只做预告与惩罚窗的可读性
        /// </summary>
        private void TickMedusaUpkeep(NPC npc) {
            if (--cooldown > 0) {
                return;
            }
            cooldown = MedusaRescanFrames;
            if (!MechEligible(npc)) {
                return;
            }
            int omenType = ModContent.ProjectileType<StonebornGazeOmen>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == omenType && (int)proj.ai[0] == npc.whoAmI) {
                    return;
                }
            }
            if (CountActive(omenType) >= GazeOmenCap) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Top, Vector2.Zero,
                omenType, 0, 0f, Main.myPlayer, npc.whoAmI, npc.type);
        }
        #endregion
    }
}
