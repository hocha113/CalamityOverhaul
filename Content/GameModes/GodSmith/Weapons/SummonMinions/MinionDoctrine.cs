using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions
{
    /// <summary>阵型形状。Solo = 不入阵（独子编制）</summary>
    internal enum GsFormationKind
    {
        Solo,
        Ring,
        Vee,
        Hive,
        Line,
        Column,
        Wings,
        Triangle,
    }

    /// <summary>
    /// 仆从条令参数包（每武器一份，加载期经 <see cref="MinionDoctrine.RegisterKit"/> 注册）。
    /// 阵型偏置只作用于无敌情的悬停段，战斗全程让位原版 AI
    /// </summary>
    internal sealed class GsMinionKit
    {
        /// <summary>阵型形状</summary>
        public GsFormationKind Formation = GsFormationKind.Ring;
        /// <summary>主半径（环阵半径 / 雁行高度 / 横列后退量等，按形状语义复用）</summary>
        public float Radius = 80f;
        /// <summary>槽间距</summary>
        public float Spacing = 32f;
        /// <summary>扇区锚（弧度，多类型共存时错开各自形状带）</summary>
        public float SectorAnchor = 0f;
        /// <summary>环阵缓转角速度（弧度每帧）</summary>
        public float RotSpeed = 0f;
        /// <summary>引导强度倍率（爬墙类调低防与原版 AI 打架）</summary>
        public float DriftMul = 1f;
        /// <summary>地面仆从只引导水平速度，纵向留给原版重力</summary>
        public bool Grounded = false;
        /// <summary>环阵纵向压扁系数</summary>
        public float VerticalSquash = 1f;
    }

    /// <summary>
    /// 军团条令：召唤·仆从族共享框架。指令三态（护卫/突击/集结）承载在
    /// <see cref="GsLegionBannerProj"/> 的 ai[0..2] 上（弹幕同步免费广播，无自定义包）；
    /// 阵型槽位各端按 (type, identity) 排序逐帧确定性重算；指挥官光环 = 已用仆从槽 ≥3。<br/>
    /// S3b 消费面：<see cref="RegisterKit"/>、<see cref="GetCommand"/>、
    /// <see cref="TryGetAssaultTarget"/>、<see cref="TryGetRallyPoint"/>、
    /// <see cref="CommanderAuraActive"/>、<see cref="CommandColor"/>、
    /// <see cref="RallyFieldAlive"/>、<see cref="FindOwnedProj"/>、<see cref="GsHitTally"/>
    /// </summary>
    internal static class MinionDoctrine
    {
        //==================== 指令常量 ====================

        /// <summary>护卫（默认态，无军旗即护卫）</summary>
        internal const int CommandGuard = 0;
        /// <summary>突击：军旗附着焦点目标，全军优先集火</summary>
        internal const int CommandAssault = 1;
        /// <summary>集结：军旗立于点，仆从改锚旗点列阵</summary>
        internal const int CommandRally = 2;

        /// <summary>指挥官光环半径</summary>
        internal const float AuraRadius = 260f;
        /// <summary>光环内自家仆从增伤</summary>
        internal const float AuraBonus = 0.08f;
        /// <summary>光环激活所需已用仆从槽</summary>
        internal const float AuraSlotNeed = 3f;

        //指令三色：护卫金 / 突击赤 / 集结青
        internal static readonly Color GuardGold = new(255, 214, 120);
        internal static readonly Color AssaultRed = new(255, 96, 74);
        internal static readonly Color RallyCyan = new(96, 226, 214);

        //==================== kit 注册表 ====================

        private static Dictionary<int, GsMinionKit> kitByProjType = [];

        /// <summary>注册仆从条令（在 scheme 的 GsSetStaticDefaults 里调用；本体与伴生弹幕都可挂同一 kit）</summary>
        internal static void RegisterKit(GsMinionKit kit, params int[] projTypes) {
            foreach (int type in projTypes) {
                kitByProjType[type] = kit;
            }
        }

        internal static bool TryGetKit(int projType, out GsMinionKit kit)
            => kitByProjType.TryGetValue(projType, out kit);

        //==================== 军旗登记与查询（各端本地缓存，由军旗 AI 每帧刷写） ====================

        private static int[] bannerByOwner;

        /// <summary>军旗 AI 每帧登记自己（各端本地）</summary>
        internal static void NoticeBanner(Projectile banner) {
            bannerByOwner ??= NewOwnerArray();
            if (banner.owner >= 0 && banner.owner < bannerByOwner.Length) {
                bannerByOwner[banner.owner] = banner.whoAmI;
            }
        }

        private static int[] NewOwnerArray() {
            int[] arr = new int[Main.maxPlayers + 1];
            Array.Fill(arr, -1);
            return arr;
        }

        /// <summary>取该玩家的军旗弹幕；无旗返回 null（即护卫态）</summary>
        internal static Projectile FindBanner(int owner) {
            if (bannerByOwner == null || owner < 0 || owner >= bannerByOwner.Length) {
                return null;
            }
            int idx = bannerByOwner[owner];
            if (idx < 0 || idx >= Main.maxProjectiles) {
                return null;
            }
            Projectile proj = Main.projectile[idx];
            if (proj.active && proj.owner == owner
                && proj.type == ModContent.ProjectileType<GsLegionBannerProj>()) {
                return proj;
            }
            bannerByOwner[owner] = -1;
            return null;
        }

        /// <summary>当前指令（含焦点失效的本地回退：突击目标死亡各端即时读回护卫）</summary>
        internal static int GetCommand(int owner) {
            Projectile banner = FindBanner(owner);
            if (banner == null) {
                return CommandGuard;
            }
            int cmd = (int)banner.ai[0];
            if (cmd == CommandAssault) {
                return ResolveAssaultTarget(banner) != null ? CommandAssault : CommandGuard;
            }
            return cmd == CommandRally ? CommandRally : CommandGuard;
        }

        /// <summary>突击焦点目标；失效返回 false</summary>
        internal static bool TryGetAssaultTarget(int owner, out NPC target) {
            target = null;
            Projectile banner = FindBanner(owner);
            if (banner == null || (int)banner.ai[0] != CommandAssault) {
                return false;
            }
            target = ResolveAssaultTarget(banner);
            return target != null;
        }

        /// <summary>按军旗 ai[1]=索引 ai[2]=类型 校验解析焦点（NPC 索引跨端一致，类型失配即视为失效）</summary>
        internal static NPC ResolveAssaultTarget(Projectile banner) {
            int idx = (int)banner.ai[1];
            if (idx < 0 || idx >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[idx];
            return npc.active && npc.type == (int)banner.ai[2] && npc.CanBeChasedBy() ? npc : null;
        }

        /// <summary>集结点；非集结态返回 false</summary>
        internal static bool TryGetRallyPoint(int owner, out Vector2 point) {
            point = default;
            Projectile banner = FindBanner(owner);
            if (banner == null || (int)banner.ai[0] != CommandRally) {
                return false;
            }
            point = new Vector2(banner.ai[1], banner.ai[2]);
            return true;
        }

        /// <summary>当前指令对应的主题色（光环与旗桩共用）</summary>
        internal static Color CommandColor(int owner) => GetCommand(owner) switch {
            CommandAssault => AssaultRed,
            CommandRally => RallyCyan,
            _ => GuardGold,
        };

        //==================== 右键指挥（只在本地玩家路径调用） ====================

        /// <summary>指挥防抖：CanUseItem 在按住右键期间每帧触发，短窗内只执行一次</summary>
        private static uint lastCommandTick;

        /// <summary>
        /// 右键指挥分发：光标点敌 = 突击；点玩家近旁或已有旗点 = 撤旗回护卫；点远处 = 集结。
        /// 军旗全部状态经 NewProjectile 的 ai 形参传初值（生成包先行陷阱规避），改令 = 杀旧旗立新旗
        /// </summary>
        internal static void ExecuteCommandAt(Player player, Vector2 cursor) {
            if (Main.GameUpdateCount < lastCommandTick + 18) {
                return;
            }
            lastCommandTick = Main.GameUpdateCount;

            Projectile banner = FindBanner(player.whoAmI);

            //光标 56px 内的可追猎敌人 = 突击目标
            NPC picked = null;
            float bestDist = 56f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, cursor);
                if (dist < bestDist) {
                    bestDist = dist;
                    picked = npc;
                }
            }

            if (picked != null) {
                ReplaceBanner(player, banner, picked.Top - new Vector2(0f, 30f),
                    CommandAssault, picked.whoAmI, picked.type);
                GsLegionBannerProj.PopCommandText(player, CommandAssault);
                return;
            }

            //撤旗：点自己近旁，或点在已立的集结旗上
            bool nearSelf = cursor.Distance(player.Center) <= 96f;
            bool onBanner = banner != null && (int)banner.ai[0] == CommandRally
                && cursor.Distance(banner.Center) <= 34f;
            if (nearSelf || onBanner) {
                if (banner != null) {
                    banner.Kill();
                    GsLegionBannerProj.PopCommandText(player, CommandGuard);
                }
                return;
            }

            //集结于光标点
            ReplaceBanner(player, banner, cursor, CommandRally, cursor.X, cursor.Y);
            GsLegionBannerProj.PopCommandText(player, CommandRally);
        }

        private static void ReplaceBanner(Player player, Projectile old, Vector2 pos,
            int command, float payload1, float payload2) {
            old?.Kill();
            Projectile.NewProjectile(player.GetSource_Misc("GsLegionBanner"), pos, Vector2.Zero,
                ModContent.ProjectileType<GsLegionBannerProj>(), 0, 0f, player.whoAmI,
                command, payload1, payload2);
        }

        //==================== 阵型缓存（每 owner 每帧惰性重算，各端确定性一致） ====================

        private sealed class FormationCache
        {
            /// <summary>缓存所属帧</summary>
            public uint Frame;
            /// <summary>该 owner 的在编仆从，按 (type, identity) 排序（identity 跨端一致，禁用 whoAmI 排序）</summary>
            public readonly List<(int Type, int Identity, int Who)> Roster = new(36);
            /// <summary>玩家周边最近可追猎敌人索引；-1 = 无敌情</summary>
            public int HostileIdx = -1;
            /// <summary>在场集结场形态位掩码（bit = GsRallyFieldProj.ai[0]）</summary>
            public int RallyFieldMask;
        }

        private static FormationCache[] cacheByOwner;
        /// <summary>编队处理上限（性能预算）</summary>
        private const int RosterCap = 32;
        /// <summary>敌情警戒半径（有敌即战斗，阵型让位原版 AI）</summary>
        private const float HostileRange = 1000f;

        private static FormationCache GetCache(int owner) {
            cacheByOwner ??= new FormationCache[Main.maxPlayers + 1];
            FormationCache cache = cacheByOwner[owner] ??= new FormationCache();
            if (cache.Frame == Main.GameUpdateCount) {
                return cache;
            }
            cache.Frame = Main.GameUpdateCount;
            cache.Roster.Clear();
            cache.HostileIdx = -1;
            cache.RallyFieldMask = 0;

            int fieldType = ModContent.ProjectileType<GsRallyFieldProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != owner) {
                    continue;
                }
                if (proj.type == fieldType) {
                    cache.RallyFieldMask |= 1 << (int)proj.ai[0];
                    continue;
                }
                if (proj.minion && kitByProjType.ContainsKey(proj.type)
                    && cache.Roster.Count < RosterCap) {
                    cache.Roster.Add((proj.type, proj.identity, proj.whoAmI));
                }
            }
            cache.Roster.Sort(static (a, b) => a.Type != b.Type
                ? a.Type.CompareTo(b.Type) : a.Identity.CompareTo(b.Identity));

            Player player = Main.player[owner];
            if (player.active && !player.dead) {
                float best = HostileRange;
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!npc.CanBeChasedBy()) {
                        continue;
                    }
                    float dist = Vector2.Distance(npc.Center, player.Center);
                    if (dist < best) {
                        best = dist;
                        cache.HostileIdx = npc.whoAmI;
                    }
                }
            }
            return cache;
        }

        //==================== 公共钩子头（由 GsMinionScheme 密封转发调用） ====================

        /// <summary>
        /// 仆从本体每帧维护：军旗续命（各端一致刷 timeLeft，无仆从在场即全端自然过期）+ 悬停段阵型引导
        /// </summary>
        internal static void MinionUpkeep(Projectile proj, GsMinionKit kit) {
            Projectile banner = FindBanner(proj.owner);
            if (banner != null && banner.timeLeft < GsLegionBannerProj.LingerFrames) {
                banner.timeLeft = GsLegionBannerProj.LingerFrames;
            }
            if (kit != null) {
                ApplyFormationDrift(proj, kit);
            }
        }

        /// <summary>命中公共加成：指挥官光环内自家仆从 +8%，灵慰领域圈内 +10%（owner 端裁决）</summary>
        internal static void ApplyCommandBonuses(Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            Player owner = Main.player[proj.owner];
            if (!owner.active) {
                return;
            }
            if (CommanderAuraActive(owner) && proj.Center.Distance(owner.Center) <= AuraRadius) {
                modifiers.FinalDamage *= 1f + AuraBonus;
            }
            //灵慰领域（阿比盖尔命中点留下的灵光圈）：圈内全族仆从增伤
            if (FindOwnedProj(proj.owner, ModContent.ProjectileType<GsSoulSolaceProj>(),
                target.Center, GsSoulSolaceProj.FieldRadius) != null) {
                modifiers.FinalDamage *= 1.10f;
            }
        }

        /// <summary>悬停段阵型引导：无敌情时把速度往槽位方向柔和牵引，不硬设位置</summary>
        private static void ApplyFormationDrift(Projectile proj, GsMinionKit kit) {
            if (kit.Formation == GsFormationKind.Solo) {
                return;
            }
            Player owner = Main.player[proj.owner];
            if (!owner.active || owner.dead) {
                return;
            }
            FormationCache cache = GetCache(proj.owner);
            //敌情在侧 = 战斗中，阵型完全让位原版战斗 AI（这也是模式关闭回退的保证）
            if (cache.HostileIdx >= 0) {
                return;
            }

            int command = GetCommand(proj.owner);
            Vector2 anchor = owner.Center;
            if (command == CommandRally && TryGetRallyPoint(proj.owner, out Vector2 rally)) {
                anchor = rally;
            }

            //组内槽序：在排序名册里数同 type 的位置（顺序各端一致）
            int slot = -1;
            int count = 0;
            foreach ((int type, int identity, int who) in cache.Roster) {
                if (type != proj.type) {
                    continue;
                }
                if (identity == proj.identity && who == proj.whoAmI) {
                    slot = count;
                }
                count++;
            }
            if (slot < 0 || count <= 0) {
                return;
            }

            Vector2 slotPos = anchor + SlotOffset(kit, slot, count, owner);
            Vector2 want = slotPos - proj.Center;
            float dist = want.Length();
            //护卫态回防引导 +20%
            float drift = 0.085f * kit.DriftMul * (command == CommandGuard ? 1.2f : 1f);
            if (dist < 12f) {
                if (!kit.Grounded) {
                    proj.velocity *= 0.92f;
                }
                return;
            }
            Vector2 targetVel = want * 0.06f;
            float maxSpeed = MathHelper.Clamp(dist * 0.08f, 2f, 11f);
            if (targetVel.Length() > maxSpeed) {
                targetVel = targetVel.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            if (kit.Grounded) {
                //地面仆从只牵水平，纵向交还原版重力与跳跃
                proj.velocity.X = MathHelper.Lerp(proj.velocity.X, targetVel.X, drift);
            }
            else {
                proj.velocity = Vector2.Lerp(proj.velocity, targetVel, drift);
            }
        }

        /// <summary>阵型槽位偏移（相对锚点；确定性纯函数，禁随机）</summary>
        private static Vector2 SlotOffset(GsMinionKit kit, int slot, int count, Player owner) {
            switch (kit.Formation) {
                case GsFormationKind.Ring: {
                    float baseAng = kit.SectorAnchor + Main.GlobalTimeWrappedHourly * 60f * kit.RotSpeed;
                    float ang = baseAng + MathHelper.TwoPi * slot / Math.Max(1, count);
                    Vector2 off = ang.ToRotationVector2() * kit.Radius;
                    off.Y *= kit.VerticalSquash;
                    if (kit.Grounded) {
                        //地面环阵 = 脚边散开的水平站位
                        off.Y = 4f;
                    }
                    return off;
                }
                case GsFormationKind.Vee: {
                    if (slot == 0) {
                        return new Vector2(0f, -kit.Radius);
                    }
                    int wing = (slot + 1) / 2;
                    float side = slot % 2 == 1 ? -1f : 1f;
                    return new Vector2(side * wing * kit.Spacing,
                        -kit.Radius - wing * kit.Spacing * 0.45f);
                }
                case GsFormationKind.Hive: {
                    Vector2 anchorDir = kit.SectorAnchor.ToRotationVector2();
                    return anchorDir * kit.Radius + HexCell(slot) * kit.Spacing;
                }
                case GsFormationKind.Line: {
                    float x = (slot - (count - 1) * 0.5f) * kit.Spacing
                        - owner.direction * kit.Radius * 0.4f;
                    return new Vector2(x, -kit.Radius);
                }
                case GsFormationKind.Column: {
                    return new Vector2(-owner.direction * kit.Spacing * (slot + 1),
                        -6f - slot % 2 * 14f);
                }
                case GsFormationKind.Wings: {
                    int layer = slot / 2;
                    float side = slot % 2 == 0 ? -1f : 1f;
                    return new Vector2(side * (kit.Radius + layer * kit.Spacing), 4f);
                }
                case GsFormationKind.Triangle: {
                    float ang = kit.SectorAnchor + MathHelper.TwoPi * (slot % 3) / 3f;
                    float layer = 1f + slot / 3;
                    return ang.ToRotationVector2() * kit.Radius * layer;
                }
            }
            return Vector2.Zero;
        }

        /// <summary>六方向单位（平顶六边形）</summary>
        private static readonly Vector2[] hexDirs = [
            new(1f, 0f), new(0.5f, 0.866f), new(-0.5f, 0.866f),
            new(-1f, 0f), new(-0.5f, -0.866f), new(0.5f, -0.866f),
        ];

        /// <summary>六边形螺旋格点（slot 0 为中心，向外逐环，确定性）</summary>
        private static Vector2 HexCell(int slot) {
            if (slot <= 0) {
                return Vector2.Zero;
            }
            //定位所属环与环内序号
            int ring = 1;
            int passed = 1;
            while (passed + ring * 6 <= slot) {
                passed += ring * 6;
                ring++;
            }
            int within = slot - passed;
            int side = within / ring;
            int step = within % ring;
            Vector2 corner = hexDirs[side] * ring;
            Vector2 walk = hexDirs[(side + 2) % 6];
            return corner + walk * step;
        }

        //==================== 集结场与通用查询 ====================

        /// <summary>该形态的集结场是否在场（读缓存位掩码，防重复生成）</summary>
        internal static bool RallyFieldAlive(int owner, int stance)
            => (GetCache(owner).RallyFieldMask & (1 << stance)) != 0;

        /// <summary>找该玩家名下指定类型、距某点一定范围内的弹幕（增益区/标记罩查询）</summary>
        internal static Projectile FindOwnedProj(int owner, int type, Vector2 near, float radius) {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == owner && proj.type == type
                    && proj.Center.Distance(near) <= radius) {
                    return proj;
                }
            }
            return null;
        }

        //==================== 指挥官光环 ====================

        /// <summary>指挥官光环激活：模式开启且已用仆从槽 ≥3（各端本地按同条件判定，视觉确定性）</summary>
        internal static bool CommanderAuraActive(Player player)
            => GameModeSystem.GodSmithActive && player.slotsMinions >= AuraSlotNeed;

        //==================== 生命周期 ====================

        internal static void ClearAll() {
            kitByProjType = [];
            bannerByOwner = null;
            cacheByOwner = null;
            lastCommandTick = 0;
        }
    }

    /// <summary>
    /// owner 本地命中计数器（协同技触发窗口）。键 = npc.whoAmI，取用时校验类型与过期，
    /// 槽位复用即重置；distinct 跟踪至多两个不同来源弹幕（「≥2 只协同」谓词用）。<br/>
    /// 只允许在命中钩子（owner 端独占执行）路径消费，天然只属于本客户端的本地玩家
    /// </summary>
    internal sealed class GsHitTally
    {
        private struct Entry
        {
            public int NpcType;
            public int Count;
            public uint Expire;
            public int SrcA;
            public int SrcB;
        }

        private readonly Dictionary<int, Entry> map = [];

        /// <summary>记一次命中，返回窗口内累计数；distinctSources = 不同来源弹幕数（封顶 2）</summary>
        internal int Bump(NPC npc, Projectile src, int windowFrames, out int distinctSources) {
            uint now = Main.GameUpdateCount;
            if (!map.TryGetValue(npc.whoAmI, out Entry e)
                || e.NpcType != npc.type || e.Expire < now) {
                e = new Entry { NpcType = npc.type, SrcA = -1, SrcB = -1 };
            }
            e.Count++;
            int id = src?.identity ?? -1;
            if (e.SrcA < 0) {
                e.SrcA = id;
            }
            else if (e.SrcA != id && e.SrcB < 0) {
                e.SrcB = id;
            }
            e.Expire = now + (uint)windowFrames;
            map[npc.whoAmI] = e;
            distinctSources = (e.SrcA >= 0 ? 1 : 0) + (e.SrcB >= 0 ? 1 : 0);
            if (map.Count > 96) {
                Prune(now);
            }
            return e.Count;
        }

        /// <summary>无副作用读当前有效计数（ModifyHit 判增伤用）</summary>
        internal int Peek(NPC npc)
            => map.TryGetValue(npc.whoAmI, out Entry e)
                && e.NpcType == npc.type && e.Expire >= Main.GameUpdateCount ? e.Count : 0;

        /// <summary>协同触发后清窗</summary>
        internal void Reset(NPC npc) => map.Remove(npc.whoAmI);

        private void Prune(uint now) {
            List<int> stale = [];
            foreach (KeyValuePair<int, Entry> pair in map) {
                if (pair.Value.Expire < now) {
                    stale.Add(pair.Key);
                }
            }
            foreach (int key in stale) {
                map.Remove(key);
            }
        }
    }

    /// <summary>指挥官光环视觉：各端为每个满足条件的玩家本地绘制（确定性条件，无需同步）</summary>
    internal class GsMinionCommandPlayer : ModPlayer
    {
        public override void PostUpdate() {
            if (VaultUtils.isServer || !MinionDoctrine.CommanderAuraActive(Player)) {
                return;
            }
            Color hue = MinionDoctrine.CommandColor(Player.whoAmI);
            Lighting.AddLight(Player.Center, hue.ToVector3() * 0.16f);
            //环缘呼吸微光（每秒约 3 粒，whoAmI 定相去同相）
            if (Main.rand.NextBool(20)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float breathe = 1f + 0.06f * (float)Math.Sin(
                    Main.GlobalTimeWrappedHourly * 2.1f + Player.whoAmI * 1.7f);
                Vector2 at = Player.Center + ang.ToRotationVector2()
                    * (MinionDoctrine.AuraRadius * 0.96f * breathe);
                PRTLoader.NewParticle<PRT_Light>(at,
                    ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 0.6f,
                    hue, Main.rand.NextFloat(0.07f, 0.12f))?.Configure(16, 0.55f);
            }
        }
    }

    /// <summary>卸载期清理族内静态注册表</summary>
    internal class MinionDoctrineLoader : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => MinionDoctrine.ClearAll();
    }
}
