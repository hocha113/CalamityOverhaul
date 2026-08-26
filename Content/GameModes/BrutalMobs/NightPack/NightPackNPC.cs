using CalamityOverhaul.Content.GameModes.BrutalMobs.NightPack.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.NightPack
{
    /// <summary>
    /// 夜行猎群行为层：僵尸扑抓突进、恶魔眼钳形俯冲、骷髅抛物掷骨、洞穴蝙蝠掠袭，
    /// 由 <see cref="NightPackScheduler"/> 统一令牌错拍调度。
    /// 只叠加行为不动数值（数值层归 <see cref="GameModeNPC"/>），原版 AI 全程继续跑，
    /// 本层仅注入速度脉冲与生成弹幕。决策全在服务端（客户端 PostAI 早退），
    /// 客户端可见状态一律来自弹幕实体与 NPC 速度的原生同步
    /// </summary>
    internal class NightPackNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==== 通用节奏 ====
        /// <summary>令牌被拒/条件未满足的重试间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>出生后首攻等待窗，随机错开避免同屏齐动</summary>
        private const int FirstCooldownMin = 100;
        private const int FirstCooldownMax = 260;
        /// <summary>攻击冷却（档位 1/2/3），另加随机抖动</summary>
        private static readonly int[] AttackCooldownByTier = [300, 245, 190];
        private const int CooldownJitter = 60;
        /// <summary>错拍节拍：猎群成型后同族两次进攻的最小间隔（档位 1/2/3）</summary>
        private static readonly int[] StaggerByTier = [45, 36, 28];

        //==== 僵尸·扑抓突进 ====
        private const float ZombieMinRangeX = 60f;
        private const float ZombieMaxRangeX = 300f;
        private const float ZombieMaxRangeY = 180f;
        /// <summary>扑抓滞空帧数（档位越高弧越快，预告时长不变）</summary>
        private static readonly int[] ZombieLungeFlightByTier = [36, 32, 28];
        /// <summary>落地收势帧</summary>
        private const int ZombieLandRecover = 14;
        /// <summary>命中缓速时长（档位 1/2/3）</summary>
        private static readonly int[] ZombieSlowTicksByTier = [120, 150, 180];
        /// <summary>原版 UpdateNPC 的重力常数，跳弧解算与之对齐</summary>
        private const float NpcGravity = 0.3f;
        private const float LungeMaxVx = 10f;
        private const float LungeMaxUpVy = -12f;

        //==== 恶魔眼·钳形俯冲 ====
        private const float EyeMinRange = 120f;
        private const float EyeMaxRange = 640f;
        private static readonly float[] EyeDiveSpeedByTier = [10.5f, 11.5f, 12.5f];

        //==== 骷髅·抛物掷骨 ====
        private const float SkeletonMinRangeX = 140f;
        private const float SkeletonMaxRangeX = 560f;
        private const float SkeletonMaxRangeY = 340f;
        /// <summary>骨镖伤害=npc.damage（已缩放值）的此比例</summary>
        private const float BoneDamageFrac = 0.5f;
        /// <summary>掷出后收势帧（骨镖自行飞行，令牌随收势归还）</summary>
        private const int SkeletonRecover = 12;

        //==== 洞穴蝙蝠·掠袭 ====
        private const float BatMinRange = 90f;
        private const float BatMaxRange = 460f;
        private static readonly float[] BatDiveSpeedByTier = [9.5f, 10.5f, 11.5f];
        /// <summary>掠袭命中的黑暗减益时长（不随档位增长）</summary>
        private const int BatDarknessTicks = 90;

        private const byte PhaseIdle = 0;
        private const byte PhaseTelegraph = 1;
        private const byte PhaseStrike = 2;

        /// <summary>本个体出生时绑定的档位，0=未绑定（镜像 GameModeNPC；中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private NightPackFamily family;
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>锁定落点（僵尸/骷髅），锁定后不再改写（预告即承诺）</summary>
        private Vector2 lockPoint;
        /// <summary>锁定俯冲方向（恶魔眼/蝙蝠）</summary>
        private float lockDir;
        /// <summary>本次攻击的俯冲预兆槽位（服务端私产）</summary>
        private int omenIndex = -1;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && NightPackScheduler.TryGetFamily(entity.type, out _);

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            if (!NightPackScheduler.TryGetFamily(npc.type, out family)) {
                return;
            }
            boundTier = tier;
            //雕像等排除项在攻击入口逐项复查（SpawnedFromStatue 在 SetDefaults 之后才置位）
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

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (VaultUtils.isClient) {
                //决策只在服务端/单人；客户端画面全部来自同步原语
                return;
            }
            if (phase == PhaseIdle) {
                if (--cooldown > 0) {
                    return;
                }
                TryStart(npc);
                return;
            }
            if (phase == PhaseTelegraph) {
                TickTelegraph(npc);
                return;
            }
            TickStrike(npc);
        }

        private static bool CanSee(NPC npc, Player player)
            => Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height);

        private static bool ZombieReady(NPC npc, Player player) {
            if (npc.velocity.Y != 0f) {
                return false;
            }
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Bottom.Y - npc.Bottom.Y);
            return dx >= ZombieMinRangeX && dx <= ZombieMaxRangeX && dy <= ZombieMaxRangeY && CanSee(npc, player);
        }

        private static bool SkeletonReady(NPC npc, Player player) {
            if (npc.velocity.Y != 0f) {
                return false;
            }
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Center.Y - npc.Center.Y);
            return dx >= SkeletonMinRangeX && dx <= SkeletonMaxRangeX && dy <= SkeletonMaxRangeY && CanSee(npc, player);
        }

        private static bool AirReady(NPC npc, Player player, float min, float max) {
            float dist = Vector2.Distance(npc.Center, player.Center);
            return dist >= min && dist <= max && CanSee(npc, player);
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

            bool ready = family switch {
                NightPackFamily.Zombie => ZombieReady(npc, player),
                NightPackFamily.DemonEye => AirReady(npc, player, EyeMinRange, EyeMaxRange),
                NightPackFamily.Skeleton => SkeletonReady(npc, player),
                NightPackFamily.CaveBat => AirReady(npc, player, BatMinRange, BatMaxRange),
                _ => false,
            };
            if (!ready) {
                cooldown = RetryDelay;
                return;
            }

            float approach = (player.Center - npc.Center).ToRotation();
            int lease = family switch {
                NightPackFamily.Zombie => NightPounceOmen.TotalFrames + 30,
                NightPackFamily.DemonEye => NightDiveOmen.EyeTelegraphFrames + NightDiveOmen.EyeStrikeFrames + 30,
                NightPackFamily.Skeleton => NightBoneProj.TelegraphFrames + SkeletonRecover + 30,
                NightPackFamily.CaveBat => NightDiveOmen.BatTelegraphFrames + NightDiveOmen.BatStrikeFrames + 30,
                _ => 120,
            };
            if (!NightPackScheduler.TryAcquire(npc, family, player.Center, lease,
                StaggerByTier[boundTier - 1], approach)) {
                cooldown = RetryDelay;
                return;
            }

            //令牌到手：锁定并起预告。预告即实体：预告体生成失败（弹幕位满）则整次进攻作废
            switch (family) {
                case NightPackFamily.Zombie: {
                    //落点=玩家脚底，自此锁死（预告即承诺）
                    lockPoint = player.Bottom - Vector2.UnitY * 4f;
                    int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), lockPoint, Vector2.Zero,
                        ModContent.ProjectileType<NightPounceOmen>(), 0, 0f, Main.myPlayer,
                        npc.whoAmI, lockPoint.X, lockPoint.Y);
                    if (omen < 0 || omen >= Main.maxProjectiles) {
                        Abort(npc);
                        return;
                    }
                    //刹车脉冲：急停蓄势即起跳前摇
                    npc.velocity.X *= 0.15f;
                    npc.netUpdate = true;
                    timer = NightPounceOmen.TelegraphFrames;
                    break;
                }
                case NightPackFamily.DemonEye:
                case NightPackFamily.CaveBat:
                    omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<NightDiveOmen>(), 0, 0f, Main.myPlayer,
                        npc.whoAmI, family == NightPackFamily.CaveBat ? 1f : 0f, 0f);
                    if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                        Abort(npc);
                        return;
                    }
                    lockDir = approach;
                    //刹车脉冲：悬停蓄势
                    npc.velocity *= 0.3f;
                    npc.netUpdate = true;
                    timer = family == NightPackFamily.DemonEye
                        ? NightDiveOmen.EyeTelegraphFrames : NightDiveOmen.BatTelegraphFrames;
                    break;
                case NightPackFamily.Skeleton: {
                    lockPoint = player.Bottom - Vector2.UnitY * 10f;
                    int damage = Math.Max(1, (int)(npc.damage * BoneDamageFrac));
                    if (!SpawnBone(npc, 0, damage)) {
                        Abort(npc);
                        return;
                    }
                    if (boundTier >= 2) {
                        //档位强化：第二枚高弧骨镖，同一落点错时抵达（不破坏落点承诺）
                        SpawnBone(npc, 1, damage);
                    }
                    timer = NightBoneProj.TelegraphFrames;
                    break;
                }
            }
            phase = PhaseTelegraph;
        }

        /// <summary>预告体生成失败的回退：还令牌、退回待机</summary>
        private void Abort(NPC npc) {
            NightPackScheduler.Release(npc);
            omenIndex = -1;
            cooldown = RetryDelay;
        }

        private bool SpawnBone(NPC npc, int variant, int damage) {
            int index = Projectile.NewProjectile(npc.GetSource_FromAI(),
                npc.Top + new Vector2(0f, -18f - 14f * variant), Vector2.Zero,
                ModContent.ProjectileType<NightBoneProj>(), damage, 0f, Main.myPlayer,
                lockPoint.X, lockPoint.Y, variant * 1000 + npc.whoAmI);
            return index >= 0 && index < Main.maxProjectiles;
        }

        private void TickTelegraph(NPC npc) {
            timer--;

            if (family is NightPackFamily.DemonEye or NightPackFamily.CaveBat) {
                //离散刹车脉冲压住游荡漂移，让预告线贴住实际出发点（非每帧，脉冲帧才跟同步）
                if (timer == 24 || timer == 12) {
                    npc.velocity *= 0.45f;
                    npc.netUpdate = true;
                }
                int lockFrames = family == NightPackFamily.DemonEye
                    ? NightDiveOmen.EyeLockFrames : NightDiveOmen.BatLockFrames;
                if (timer == lockFrames) {
                    //锁定帧：方向自此为承诺，写回预兆实体做各端权威纠偏
                    if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                        lockDir = (Main.player[npc.target].Center - npc.Center).ToRotation();
                    }
                    if (omenIndex >= 0 && omenIndex < Main.maxProjectiles) {
                        Projectile omen = Main.projectile[omenIndex];
                        if (omen.active && omen.type == ModContent.ProjectileType<NightDiveOmen>()
                            && (int)omen.ai[0] == npc.whoAmI) {
                            omen.ai[2] = lockDir + 10f;
                            omen.netUpdate = true;
                        }
                    }
                }
            }
            else if (family == NightPackFamily.Zombie && timer == 16) {
                //中段再刹一次，压住走位漂移让跳弧贴住预告落点
                npc.velocity.X *= 0.3f;
                npc.netUpdate = true;
            }

            if (timer <= 0) {
                Commit(npc);
                phase = PhaseStrike;
            }
        }

        private void Commit(NPC npc) {
            //GameModeNPC 的提速层按 velocity*SpeedBonus 追加位移，
            //本层速度全部除回该系数，防双重缩放：档位强度只由本文件常量表达
            float advance = 1f + GameModeTuning.SpeedBonus(boundTier);
            switch (family) {
                case NightPackFamily.Zombie: {
                    //向锁定落点做定时长跳弧解算（位移项除提速系数，重力项不受提速影响）；
                    //原版 AI 空中的残余转向已被预兆标记宽度覆盖
                    int flight = ZombieLungeFlightByTier[boundTier - 1];
                    Vector2 to = lockPoint - npc.Bottom;
                    npc.velocity = new Vector2(
                        MathHelper.Clamp(to.X / (flight * advance), -LungeMaxVx, LungeMaxVx),
                        MathHelper.Clamp(to.Y / (flight * advance) - NpcGravity * flight * 0.5f, LungeMaxUpVy, 2f));
                    npc.netUpdate = true;
                    timer = flight + ZombieLandRecover;
                    break;
                }
                case NightPackFamily.DemonEye:
                    npc.velocity = lockDir.ToRotationVector2() * (EyeDiveSpeedByTier[boundTier - 1] / advance);
                    npc.netUpdate = true;
                    timer = NightDiveOmen.EyeStrikeFrames;
                    break;
                case NightPackFamily.CaveBat:
                    npc.velocity = lockDir.ToRotationVector2() * (BatDiveSpeedByTier[boundTier - 1] / advance);
                    npc.netUpdate = true;
                    timer = NightDiveOmen.BatStrikeFrames;
                    break;
                case NightPackFamily.Skeleton:
                    //骨镖在自身实体里掷出，骷髅只收势
                    timer = SkeletonRecover;
                    break;
            }
        }

        private void TickStrike(NPC npc) {
            timer--;
            if (timer > 0) {
                return;
            }
            NightPackScheduler.Release(npc);
            phase = PhaseIdle;
            omenIndex = -1;
            cooldown = AttackCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) {
            if (boundTier <= 0) {
                return;
            }
            //命中方本机结算，减益原生同步；突进窗由已同步的预兆实体判定，不读服务端私产计时器
            if (family == NightPackFamily.Zombie && NightPounceOmen.IsStrikeWindowFor(npc.whoAmI)) {
                target.AddBuff(BuffID.Slow, ZombieSlowTicksByTier[boundTier - 1]);
            }
            else if (family == NightPackFamily.CaveBat && NightDiveOmen.IsStrikeWindowFor(npc.whoAmI)) {
                target.AddBuff(BuffID.Darkness, BatDarknessTicks);
            }
        }
    }
}
