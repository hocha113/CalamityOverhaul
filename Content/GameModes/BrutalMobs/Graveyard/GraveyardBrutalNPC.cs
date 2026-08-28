using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.Graveyard.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Graveyard
{
    /// <summary>墓地组家族</summary>
    internal enum GraveFamily
    {
        None,
        /// <summary>鬼魂：怨聚点名</summary>
        Ghost,
        /// <summary>蛆尸：生前短扑+死亡蛆涌</summary>
        Maggot,
        /// <summary>渡鸦：惊掠</summary>
        Raven,
    }

    /// <summary>
    /// 残酷模式墓地组行为机制层，主题：怨之仪程——鬼魂不袭击，鬼魂"点名"。
    /// 叠加在原版 AI 之上，不接管：鬼魂怨聚点名（幽光丝线渐亮渐响→沿线幽冲→力竭半透明惩罚窗）、
    /// 蛆尸短扑与死亡蛆涌（凝聚预告→三蛆扇迸，具名角距间隙）、渡鸦惊掠（收翅标线→弧线掠面挂黑暗→拉起脱离）。
    /// 阵容声明：墓地常驻的普通僵尸/骷髅族已由 NightPack 承接，本包只管墓地专属三型；
    /// 决策与生成只在权威端跑，客户端可见状态一律来自同步弹幕实体与疲劳盖戳；数值层归 GameModeNPC
    /// </summary>
    internal class GraveyardBrutalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==== 通用节奏 ====
        /// <summary>条件未满足的重试间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>出生首攻错拍窗（M7 密度预算：60~180 帧）</summary>
        private const int FirstCooldownMin = 60;
        private const int FirstCooldownMax = 180;
        /// <summary>冷却随机抖动上限</summary>
        private const int CooldownJitter = 60;

        //==== 鬼魂·怨聚点名 ====
        /// <summary>点名触发距离窗（任务口径 200~420px）</summary>
        private const float GhostNameMinRange = 200f;
        private const float GhostNameMaxRange = 420f;
        /// <summary>同屏点名并发上限（静态计数：数活体丝线实体，自愈无漂移）</summary>
        private const int GhostNameCap = 2;
        /// <summary>点名冷却（档位只调频率不换机制）</summary>
        private static readonly int[] GhostCooldownByTier = [430, 380, 330];
        /// <summary>幽冲名义峰速（注入前除回 MoveGain）</summary>
        private static readonly float[] GhostDashPeakByTier = [11.5f, 12.5f, 13.5f];
        /// <summary>幽冲包络三段：rise+hold+decay 之和须等于 GyGhostNameThread.StrikeFrames</summary>
        private const int GhostDashRise = 6;
        private const int GhostDashHold = 10;
        private const int GhostDashDecay = 10;
        /// <summary>力竭期宿主半透明目标值（疲劳可读性）</summary>
        private const int FatigueAlpha = 170;

        //==== 蛆尸·短扑与蛆涌 ====
        /// <summary>短扑冷却（任务口径 ~480 帧一次）</summary>
        private static readonly int[] MaggotLungeCooldownByTier = [500, 470, 440];
        /// <summary>短扑前摇帧（纯近身体术：压速+血沫即可见信号，M3 姿态口径）</summary>
        private const int MaggotWindupFrames = 24;
        /// <summary>短扑包络三段与总帧</summary>
        private const int MaggotLungeRise = 5;
        private const int MaggotLungeHold = 8;
        private const int MaggotLungeDecay = 8;
        private const int MaggotLungeFrames = MaggotLungeRise + MaggotLungeHold + MaggotLungeDecay;
        private const int MaggotRecoverFrames = 10;
        /// <summary>短扑名义峰速（横向，注入前除回 MoveGain）</summary>
        private static readonly float[] MaggotLungePeakByTier = [7.5f, 8.2f, 8.9f];
        private const float MaggotLungeMinX = 50f;
        private const float MaggotLungeMaxX = 260f;
        private const float MaggotLungeMaxY = 120f;
        /// <summary>蛆弹伤害 = 已缩放 npc.damage × 此值</summary>
        private const float MaggotBoltDamageFrac = 0.45f;
        /// <summary>尸涌核全局并发上限</summary>
        private const int MaggotCoreCap = 6;

        //==== 渡鸦·惊掠 ====
        /// <summary>惊掠冷却（任务口径 ≥480 帧：骚扰定位不是主伤）</summary>
        private static readonly int[] RavenCooldownByTier = [560, 520, 480];
        /// <summary>同屏惊掠并发上限（静态计数：数活体掠袭预告实体）</summary>
        private const int RavenSwoopCap = 2;
        private const float RavenMinRange = 140f;
        private const float RavenMaxRange = 420f;
        /// <summary>掠面名义峰速（注入前除回 MoveGain）</summary>
        private static readonly float[] RavenPeakByTier = [10.5f, 11.5f, 12.5f];
        /// <summary>掠面包络三段：之和须等于 GyRavenSwoopOmen.StrikeFrames</summary>
        private const int RavenRise = 6;
        private const int RavenHold = 8;
        private const int RavenDecay = 10;
        /// <summary>弧线弓幅：垂直于掠线的 cos 弓速（半程下潜半程回升，净位移归零）</summary>
        private const float RavenBowAmp = 3.2f;
        private const int RavenRecoverFrames = 14;
        /// <summary>掠面命中黑暗减益时长（任务口径 3 秒，不随档位增长）</summary>
        private const int RavenDarknessTicks = 180;

        private const byte PhaseIdle = 0;
        private const byte PhaseTelegraph = 1;
        private const byte PhaseStrike = 2;
        private const byte PhaseRecover = 3;

        /// <summary>本个体出生时绑定的档位，0=未绑定（中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private GraveFamily family;
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>锁定方向：鬼魂/渡鸦为弧度，蛆尸为横向正负号（锁定后不再改写，预告即承诺）</summary>
        private float lockDir;
        /// <summary>本次攻击的预告体槽位（权威端私产）</summary>
        private int omenIndex = -1;
        /// <summary>力竭盖戳有效期（由丝线实体在所有端盖，2 帧租约；镜像 EliteMove Stamp 模式）</summary>
        private uint fatigueVisUntil;

        private static GraveFamily ResolveFamily(int type) => type switch {
            NPCID.Ghost => GraveFamily.Ghost,
            NPCID.MaggotZombie => GraveFamily.Maggot,
            NPCID.Raven => GraveFamily.Raven,
            _ => GraveFamily.None,
        };

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && ResolveFamily(entity.type) != GraveFamily.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            family = GraveFamily.None;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            GraveFamily resolved = ResolveFamily(npc.type);
            if (resolved == GraveFamily.None) {
                return;
            }
            family = resolved;
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

        /// <summary>NPC 侧回读校验绑定的预告实体（索引+类型+归属），缺位=不打无预告的冲</summary>
        private bool OmenBound(NPC npc, int projType) {
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile proj = Main.projectile[omenIndex];
            return proj.active && proj.type == projType && (int)proj.ai[0] == PackSource(npc);
        }

        /// <summary>力竭盖戳（丝线实体在所有端逐帧调用）</summary>
        internal void StampGhostFatigue() => fatigueVisUntil = Main.GameUpdateCount + 2;

        /// <summary>
        /// 幽魂力竭可视化：戳新鲜时抬半透明（只抬不压，与原版自管值取更大者），
        /// 过期只回收本层可能抬上去的区间（镜像 WastesBrutalNPC 沙隐的退隐回收口径）
        /// </summary>
        private void ApplyFatigueVeil(NPC npc) {
            if (Main.GameUpdateCount < fatigueVisUntil) {
                npc.alpha = Math.Max(npc.alpha, FatigueAlpha);
                if (!Main.dedServ && Main.rand.NextBool(7)) {
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.Smoke, 0f, -0.4f, 190, default, 0.9f);
                    dust.noGravity = true;
                    dust.velocity *= 0.3f;
                }
            }
            else if (npc.alpha > 0 && npc.alpha <= FatigueAlpha + 1) {
                npc.alpha = Math.Max(0, npc.alpha - 6);
            }
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (family == GraveFamily.Ghost) {
                ApplyFatigueVeil(npc);//全端确定性表现，须在客户端早退之前
            }
            if (VaultUtils.isClient) {
                return;//决策只在权威端
            }

            if (phase == PhaseIdle) {
                if (--cooldown > 0) {
                    return;
                }
                TryStart(npc);
                return;
            }

            switch (family) {
                case GraveFamily.Ghost:
                    if (phase == PhaseTelegraph) {
                        TickGhostTelegraph(npc);
                    }
                    else if (phase == PhaseStrike) {
                        TickGhostStrike(npc);
                    }
                    else {
                        TickGhostFatigue(npc);
                    }
                    return;
                case GraveFamily.Maggot:
                    if (phase == PhaseTelegraph) {
                        TickMaggotWindup(npc);
                    }
                    else if (phase == PhaseStrike) {
                        TickMaggotStrike(npc);
                    }
                    else {
                        TickMaggotRecover(npc);
                    }
                    return;
                case GraveFamily.Raven:
                    if (phase == PhaseTelegraph) {
                        TickRavenTelegraph(npc);
                    }
                    else if (phase == PhaseStrike) {
                        TickRavenStrike(npc);
                    }
                    else {
                        TickRavenRecover(npc);
                    }
                    return;
            }
        }

        /// <summary>预告体缺位/生成失败的回退：退回待机短冷却（失败方向=安全方向）</summary>
        private void AbortToCooldown() {
            phase = PhaseIdle;
            omenIndex = -1;
            cooldown = RetryDelay;
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
            switch (family) {
                case GraveFamily.Ghost:
                    TryStartGhost(npc, target);
                    return;
                case GraveFamily.Maggot:
                    TryStartMaggot(npc, target);
                    return;
                case GraveFamily.Raven:
                    TryStartRaven(npc, target);
                    return;
            }
        }

        //==== 鬼魂：怨聚点名 ====

        /// <summary>点名起手：绕行漂近由原版 AI 负责，本层只在距离窗内立定并起丝线</summary>
        private void TryStartGhost(NPC npc, Player target) {
            float dist = npc.Distance(target.Center);
            if (dist < GhostNameMinRange || dist > GhostNameMaxRange) {
                cooldown = RetryDelay;
                return;
            }
            if (CountActive(ModContent.ProjectileType<GyGhostNameThread>()) >= GhostNameCap) {
                cooldown = RetryDelay;
                return;
            }
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<GyGhostNameThread>(), 0, 0f, Main.myPlayer,
                PackSource(npc), boundTier);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                cooldown = RetryDelay;
                return;
            }
            omenIndex = omen;
            lockDir = (target.Center - npc.Center).ToRotation();
            //立定蓄势：点名期不追击（鬼魂不袭击，鬼魂点名）
            npc.velocity *= 0.2f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = GyGhostNameThread.TelegraphFrames;
        }

        private void TickGhostTelegraph(NPC npc) {
            timer--;
            if (!OmenBound(npc, ModContent.ProjectileType<GyGhostNameThread>())) {
                AbortToCooldown();
                return;
            }
            //立定：离散刹车脉冲压住原版漂移（脉冲帧才跟同步）
            if (timer == 34 || timer == 22 || timer == 10) {
                npc.velocity *= 0.3f;
                npc.netUpdate = true;
            }
            if (timer == GyGhostNameThread.LockFrames) {
                //锁定帧：方向自此为承诺，写回丝线实体做各端权威纠偏
                if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                    lockDir = (Main.player[npc.target].Center - npc.Center).ToRotation();
                }
                Projectile omen = Main.projectile[omenIndex];
                omen.ai[2] = lockDir + 10f;
                omen.netUpdate = true;
            }
            if (timer <= 0) {
                phase = PhaseStrike;
                timer = GyGhostNameThread.StrikeFrames;
                npc.netUpdate = true;
            }
        }

        private void TickGhostStrike(NPC npc) {
            int t = GyGhostNameThread.StrikeFrames - timer + 1;
            //沿锁定丝线一次幽冲：包络塑形，穿墙由鬼魂原生 noTileCollide 保留
            npc.velocity = MobDash.Velocity(lockDir.ToRotationVector2(),
                GhostDashPeakByTier[boundTier - 1] / MoveGain(npc),
                t, GhostDashRise, GhostDashHold, GhostDashDecay);
            if (t % 6 == 0) {
                npc.netUpdate = true;//长保持段低频重推
            }
            timer--;
            if (timer <= 0) {
                phase = PhaseRecover;
                timer = GyGhostNameThread.FatigueFrames;
                npc.netUpdate = true;
            }
        }

        /// <summary>力竭惩罚窗：压住追击（不再穿墙袭击），半透明表现由丝线实体盖戳驱动</summary>
        private void TickGhostFatigue(NPC npc) {
            timer--;
            if (timer % 6 == 0) {
                npc.velocity *= 0.45f;
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = GhostCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            }
        }

        //==== 蛆尸：生前短扑 ====

        private void TryStartMaggot(NPC npc, Player target) {
            if (npc.velocity.Y != 0f) {
                cooldown = RetryDelay;
                return;
            }
            float dx = Math.Abs(target.Center.X - npc.Center.X);
            float dy = Math.Abs(target.Bottom.Y - npc.Bottom.Y);
            if (dx < MaggotLungeMinX || dx > MaggotLungeMaxX || dy > MaggotLungeMaxY || !CanSee(npc, target)) {
                cooldown = RetryDelay;
                return;
            }
            //锁向即承诺：横向短扑只锁朝向
            lockDir = Math.Sign(target.Center.X - npc.Center.X);
            npc.velocity.X *= 0.15f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = MaggotWindupFrames;
        }

        /// <summary>短扑前摇：压速（原生同步，M3 姿态信号）+ 血沫蠕动（非专用服务器端渲染）</summary>
        private void TickMaggotWindup(NPC npc) {
            timer--;
            if (timer == 16 || timer == 8) {
                npc.velocity.X *= 0.3f;
                npc.netUpdate = true;
            }
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                    Main.rand.NextBool() ? DustID.Blood : DustID.JungleGrass, 0f, -0.5f, 140, default, 0.9f);
                dust.velocity *= 0.4f;
            }
            if (timer <= 0) {
                phase = PhaseStrike;
                timer = MaggotLungeFrames;
                npc.netUpdate = true;
            }
        }

        private void TickMaggotStrike(NPC npc) {
            int t = MaggotLungeFrames - timer + 1;
            //横向包络短扑：竖直方向交给原版重力（重力项不除提速系数）
            npc.velocity.X = lockDir * (MaggotLungePeakByTier[boundTier - 1]
                * MobDash.Envelope(t, MaggotLungeRise, MaggotLungeHold, MaggotLungeDecay) / MoveGain(npc));
            if (t % 6 == 0) {
                npc.netUpdate = true;
            }
            timer--;
            if (timer <= 0) {
                phase = PhaseRecover;
                timer = MaggotRecoverFrames;
                npc.netUpdate = true;
            }
        }

        private void TickMaggotRecover(NPC npc) {
            timer--;
            if (timer % 6 == 0) {
                npc.velocity.X *= 0.4f;
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                phase = PhaseIdle;
                cooldown = MaggotLungeCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            }
        }

        //==== 渡鸦：惊掠 ====

        private void TryStartRaven(NPC npc, Player target) {
            float dist = npc.Distance(target.Center);
            if (dist < RavenMinRange || dist > RavenMaxRange || !CanSee(npc, target)) {
                cooldown = RetryDelay;
                return;
            }
            if (CountActive(ModContent.ProjectileType<GyRavenSwoopOmen>()) >= RavenSwoopCap) {
                cooldown = RetryDelay;
                return;
            }
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<GyRavenSwoopOmen>(), 0, 0f, Main.myPlayer,
                PackSource(npc), boundTier);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                cooldown = RetryDelay;
                return;
            }
            omenIndex = omen;
            lockDir = (target.Center - npc.Center).ToRotation();
            //收翅蓄势
            npc.velocity *= 0.3f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = GyRavenSwoopOmen.TelegraphFrames;
        }

        private void TickRavenTelegraph(NPC npc) {
            timer--;
            if (!OmenBound(npc, ModContent.ProjectileType<GyRavenSwoopOmen>())) {
                AbortToCooldown();
                return;
            }
            if (timer == 20 || timer == GyRavenSwoopOmen.LockFrames) {
                npc.velocity *= 0.4f;
                npc.netUpdate = true;
            }
            if (timer == GyRavenSwoopOmen.LockFrames) {
                if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                    lockDir = (Main.player[npc.target].Center - npc.Center).ToRotation();
                }
                Projectile omen = Main.projectile[omenIndex];
                omen.ai[2] = lockDir + 10f;
                omen.netUpdate = true;
            }
            if (timer <= 0) {
                phase = PhaseStrike;
                timer = GyRavenSwoopOmen.StrikeFrames;
                npc.netUpdate = true;
            }
        }

        private void TickRavenStrike(NPC npc) {
            int t = GyRavenSwoopOmen.StrikeFrames - timer + 1;
            float gain = MoveGain(npc);
            Vector2 dir = lockDir.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            if (perp.Y < 0f) {
                perp = -perp;
            }
            //弧线掠面：主轴包络 + 垂直弓弧（cos 项半程下潜半程回升，净位移归零；均为位移项，同除提速系数）
            float bow = RavenBowAmp * MathF.Cos(MathHelper.Pi * t / GyRavenSwoopOmen.StrikeFrames);
            npc.velocity = (dir * (RavenPeakByTier[boundTier - 1]
                * MobDash.Envelope(t, RavenRise, RavenHold, RavenDecay)) + perp * bow) / gain;
            if (t % 6 == 0) {
                npc.netUpdate = true;
            }
            timer--;
            if (timer <= 0) {
                //拉起脱离：残速上抬后逐步交还原版 AI
                npc.velocity = new Vector2(npc.velocity.X * 0.3f, -3f / gain);
                npc.netUpdate = true;
                phase = PhaseRecover;
                timer = RavenRecoverFrames;
            }
        }

        private void TickRavenRecover(NPC npc) {
            timer--;
            if (timer % 6 == 0) {
                npc.velocity *= 0.6f;
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = RavenCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            }
        }

        /// <summary>死亡蛆涌：先出无害凝聚核（≥30 帧预告），由核在提交帧迸出蛆弹（权威端）</summary>
        public override void OnKill(NPC npc) {
            if (boundTier <= 0 || Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (family != GraveFamily.Maggot || !Eligible(npc)) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<GyMaggotBurstCore>()) >= MaggotCoreCap) {
                return;
            }
            int damage = (int)(npc.damage * MaggotBoltDamageFrac);
            Projectile.NewProjectile(npc.GetSource_Death(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<GyMaggotBurstCore>(), damage, 0.5f, Main.myPlayer, boundTier);
        }

        /// <summary>掠面命中挂黑暗（命中方本机结算，减益原生同步；判窗读已同步的预告实体，不读服务端私产）</summary>
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) {
            if (boundTier <= 0 || family != GraveFamily.Raven) {
                return;
            }
            if (GyRavenSwoopOmen.IsStrikeWindowFor(npc.whoAmI)) {
                target.AddBuff(BuffID.Darkness, RavenDarknessTicks);
            }
        }
    }
}
