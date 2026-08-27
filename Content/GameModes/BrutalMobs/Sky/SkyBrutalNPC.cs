using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.Sky.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Sky
{
    /// <summary>
    /// 残酷模式天空组行为层，主题：风的掠夺者。
    /// 覆盖名单：鸟妖（羽刃扇面齐射 / 风压俯冲，双技轮换 + 群体错拍）。
    /// 豁免名单：飞龙 WyvernHead——原版飞龙蠕虫的追压强度已足，本批只吃 GameModeNPC 数值层，
    /// 不入类型表；WyvernBody/Legs/Tail 体节同样不入表（realLife 双保险排除）。
    /// 叠加在原版 AI 之上不接管、不动数值（数值层归 GameModeNPC）；
    /// 决策只在权威端（客户端 PostAI 早退），客户端可见状态一律来自同步的预兆弹幕实体
    /// </summary>
    internal class SkyBrutalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==== 通用节奏 ====
        /// <summary>触发条件未满足的复查间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>出生首攻错拍窗（M7 密度预算：遭遇 3 秒内可见首个机制）</summary>
        private const int FirstCooldownMin = 60;
        private const int FirstCooldownMax = 180;
        /// <summary>攻击冷却（档位 1/2/3），另加随机抖动</summary>
        private static readonly int[] AttackCooldownByTier = [320, 265, 210];
        private const int CooldownJitter = 50;

        //==== 群体错拍 ====
        /// <summary>触发错拍规则的同屏鸟妖数下限</summary>
        private const int FlockStaggerCount = 3;
        /// <summary>成群时同族并发突进上限（静态计数现存预兆实体）</summary>
        private const int FlockConcurrentCap = 2;
        /// <summary>天空组预兆全局并发上限（M7 并发闸）</summary>
        private const int SkyOmenCap = 6;

        //==== 羽刃扇面 ====
        private const float FanMinRange = 160f;
        private const float FanMaxRange = 560f;
        /// <summary>羽刃伤害 = 已缩放 npc.damage × 此值</summary>
        private const float FeatherDamageFrac = 0.5f;
        /// <summary>齐射后的收势帧</summary>
        private const int FanRecoverFrames = 14;

        //==== 风压俯冲 ====
        private const float DiveMinRange = 160f;
        private const float DiveMaxRange = 640f;
        /// <summary>俯冲要求的最小高度优势（高位斜俯冲）</summary>
        private const float DiveHeightAdvantage = 100f;
        /// <summary>俯冲名义峰速（档位 1/2/3；未含提速补偿，注入时除回 MoveGain）</summary>
        private static readonly float[] DivePeakByTier = [12.5f, 13.5f, 14.5f];
        /// <summary>俯冲包络三段：起势/保持/力竭（总和 = SkyGaleDiveOmen.StrikeFrames，余痕窗=突进窗）</summary>
        private const int DiveRise = 8;
        private const int DiveHold = 14;
        private const int DiveDecay = 18;
        /// <summary>力竭后的收势帧（清残速，把控制权干净还给原版 AI）</summary>
        private const int DiveSettleFrames = 10;
        /// <summary>俯冲擦过命中的推挤速度（受害端本机结算）</summary>
        private const float DivePushSpeed = 5f;

        private const byte PhaseIdle = 0;
        private const byte PhaseFan = 1;
        private const byte PhaseDiveAim = 2;
        private const byte PhaseDiveStrike = 3;

        /// <summary>本个体出生时绑定的档位，0=未绑定（镜像 GameModeNPC；中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>双技轮换开关：false=下次优先羽刃，true=下次优先俯冲（权威端私产）</summary>
        private bool preferDive;
        /// <summary>锁定俯冲方向（锁定帧后不再改写，预告即承诺）</summary>
        private float lockDir;
        /// <summary>本次攻击的预兆槽位（权威端私产）</summary>
        private int omenIndex = -1;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && entity.type == NPCID.Harpy;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            boundTier = tier;
            //首攻错拍：冷却是权威端决策私产，Main.rand 无同步语义；
            //此刻 npc.whoAmI 恒为 0（NewNPC 之后才赋值），不可用作错拍源
            cooldown = FirstCooldownMin + Main.rand.Next(FirstCooldownMax - FirstCooldownMin + 1);
        }

        /// <summary>机制入口资格：友方/无敌/Boss/小动物载体/雕像怪/共享血池体节逐项排除</summary>
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

        /// <summary>同型弹幕并发计数（到 stopAt 提前退出；只在触发时调用，非每帧）</summary>
        private static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>同屏鸟妖计数（到 stopAt 提前退出）</summary>
        private static int CountHarpies(int stopAt) {
            int count = 0;
            foreach (NPC other in Main.ActiveNPCs) {
                if (other.type == NPCID.Harpy && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>来源校验包：低位=槽+1、高位=类型（槽位被新怪复用时校验不被骗过）</summary>
        private static int PackSource(NPC npc) => (npc.whoAmI + 1) | (npc.type << 8);

        /// <summary>校验名下预兆仍有效（索引+类型+来源包比对）；缺位=攻击作废（失败方向=安全方向）</summary>
        private bool TryGetBoundOmen(int projType, int sourceSlot, int packedSource, out Projectile proj) {
            proj = null;
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile p = Main.projectile[omenIndex];
            if (!p.active || p.type != projType || (int)p.ai[sourceSlot] != packedSource) {
                return false;
            }
            proj = p;
            return true;
        }

        private static bool CanSee(NPC npc, Player player)
            => Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height);

        private static bool FanReady(NPC npc, Player player) {
            float dist = Vector2.Distance(npc.Center, player.Center);
            return dist >= FanMinRange && dist <= FanMaxRange && CanSee(npc, player);
        }

        private static bool DiveReady(NPC npc, Player player) {
            if (npc.Center.Y > player.Center.Y - DiveHeightAdvantage) {
                return false;
            }
            float dist = Vector2.Distance(npc.Center, player.Center);
            return dist >= DiveMinRange && dist <= DiveMaxRange && CanSee(npc, player);
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (VaultUtils.isClient) {
                //决策只在权威端；客户端画面全部来自同步的预兆实体与 NPC 速度原生同步
                return;
            }
            switch (phase) {
                case PhaseIdle:
                    if (--cooldown <= 0) {
                        TryStart(npc);
                    }
                    break;
                case PhaseFan:
                    TickFan(npc);
                    break;
                case PhaseDiveAim:
                    TickDiveAim(npc);
                    break;
                default:
                    TickDiveStrike(npc);
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

            //并发闸 + 群体错拍：成群（≥3 只）时同族并发突进 ≤2，静态计数现存预兆
            int busy = CountActive(ModContent.ProjectileType<SkyFeatherFanOmen>())
                + CountActive(ModContent.ProjectileType<SkyGaleDiveOmen>());
            if (busy >= SkyOmenCap) {
                cooldown = RetryDelay;
                return;
            }
            if (busy >= FlockConcurrentCap && CountHarpies(FlockStaggerCount) >= FlockStaggerCount) {
                cooldown = RetryDelay;
                return;
            }

            //双技轮换：优先轮到的一技，条件不满足则换用另一技
            bool diveOk = DiveReady(npc, player);
            bool fanOk = FanReady(npc, player);
            bool useDive = preferDive ? diveOk : (!fanOk && diveOk);
            if (useDive) {
                StartDive(npc, player);
            }
            else if (fanOk) {
                StartFan(npc);
            }
            else {
                cooldown = RetryDelay;
            }
        }

        /// <summary>羽刃扇面：预兆实体全程负责悬停凝羽信号、锁向承诺与齐射，鸟妖只压速与收势</summary>
        private void StartFan(NPC npc) {
            int offsetQuant = Main.rand.Next(64);
            int damage = Math.Max(1, (int)(npc.damage * FeatherDamageFrac));
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<SkyFeatherFanOmen>(), damage, 0f, Main.myPlayer,
                0f, SkyFeatherFanOmen.Pack(boundTier, offsetQuant), PackSource(npc));
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                Abort();
                return;
            }
            //刹车脉冲：悬停定位蓄势
            npc.velocity *= 0.3f;
            npc.netUpdate = true;
            timer = SkyFeatherFanOmen.TotalFrames + FanRecoverFrames;
            phase = PhaseFan;
            preferDive = true;
        }

        /// <summary>风压俯冲：高位标线预兆追踪→锁向→俯冲，余痕窗即推挤判定窗</summary>
        private void StartDive(NPC npc, Player player) {
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<SkyGaleDiveOmen>(), 0, 0f, Main.myPlayer,
                PackSource(npc), boundTier, 0f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                Abort();
                return;
            }
            lockDir = (player.Center - npc.Center).ToRotation();
            npc.velocity *= 0.3f;
            npc.netUpdate = true;
            timer = SkyGaleDiveOmen.TelegraphFrames;
            phase = PhaseDiveAim;
            preferDive = false;
        }

        /// <summary>预兆生成失败/中途缺位的回退：退回待机（无预告不许出手）</summary>
        private void Abort() {
            omenIndex = -1;
            phase = PhaseIdle;
            cooldown = RetryDelay;
        }

        private void TickFan(NPC npc) {
            timer--;
            if (timer > FanRecoverFrames + 1) {
                //预告期逐帧回读校验：预兆缺位则整次进攻作废（失败方向=安全方向）
                if (!TryGetBoundOmen(ModContent.ProjectileType<SkyFeatherFanOmen>(), 2, PackSource(npc), out Projectile omen)) {
                    Abort();
                    return;
                }
                //悬停期与锁向期离散刹车脉冲，压住游荡漂移让扇面贴住冻结的出手点（脉冲帧才跟同步）
                int hoverMark = timer - FanRecoverFrames - SkyFeatherFanOmen.AimFrames;
                if (hoverMark == 18 || hoverMark == 8 || hoverMark == -12 || hoverMark == -24) {
                    npc.velocity *= 0.4f;
                    npc.netUpdate = true;
                }
                if (hoverMark == 0) {
                    //锁向帧：方向自此为承诺，写回预兆实体做各端权威纠偏
                    if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                        float aim = (Main.player[npc.target].Center - npc.Center).ToRotation();
                        omen.ai[0] = aim + 10f;
                        omen.netUpdate = true;
                    }
                    npc.velocity *= 0.2f;
                    npc.netUpdate = true;
                }
            }
            if (timer <= 0) {
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = AttackCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            }
        }

        private void TickDiveAim(NPC npc) {
            timer--;
            if (!TryGetBoundOmen(ModContent.ProjectileType<SkyGaleDiveOmen>(), 0, PackSource(npc), out Projectile omen)) {
                Abort();
                return;
            }
            //离散刹车脉冲压住漂移，让标线贴住实际出发点
            if (timer == 24 || timer == 12) {
                npc.velocity *= 0.45f;
                npc.netUpdate = true;
            }
            if (timer == SkyGaleDiveOmen.LockFrames) {
                //锁定帧：方向自此为承诺，写回预兆做各端权威纠偏
                if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                    lockDir = (Main.player[npc.target].Center - npc.Center).ToRotation();
                }
                omen.ai[2] = lockDir + 10f;
                omen.netUpdate = true;
            }
            if (timer <= 0) {
                phase = PhaseDiveStrike;
                timer = DiveRise + DiveHold + DiveDecay + DiveSettleFrames;
                npc.netUpdate = true;
            }
        }

        private void TickDiveStrike(NPC npc) {
            int total = DiveRise + DiveHold + DiveDecay + DiveSettleFrames;
            int elapsed = total - timer + 1;
            timer--;
            if (elapsed <= DiveRise + DiveHold + DiveDecay) {
                //包络塑形持有：抵住原版空中转向；承诺性速度除回提速补偿
                npc.velocity = MobDash.Velocity(lockDir.ToRotationVector2(),
                    DivePeakByTier[boundTier - 1] / MoveGain(npc), elapsed, DiveRise, DiveHold, DiveDecay);
                if (elapsed == 1 || timer % 6 == 0) {
                    npc.netUpdate = true;
                }
            }
            else {
                //力竭收势：衰减清残速，把控制权干净还给原版 AI
                npc.velocity *= 0.82f;
                if (timer % 6 == 0) {
                    npc.netUpdate = true;
                }
            }
            if (timer <= 0) {
                npc.velocity *= 0.5f;
                npc.netUpdate = true;
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = AttackCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            }
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) {
            if (boundTier <= 0) {
                return;
            }
            //命中方本机结算；突进窗由已同步的预兆实体判定，不读权威端私产计时器
            if (SkyGaleDiveOmen.TryGetStrikeDir(npc.whoAmI, npc.type, out float dir)) {
                Vector2 push = dir.ToRotationVector2() * DivePushSpeed;
                //垂直分量收窄：擦过是横向推挤，不把人往地里怼
                push.Y = MathHelper.Clamp(push.Y - 1.2f, -3.5f, 2f);
                target.velocity += push;
            }
        }
    }
}
