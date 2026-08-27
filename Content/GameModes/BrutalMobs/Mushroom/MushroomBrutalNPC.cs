using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.Mushroom.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Mushroom
{
    /// <summary>
    /// 残酷模式发光蘑菇组行为机制层，主题：孢子生态（云、爆、链一切攻击都长孢子的形状）。
    /// 叠加在原版 AI 之上，不接管：寄居蟹滚壳冲刺（缩壳蓄力→包络滚进→眩壳惩罚窗）、
    /// 蘑菇瓢虫孢尘喷吐（锥幕预告+具名中央槽缺口）、真菌球藤蔓抽打（弧线预告→鞭击，巨型落孢斑）、
    /// 真菌鱼破水孢跃（聚力→弧线扑咬→落水留漂浮孢囊）、困难孢子系活着孢雾喷+死亡孢爆（凝核预告+具名槽位缺口）。
    /// 覆盖：AnomuraFungus / MushiLadybug / FungiBulb / GiantFungiBulb / FungoFish /
    /// ZombieMushroom+ZombieMushroomHat / SporeBat / SporeSkeleton
    /// （原版 NPCID 无 SporeZombie，孢子僵尸的字段名是 ZombieMushroom 双变体，游戏内名 Spore Zombie）。
    /// 刻意排除：FungiSpore（原版即近身自爆式孢子体，行为已自洽；离线未核细节，保持原版）。
    /// 决策与生成只在权威端跑，客户端可见状态一律来自同步弹幕实体；数值增强归 GameModeNPC，此处只加行为
    /// </summary>
    internal class MushroomBrutalNPC : GlobalNPC
    {
        //==== 通用节奏（M7 密度预算） ====
        /// <summary>出生首攻错拍窗下限/上限（遭遇 ≤3 秒可见首个机制）</summary>
        private const int FirstCooldownMin = 60;
        private const int FirstCooldownMax = 180;
        /// <summary>条件未满足的重试间隔</summary>
        private const int RetryFrames = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>冷却随机抖动上限</summary>
        private const int CooldownJitter = 40;

        //==== 寄居蟹·滚壳冲刺 ====
        private const float RollMinRangeX = 90f;
        private const float RollMaxRangeX = 430f;
        private const float RollMaxRangeY = 150f;
        /// <summary>蓄力期每帧压速阻尼（缩壳定身的可见信号之一）</summary>
        private const float RollWindupDamp = 0.78f;
        /// <summary>滚进包络三段：爬升/保持（档位只拉保持段）/衰减帧</summary>
        private const int RollRise = 6;
        private static readonly int[] RollHoldByTier = [12, 16, 20];
        private const int RollDecay = 12;
        /// <summary>滚进名义峰速（未含提速补偿，注入时除回 MoveGain）</summary>
        private static readonly float[] RollPeakByTier = [8f, 9f, 10f];
        private static readonly int[] RollCooldownByTier = [420, 370, 320];
        /// <summary>滚壳自旋速度阈：原版蟹步走不到，只有滚进段过阈（击退偶发过阈=短暂打滚，可接受）</summary>
        private const float RollSpinMinVx = 4.4f;
        private const float RollSpinRate = 0.05f;

        //==== 蘑菇瓢虫·孢尘喷吐 / 困难孢子系·短距孢雾（参数档见 MushroomSporeMistTelegraph.Profiles） ====
        /// <summary>孢雾窗口期每帧压速阻尼（立定鼓腹）</summary>
        private const float MistWindupDamp = 0.72f;
        /// <summary>喷吐收势帧</summary>
        private const int MistRecoverFrames = 14;
        /// <summary>孢弹伤害 = 已缩放 npc.damage × 此值</summary>
        private const float MistDamageFrac = 0.5f;
        private static readonly int[] MistCooldownByTier = [380, 340, 300];
        /// <summary>困难孢子系活着孢雾喷频率（任务锚点 ~480 帧）</summary>
        private static readonly int[] PuffCooldownByTier = [520, 480, 440];
        /// <summary>孢雾预告全局并发上限</summary>
        private const int MistCap = 6;

        //==== 真菌球·藤蔓抽打 ====
        /// <summary>藤鞭触发距离（任务锚点 240px）</summary>
        private const float WhipTriggerRange = 240f;
        /// <summary>鞭击伤害 = 已缩放 npc.damage × 此值</summary>
        private const float WhipDamageFrac = 0.7f;
        private static readonly int[] VineCooldownByTier = [430, 390, 350];
        /// <summary>藤鞭预告全局并发上限</summary>
        private const int WhipOmenCap = 6;

        //==== 真菌鱼·破水孢跃 ====
        private const float FishMinRange = 130f;
        private const float FishMaxRange = 520f;
        /// <summary>目标相对高差许可（向上最多 360px、向下最多 90px 才起跳）</summary>
        private const float FishMaxRise = 360f;
        private const float FishMaxDrop = 90f;
        private const float FishWindupDamp = 0.82f;
        /// <summary>聚力段帧数（前摇总长 24 帧的末段，锁点自此不再重瞄）</summary>
        private const int FishGatherFrames = 12;
        /// <summary>跃弧解算帧与出水重力估值（原版鱼类出水重力离线未核，落点非承诺点，扑咬以接触为准）</summary>
        private const float FishLeapSolveFrames = 30f;
        private const float FishLeapGravity = 0.25f;
        private const float FishMaxVx = 8.5f;
        private const float FishMaxUpVy = 13f;
        /// <summary>跃出横向包络三段（纵向交给重力弧线）</summary>
        private const int FishLeapRise = 3;
        private const int FishLeapHold = 22;
        private const int FishLeapDecay = 10;
        /// <summary>跃出强制收尾帧（未落水也收）</summary>
        private const int FishLeapTimeout = 46;
        private const int FishRecoverFrames = 10;
        private static readonly int[] FishCooldownByTier = [400, 350, 300];
        /// <summary>落水尾迹孢囊数</summary>
        private const int SacCount = 2;
        /// <summary>孢囊迷你弹伤害 = 已缩放 npc.damage × 此值</summary>
        private const float SacDamageFrac = 0.45f;
        /// <summary>孢囊全局并发上限</summary>
        private const int SacCap = 6;

        //==== 困难孢子系·死亡孢爆 ====
        /// <summary>孢爆弹伤害 = 已缩放 npc.damage × 此值</summary>
        private const float BurstDamageFrac = 0.5f;
        /// <summary>死亡孢核全局并发上限</summary>
        private const int BurstCoreCap = 6;

        /// <summary>姿态可视实体全局并发上限</summary>
        private const int GlowCap = 6;

        //==== 角色分派 ====
        private const byte RoleRoll = 0;
        private const byte RoleMist = 1;
        private const byte RoleVine = 2;
        private const byte RoleFish = 3;
        private const byte RoleSporeKin = 4;

        private const byte PhaseIdle = 0;
        private const byte PhaseWindup = 1;
        private const byte PhaseStrike = 2;
        private const byte PhaseRecover = 3;

        public override bool InstancePerEntity => true;

        /// <summary>本个体生成时绑定的档位，0=未绑定（中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private byte role;
        private byte phase;
        /// <summary>相位计时器；服务端决策私产，客户端不得用它驱动画面</summary>
        private int timer;
        private int cooldown;
        /// <summary>本次动作收尾要落的冷却（不同触发入口冷却表不同）</summary>
        private int pendingCooldown;
        /// <summary>滚进锁定方向（蓄力起点锁死，预告即承诺）</summary>
        private float lockDirX;
        /// <summary>孢跃锁定落点（聚力段起点锁死）</summary>
        private Vector2 lockPoint;
        /// <summary>跃出横向解算速度（包络持有用）</summary>
        private float leapVx;
        /// <summary>本次动作绑定的预告/可视实体槽位（服务端私产）</summary>
        private int boundProjIndex = -1;
        /// <summary>跃出期间是否已出过水（落水沿 !wet→wet 判定）</summary>
        private bool leftWater;
        /// <summary>滚壳自旋视觉是否在场（各端本地视觉状态）</summary>
        private bool rollVisual;

        private static bool TryResolveRole(int type, out byte role) {
            switch (type) {
                case NPCID.AnomuraFungus:
                    role = RoleRoll;
                    return true;
                case NPCID.MushiLadybug:
                    role = RoleMist;
                    return true;
                case NPCID.FungiBulb:
                case NPCID.GiantFungiBulb:
                    role = RoleVine;
                    return true;
                case NPCID.FungoFish:
                    role = RoleFish;
                    return true;
                case NPCID.ZombieMushroom:
                case NPCID.ZombieMushroomHat:
                case NPCID.SporeBat:
                case NPCID.SporeSkeleton:
                    role = RoleSporeKin;
                    return true;
                default:
                    role = 0;
                    return false;
            }
        }

        /// <summary>困难孢子系三型风味（弹道差异的唯一入口）</summary>
        private static int SporeFlavor(int type) => type switch {
            NPCID.SporeBat => MushroomSporeBoltProj.FlavorBat,
            NPCID.SporeSkeleton => MushroomSporeBoltProj.FlavorSkeleton,
            _ => MushroomSporeBoltProj.FlavorZombie,
        };

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && TryResolveRole(entity.type, out _);

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            if (!TryResolveRole(npc.type, out role)) {
                return;
            }
            boundTier = tier;
            boundProjIndex = -1;
            //出生错拍：同屏群体不同帧齐动（M7：60~180 帧窗）。
            //SetDefaults 期 whoAmI 尚未赋值不可作错拍种；Main.rand 此处为权威端决策私产
            cooldown = FirstCooldownMin + Main.rand.Next(FirstCooldownMax - FirstCooldownMin + 1);
        }

        /// <summary>机制资格（每个机制入口都要过；雕像怪在此排除）</summary>
        private static bool MechEligible(NPC npc) {
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage) {
                return false;
            }
            if (npc.lifeMax <= 5 || npc.damage <= 0) {
                return false;
            }
            if (npc.boss || npc.realLife >= 0 || npc.SpawnedFromStatue) {
                return false;
            }
            return true;
        }

        /// <summary>统计某类弹幕的活动实例数（到 stopAt 提前退出；只在触发时调用，非每帧）</summary>
        internal static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>
        /// 提速位移补偿：GameModeNPC.PostAI 对非 Boss 怪按 velocity×SpeedBonus 追加位置推进，
        /// 本层注入的承诺性速度一律除回该系数（位移项除回、重力项不除），
        /// 口径镜像 PumpkinMoonNPC.MoveGain：boss 旗标个体与体节不吃提速层，系数为 1
        /// </summary>
        private float MoveGain(NPC npc) => !npc.boss && npc.realLife < 0 ? 1f + GameModeTuning.SpeedBonus(boundTier) : 1f;

        /// <summary>来源打包（槽位+1|类型&lt;&lt;8），预告实体与 NPC 侧回读共用（镜像沙锥）</summary>
        private static int SrcPack(NPC npc) => (npc.whoAmI + 1) | (npc.type << 8);

        /// <summary>绑定实体回读校验（索引+类型+来源三重比对），实体缺位→回冷却（失败方向=安全方向）</summary>
        private bool BoundAlive(NPC npc, int projType) {
            if (boundProjIndex < 0 || boundProjIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile proj = Main.projectile[boundProjIndex];
            return proj.active && proj.type == projType && (int)proj.ai[2] == SrcPack(npc);
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            RollVisualTick(npc);//全端确定性视觉，须在客户端早退之前
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
            switch (role) {
                case RoleRoll:
                    TickRoll(npc);
                    break;
                case RoleMist:
                case RoleSporeKin:
                    TickMist(npc);
                    break;
                case RoleVine:
                    TickVine(npc);
                    break;
                case RoleFish:
                    TickFish(npc);
                    break;
            }
        }

        /// <summary>滚壳自旋：各端从同步速度确定性推得，无需另发包（客户端早退前调用）</summary>
        private void RollVisualTick(NPC npc) {
            if (role != RoleRoll) {
                return;
            }
            float vx = npc.velocity.X;
            if (Math.Abs(vx) >= RollSpinMinVx) {
                npc.rotation += vx * RollSpinRate;
                rollVisual = true;
            }
            else if (rollVisual) {
                //降速后把贴图角度还给原版（渐进回正，不跳变）
                npc.rotation = MathHelper.WrapAngle(npc.rotation) * 0.7f;
                if (Math.Abs(npc.rotation) < 0.06f) {
                    npc.rotation = 0f;
                    rollVisual = false;
                }
            }
        }

        private void TryStart(NPC npc) {
            cooldown = RetryFrames;
            if (!MechEligible(npc)) {
                cooldown = IneligibleDelay;
                return;
            }
            if (!npc.HasValidTarget) {
                return;
            }
            Player target = Main.player[npc.target];
            if (!target.Alives()) {
                return;
            }
            switch (role) {
                case RoleRoll:
                    TryStartRoll(npc, target);
                    break;
                case RoleMist:
                    TryStartMist(npc, target, MushroomSporeMistTelegraph.ProfileLadybug,
                        MushroomSporeBoltProj.FlavorLadybug, MistCooldownByTier);
                    break;
                case RoleSporeKin:
                    TryStartMist(npc, target, MushroomSporeMistTelegraph.ProfilePuff,
                        SporeFlavor(npc.type), PuffCooldownByTier);
                    break;
                case RoleVine:
                    TryStartVine(npc, target);
                    break;
                case RoleFish:
                    TryStartFish(npc, target);
                    break;
            }
        }

        /// <summary>动作收尾：还相位、清绑定、落冷却</summary>
        private void EndMove(int cd) {
            phase = PhaseIdle;
            boundProjIndex = -1;
            cooldown = cd;
        }

        /// <summary>生成姿态可视实体；失败=整次动作作废（预告即实体）</summary>
        private bool SpawnGlow(NPC npc, int mode) {
            int index = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<MushroomChargeGlowProj>(), 0, 0f, Main.myPlayer,
                mode, 0f, SrcPack(npc));
            if (index < 0 || index >= Main.maxProjectiles) {
                return false;
            }
            boundProjIndex = index;
            return true;
        }

        //====== 寄居蟹·滚壳冲刺 ======

        private void TryStartRoll(NPC npc, Player target) {
            if (npc.velocity.Y != 0f) {
                return;//落地才能缩壳起滚
            }
            float dx = Math.Abs(target.Center.X - npc.Center.X);
            float dy = Math.Abs(target.Center.Y - npc.Center.Y);
            if (dx < RollMinRangeX || dx > RollMaxRangeX || dy > RollMaxRangeY) {
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, target.Center, 1, 1)) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<MushroomChargeGlowProj>()) >= GlowCap) {
                return;
            }
            //出手锁向：蓄力起点即锁死滚进方向，此后不再重瞄（预告即承诺）
            lockDirX = target.Center.X > npc.Center.X ? 1f : -1f;
            if (!SpawnGlow(npc, MushroomChargeGlowProj.ModeShellCharge)) {
                return;
            }
            npc.velocity.X *= 0.4f;
            npc.netUpdate = true;
            phase = PhaseWindup;
            timer = MushroomChargeGlowProj.ShellChargeFrames;
        }

        private void TickRoll(NPC npc) {
            if (phase == PhaseWindup) {
                //缩壳蓄力：压速定身，壳光渐亮由可视实体在各端渲染；实体缺位→回冷却
                if (!BoundAlive(npc, ModContent.ProjectileType<MushroomChargeGlowProj>())) {
                    EndMove(RetryFrames);
                    return;
                }
                npc.velocity.X *= RollWindupDamp;
                if (timer % 6 == 0) {
                    npc.netUpdate = true;
                }
                if (--timer <= 0) {
                    phase = PhaseStrike;
                    timer = RollRise + RollHoldByTier[boundTier - 1] + RollDecay;
                    npc.netUpdate = true;
                }
                return;
            }
            if (phase == PhaseStrike) {
                int total = RollRise + RollHoldByTier[boundTier - 1] + RollDecay;
                int t = total - timer;
                //包络塑形滚进：位移承诺除回提速补偿，纵向交给原版重力；自旋由 RollVisualTick 全端推得
                float env = MobDash.Envelope(t, RollRise, RollHoldByTier[boundTier - 1], RollDecay);
                npc.velocity.X = lockDirX * (RollPeakByTier[boundTier - 1] / MoveGain(npc)) * env;
                if (t % 6 == 0) {
                    npc.netUpdate = true;
                }
                //撞墙即入眩壳；力竭（包络走完）同样入眩壳——两条路都给惩罚窗
                if ((npc.collideX && t > RollRise) || --timer <= 0) {
                    EnterShellStun(npc);
                }
                return;
            }
            //PhaseRecover：眩壳惩罚窗，压残速
            npc.velocity.X *= 0.72f;
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (--timer <= 0) {
                npc.velocity.X = 0f;//收尾清残速，控制权干净还给原版 AI
                npc.netUpdate = true;
                EndMove(RollCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1));
            }
        }

        private void EnterShellStun(NPC npc) {
            phase = PhaseRecover;
            timer = MushroomChargeGlowProj.ShellStunFrames;
            //眩壳标记纯表现，生成失败不影响惩罚窗本身
            SpawnGlow(npc, MushroomChargeGlowProj.ModeShellStun);
            npc.velocity.X *= 0.3f;
            npc.netUpdate = true;
        }

        //====== 孢雾喷吐（瓢虫与困难孢子系共用装备，风味与冷却分表） ======

        private void TryStartMist(NPC npc, Player target, int profileId, int flavor, int[] cdTable) {
            MushroomSporeMistTelegraph.MistProfile profile = MushroomSporeMistTelegraph.GetProfile(profileId);
            float dist = npc.Distance(target.Center);
            if (dist < profile.MinRange || dist > profile.MaxRange) {
                return;
            }
            //地面型立定需落地；孢子蝙蝠悬停不受限
            if (npc.type != NPCID.SporeBat && npc.velocity.Y != 0f) {
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, target.Center, 1, 1)) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<MushroomSporeMistTelegraph>()) >= MistCap) {
                return;
            }

            //预告即承诺：原点与方向此帧锁死，此后不再重瞄
            float aim = (target.Center - npc.Center).ToRotation();
            int damage = (int)(npc.damage * MistDamageFrac);
            int index = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<MushroomSporeMistTelegraph>(), damage, 1f, Main.myPlayer,
                aim, MushroomSporeMistTelegraph.Pack(profileId, flavor), SrcPack(npc));
            if (index < 0 || index >= Main.maxProjectiles) {
                return;
            }
            boundProjIndex = index;
            npc.velocity.X *= 0.4f;
            npc.netUpdate = true;
            phase = PhaseWindup;
            timer = profile.TelegraphFrames;
            pendingCooldown = cdTable[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
        }

        private void TickMist(NPC npc) {
            if (phase == PhaseWindup) {
                if (!BoundAlive(npc, ModContent.ProjectileType<MushroomSporeMistTelegraph>())) {
                    EndMove(RetryFrames);
                    return;
                }
                //立定鼓腹：压速定身（可见信号：腹光+孢尘聚拢由预告实体在各端渲染）
                npc.velocity.X *= MistWindupDamp;
                if (npc.type == NPCID.SporeBat) {
                    npc.velocity.Y *= MistWindupDamp;
                }
                if (timer % 6 == 0) {
                    npc.netUpdate = true;
                }
                if (--timer <= 0) {
                    phase = PhaseRecover;
                    timer = MistRecoverFrames;
                }
                return;
            }
            //收势：不注入速度，让原版 AI 自然接管
            if (--timer <= 0) {
                EndMove(pendingCooldown);
            }
        }

        //====== 真菌球·藤蔓抽打（原版拉拽保留，本层只追加鞭击） ======

        private void TryStartVine(NPC npc, Player target) {
            if (npc.Distance(target.Center) > WhipTriggerRange) {
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, target.Center, 1, 1)) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<MushroomVineWhipOmen>()) >= WhipOmenCap) {
                return;
            }
            bool giant = npc.type == NPCID.GiantFungiBulb;
            //出手锁向：弧线标记此帧锁死（世界锁位由预告实体承载）
            float aim = (target.Center - npc.Center).ToRotation();
            int damage = (int)(npc.damage * WhipDamageFrac);
            int index = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<MushroomVineWhipOmen>(), damage, 2f, Main.myPlayer,
                aim, MushroomVineWhipOmen.Pack(giant, Main.rand.NextBool()), SrcPack(npc));
            if (index < 0 || index >= Main.maxProjectiles) {
                return;
            }
            boundProjIndex = index;
            phase = PhaseWindup;
            timer = MushroomVineWhipOmen.TelegraphFrames;
            pendingCooldown = VineCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
        }

        private void TickVine(NPC npc) {
            if (phase == PhaseWindup) {
                if (!BoundAlive(npc, ModContent.ProjectileType<MushroomVineWhipOmen>())) {
                    EndMove(RetryFrames);
                    return;
                }
                //藤体不注入速度：原版藤蔓摆动照跑，弧标由世界锁位的预告实体保证
                if (--timer <= 0) {
                    phase = PhaseStrike;
                    timer = MushroomVineWhipProj.SweepFrames + 6;
                }
                return;
            }
            //鞭击由实体自治，本体只等收势
            if (--timer <= 0) {
                EndMove(pendingCooldown);
            }
        }

        //====== 真菌鱼·破水孢跃 ======

        private void TryStartFish(NPC npc, Player target) {
            if (!npc.wet) {
                return;//水中绕行才有跃出
            }
            float dist = npc.Distance(target.Center);
            if (dist < FishMinRange || dist > FishMaxRange) {
                return;
            }
            float dy = target.Center.Y - npc.Center.Y;
            if (dy < -FishMaxRise || dy > FishMaxDrop) {
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, target.Center, 1, 1)) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<MushroomChargeGlowProj>()) >= GlowCap) {
                return;
            }
            if (!SpawnGlow(npc, MushroomChargeGlowProj.ModeFishGather)) {
                return;
            }
            npc.velocity *= 0.5f;
            npc.netUpdate = true;
            phase = PhaseWindup;
            timer = MushroomChargeGlowProj.FishGatherTotalFrames;
            lockPoint = target.Center;
        }

        private void TickFish(NPC npc) {
            if (phase == PhaseWindup) {
                if (!BoundAlive(npc, ModContent.ProjectileType<MushroomChargeGlowProj>())) {
                    EndMove(RetryFrames);
                    return;
                }
                npc.velocity *= FishWindupDamp;
                if (timer % 6 == 0) {
                    npc.netUpdate = true;
                }
                if (timer == FishGatherFrames && npc.HasValidTarget) {
                    //聚力段起点锁定扑咬落点（预告即承诺，此后不再重瞄）
                    lockPoint = Main.player[npc.target].Center;
                }
                if (--timer <= 0) {
                    CommitFishLeap(npc);
                }
                return;
            }
            if (phase == PhaseStrike) {
                int t = FishLeapTimeout - timer;
                //横向包络持有（抵住水阻与原版转向），纵向交给重力弧线
                float env = MobDash.Envelope(t, FishLeapRise, FishLeapHold, FishLeapDecay);
                npc.velocity.X = leapVx * env;
                if (t % 6 == 0) {
                    npc.netUpdate = true;
                }
                if (!npc.wet) {
                    leftWater = true;
                }
                if (leftWater && npc.wet) {
                    //落水沿：尾迹留漂浮孢囊，随后收势
                    SplashSacs(npc);
                    phase = PhaseRecover;
                    timer = FishRecoverFrames;
                    return;
                }
                if (--timer <= 0) {
                    phase = PhaseRecover;
                    timer = FishRecoverFrames;
                }
                return;
            }
            //收势：压残横速，纵向还给原版
            npc.velocity.X *= 0.85f;
            if (--timer <= 0) {
                EndMove(FishCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1));
            }
        }

        private void CommitFishLeap(NPC npc) {
            float gain = MoveGain(npc);
            Vector2 d = lockPoint - npc.Center;
            //弹道解算：位移项除回提速补偿；纵向初速为重力域弹道量不除（镜像 NightPackNPC 跳弧口径）。
            //出水重力 FishLeapGravity 为估值（离线未核），落点非承诺点，弧线扑咬以接触为准
            leapVx = MathHelper.Clamp(d.X / (FishLeapSolveFrames * gain), -FishMaxVx, FishMaxVx);
            float vy = MathHelper.Clamp(d.Y / FishLeapSolveFrames - FishLeapGravity * FishLeapSolveFrames * 0.5f,
                -FishMaxUpVy, -3f);
            npc.velocity = new Vector2(leapVx * 0.5f, vy);
            npc.netUpdate = true;
            leftWater = false;
            phase = PhaseStrike;
            timer = FishLeapTimeout;
        }

        private void SplashSacs(NPC npc) {
            if (CountActive(ModContent.ProjectileType<MushroomSporeSacProj>()) >= SacCap) {
                return;
            }
            int damage = (int)(npc.damage * SacDamageFrac);
            float back = leapVx != 0f ? -Math.Sign(leapVx) : -npc.direction;
            for (int i = 0; i < SacCount; i++) {
                Vector2 pos = npc.Center + new Vector2(back * (26f + 30f * i), -6f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<MushroomSporeSacProj>(), damage, 0f, Main.myPlayer);
            }
        }

        //====== 困难孢子系·死亡孢爆 ======

        /// <summary>死亡孢爆：先出无害孢核（34 帧凝聚预告），由核在提交帧沿具名槽位缺口放射</summary>
        public override void OnKill(NPC npc) {
            //OnKill 本就权威端执行，isClient 双保险
            if (boundTier <= 0 || VaultUtils.isClient) {
                return;
            }
            if (role != RoleSporeKin || !MechEligible(npc)) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<MushroomSporeBurstCore>()) >= BurstCoreCap) {
                return;
            }
            int damage = (int)(npc.damage * BurstDamageFrac);
            //基准角权威端随机后随生成包同步，各端缺口方向一致
            Projectile.NewProjectile(npc.GetSource_Death(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<MushroomSporeBurstCore>(), damage, 1f, Main.myPlayer,
                SporeFlavor(npc.type), Main.rand.NextFloat(MathHelper.TwoPi));
        }
    }
}
