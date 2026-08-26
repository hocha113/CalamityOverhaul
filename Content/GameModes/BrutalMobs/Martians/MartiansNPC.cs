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
    }

    /// <summary>
    /// 火星暴乱行为层「链式电网」。只叠加行为不动数值（数值层归 <see cref="GameModeNPC"/>），
    /// 原版 AI 全程继续跑，本层不注入任何 NPC 速度（无弹道承诺需提速补偿的情形）。
    /// 决策与弹幕生成全在权威端（客户端 PostAI 早退），客户端可见状态一律来自弹幕实体与原版同步原语。
    /// 排除条目：MartianProbe（侦测流程不碰）、ForceBubble（军官护盾只读不改）、
    /// MartianSaucer 本体（部件排除表，机制只挂核心）、Scutlix 坐骑（主炮机制挂骑手，同一躯体不双挂）、
    /// GrayGrunt（分工清单未列入）。
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
        /// <summary>出生后首攻等待窗，随机错开避免同屏齐动</summary>
        private const int FirstCooldownMin = 90;
        private const int FirstCooldownMax = 300;
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
        /// <summary>锁定射向（锁定帧写定后不再改写：预告即承诺）</summary>
        private float lockDir;
        /// <summary>本次打击的标线槽位（开火前按索引+类型复验）</summary>
        private int lineIndex = -1;
        private int shotsLeft;
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
            }
        }

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
            FireBolt(npc);
            shotsLeft--;
            phase = PhaseStrike;
            timer = MrtLaserMarkLine.RemnantFrames;
        }

        private void FireBolt(NPC npc) {
            int flavor = ResolveFlavor(npc.type);
            MrtStrikeProfile profile = MrtLaserMarkLine.Profiles[flavor];
            int damage = Math.Max(1, (int)(npc.damage * profile.DamageFrac));
            Vector2 velocity = lockDir.ToRotationVector2() * profile.BoltSpeed;
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
            if (shotsLeft > 0 && timer == MrtLaserMarkLine.RemnantFrames - MrtLaserMarkLine.SecondShotGapFrames) {
                //多连发沿同一锁定方向（预告即承诺：绝不重瞄）
                FireBolt(npc);
                shotsLeft--;
            }
            if (timer > 0) {
                return;
            }
            phase = PhaseIdle;
            lineIndex = -1;
            MrtStrikeProfile profile = MrtLaserMarkLine.Profiles[ResolveFlavor(npc.type)];
            cooldown = profile.Cooldown(boundTier) + Main.rand.Next(CooldownJitter + 1);
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

        #region 过热易伤（攻击方本机结算）
        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
            => ApplyOverheatVulnerability(npc, ref modifiers);

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
            => ApplyOverheatVulnerability(npc, ref modifiers);

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
        #endregion
    }
}
