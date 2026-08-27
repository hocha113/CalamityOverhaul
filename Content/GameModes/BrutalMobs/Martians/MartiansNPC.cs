using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.Martians.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Martians
{
    /// <summary>火星暴乱分工角色</summary>
    internal enum MrtRole : byte
    {
        None,
        /// <summary>特斯拉源：单位间结电弧链（特斯拉炮塔/电击矛兵/工程师）</summary>
        TeslaSource,
        /// <summary>标线射手：激光标线锁定后开火（无人机/行走者/射线枪手/扰乱者/军官）</summary>
        LineStriker,
        /// <summary>Scutlix 骑手：蓄力主炮（骑手被击落后坐骑惊走=原版行为，只读不碰）</summary>
        RiderCannon,
        /// <summary>飞碟核心：签名技唯一决策席（相位弱点轮转 + 死亡射线扫描线预演）</summary>
        SaucerCore,
        /// <summary>飞碟炮塔/加农部件：无自主逻辑，仅被动承接过热易伤（决策全在核心）</summary>
        SaucerPart,
        /// <summary>灰皮步兵：掷扳手 + 一生一次呼叫增援</summary>
        Grunt,
    }

    /// <summary>
    /// 火星暴乱行为层「链式电网」。只叠加行为不动数值（数值层归 <see cref="GameModeNPC"/>），
    /// 原版 AI 全程继续跑。五名标线射手共用标线机为共同语言，每型各有一条族内签名：
    /// 军官=护盾链接（支援）、无人机=齐射后侧移（游走）、行走者=压制扫描（扫掠线）、
    /// 射线枪手=三连点射、扰乱者=扰乱脉冲（黑暗减益+紫化标线）；灰皮步兵=掷扳手+一次性呼叫增援。
    /// 本层唯一的 NPC 速度注入是无人机侧移：MobDash 包络塑形并除回 <see cref="MoveGain"/>。
    /// 决策与弹幕/增援生成全在权威端（客户端 PostAI 早退），客户端可见状态一律来自弹幕实体与原版同步原语。
    /// 排除条目：MartianProbe（侦测流程不碰）、ForceBubble（军官原版护盾只读不改）、
    /// MartianSaucer 本体（部件排除表，机制只挂核心）、Scutlix 坐骑（主炮机制挂骑手，同一躯体不双挂）。
    /// 飞碟部件间的 ai 链接语义离线查证不实（tModLoader.xml 只记 aiStyle 归属），
    /// 按第二波简报 §2.2 降级为核心半径归属；核心/部件按显式类型名单放行，不依赖 boss 旗标
    /// </summary>
    internal class MartiansNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==== 通用节奏 ====
        /// <summary>条件未满足的重试间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>出生后首攻等待窗，随机错开避免同屏齐动（M7：收进 60~180 帧）</summary>
        private const int FirstCooldownMin = 60;
        private const int FirstCooldownMax = 180;
        private const int CooldownJitter = 60;

        //==== 电弧链 ====
        /// <summary>结链距离：仅两源相距不超此值才结（断链距离在弧实体侧更大，构成迟滞）</summary>
        private const float ArcLinkDistance = 420f;
        /// <summary>全局并发电弧上限（尝试帧扫描现存弧计数）</summary>
        private const int MaxConcurrentArcs = 3;
        /// <summary>电弧伤害 = 两端 npc.damage（已缩放值）较小者的此比例</summary>
        private const float ArcDamageFrac = 0.6f;
        /// <summary>结链后两端的再结链冷却（档位 1/2/3；档位只缩冷却，预热帧不变）</summary>
        private static readonly int[] ArcCooldownByTier = [660, 540, 420];
        /// <summary>相关性阀门：弧中点此半径内有活人才结链</summary>
        private const float ArcRelevanceRange = 900f;

        //==== 标线打击 ====
        /// <summary>全局并发标线上限（尝试帧扫描现存标线计数）</summary>
        private const int MaxConcurrentStrikes = 6;

        //==== 标线族内签名 ====
        /// <summary>射线枪手三连点射的单发伤害补偿（发数 ×3，单发 ×0.5）</summary>
        private const float RayGunnerBurstDamageMult = 0.5f;
        /// <summary>军官护盾链接的友军搜索半径</summary>
        private const float OfficerShieldRange = 480f;
        /// <summary>无人机侧移包络：起/峰/衰帧（总 19 帧，收在标线余痕窗内走完）</summary>
        private const int DroneDashRise = 5;
        private const int DroneDashHold = 4;
        private const int DroneDashDecay = 10;
        /// <summary>无人机侧移名义峰速（注入前除回 MoveGain）</summary>
        private const float DroneDashPeak = 6.5f;

        //==== 灰皮步兵 ====
        /// <summary>掷扳手射程窗</summary>
        private const float WrenchMinRange = 90f;
        private const float WrenchMaxRange = 520f;
        /// <summary>扳手伤害 = npc.damage（已缩放值）× 此比例</summary>
        private const float WrenchDamageFrac = 0.65f;
        /// <summary>掷扳手冷却（档位 1/2/3；档位只缩冷却，前摇帧不变）</summary>
        private static readonly int[] WrenchCooldownByTier = [460, 400, 340];
        /// <summary>掷出后收势帧</summary>
        private const int WrenchRecoverFrames = 14;
        /// <summary>全局并发扳手上限（尝试帧扫描现存扳手计数）</summary>
        private const int MaxConcurrentWrenches = 5;
        /// <summary>呼叫增援的触发血量比（首次跌破即触发）</summary>
        private const float ReinforceLifeFrac = 0.5f;
        /// <summary>电台收势帧</summary>
        private const int ReinforceRecoverFrames = 18;
        /// <summary>增援落地后的下次动作冷却</summary>
        private const int PostReinforceCooldown = 90;

        //==== 飞碟签名技 ====
        /// <summary>部件归属半径（ai 链接查证不实的降级方案：核心周边此半径内的炮塔/加农视为其部件）</summary>
        private const float SaucerPartRadius = 900f;
        /// <summary>部件/相位巡检间隔帧</summary>
        private const int PartScanInterval = 30;
        /// <summary>过热轮转间隔（档位 1/2/3；均大于标记总寿命，保证同时至多一个签名技进行中）</summary>
        private static readonly int[] OverheatIntervalByTier = [540, 450, 360];
        /// <summary>过热易伤倍率（档位 1/2/3）</summary>
        private static readonly float[] OverheatVulnMultByTier = [1.30f, 1.35f, 1.40f];
        /// <summary>入场后首次过热延迟</summary>
        private const int FirstOverheatDelay = 240;
        /// <summary>扫描线预演的核心最小在场帧（避开部件尚未生成完毕的出生窗）</summary>
        private const int ScanMinCoreAge = 90;

        private const byte PhaseIdle = 0;
        private const byte PhaseTelegraph = 1;
        private const byte PhaseStrike = 2;

        /// <summary>本个体出生时绑定的档位，0=未绑定（镜像 GameModeNPC；中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private MrtRole role;
        /// <summary>标线射手相位机（权威端决策私产，客户端可见状态全在标线实体上）</summary>
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>锁定射向（锁定帧写定后不再改写：预告即承诺；行走者语义=扫掠中心角）</summary>
        private float lockDir;
        /// <summary>本次动作的绑定实体槽位（标线/增援信号，动作提交前按索引+类型复验）</summary>
        private int lineIndex = -1;
        private int shotsLeft;
        /// <summary>无人机侧移相位：-1=未进行，≥0=包络帧序（权威端私产）</summary>
        private int dashTimer = -1;
        private int dashSign;
        /// <summary>灰皮步兵当前相位是否增援呼叫（false=掷扳手）</summary>
        private bool gruntCalling;
        /// <summary>ReinforceOncePerLife：一生一次，信号成功挂出即耗尽，中断/增援位满均不返还</summary>
        private bool reinforceUsed;
        /// <summary>特斯拉源的在弧租约（游戏刻），到期前不再结链</summary>
        private uint arcBusyUntil;
        /// <summary>飞碟核心在场计时与过热轮转指针</summary>
        private int coreTimer;
        private int nextOverheatAt;
        private int overheatRotation;

        private static MrtRole ResolveRole(int type) => type switch {
            NPCID.MartianTurret or NPCID.GigaZapper or NPCID.MartianEngineer => MrtRole.TeslaSource,
            NPCID.MartianDrone or NPCID.MartianWalker or NPCID.RayGunner
                or NPCID.BrainScrambler or NPCID.MartianOfficer => MrtRole.LineStriker,
            NPCID.ScutlixRider => MrtRole.RiderCannon,
            NPCID.MartianSaucerCore => MrtRole.SaucerCore,
            NPCID.MartianSaucerTurret or NPCID.MartianSaucerCannon => MrtRole.SaucerPart,
            NPCID.GrayGrunt => MrtRole.Grunt,
            _ => MrtRole.None,
        };

        /// <summary>标线风味索引（与 <see cref="MrtLaserMarkLine.Profiles"/> 对齐）</summary>
        private static int ResolveFlavor(int type) => type switch {
            NPCID.MartianDrone => 0,
            NPCID.MartianWalker => 1,
            NPCID.RayGunner => 2,
            NPCID.BrainScrambler => 3,
            NPCID.MartianOfficer => 4,
            NPCID.ScutlixRider => 5,
            _ => 0,
        };

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && ResolveRole(entity.type) != MrtRole.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            role = MrtRole.None;
            dashTimer = -1;
            gruntCalling = false;
            reinforceUsed = false;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            role = ResolveRole(npc.type);
            if (role == MrtRole.None) {
                return;
            }
            boundTier = tier;
            //出生错拍：冷却是权威端决策私产（客户端 PostAI 早退不读），此处 Main.rand 无同步语义。
            //此刻 npc.whoAmI 恒为 0，不得用作种子
            cooldown = FirstCooldownMin + Main.rand.Next(FirstCooldownMax - FirstCooldownMin + 1);
            if (role == MrtRole.SaucerCore) {
                nextOverheatAt = FirstOverheatDelay;
            }
        }

        /// <summary>
        /// 小怪机制入口资格：友方/无敌/Boss 旗标/小动物载体/雕像怪/共享血池体节逐项排除。
        /// 飞碟核心与部件不走此口径（显式类型名单放行，旗标无关设计）
        /// </summary>
        private static bool MobEligible(NPC npc) {
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
            if (boundTier <= 0 || role == MrtRole.SaucerPart) {
                return;
            }
            if (VaultUtils.isClient) {
                //决策只在权威端；客户端画面全部来自同步原语
                return;
            }
            switch (role) {
                case MrtRole.TeslaSource:
                    ArcStep(npc);
                    return;
                case MrtRole.LineStriker:
                case MrtRole.RiderCannon:
                    StrikeStep(npc);
                    return;
                case MrtRole.SaucerCore:
                    CoreStep(npc);
                    return;
                case MrtRole.Grunt:
                    GruntStep(npc);
                    return;
            }
        }

        /// <summary>
        /// 提速位移补偿：<see cref="GameModeNPC.PostAI"/> 对非 Boss 怪按 velocity×SpeedBonus 追加位置推进，
        /// 本层注入的承诺性速度一律除回该系数（位移项除回、重力项不除）。boss 旗标个体与体节不吃提速层，系数为 1
        /// </summary>
        private float MoveGain(NPC npc) => !npc.boss && npc.realLife < 0 ? 1f + GameModeTuning.SpeedBonus(boundTier) : 1f;

        #region 电弧链
        private void ArcStep(NPC npc) {
            if (--cooldown > 0) {
                return;
            }
            if (!MobEligible(npc)) {
                cooldown = IneligibleDelay;
                return;
            }
            uint now = Main.GameUpdateCount;
            if (arcBusyUntil > now) {
                cooldown = RetryDelay;
                return;
            }

            //并发上限：仅尝试帧扫描现存电弧
            int arcType = ModContent.ProjectileType<MrtTeslaArcProj>();
            int aliveArcs = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == arcType) {
                    aliveArcs++;
                }
            }
            if (aliveArcs >= MaxConcurrentArcs) {
                cooldown = RetryDelay * 2;
                return;
            }

            //找搭档：结链距离内最近的空闲特斯拉源，两端间无墙（不隔墙结弧）
            NPC partner = null;
            MartiansNPC partnerGlobal = null;
            float best = ArcLinkDistance;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (i == npc.whoAmI || !other.active || ResolveRole(other.type) != MrtRole.TeslaSource) {
                    continue;
                }
                if (!MobEligible(other)) {
                    continue;
                }
                MartiansNPC global = other.GetGlobalNPC<MartiansNPC>();
                if (global.boundTier <= 0 || global.arcBusyUntil > now) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, other.Center);
                if (dist > best || !Collision.CanHitLine(npc.Center, 1, 1, other.Center, 1, 1)) {
                    continue;
                }
                best = dist;
                partner = other;
                partnerGlobal = global;
            }
            if (partner == null) {
                cooldown = RetryDelay;
                return;
            }

            //相关性阀门：中点附近要有活人
            Vector2 mid = (npc.Center + partner.Center) / 2f;
            bool relevant = false;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player.Alives() && Vector2.DistanceSquared(player.Center, mid) <= ArcRelevanceRange * ArcRelevanceRange) {
                    relevant = true;
                    break;
                }
            }
            if (!relevant) {
                cooldown = RetryDelay * 2;
                return;
            }

            //链两端 index+type 双校验信息全部经 ai 原生同步：ai[2]=typeA*1000+typeB
            int damage = Math.Max(1, (int)(Math.Min(npc.damage, partner.damage) * ArcDamageFrac));
            int index = Projectile.NewProjectile(npc.GetSource_FromAI(), mid, Vector2.Zero,
                arcType, damage, 0f, Main.myPlayer,
                npc.whoAmI, partner.whoAmI, npc.type * 1000 + partner.type);
            if (index < 0 || index >= Main.maxProjectiles) {
                cooldown = RetryDelay;
                return;
            }

            //两端同上租约与冷却，弧存续期内不重复结链、断链后也不瞬间回连
            uint lease = (uint)(MrtTeslaArcProj.TotalLifeFrames + 30);
            arcBusyUntil = now + lease;
            partnerGlobal.arcBusyUntil = now + lease;
            cooldown = ArcCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            partnerGlobal.cooldown = ArcCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
        }
        #endregion

        #region 标线打击
        private void StrikeStep(NPC npc) {
            if (phase == PhaseIdle) {
                if (--cooldown > 0) {
                    return;
                }
                TryStartStrike(npc);
                return;
            }
            if (phase == PhaseTelegraph) {
                TickTelegraph(npc);
                return;
            }
            TickStrike(npc);
        }

        private void TryStartStrike(NPC npc) {
            if (!MobEligible(npc)) {
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

            int flavor = ResolveFlavor(npc.type);
            MrtStrikeProfile profile = MrtLaserMarkLine.Profiles[flavor];
            float dist = Vector2.Distance(npc.Center, player.Center);
            if (dist < profile.MinRange || dist > profile.MaxRange
                || !Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)) {
                cooldown = RetryDelay;
                return;
            }

            //并发上限：仅尝试帧扫描现存标线
            int lineType = ModContent.ProjectileType<MrtLaserMarkLine>();
            int aliveLines = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == lineType) {
                    aliveLines++;
                }
            }
            if (aliveLines >= MaxConcurrentStrikes) {
                cooldown = RetryDelay;
                return;
            }

            //预告即实体：标线生成失败（弹幕位满）则整次打击作废
            lineIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                lineType, 0, 0f, Main.myPlayer, npc.whoAmI, npc.type * 10 + flavor, 0f);
            if (lineIndex < 0 || lineIndex >= Main.maxProjectiles) {
                lineIndex = -1;
                cooldown = RetryDelay;
                return;
            }
            phase = PhaseTelegraph;
            timer = profile.TelegraphFrames;
            shotsLeft = profile.Shots;
        }

        /// <summary>标线复验：索引+类型+锚定己身三重校验（槽位复用不冒充）</summary>
        private bool LineValid(NPC npc, out Projectile line) {
            line = null;
            if (lineIndex < 0 || lineIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile proj = Main.projectile[lineIndex];
            if (!proj.active || proj.type != ModContent.ProjectileType<MrtLaserMarkLine>()
                || (int)proj.ai[0] != npc.whoAmI) {
                return false;
            }
            line = proj;
            return true;
        }

        private void TickTelegraph(NPC npc) {
            MrtStrikeProfile profile = MrtLaserMarkLine.Profiles[ResolveFlavor(npc.type)];
            timer--;

            if (timer == profile.LockFrames) {
                //锁定帧：方向自此为承诺，写回标线实体作各端权威纠偏
                if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                    lockDir = (Main.player[npc.target].Center - npc.Center).ToRotation();
                }
                else {
                    lockDir = npc.direction >= 0 ? 0f : MathHelper.Pi;
                }
                if (LineValid(npc, out Projectile line)) {
                    line.ai[2] = lockDir + 10f;
                    line.netUpdate = true;
                }
            }

            if (timer > 0) {
                return;
            }
            //开火帧：标线实体已消失则不放冷枪（无预告不开火），直接收势
            if (!LineValid(npc, out _)) {
                phase = PhaseStrike;
                timer = MrtLaserMarkLine.RemnantFrames;
                shotsLeft = 0;
                return;
            }
            if (npc.type == NPCID.MartianWalker) {
                //压制扫描签名：首发在扫掠起点角，其余沿固定角速度逐里程碑发出（TickWalkerSweep）
                FireBoltAt(npc, MrtLaserMarkLine.SweepAngle(lockDir, 0));
                shotsLeft = MrtLaserMarkLine.SweepShots - 1;
                phase = PhaseStrike;
                timer = MrtLaserMarkLine.SweepFrames + MrtLaserMarkLine.RemnantFrames;
                return;
            }
            FireBolt(npc);
            shotsLeft--;
            if (npc.type == NPCID.MartianDrone) {
                //游走签名：出手即起小幅侧移换位（包络在余痕窗内走完）
                dashSign = Main.rand.NextBool() ? 1 : -1;
                dashTimer = 0;
            }
            phase = PhaseStrike;
            timer = MrtLaserMarkLine.RemnantFrames;
        }

        private void FireBolt(NPC npc) => FireBoltAt(npc, lockDir);

        private void FireBoltAt(NPC npc, float dir) {
            int flavor = ResolveFlavor(npc.type);
            MrtStrikeProfile profile = MrtLaserMarkLine.Profiles[flavor];
            int damage = Math.Max(1, (int)(npc.damage * profile.DamageFrac));
            if (npc.type == NPCID.RayGunner) {
                //三连点射签名的补偿：单发 ×0.5
                damage = Math.Max(1, (int)(damage * RayGunnerBurstDamageMult));
            }
            Vector2 velocity = dir.ToRotationVector2() * profile.BoltSpeed;
            //弹体是弹幕，不吃 GameModeNPC 的 NPC 位移补偿层，速度无需除回
            if (role == MrtRole.RiderCannon) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velocity,
                    ModContent.ProjectileType<MrtScutlixHeavyBolt>(), damage, 3f, Main.myPlayer);
            }
            else {
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velocity,
                    ModContent.ProjectileType<MrtLaserBoltProj>(), damage, 1f, Main.myPlayer, flavor);
            }
        }

        private void TickStrike(NPC npc) {
            timer--;
            if (npc.type == NPCID.MartianWalker) {
                TickWalkerSweep(npc);
            }
            else if (shotsLeft > 0 && (MrtLaserMarkLine.RemnantFrames - timer) % MrtLaserMarkLine.SecondShotGapFrames == 0) {
                //连发沿同一锁定方向（预告即承诺：绝不重瞄；射线枪手三连点射的次发/三发走此口）
                FireBolt(npc);
                shotsLeft--;
            }
            if (dashTimer >= 0) {
                TickDroneDash(npc);
            }
            if (timer > 0) {
                return;
            }
            if (npc.type == NPCID.MartianOfficer) {
                //支援签名：齐射收势帧为最近友军挂单次格挡护罩
                GrantGuardShield(npc);
            }
            phase = PhaseIdle;
            lineIndex = -1;
            MrtStrikeProfile profile = MrtLaserMarkLine.Profiles[ResolveFlavor(npc.type)];
            cooldown = profile.Cooldown(boundTier) + Main.rand.Next(CooldownJitter + 1);
        }

        /// <summary>
        /// 压制扫描发射循环：与标线预览共用 SweepAngle/SweepStepFrames（所见即所射）。
        /// 扫掠即缺口的反面：亮线已越过的角域不再产生新弹（扫过即安全，在途弹体自身可见可躲）
        /// </summary>
        private void TickWalkerSweep(NPC npc) {
            if (shotsLeft <= 0) {
                return;
            }
            int sweepFrame = MrtLaserMarkLine.SweepFrames + MrtLaserMarkLine.RemnantFrames - timer;
            if (sweepFrame > MrtLaserMarkLine.SweepFrames || sweepFrame % MrtLaserMarkLine.SweepStepFrames != 0) {
                return;
            }
            //标线实体缺位 → 剩余扫掠作废（无预告不开火，失败方向=安全方向）
            if (!LineValid(npc, out _)) {
                shotsLeft = 0;
                return;
            }
            FireBoltAt(npc, MrtLaserMarkLine.SweepAngle(lockDir, sweepFrame));
            shotsLeft--;
        }

        /// <summary>
        /// 游走签名：垂直于锁线的包络微冲刺（M2 三段塑形），名义速度除回 MoveGain。
        /// 速度注入在相位沿与低频重推帧跟同步，收势清残速把控制权干净还给原版 AI
        /// </summary>
        private void TickDroneDash(NPC npc) {
            dashTimer++;
            int total = DroneDashRise + DroneDashHold + DroneDashDecay;
            float envelope = MobDash.Envelope(dashTimer, DroneDashRise, DroneDashHold, DroneDashDecay);
            Vector2 side = (lockDir + MathHelper.PiOver2).ToRotationVector2() * dashSign;
            npc.velocity = side * (DroneDashPeak * envelope / MoveGain(npc));
            if (dashTimer == 1 || dashTimer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (dashTimer >= total) {
                npc.velocity = Vector2.Zero;
                npc.netUpdate = true;
                dashTimer = -1;
            }
        }

        /// <summary>
        /// 护盾链接（支援签名）：为最近一名友军火星怪挂单次格挡护罩。
        /// 护罩是已同步弹幕实体，格挡判定与破裂全由实体承载（<see cref="MrtGuardShieldProj"/>）
        /// </summary>
        private void GrantGuardShield(NPC npc) {
            NPC best = null;
            float bestDist = OfficerShieldRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (i == npc.whoAmI || !other.active || !MobEligible(other)) {
                    continue;
                }
                MrtRole otherRole = ResolveRole(other.type);
                if (otherRole is MrtRole.None or MrtRole.SaucerCore or MrtRole.SaucerPart) {
                    continue;
                }
                if (MrtGuardShieldProj.IsGuarding(other.whoAmI, other.type)) {
                    continue;//已有护罩不叠挂（单次格挡语义不稀释）
                }
                float dist = Vector2.Distance(npc.Center, other.Center);
                if (dist > bestDist) {
                    continue;
                }
                bestDist = dist;
                best = other;
            }
            if (best == null) {
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), best.Center, Vector2.Zero,
                ModContent.ProjectileType<MrtGuardShieldProj>(), 0, 0f, Main.myPlayer,
                best.whoAmI, best.type, npc.whoAmI + 1);
        }
        #endregion

        #region 灰皮步兵
        private void GruntStep(NPC npc) {
            if (phase == PhaseIdle) {
                //一生一次呼叫增援：首次跌破半血把剩余冷却压到重试间隔，尽快插队呼叫
                bool wantReinforce = !reinforceUsed && npc.life < npc.lifeMax * ReinforceLifeFrac;
                if (wantReinforce && cooldown > RetryDelay) {
                    cooldown = RetryDelay;
                }
                if (--cooldown > 0) {
                    return;
                }
                if (!MobEligible(npc)) {
                    cooldown = IneligibleDelay;
                    return;
                }
                if (wantReinforce) {
                    TryStartReinforce(npc);
                    return;
                }
                TryStartWrench(npc);
                return;
            }
            if (phase == PhaseTelegraph) {
                TickGruntTelegraph(npc);
                return;
            }
            //收势：本相位机不残留任何注入速度，控制权还给原版 AI
            timer--;
            if (timer > 0) {
                return;
            }
            phase = PhaseIdle;
            lineIndex = -1;
            cooldown = gruntCalling ? PostReinforceCooldown
                : WrenchCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            gruntCalling = false;
        }

        private void TryStartWrench(NPC npc) {
            if (npc.velocity.Y != 0f) {
                //站稳才抬臂
                cooldown = RetryDelay;
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
            if (dist < WrenchMinRange || dist > WrenchMaxRange
                || !Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)) {
                cooldown = RetryDelay;
                return;
            }

            //并发上限：仅尝试帧扫描现存扳手
            int wrenchType = ModContent.ProjectileType<MrtWrenchProj>();
            int aliveWrenches = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == wrenchType) {
                    aliveWrenches++;
                }
            }
            if (aliveWrenches >= MaxConcurrentWrenches) {
                cooldown = RetryDelay;
                return;
            }

            //预告即实体：扳手悬浮期即抬臂前摇，落点自此锁死（预告即承诺）；生成失败则整次作废
            Vector2 lockPoint = player.Bottom - Vector2.UnitY * 10f;
            int damage = Math.Max(1, (int)(npc.damage * WrenchDamageFrac));
            lineIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Top - Vector2.UnitY * 8f, Vector2.Zero,
                wrenchType, damage, 1f, Main.myPlayer, lockPoint.X, lockPoint.Y, npc.whoAmI);
            if (lineIndex < 0 || lineIndex >= Main.maxProjectiles) {
                lineIndex = -1;
                cooldown = RetryDelay;
                return;
            }
            gruntCalling = false;
            phase = PhaseTelegraph;
            timer = MrtWrenchProj.TelegraphFrames;
            //抬臂定身：刹车脉冲即前摇可读信号之一（悬浮扳手+落点标记为主信号）
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
        }

        /// <summary>电台呼叫增援：挂出信号实体作可见前摇。信号成功挂出即消耗一生一次的名额</summary>
        private void TryStartReinforce(NPC npc) {
            int signalType = ModContent.ProjectileType<MrtReinforceSignalProj>();
            lineIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Top, Vector2.Zero,
                signalType, 0, 0f, Main.myPlayer, npc.whoAmI, npc.type);
            if (lineIndex < 0 || lineIndex >= Main.maxProjectiles) {
                //弹幕位满：名额不消耗，稍后重试
                lineIndex = -1;
                cooldown = RetryDelay;
                return;
            }
            reinforceUsed = true;
            gruntCalling = true;
            phase = PhaseTelegraph;
            timer = MrtReinforceSignalProj.SignalFrames;
            //电台定身前摇
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
        }

        private void TickGruntTelegraph(NPC npc) {
            timer--;
            //前摇中段周期性再刹，压住走位漂移（脉冲帧才跟同步）
            if (timer > 0 && timer % 12 == 0) {
                npc.velocity.X *= 0.3f;
                npc.netUpdate = true;
            }
            if (timer > 0) {
                return;
            }
            if (gruntCalling) {
                //信号实体复验（索引+类型+锚定己身）：实体缺位则增援不发生（失败方向=安全方向）
                bool signalAlive = lineIndex >= 0 && lineIndex < Main.maxProjectiles
                    && Main.projectile[lineIndex].active
                    && Main.projectile[lineIndex].type == ModContent.ProjectileType<MrtReinforceSignalProj>()
                    && (int)Main.projectile[lineIndex].ai[0] == npc.whoAmI;
                if (signalAlive) {
                    SpawnReinforcement(npc);
                }
                timer = ReinforceRecoverFrames;
            }
            else {
                //掷出帧由扳手实体按锁定落点自行解算（NightBoneProj 口径），步兵只收势
                timer = WrenchRecoverFrames;
            }
            phase = PhaseStrike;
        }

        /// <summary>权威端生成增援（镜像仓内 NewNPC 先例：越界检查 → 赋 target → netUpdate 标准同步）</summary>
        private void SpawnReinforcement(NPC npc) {
            int recruitType = Main.rand.NextBool() ? NPCID.MartianDrone : NPCID.BrainScrambler;
            int index = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)(npc.Center.Y - 24f), recruitType);
            if (index < 0 || index >= Main.maxNPCs) {
                //增援位满：呼叫名额已按一生一次消耗，不返还
                return;
            }
            NPC recruit = Main.npc[index];
            recruit.target = npc.target;
            recruit.netUpdate = true;
        }
        #endregion

        #region 飞碟签名技
        private void CoreStep(NPC npc) {
            coreTimer++;
            if (coreTimer % PartScanInterval != 0) {
                return;
            }

            //部件归属：半径归属（§2.2 降级方案），只读部件 active/type/Center
            int partsAlive = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (other.active
                    && (other.type == NPCID.MartianSaucerTurret || other.type == NPCID.MartianSaucerCannon)
                    && Vector2.DistanceSquared(other.Center, npc.Center) <= SaucerPartRadius * SaucerPartRadius) {
                    partsAlive++;
                }
            }

            if (partsAlive > 0) {
                TickOverheat(npc, partsAlive);
                return;
            }
            //部件全灭=二阶段（普通模式此时飞碟随即死亡，核心失效后预演线自会消散）
            if (coreTimer >= ScanMinCoreAge) {
                EnsureScanLine(npc);
            }
        }

        /// <summary>相位弱点：轮转点名一门炮过热（标记实体即预告与易伤窗）</summary>
        private void TickOverheat(NPC npc, int partsAlive) {
            if (coreTimer < nextOverheatAt) {
                return;
            }
            //每实例同时至多一个签名技进行中：本核心已有存续标记则等待
            int markType = ModContent.ProjectileType<MrtOverheatMarkProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == markType && (int)proj.ai[2] == npc.whoAmI + 1) {
                    return;
                }
            }

            //轮转可见：按槽位升序取第 rotation 个部件，标记逐门炮迁移
            int pick = overheatRotation % partsAlive;
            int seen = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (!other.active
                    || (other.type != NPCID.MartianSaucerTurret && other.type != NPCID.MartianSaucerCannon)
                    || Vector2.DistanceSquared(other.Center, npc.Center) > SaucerPartRadius * SaucerPartRadius) {
                    continue;
                }
                if (seen++ != pick) {
                    continue;
                }
                int index = Projectile.NewProjectile(npc.GetSource_FromAI(), other.Center, Vector2.Zero,
                    markType, 0, 0f, Main.myPlayer, i, other.type, npc.whoAmI + 1);
                if (index >= 0 && index < Main.maxProjectiles) {
                    overheatRotation++;
                    nextOverheatAt = coreTimer + OverheatIntervalByTier[boundTier - 1];
                }
                return;
            }
        }

        /// <summary>死亡射线扫描线预演：核心在场则保证唯一一条预演线存在</summary>
        private void EnsureScanLine(NPC npc) {
            int scanType = ModContent.ProjectileType<MrtSaucerScanProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == scanType && (int)proj.ai[0] == npc.whoAmI) {
                    return;
                }
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                scanType, 0, 0f, Main.myPlayer, npc.whoAmI);
        }
        #endregion

        #region 过热易伤与护盾格挡（攻击方本机结算）
        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            ApplyOverheatVulnerability(npc, ref modifiers);
            ApplyGuardShield(npc, ref modifiers);
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            ApplyOverheatVulnerability(npc, ref modifiers);
            ApplyGuardShield(npc, ref modifiers);
        }

        /// <summary>
        /// 相位弱点的承接端：打击判定在攻击方本机进行，窗口证据 = 已同步的过热标记实体
        /// （索引+类型双校验），不读任何服务端私产计时器
        /// </summary>
        private void ApplyOverheatVulnerability(NPC npc, ref NPC.HitModifiers modifiers) {
            if (boundTier <= 0 || role != MrtRole.SaucerPart) {
                return;
            }
            if (MrtOverheatMarkProj.IsVulnerable(npc.whoAmI, npc.type)) {
                modifiers.FinalDamage *= OverheatVulnMultByTier[boundTier - 1];
            }
        }

        /// <summary>
        /// 军官护盾链接的承接端：攻击方本机判窗，窗口证据 = 已同步护罩实体（索引+类型双校验）。
        /// 格挡=本次伤害乘零（引擎钳底为 1 点），护罩实体各端观测掉血自行破裂（单次格挡语义）
        /// </summary>
        private void ApplyGuardShield(NPC npc, ref NPC.HitModifiers modifiers) {
            if (boundTier <= 0 || role is MrtRole.None or MrtRole.SaucerCore or MrtRole.SaucerPart) {
                return;
            }
            if (MrtGuardShieldProj.IsGuarding(npc.whoAmI, npc.type)) {
                modifiers.FinalDamage *= 0f;
            }
        }
        #endregion
    }
}
