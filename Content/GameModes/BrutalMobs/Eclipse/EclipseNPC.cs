using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.Eclipse.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Eclipse
{
    /// <summary>
    /// 日食「处刑与破绽」行为层：每类怪一记带实体预告的蓄力重击（冲锋/俯冲/重载荷三家族），
    /// 挥空则进入 60-90 帧可见破绽态（承伤加深+踉跄），躲招的奖励是反打窗口。
    /// 冲锋家族带 M6 签名分支：Frankenstein 落点电火花、SwampThing 两段小跳接扑、
    /// CreatureFromTheDeep 力竭长滑行（Vampire 的血狩印为既有签名）；Fritz/Psycho/Butcher 仍走基础冲锋。
    /// 不接管原版 AI，只做叠加注入；决策全在服务端（客户端 PostAI 早退），
    /// 客户端可见状态一律来自已同步实体（预兆/破绽/血狩印），实体每帧向本类盖镜像戳，
    /// 命中门与减速只读镜像（受击结算端本地可读，各端数字一致）。
    /// 吸血鬼双形态经 Transform 互变时本实例会重建，跨形态状态全部由实体携带
    /// </summary>
    internal class EclipseNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==== 通用节奏 ====
        /// <summary>条件未满足的重试间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>出生首攻错拍窗下界（M7 契约 60~180：初见 3 秒内可见首招，个体错帧防齐动）</summary>
        private const int FirstCooldownMin = 60;
        /// <summary>出生首攻错拍窗上界</summary>
        private const int FirstCooldownMax = 180;
        /// <summary>冷却随机抖动上限</summary>
        private const int CooldownJitter = 70;
        /// <summary>处刑重击全局并发上限（数活预兆实体，自愈无漂移）</summary>
        private const int StrikeConcurrentCap = 6;

        //==== 基础 Rush 冲锋包络（M2：缓入→峰值→力竭衰减，替代起手瞬间满速的匀速回写）====
        /// <summary>冲锋包络爬升帧</summary>
        private const int RushRiseFrames = 4;
        /// <summary>冲锋包络力竭衰减帧（峰值保持=Strike-rise-decay，各型按档案自解）</summary>
        private const int RushDecayFrames = 7;
        /// <summary>起手帧初始推力系数：垫住包络首帧前的原版接管空窗</summary>
        private const float RushLaunchPulse = 0.35f;

        //==== Rush 签名差异（M6：每型一条玩家叫得出名字的行为差异，不是数值微调）====
        /// <summary>SwampThing 签名·蹒跚双跳：跳数</summary>
        private const int SwampHopCount = 2;
        /// <summary>小跳前向名义速（承诺位移，注入时除提速补偿）</summary>
        private const float SwampHopSpeed = 3.4f;
        /// <summary>小跳起跳竖速（重力域弹道量，不除补偿）</summary>
        private const float SwampHopLaunchVy = -3.8f;
        /// <summary>单跳超时帧（落地判定的兜底推进，防卡崖/水中悬滞）</summary>
        private const int SwampHopTimeout = 34;
        /// <summary>CreatureFromTheDeep 签名·长滑行：滑行窗帧数</summary>
        private const int DeepGlideFrames = 26;
        /// <summary>滑行每帧衰减（力竭曲线，M2 后摇=显式衰减帧）</summary>
        private const float DeepGlideDecay = 0.93f;
        /// <summary>Frankenstein 签名·落点电火花伤害系数（基于已缩放 npc.damage）</summary>
        private const float SparkDamageFrac = 0.45f;

        //==== 家族触发窗 ====
        /// <summary>冲锋家族的纵向高差容忍</summary>
        private const float RushMaxRangeY = 150f;

        //==== 载荷参数（伤害以 npc.damage 已缩放值为基准，全部 ≤75%）====
        private const float FlaskDamageFrac = 0.6f;
        private const float OrbDamageFrac = 0.7f;
        private const float NailDamageFrac = 0.5f;
        /// <summary>眼弹速度（档位 1/2/3；弹幕不吃提速层，无需补偿）</summary>
        private static readonly float[] OrbSpeedByTier = [10.5f, 11.5f, 12.5f];
        /// <summary>重钉束数量（档位 1/2/3），束内均布</summary>
        private static readonly int[] NailCountByTier = [3, 4, 5];
        /// <summary>重钉束半张角（弧度）：束外即安全，方向锁死不追踪=本机制的逃生保证，发射循环直接读取</summary>
        private const float NailSpreadHalfAngle = 0.11f;
        private const float NailSpeed = 13.5f;

        //==== 破绽踉跄 ====
        /// <summary>破绽期地面怪横向拖拽系数（每帧乘）</summary>
        private const float OpeningDragGround = 0.88f;
        /// <summary>破绽期飞行怪全轴拖拽系数</summary>
        private const float OpeningDragAir = 0.92f;

        private const byte PhaseIdle = 0;
        private const byte PhaseTelegraph = 1;
        private const byte PhaseStrike = 2;
        /// <summary>SwampThing 专属：双跳段（预告结束→正式扑出之间）</summary>
        private const byte PhaseHop = 3;
        /// <summary>CreatureFromTheDeep 专属：冲锋后力竭滑行段</summary>
        private const byte PhaseGlide = 4;

        /// <summary>本个体出生时绑定的档位，0=未绑定（中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private EclProfile profile;

        //——服务端决策私产（客户端不读）——
        private bool initialized;
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>锁定方向（锁定帧后即承诺，不再改写）</summary>
        private float lockDir;
        /// <summary>毒瓶锁定落点（生成预兆瞬间锁死）</summary>
        private Vector2 lockPoint;
        /// <summary>执行期注入速度（每帧回写抵住原版 AI 衰减）</summary>
        private Vector2 dashVec;
        /// <summary>SwampThing 双跳游标</summary>
        private int hopIndex;
        private int omenIndex = -1;
        /// <summary>本次重击是否碰到过玩家（服务端几何采样；碰到=不给破绽）</summary>
        private bool strikeConnected;
        /// <summary>在飞载荷计数（载荷死亡回报递减，归零即裁决）</summary>
        private int payloadsAlive;

        //——镜像字段：由已同步实体每帧盖戳，各端一致——
        /// <summary>破绽窗（EclOpeningProj 盖戳）：承伤加深与踉跄减速只读它</summary>
        private uint openingUntil;
        /// <summary>重击执行窗（EclStrikeOmen 盖戳）：吸血鬼挂印只认这扇窗</summary>
        private uint strikeWindowUntil;

        internal void StampOpening() => openingUntil = Main.GameUpdateCount + 2;
        internal void StampStrikeWindow() => strikeWindowUntil = Main.GameUpdateCount + 2;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && EclEclipseSets.FamilyOf(entity.type) != EclFamily.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            if (!EclEclipseSets.Profiles.TryGetValue(npc.type, out profile)) {
                return;
            }
            boundTier = tier;
            //出生此刻 npc.whoAmI 恒为 0（NewNPC 之后才赋值），首发错拍推迟到首个决策帧播种
        }

        /// <summary>机制入口资格：友方/无敌/Boss/小动物载体/雕像怪/共享血池体节逐项排除（每次触发都过）</summary>
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

        private static bool CanSee(NPC npc, Player player)
            => Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height);

        /// <summary>并发计数：数活着的处刑预兆（仅触发时调用）</summary>
        private static int CountActiveOmens() {
            int type = ModContent.ProjectileType<EclStrikeOmen>();
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].type == type) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>校验自己名下的预兆仍有效（槽位不是身份：index+type+锚三重校验）</summary>
        private bool TryGetBoundOmen(NPC npc, out Projectile omen) {
            omen = null;
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile p = Main.projectile[omenIndex];
            if (!p.active || p.type != ModContent.ProjectileType<EclStrikeOmen>() || (int)p.ai[0] != npc.whoAmI) {
                return false;
            }
            omen = p;
            return true;
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }

            //破绽踉跄：所有端读同一镜像戳同样压速度，模拟一致不橡皮筋
            if (Main.GameUpdateCount < openingUntil) {
                if (profile.Family == EclFamily.Dive) {
                    npc.velocity *= OpeningDragAir;
                }
                else {
                    npc.velocity.X *= OpeningDragGround;
                }
            }

            if (VaultUtils.isClient) {
                //决策只在服务端/单人；客户端画面全部来自同步原语
                return;
            }

            if (!initialized) {
                initialized = true;
                //首攻错拍冷却收进 60~180 帧（M7）：此刻 whoAmI 已有效，个体错帧防同屏齐动
                cooldown = FirstCooldownMin + npc.whoAmI * 37 % (FirstCooldownMax - FirstCooldownMin + 1);
            }

            switch (phase) {
                case PhaseIdle:
                    if (--cooldown <= 0) {
                        TryStart(npc);
                    }
                    break;
                case PhaseTelegraph:
                    TickTelegraph(npc);
                    break;
                case PhaseHop:
                    TickHop(npc);
                    break;
                case PhaseGlide:
                    TickGlide(npc);
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
            //自己还在破绽踉跄里：不许起手（惩罚窗要完整兑现）
            if (Main.GameUpdateCount < openingUntil) {
                cooldown = RetryDelay;
                return;
            }
            if (!npc.HasValidTarget) {
                cooldown = RetryDelay;
                return;
            }
            Player player = Main.player[npc.target];
            if (!player.Alives() || !CanSee(npc, player)) {
                cooldown = RetryDelay;
                return;
            }

            bool ready;
            float dist = npc.Distance(player.Center);
            switch (profile.Family) {
                case EclFamily.Rush: {
                    float dx = Math.Abs(player.Center.X - npc.Center.X);
                    float dy = Math.Abs(player.Bottom.Y - npc.Bottom.Y);
                    ready = npc.velocity.Y == 0f && dx >= profile.RangeMin && dx <= profile.RangeMax && dy <= RushMaxRangeY;
                    break;
                }
                case EclFamily.Dive:
                    ready = dist >= profile.RangeMin && dist <= profile.RangeMax;
                    break;
                default:
                    //蝇医悬空可掷；眼佐尔/钉头是步行怪，落地才蓄力
                    ready = dist >= profile.RangeMin && dist <= profile.RangeMax
                        && (profile.Payload == EclPayloadKind.Flask || npc.velocity.Y == 0f);
                    break;
            }
            if (!ready) {
                cooldown = RetryDelay;
                return;
            }

            if (CountActiveOmens() >= StrikeConcurrentCap) {
                cooldown = 45;
                return;
            }

            //初始瞄向（追踪期预兆还会跟，锁定帧才是承诺）
            lockDir = profile.Family == EclFamily.Rush
                ? (player.Center.X >= npc.Center.X ? 0f : MathHelper.Pi)
                : (player.Center - npc.Center).ToRotation();
            strikeConnected = false;
            payloadsAlive = 0;

            //预告即实体：预兆生成失败（弹幕位满）则整次进攻作废
            int mode;
            Vector2 omenPos;
            switch (profile.Family) {
                case EclFamily.Rush:
                    mode = EclStrikeOmen.ModeRushLane;
                    omenPos = npc.Bottom;
                    break;
                case EclFamily.Dive:
                    mode = EclStrikeOmen.ModeDiveLine;
                    omenPos = npc.Center;
                    break;
                default:
                    if (profile.Payload == EclPayloadKind.Flask) {
                        //落点=玩家脚底，自此锁死（预告即承诺，标记生成即锁定）
                        lockPoint = player.Bottom - Vector2.UnitY * 4f;
                        mode = EclStrikeOmen.ModeDropMarker;
                        omenPos = lockPoint;
                    }
                    else {
                        mode = EclStrikeOmen.ModeAimStub;
                        omenPos = npc.Center;
                    }
                    break;
            }
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), omenPos, Vector2.Zero,
                ModContent.ProjectileType<EclStrikeOmen>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, npc.type * 10 + mode, 0f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                omenIndex = -1;
                cooldown = RetryDelay;
                return;
            }

            //刹车脉冲：急停蓄势即起手前摇（仅脉冲帧跟同步）
            if (profile.Family == EclFamily.Rush) {
                npc.velocity.X *= 0.15f;
            }
            else {
                npc.velocity *= 0.3f;
            }
            npc.netUpdate = true;
            timer = profile.Telegraph;
            phase = PhaseTelegraph;
        }

        private void TickTelegraph(NPC npc) {
            timer--;

            //离散刹车脉冲压住游荡漂移，让预兆贴住实际出发点（非每帧，脉冲帧才跟同步）
            if (timer == 24 || timer == 12) {
                if (profile.Family == EclFamily.Rush) {
                    npc.velocity.X *= 0.4f;
                }
                else {
                    npc.velocity *= 0.45f;
                }
                npc.netUpdate = true;
            }

            //锁定帧：方向自此为承诺，写回预兆实体做各端权威纠偏（毒瓶落点生成即锁，无此步）
            if (timer == profile.LockFrames && profile.Payload != EclPayloadKind.Flask) {
                if (npc.HasValidTarget && Main.player[npc.target].Alives()) {
                    Player player = Main.player[npc.target];
                    lockDir = profile.Family == EclFamily.Rush
                        ? (player.Center.X >= npc.Center.X ? 0f : MathHelper.Pi)
                        : (player.Center - npc.Center).ToRotation();
                }
                if (TryGetBoundOmen(npc, out Projectile omen)) {
                    omen.ai[2] = lockDir + 10f;
                    omen.netUpdate = true;
                }
            }

            if (timer <= 0) {
                Commit(npc);
            }
        }

        private void Commit(NPC npc) {
            //GameModeNPC 的提速层按 velocity×SpeedBonus 追加位移，注入速度除回该系数防双重缩放
            //（位移项除、重力项不除；载荷弹幕不吃提速层，不补偿）
            float gain = EclEclipseSets.MoveGain(npc, boundTier);
            switch (profile.Family) {
                case EclFamily.Rush:
                    dashVec = lockDir.ToRotationVector2() * (profile.Power / gain);
                    if (npc.type == NPCID.SwampThing) {
                        //【SwampThing 签名】两段小跳接扑（M6）：不从静止直接起冲，先蹒跚双跳逼近再扑
                        hopIndex = 0;
                        StartSwampHop(npc);
                        break;
                    }
                    //基础冲锋起手帧只给垫底推力，满速交给 TickStrike 的包络爬升；
                    //CreatureFromTheDeep 豁免（保持现状满速出手，衰减段由 PhaseGlide 滑行承担）
                    npc.velocity.X = npc.type == NPCID.CreatureFromTheDeep
                        ? dashVec.X
                        : dashVec.X * RushLaunchPulse;
                    npc.netUpdate = true;
                    timer = profile.Strike;
                    phase = PhaseStrike;
                    break;
                case EclFamily.Dive:
                    dashVec = lockDir.ToRotationVector2() * (profile.Power / gain);
                    npc.velocity = dashVec;
                    npc.netUpdate = true;
                    timer = profile.Strike;
                    phase = PhaseStrike;
                    break;
                default:
                    FirePayload(npc);
                    if (payloadsAlive <= 0) {
                        //载荷全数生成失败：本次作废，安全回待机
                        phase = PhaseIdle;
                        omenIndex = -1;
                        cooldown = RetryDelay;
                        return;
                    }
                    timer = profile.Strike;    //裁决超时窗（载荷死亡回报归零即提前裁决）
                    phase = PhaseStrike;
                    break;
            }
        }

        private void FirePayload(NPC npc) {
            switch (profile.Payload) {
                case EclPayloadKind.Flask: {
                    int damage = Math.Max(1, (int)(npc.damage * FlaskDamageFrac));
                    //向锁定落点做定时长弹道解算（自施重力与瓶体共用常数；不重瞄）
                    Vector2 to = lockPoint - npc.Center;
                    float t = MathHelper.Clamp(MathF.Abs(to.X) / 8f, 30f, EclManFlyFlaskProj.MaxFlightFrames - 6f);
                    Vector2 vel = new Vector2(
                        MathHelper.Clamp(to.X / t, -11f, 11f),
                        MathHelper.Clamp(to.Y / t - EclManFlyFlaskProj.Gravity * t * 0.5f, -14f, 7f));
                    int idx = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                        ModContent.ProjectileType<EclManFlyFlaskProj>(), damage, 1f, Main.myPlayer,
                        lockPoint.X, lockPoint.Y, npc.whoAmI * 1000 + npc.type);
                    if (idx >= 0 && idx < Main.maxProjectiles) {
                        payloadsAlive++;
                    }
                    break;
                }
                case EclPayloadKind.Orb: {
                    int damage = Math.Max(1, (int)(npc.damage * OrbDamageFrac));
                    int idx = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                        lockDir.ToRotationVector2() * OrbSpeedByTier[boundTier - 1],
                        ModContent.ProjectileType<EclEyezorOrbProj>(), damage, 1f, Main.myPlayer,
                        npc.whoAmI * 1000 + npc.type);
                    if (idx >= 0 && idx < Main.maxProjectiles) {
                        payloadsAlive++;
                    }
                    break;
                }
                default: {
                    int damage = Math.Max(1, (int)(npc.damage * NailDamageFrac));
                    int count = NailCountByTier[boundTier - 1];
                    for (int i = 0; i < count; i++) {
                        //束内均布：发射循环直读半张角常量，束外即安全走廊
                        float lerp = count == 1 ? 0f : -1f + 2f * i / (count - 1);
                        float angle = lockDir + NailSpreadHalfAngle * lerp;
                        int idx = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                            angle.ToRotationVector2() * NailSpeed,
                            ModContent.ProjectileType<EclNailheadNailProj>(), damage, 0.5f, Main.myPlayer,
                            npc.whoAmI * 1000 + npc.type);
                        if (idx >= 0 && idx < Main.maxProjectiles) {
                            payloadsAlive++;
                        }
                    }
                    break;
                }
            }
        }

        private void TickStrike(NPC npc) {
            timer--;

            if (profile.Family == EclFamily.Rush) {
                if (npc.type is NPCID.SwampThing or NPCID.CreatureFromTheDeep) {
                    //签名豁免：SwampThing 从双跳带动量入冲（包络起零会顿挫），
                    //CreatureFromTheDeep 的衰减段就是 PhaseGlide 滑行——两型保持满速回写抵住原版衰减
                    npc.velocity.X = dashVec.X;
                }
                else {
                    //M2 包络塑形：缓入→峰值→力竭衰减；每帧回写=包络内抵住原版衰减的持有
                    int t = profile.Strike - timer;
                    npc.velocity.X = dashVec.X * MobDash.Envelope(t, RushRiseFrames,
                        profile.Strike - RushRiseFrames - RushDecayFrames, RushDecayFrames);
                }
                if (timer % 6 == 0) {
                    npc.netUpdate = true;
                }
                SampleBodyHit(npc);
                if (timer <= 0) {
                    if (npc.type == NPCID.Frankenstein) {
                        //【Frankenstein 签名】突进落点滞留 8 帧电火花判定（M6）：
                        //火花区落在预告警示带内（带长≥全程），亮窗=判窗由实体自身把守
                        int sparkDamage = Math.Max(1, (int)(npc.damage * SparkDamageFrac));
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom - Vector2.UnitY * 18f,
                            Vector2.Zero, ModContent.ProjectileType<EclFrankSparkProj>(), sparkDamage, 0.5f, Main.myPlayer);
                    }
                    else if (npc.type == NPCID.CreatureFromTheDeep) {
                        //【CreatureFromTheDeep 签名】收势不急停，转入力竭长滑行段（M6）
                        timer = DeepGlideFrames;
                        phase = PhaseGlide;
                        npc.netUpdate = true;
                        return;
                    }
                    npc.velocity.X *= 0.3f;    //收势急停
                    npc.netUpdate = true;
                    Resolve(npc, allowOpening: true);
                }
                return;
            }

            if (profile.Family == EclFamily.Dive) {
                npc.velocity = dashVec;    //抵住原版飞行 AI 转向，兑现直线承诺
                if (timer % 8 == 0) {
                    npc.netUpdate = true;
                }
                SampleBodyHit(npc);
                if (timer <= 0) {
                    npc.velocity *= 0.4f;
                    npc.netUpdate = true;
                    Resolve(npc, allowOpening: true);
                }
                return;
            }

            //载荷家族：死亡回报归零即裁决；超时（卡墙外飞失联等）不惩罚只回冷却
            if (payloadsAlive <= 0) {
                Resolve(npc, allowOpening: true);
            }
            else if (timer <= 0) {
                Resolve(npc, allowOpening: false);
            }
        }

        /// <summary>
        /// SwampThing 蹒跚小跳起跳注入：前向为承诺位移除提速补偿，竖向为重力域弹道量不除
        /// （落地时机由真实重力决定，镜像 NightPackNPC 跳弧的补偿口径）
        /// </summary>
        private void StartSwampHop(NPC npc) {
            float gain = EclEclipseSets.MoveGain(npc, boundTier);
            npc.velocity = new Vector2(lockDir.ToRotationVector2().X * (SwampHopSpeed / gain), SwampHopLaunchVy);
            npc.netUpdate = true;
            timer = SwampHopTimeout;
            phase = PhaseHop;
            //起跳泥尘：沼泽步态的落脚反馈（决策端演出，专用服务器无尘；客户端凭同步跳弧读招）
            if (!Main.dedServ) {
                for (int i = 0; i < 6; i++) {
                    Dust mud = Dust.NewDustPerfect(npc.Bottom + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f),
                        DustID.Mud, new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(-2.2f, -0.6f)),
                        90, default, Main.rand.NextFloat(0.9f, 1.3f));
                    mud.noGravity = Main.rand.NextBool();
                }
            }
        }

        /// <summary>
        /// 【SwampThing 签名】双跳段推进（M6）：跳-跳-扑的沼泽蹒跚步态；
        /// 落地判定推进、超时兜底，双跳完毕沿锁定承诺正式扑出（不重瞄）
        /// </summary>
        private void TickHop(NPC npc) {
            timer--;
            SampleBodyHit(npc);
            //离地至少 5 帧后才认落地，防起跳帧误判
            bool landed = timer <= SwampHopTimeout - 5 && npc.velocity.Y == 0f;
            if (!landed && timer > 0) {
                return;
            }
            hopIndex++;
            if (hopIndex < SwampHopCount) {
                StartSwampHop(npc);
                return;
            }
            npc.velocity.X = dashVec.X;
            npc.netUpdate = true;
            timer = profile.Strike;
            phase = PhaseStrike;
        }

        /// <summary>
        /// 【CreatureFromTheDeep 签名】力竭长滑行段（M6）：指数衰减的余势滑步带水花，
        /// 滑行期怪体仍是威胁（继续采样命中）；衰减到位后清残速把控制权还给原版 AI
        /// </summary>
        private void TickGlide(NPC npc) {
            timer--;
            dashVec.X *= DeepGlideDecay;
            npc.velocity.X = dashVec.X;    //每帧回写=包络衰减段，抵住原版步行 AI 的改写
            if (timer % 8 == 0) {
                npc.netUpdate = true;
            }
            SampleBodyHit(npc);
            //滑行水花（决策端演出，专用服务器无尘；客户端凭同步滑行速度读招）
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust splash = Dust.NewDustPerfect(npc.Bottom + new Vector2(Main.rand.NextFloat(-8f, 8f), -2f),
                    DustID.Water, new Vector2(-npc.velocity.X * 0.2f, Main.rand.NextFloat(-1.8f, -0.4f)),
                    80, default, Main.rand.NextFloat(0.9f, 1.4f));
                splash.noGravity = false;
            }
            if (timer <= 0 || Math.Abs(dashVec.X) < 0.8f) {
                npc.velocity.X *= 0.25f;    //清残速，控制权干净还给原版
                npc.netUpdate = true;
                Resolve(npc, allowOpening: true);
            }
        }

        /// <summary>服务端几何采样：重击执行窗内怪体是否碰到玩家（碰到=命中，不给破绽）</summary>
        private void SampleBodyHit(NPC npc) {
            if (strikeConnected) {
                return;
            }
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player.active && !player.dead && npc.Hitbox.Intersects(player.Hitbox)) {
                    strikeConnected = true;
                    return;
                }
            }
        }

        /// <summary>重击了结：挥空则开破绽（躲招的奖励=反打窗口），命中只回冷却</summary>
        private void Resolve(NPC npc, bool allowOpening) {
            if (allowOpening && !strikeConnected) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<EclOpeningProj>(), 0, 0f, Main.myPlayer,
                    npc.whoAmI, npc.type, EclEclipseSets.OpeningFramesByTier[boundTier - 1]);
            }
            phase = PhaseIdle;
            omenIndex = -1;
            int tierCooldown = (int)(profile.Cooldown * (boundTier >= 3 ? 0.7f : boundTier >= 2 ? 0.85f : 1f));
            cooldown = tierCooldown + Main.rand.Next(CooldownJitter + 1);
        }

        #region 载荷回报（载荷实体在服务端调用；锚索引+登记类型双校验防槽位复用）
        private static bool TryResolveAnchor(int npcIndex, int recordedType, out EclipseNPC eclipse) {
            eclipse = null;
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[npcIndex];
            return npc.active && npc.type == recordedType && npc.TryGetGlobalNPC(out eclipse);
        }

        /// <summary>载荷几何碰到玩家：本次重击记为命中</summary>
        internal static void NotifyPayloadHit(int npcIndex, int recordedType) {
            if (TryResolveAnchor(npcIndex, recordedType, out EclipseNPC eclipse)) {
                eclipse.strikeConnected = true;
            }
        }

        /// <summary>载荷了结：在飞计数递减，归零触发挥空裁决</summary>
        internal static void NotifyPayloadGone(int npcIndex, int recordedType) {
            if (TryResolveAnchor(npcIndex, recordedType, out EclipseNPC eclipse) && eclipse.payloadsAlive > 0) {
                eclipse.payloadsAlive--;
            }
        }
        #endregion

        #region 命中门与标记（读镜像，各端一致）
        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (boundTier > 0 && Main.GameUpdateCount < openingUntil) {
                //破绽承伤加深：躲招奖励的反打窗（受击结算端本地读镜像，数字各端一致）
                modifiers.FinalDamage *= EclEclipseSets.OpeningDamageAmp;
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (boundTier > 0 && Main.GameUpdateCount < openingUntil) {
                modifiers.FinalDamage *= EclEclipseSets.OpeningDamageAmp;
            }
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) {
            if (boundTier <= 0 || npc.SpawnedFromStatue) {
                //雕像怪资格懒检查：出生帧空窗不给挂印
                return;
            }
            //血狩印：只在吸血鬼（两形态皆可）的重击执行窗内挂，窗口由已同步预兆实体盖戳判定；
            //受击方本机生成标记实体（owner=受害者），原生同步
            if (!EclEclipseSets.IsVampireForm(npc.type) || Main.GameUpdateCount >= strikeWindowUntil) {
                return;
            }
            if (target.whoAmI != Main.myPlayer || EclBloodMarkProj.ExistsFor(target.whoAmI, npc.whoAmI)) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), target.Top, Vector2.Zero,
                ModContent.ProjectileType<EclBloodMarkProj>(), 0, 0f, target.whoAmI,
                npc.whoAmI, boundTier, 0f);
        }
        #endregion
    }
}
