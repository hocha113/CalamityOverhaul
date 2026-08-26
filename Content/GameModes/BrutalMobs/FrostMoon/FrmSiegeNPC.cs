using CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon
{
    /// <summary>
    /// 霜月小怪「攻城矩阵」行为层：礼盒迫击炮（礼物拟怪/精灵弓手/坎卜斯）、
    /// 精灵直升机扫射航线、胡桃夹子跳弹瞄准。只叠加行为不动数值，原版 AI 全程继续跑；
    /// 决策全在权威端（客户端 PostAI 早退），客户端可见状态一律来自已同步的弹幕实体。
    /// 纯近战填充怪（僵尸精灵三型/姜饼人/雪人怪/雪花怪）不入类型表，理由见组报告
    /// </summary>
    internal class FrmSiegeNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private enum SiegeFamily : byte
        {
            None,
            /// <summary>礼盒迫击炮：抛射+落点标记环</summary>
            Mortar,
            /// <summary>直升机扫射航线</summary>
            Strafe,
            /// <summary>胡桃夹子跳弹</summary>
            Bounce,
        }

        //==== 通用节奏 ====
        /// <summary>条件未满足的重试间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>出生首攻等待窗（随机错开避免同屏齐射）</summary>
        private const int FirstCooldownMin = 90;
        private const int FirstCooldownMax = 240;
        private const int CooldownJitter = 60;

        //==== 礼盒迫击炮 ====
        /// <summary>弹着伤害 = npc.damage（已缩放值）× 此比例</summary>
        private const float MortarDamageFrac = 0.65f;
        private const float MortarMinRangeX = 200f;
        private const float MortarMaxRangeX = 760f;
        private const float MortarMaxRangeY = 480f;
        /// <summary>落点地表扫描深度（物块）</summary>
        private const int MortarGroundScanTiles = 46;
        /// <summary>标记环并发上限（触发时点算）</summary>
        private const int MortarConcurrentCap = 6;

        //==== 直升机扫射 ====
        private const float StrafeBulletDamageFrac = 0.45f;
        /// <summary>扫掠实速（像素/帧）。注入速度=此值÷(1+提速系数)，被 GameModeNPC.PostAI
        /// 的位移补偿乘回后恰为此值，故扫射帧数=航线长÷此值 与档位无关（判定窗覆盖核算）</summary>
        private const float StrafeSpeed = 10f;
        private const float StrafeBulletSpeed = 13f;
        /// <summary>机炮点射间隔（帧）</summary>
        private const int StrafeFireInterval = 5;
        private const float StrafeMinRange = 200f;
        private const float StrafeMaxRange = 640f;
        /// <summary>同屏扫射航线并发上限</summary>
        private const int StrafeConcurrentCap = 2;

        //==== 胡桃夹子跳弹 ====
        private const float NutShellDamageFrac = 0.6f;
        private const float NutShellSpeed = 8.6f;
        private const float NutMinRange = 140f;
        private const float NutMaxRange = 560f;
        /// <summary>瞄准线并发上限（与小Boss共用同一预兆类型，计数自然合并）</summary>
        private const int NutConcurrentCap = 6;
        /// <summary>发射后收势帧</summary>
        private const int NutRecoverFrames = 18;

        private const byte PhaseIdle = 0;
        private const byte PhaseTelegraph = 1;
        private const byte PhaseStrike = 2;

        /// <summary>出生绑定档位，0=未绑定（中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private SiegeFamily family;
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>锁定方向（承诺后不再改写）</summary>
        private float lockedAngle;
        /// <summary>扫射期注入速度（已除提速补偿）</summary>
        private Vector2 runVelocity;
        /// <summary>本次攻击的预兆槽位（权威端私产，回读时索引+类型双校验）</summary>
        private int omenIndex = -1;

        private static SiegeFamily ResolveFamily(int type) {
            if (type == NPCID.PresentMimic || type == NPCID.ElfArcher || type == NPCID.Krampus) {
                return SiegeFamily.Mortar;
            }
            if (type == NPCID.ElfCopter) {
                return SiegeFamily.Strafe;
            }
            if (type == NPCID.Nutcracker || type == NPCID.NutcrackerSpinning) {
                return SiegeFamily.Bounce;
            }
            return SiegeFamily.None;
        }

        /// <summary>迫击炮型号：半径比例 / 飞行帧（=标记环可见时长）/ 基准冷却</summary>
        private static (float scale, int flight, int cooldown) MortarProfile(int type) {
            if (type == NPCID.Krampus) {
                return (1.25f, 78, 500);
            }
            if (type == NPCID.ElfArcher) {
                return (0.8f, 60, 380);
            }
            return (1f, 66, 430);
        }

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && ResolveFamily(entity.type) != SiegeFamily.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            family = SiegeFamily.None;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            family = ResolveFamily(npc.type);
            if (family == SiegeFamily.None) {
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

        /// <summary>档位冷却系数（机制只调强度不换形态）</summary>
        private int TierCooldown(int baseCooldown)
            => (int)(baseCooldown * (boundTier >= 3 ? 0.7f : boundTier >= 2 ? 0.85f : 1f))
               + Main.rand.Next(CooldownJitter + 1);

        /// <summary>校验自己名下的预兆弹幕仍有效（索引+类型双校验，防槽位复用）</summary>
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

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (VaultUtils.isClient) {
                //决策只在权威端；客户端画面全部来自同步原语
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
                case SiegeFamily.Strafe:
                    StrafeActive(npc);
                    break;
                case SiegeFamily.Bounce:
                    BounceActive(npc);
                    break;
                default:
                    //迫击炮为发射即忘，不应停留在非待机相位；防御性回正
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
                case SiegeFamily.Mortar:
                    TryMortar(npc, player);
                    break;
                case SiegeFamily.Strafe:
                    TryStrafe(npc, player);
                    break;
                case SiegeFamily.Bounce:
                    TryBounce(npc, player);
                    break;
            }
        }

        #region 礼盒迫击炮（发射即忘：标记环+炮弹自治）
        private void TryMortar(NPC npc, Player player) {
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Center.Y - npc.Center.Y);
            if (dx < MortarMinRangeX || dx > MortarMaxRangeX || dy > MortarMaxRangeY) {
                cooldown = RetryDelay;
                return;
            }
            if (FrmSiegeUtils.CountProjOfType(ModContent.ProjectileType<FrmMortarBlastProj>()) >= MortarConcurrentCap) {
                cooldown = RetryDelay + 15;
                return;
            }
            //弹着点=此刻目标脚下地表（自此锁死，预告即承诺）；无地表则不开火
            if (!FrmSiegeUtils.TryFindGroundY(player.Bottom - Vector2.UnitY * 8f, MortarGroundScanTiles, out float groundY)) {
                cooldown = RetryDelay;
                return;
            }
            (float scale, int flight, int baseCooldown) = MortarProfile(npc.type);
            Vector2 mark = new Vector2(player.Center.X, groundY);
            int damage = Math.Max(1, (int)(npc.damage * MortarDamageFrac));
            FrmSiegeUtils.SpawnMortarShot(npc, mark, flight, scale, damage);

            //出手顿挫（脉冲帧才跟同步）
            npc.velocity.X *= 0.3f;
            npc.netUpdate = true;
            cooldown = TierCooldown(baseCooldown);
        }
        #endregion

        #region 直升机扫射航线
        private void TryStrafe(NPC npc, Player player) {
            float dist = Vector2.Distance(npc.Center, player.Center);
            if (dist < StrafeMinRange || dist > StrafeMaxRange
                || !Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)) {
                cooldown = RetryDelay;
                return;
            }
            if (FrmSiegeUtils.CountProjOfType(ModContent.ProjectileType<FrmStrafeLaneProj>()) >= StrafeConcurrentCap) {
                cooldown = RetryDelay + 15;
                return;
            }
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<FrmStrafeLaneProj>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, npc.type, 0f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                //预告体生成失败（弹幕位满）：无预告不开火（失败方向=安全方向）
                cooldown = RetryDelay;
                return;
            }
            //悬停蓄势
            npc.velocity *= 0.3f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = FrmStrafeLaneProj.TelegraphFrames;
        }

        private void StrafeActive(NPC npc) {
            if (phase == PhaseTelegraph) {
                timer--;
                //离散刹车脉冲压住游荡漂移，让航线起点贴住实际出发点
                if (timer == 30 || timer == 12) {
                    npc.velocity *= 0.4f;
                    npc.netUpdate = true;
                }
                bool omenValid = TryGetOmen(ModContent.ProjectileType<FrmStrafeLaneProj>(), npc.whoAmI, out Projectile lane);
                if (!omenValid) {
                    //预告体缺位：整次扫射作废（无预告不开火）
                    phase = PhaseIdle;
                    cooldown = TierCooldown(360);
                    return;
                }
                if (timer == FrmStrafeLaneProj.LockFrames) {
                    //锁定帧：方向自此为承诺，写回航线实体作各端权威纠偏
                    lockedAngle = npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()
                        ? (Main.player[npc.target].Center - npc.Center).ToRotation()
                        : lane.rotation;
                    lane.ai[2] = lockedAngle + 10f;
                    lane.netUpdate = true;
                }
                if (timer <= 0) {
                    //扫掠注入：除以提速补偿，保证实际轨迹速度=StrafeSpeed（航线承诺）
                    runVelocity = lockedAngle.ToRotationVector2()
                        * (StrafeSpeed / (1f + GameModeTuning.SpeedBonus(boundTier)));
                    npc.velocity = runVelocity;
                    npc.netUpdate = true;
                    phase = PhaseStrike;
                    timer = FrmStrafeLaneProj.RunFrames;
                }
                return;
            }

            //扫射窗：钉住注入速度抵抗原版 AI 转向（非每帧同步，低频跟包）
            npc.velocity = runVelocity;
            if (timer % 10 == 0) {
                npc.netUpdate = true;
            }
            if (timer % StrafeFireInterval == 0) {
                //机炮点射：横向散布被 LaneHalfWidth 钳制（航道边界线共用此常量），弹向严格沿锁定角
                float perpOffset = Main.rand.NextFloat(-1f, 1f) * (FrmStrafeLaneProj.LaneHalfWidth - 10f);
                Vector2 spawnPos = npc.Center + (lockedAngle + MathHelper.PiOver2).ToRotationVector2() * perpOffset;
                float traveled = (FrmStrafeLaneProj.RunFrames - timer) * StrafeSpeed;
                float remain = FrmStrafeLaneProj.LaneLength - traveled;
                if (remain > 40f) {
                    int damage = Math.Max(1, (int)(npc.damage * StrafeBulletDamageFrac));
                    Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos,
                        lockedAngle.ToRotationVector2() * StrafeBulletSpeed,
                        ModContent.ProjectileType<FrmStrafeBulletProj>(), damage, 0.5f, Main.myPlayer, remain);
                }
            }
            if (--timer <= 0) {
                //收势：泄掉注入速度还给原版 AI
                npc.velocity *= 0.3f;
                npc.netUpdate = true;
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = TierCooldown(520);
            }
        }
        #endregion

        #region 胡桃夹子跳弹
        private void TryBounce(NPC npc, Player player) {
            float dist = Vector2.Distance(npc.Center, player.Center);
            if (dist < NutMinRange || dist > NutMaxRange
                || !Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)) {
                cooldown = RetryDelay;
                return;
            }
            if (FrmSiegeUtils.CountProjOfType(ModContent.ProjectileType<FrmAimLaneOmen>()) >= NutConcurrentCap) {
                cooldown = RetryDelay + 15;
                return;
            }
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<FrmAimLaneOmen>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, FrmAimLaneOmen.StyleNut * 1000 + npc.type, 0f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                cooldown = RetryDelay;
                return;
            }
            //瞄准顿步
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = FrmAimLaneOmen.NutTrackFrames + FrmAimLaneOmen.NutLockFrames;
        }

        private void BounceActive(NPC npc) {
            if (phase == PhaseTelegraph) {
                timer--;
                if (timer == FrmAimLaneOmen.NutLockFrames + 6) {
                    //中段再刹一次，压住走位漂移
                    npc.velocity.X *= 0.25f;
                    npc.netUpdate = true;
                }
                bool omenValid = TryGetOmen(ModContent.ProjectileType<FrmAimLaneOmen>(), npc.whoAmI, out Projectile omen);
                if (!omenValid) {
                    phase = PhaseIdle;
                    cooldown = TierCooldown(300);
                    return;
                }
                if (timer == FrmAimLaneOmen.NutLockFrames) {
                    //锁定帧：方向承诺
                    lockedAngle = npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()
                        ? (Main.player[npc.target].Center - npc.Center).ToRotation()
                        : omen.rotation;
                    omen.ai[2] = lockedAngle + 10f;
                    omen.netUpdate = true;
                }
                if (timer <= 0) {
                    //沿承诺线发出跳弹（弹幕不吃提速层，无需补偿）
                    int damage = Math.Max(1, (int)(npc.damage * NutShellDamageFrac));
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                        lockedAngle.ToRotationVector2() * NutShellSpeed,
                        ModContent.ProjectileType<FrmNutShellProj>(), damage, 1f, Main.myPlayer);
                    phase = PhaseStrike;
                    timer = NutRecoverFrames;
                }
                return;
            }

            if (--timer <= 0) {
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = TierCooldown(340);
            }
        }
        #endregion
    }
}
