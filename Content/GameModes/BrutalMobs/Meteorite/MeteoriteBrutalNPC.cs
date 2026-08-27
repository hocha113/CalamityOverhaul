using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.Meteorite.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Meteorite
{
    /// <summary>
    /// 残酷模式陨石组行为层，主题：余烬俯冲。
    /// 覆盖名单：陨石头（灼热俯冲——环绕蓄热预告→锁向俯冲→沿途滴落熔滴留火斑→力竭漂移）。
    /// 陨石地物常驻敌怪即陨石头一种，无其他豁免项。
    /// 叠加在原版 AI 之上不接管、不动数值（数值层归 GameModeNPC）；
    /// 决策只在权威端（客户端 PostAI 早退），客户端可见状态一律来自同步的预兆/弹幕实体
    /// </summary>
    internal class MeteoriteBrutalNPC : GlobalNPC
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
        /// <summary>俯冲冷却（档位 1/2/3，一律 ≥300 帧），另加随机抖动</summary>
        private static readonly int[] DiveCooldownByTier = [440, 380, 320];
        private const int CooldownJitter = 40;

        //==== 灼热俯冲 ====
        private const float DiveMinRange = 180f;
        private const float DiveMaxRange = 620f;
        /// <summary>俯冲名义峰速（档位 1/2/3；未含提速补偿，注入时除回 MoveGain）</summary>
        private static readonly float[] DivePeakByTier = [10.5f, 11.5f, 12.5f];
        /// <summary>俯冲包络三段：蓄势/保持/力竭（总和 = MeteoriteDiveOmen.StrikeFrames，余痕窗=俯冲窗）</summary>
        private const int DiveRise = 10;
        private const int DiveHold = 16;
        private const int DiveDecay = 22;
        /// <summary>力竭后的漂移后摇帧（缓速下沉，清残速）</summary>
        private const int DiveSettleFrames = 16;
        /// <summary>后摇期每帧下沉量（力竭失浮感，重力项不除提速补偿）</summary>
        private const float SettleSagPerFrame = 0.05f;
        /// <summary>俯冲预兆全局并发上限（M7 并发闸）</summary>
        private const int DiveOmenCap = 4;

        //==== 熔滴与火斑 ====
        /// <summary>沿途熔滴数（档位 1/2/3）：档位只加密度，火斑间距测试不变</summary>
        private static readonly int[] DripCountByTier = [2, 3, 3];
        /// <summary>熔滴伤害 = 已缩放 npc.damage × 此值</summary>
        private const float GlobDamageFrac = 0.55f;
        /// <summary>熔滴全局并发上限</summary>
        private const int GlobCap = 6;

        private const byte PhaseIdle = 0;
        private const byte PhaseAim = 1;
        private const byte PhaseStrike = 2;

        /// <summary>本个体出生时绑定的档位，0=未绑定（镜像 GameModeNPC；中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>锁定俯冲方向（锁定帧后不再改写，预告即承诺）</summary>
        private float lockDir;
        /// <summary>本次俯冲的预兆槽位（权威端私产）</summary>
        private int omenIndex = -1;
        /// <summary>本轮俯冲剩余熔滴数</summary>
        private int dripsLeft;
        /// <summary>上一滴熔滴的布点位置：布点循环以 EmberGapPx 为最小间距真正读取</summary>
        private Vector2 lastDripPos;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && entity.type == NPCID.MeteorHead;

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

        /// <summary>同型弹幕并发计数（到 stopAt 提前退出；只在触发/布点时调用，非每帧）</summary>
        internal static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>来源校验包：低位=槽+1、高位=类型（槽位被新怪复用时校验不被骗过）</summary>
        private static int PackSource(NPC npc) => (npc.whoAmI + 1) | (npc.type << 8);

        /// <summary>校验名下预兆仍有效（索引+类型+来源包比对）；缺位=俯冲作废（失败方向=安全方向）</summary>
        private bool TryGetBoundOmen(int packedSource, out Projectile proj) {
            proj = null;
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile p = Main.projectile[omenIndex];
            if (!p.active || p.type != ModContent.ProjectileType<MeteoriteDiveOmen>()
                || (int)p.ai[0] != packedSource) {
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
                //决策只在权威端；客户端画面全部来自同步的预兆实体与 NPC 速度原生同步
                return;
            }
            switch (phase) {
                case PhaseIdle:
                    if (--cooldown <= 0) {
                        TryStart(npc);
                    }
                    break;
                case PhaseAim:
                    TickAim(npc);
                    break;
                default:
                    TickStrike(npc);
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
            float dist = Vector2.Distance(npc.Center, player.Center);
            if (dist < DiveMinRange || dist > DiveMaxRange) {
                cooldown = RetryDelay;
                return;
            }
            //穿墙怪也只在有视线时俯冲：从墙后无预兆窜出不公平
            if (!Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height)) {
                cooldown = RetryDelay;
                return;
            }
            if (CountActive(ModContent.ProjectileType<MeteoriteDiveOmen>()) >= DiveOmenCap) {
                cooldown = RetryDelay;
                return;
            }

            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<MeteoriteDiveOmen>(), 0, 0f, Main.myPlayer,
                PackSource(npc), boundTier, 0f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                Abort();
                return;
            }
            lockDir = (player.Center - npc.Center).ToRotation();
            //刹车脉冲：环绕蓄热压速
            npc.velocity *= 0.35f;
            npc.netUpdate = true;
            timer = MeteoriteDiveOmen.TelegraphFrames;
            phase = PhaseAim;
        }

        /// <summary>预兆生成失败/中途缺位的回退：退回待机（无预告不许出手）</summary>
        private void Abort() {
            omenIndex = -1;
            phase = PhaseIdle;
            cooldown = RetryDelay;
        }

        private void TickAim(NPC npc) {
            timer--;
            if (!TryGetBoundOmen(PackSource(npc), out Projectile omen)) {
                Abort();
                return;
            }
            //离散刹车脉冲压住游荡漂移，让标线贴住实际出发点
            if (timer == 22 || timer == 10) {
                npc.velocity *= 0.45f;
                npc.netUpdate = true;
            }
            if (timer == MeteoriteDiveOmen.LockFrames) {
                //锁定帧：方向自此为承诺，写回预兆做各端权威纠偏
                if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                    lockDir = (Main.player[npc.target].Center - npc.Center).ToRotation();
                }
                omen.ai[2] = lockDir + 10f;
                omen.netUpdate = true;
            }
            if (timer <= 0) {
                phase = PhaseStrike;
                timer = DiveRise + DiveHold + DiveDecay + DiveSettleFrames;
                dripsLeft = DripCountByTier[boundTier - 1];
                lastDripPos = npc.Center;
                npc.netUpdate = true;
            }
        }

        private void TickStrike(NPC npc) {
            int total = DiveRise + DiveHold + DiveDecay + DiveSettleFrames;
            int elapsed = total - timer + 1;
            timer--;
            if (elapsed <= DiveRise + DiveHold + DiveDecay) {
                //包络塑形持有：抵住原版追尾转向；承诺性速度除回提速补偿
                npc.velocity = MobDash.Velocity(lockDir.ToRotationVector2(),
                    DivePeakByTier[boundTier - 1] / MoveGain(npc), elapsed, DiveRise, DiveHold, DiveDecay);
                if (elapsed == 1 || timer % 6 == 0) {
                    npc.netUpdate = true;
                }
                TryDrip(npc);
            }
            else {
                //力竭漂移后摇：缓速下沉，读作烧尽失浮
                npc.velocity *= 0.86f;
                npc.velocity.Y += SettleSagPerFrame;
                if (timer % 6 == 0) {
                    npc.netUpdate = true;
                }
            }
            if (timer <= 0) {
                npc.velocity *= 0.5f;
                npc.netUpdate = true;
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = DiveCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            }
        }

        /// <summary>
        /// 布点循环：自上一滴起俯冲路径每推进 <see cref="MeteoriteFireSpot.EmberGapPx"/> 才许落下一滴，
        /// 火斑最小间距由该常量真正保证（落地端还会以同一常量复查一次）
        /// </summary>
        private void TryDrip(NPC npc) {
            if (dripsLeft <= 0) {
                return;
            }
            if (Vector2.Distance(npc.Center, lastDripPos) < MeteoriteFireSpot.EmberGapPx) {
                return;
            }
            lastDripPos = npc.Center;
            dripsLeft--;
            if (CountActive(ModContent.ProjectileType<MeteoriteMoltenGlob>()) >= GlobCap
                || CountActive(ModContent.ProjectileType<MeteoriteFireSpot>()) >= MeteoriteFireSpot.FireSpotCap) {
                return;
            }
            int damage = Math.Max(1, (int)(npc.damage * GlobDamageFrac));
            Vector2 vel = npc.velocity * 0.25f + new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), 0.8f);
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(0f, npc.height * 0.3f),
                vel, ModContent.ProjectileType<MeteoriteMoltenGlob>(), damage, 0.5f, Main.myPlayer, boundTier);
        }
    }
}
