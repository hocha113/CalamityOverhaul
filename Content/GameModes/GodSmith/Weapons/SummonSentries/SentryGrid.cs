using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries
{
    /// <summary>哨兵族系序号，供 LinkMask 位运算与 kit 归属</summary>
    internal static class GsSentryFamilyIdx
    {
        public const int Houndius = 0;
        public const int QueenSpider = 1;
        public const int FrostHydra = 2;
        public const int Flameburst = 3;
        public const int Ballista = 4;
        public const int ExplosiveTrap = 5;
        public const int LightningAura = 6;
        public const int RainbowCrystal = 7;
        public const int LunarPortal = 8;
    }

    /// <summary>
    /// 每弹幕本地状态包（挂 <see cref="GodSmithProjRouter.LocalState"/>）。
    /// 各端各自一份不过线：Charge 等 owner 量只在 owner 端有真值，
    /// 图字段由各端从同一确定性输入重算，天然一致
    /// </summary>
    internal class GsSentryLocal
    {
        /// <summary>第一帧初始化已完成（塔=预充能，弹体=归属+超频判定）</summary>
        public bool SpawnHandled;

        //==================== 塔态 ====================

        /// <summary>充能计数（owner 本地权威，远端恒 0）</summary>
        public int Charge;
        /// <summary>超频续期帧：GsOverdriveProj 每帧刷，各端一致可读</summary>
        public uint OverdriveExpire;
        /// <summary>排气冷却：超频窗内与结束后 120 帧不积充能（owner）</summary>
        public uint ChargeVentUntil;
        /// <summary>殉爆参与节流：每座 90 帧至多一次（owner）</summary>
        public uint LastChainTick;
        /// <summary>组合技节流（火焰溅射/水晶折射等，owner）</summary>
        public uint LastComboTick;
        /// <summary>女王蜘蛛伴蛋计数（owner）</summary>
        public int EggCounter;
        /// <summary>链边数（图重算写，各端一致）</summary>
        public int LinkCount;
        /// <summary>邻接系位掩码 1&lt;&lt;FamilyIdx（图重算写）</summary>
        public int LinkMask;
        /// <summary>与月门枢纽成链（图重算写）</summary>
        public bool HubLinked;
        /// <summary>邻接塔 whoAmI 快照，10 帧内有效，使用前校验（图重算写）</summary>
        public List<int> LinkedTowers;

        //==================== 弹体态 ====================

        /// <summary>所属塔网络身份，-1 无归属</summary>
        public int HomeTowerIdentity = -1;
        /// <summary>所属塔类型</summary>
        public int HomeTowerType;
        /// <summary>所属塔本地槽快取（用前校验）</summary>
        public int HomeTowerWhoAmI = -1;
        /// <summary>出生于超频窗（各端按塔 OverdriveExpire 各自判定）</summary>
        public bool OverdriveShot;
    }

    /// <summary>一套哨兵 kit 的参数行（DD2 系三档共用一份，档位由塔类型下标区分）</summary>
    internal class SentryKit
    {
        /// <summary>回调宿主方案（类型通道注册者）</summary>
        public GsSentryScheme Host;
        /// <summary>系序号（GsSentryFamilyIdx）</summary>
        public int FamilyIdx;
        /// <summary>塔弹幕类型，下标即档位</summary>
        public int[] TowerTypes = [];
        /// <summary>弹体弹幕类型</summary>
        public int[] BoltTypes = [];
        /// <summary>按档位的充能阈值</summary>
        public int[] ChargeMax = [];
        /// <summary>超频持续帧</summary>
        public int OverdriveDuration;
        /// <summary>满充自动超频（爆炸陷阱）</summary>
        public bool AutoOverdrive;
        /// <summary>按弹体消亡而非命中计充能（爆炸陷阱按爆炸次数）</summary>
        public bool ChargeOnBoltKill;

        /// <summary>塔类型 → 档位下标，-1 表示不是本系塔</summary>
        public int TierOf(int towerType) => Array.IndexOf(TowerTypes, towerType);

        public int ChargeMaxOf(int tier) => ChargeMax[Math.Clamp(tier, 0, ChargeMax.Length - 1)];
    }

    /// <summary>
    /// 哨兵族「阵地工程」共享框架：联动图重算、充能记账、超频调度、敌怪标记表。<br/>
    /// 联动图各端每 10 帧从场上弹幕确定性重算（不发包）；充能是 owner 本地权威，
    /// owner 端画全量辉光，远端只在超频触发时看到 GsOverdriveProj 真弹幕（表现拆分刻意为之）。<br/>
    /// 模式关闭：路由闸门停发一切回调，本类也停止重算并清标记，在场哨兵即刻回原版
    /// </summary>
    internal class SentryGrid : ModSystem
    {
        //==================== kit 注册表（加载期填充，只读消费） ====================

        private static readonly Dictionary<int, SentryKit> kitByTower = [];
        private static readonly Dictionary<int, SentryKit> kitByBolt = [];

        internal static void RegisterKit(SentryKit kit) {
            foreach (int type in kit.TowerTypes) {
                kitByTower[type] = kit;
            }
            foreach (int type in kit.BoltTypes) {
                kitByBolt[type] = kit;
            }
        }

        /// <summary>按弹幕类型查 kit；isTower 区分塔与弹体</summary>
        internal static bool TryGetKit(int projType, out SentryKit kit, out bool isTower) {
            if (kitByTower.TryGetValue(projType, out kit)) {
                isTower = true;
                return true;
            }
            isTower = false;
            return kitByBolt.TryGetValue(projType, out kit);
        }

        internal static bool TryGetTowerKit(int projType, out SentryKit kit)
            => kitByTower.TryGetValue(projType, out kit);

        //==================== 敌怪标记表（owner 本地量，键含代际防槽位复用） ====================

        private static readonly Dictionary<NetworkNPCIdentity, uint> exposeUntil = [];
        private static readonly Dictionary<NetworkNPCIdentity, uint> shockUntil = [];
        private static readonly Dictionary<NetworkNPCIdentity, uint> lastBoltHitTick = [];
        private static readonly Dictionary<NetworkNPCIdentity, uint> crossCdUntil = [];
        private static readonly List<NetworkNPCIdentity> expiredKeys = [];

        /// <summary>手动超频右键节流（本客户端量）</summary>
        private static uint manualUseCd;

        private static readonly List<Projectile> towerCache = [];

        /// <summary>孤儿兜底，防路由缺失时空引用（正常路径不会走到）</summary>
        private static readonly GsSentryLocal orphanState = new();

        public override void Unload() {
            kitByTower.Clear();
            kitByBolt.Clear();
            ClearMarks();
            towerCache.Clear();
        }

        public override void OnWorldUnload() => ClearMarks();

        private static void ClearMarks() {
            exposeUntil.Clear();
            shockUntil.Clear();
            lastBoltHitTick.Clear();
            crossCdUntil.Clear();
        }

        //==================== 每帧驱动：图重算 + 标记清扫 ====================

        public override void PostUpdateProjectiles() {
            if (!GameModeSystem.GodSmithActive) {
                if (exposeUntil.Count + shockUntil.Count + lastBoltHitTick.Count + crossCdUntil.Count > 0) {
                    ClearMarks();
                }
                return;
            }
            if (Main.GameUpdateCount % 10 != 0) {
                return;
            }
            RebuildGraph();
            Sweep(exposeUntil);
            Sweep(shockUntil);
            Sweep(lastBoltHitTick);
            Sweep(crossCdUntil);
        }

        /// <summary>
        /// 联动图重算：收集全部在编塔，同 owner 且相距 ≤480px 即成链。
        /// 输入是各端可见的同步弹幕，按确定性规则写回各塔 LocalState，不发包
        /// </summary>
        private static void RebuildGraph() {
            towerCache.Clear();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (kitByTower.ContainsKey(proj.type)) {
                    towerCache.Add(proj);
                }
            }
            for (int i = 0; i < towerCache.Count; i++) {
                GsSentryLocal st = StateOf(towerCache[i]);
                st.LinkCount = 0;
                st.LinkMask = 0;
                st.HubLinked = false;
                (st.LinkedTowers ??= []).Clear();
            }
            for (int i = 0; i < towerCache.Count; i++) {
                Projectile a = towerCache[i];
                GsSentryLocal sa = StateOf(a);
                for (int j = i + 1; j < towerCache.Count; j++) {
                    Projectile b = towerCache[j];
                    if (a.owner != b.owner || a.Center.Distance(b.Center) > 480f) {
                        continue;
                    }
                    GsSentryLocal sb = StateOf(b);
                    sa.LinkCount++;
                    sb.LinkCount++;
                    sa.LinkedTowers.Add(b.whoAmI);
                    sb.LinkedTowers.Add(a.whoAmI);
                    sa.LinkMask |= 1 << kitByTower[b.type].FamilyIdx;
                    sb.LinkMask |= 1 << kitByTower[a.type].FamilyIdx;
                    if (b.type == ProjectileID.MoonlordTurret) {
                        sa.HubLinked = true;
                    }
                    if (a.type == ProjectileID.MoonlordTurret) {
                        sb.HubLinked = true;
                    }
                }
            }
        }

        /// <summary>清扫过期标记，键列表复用零反复分配</summary>
        private static void Sweep(Dictionary<NetworkNPCIdentity, uint> table) {
            if (table.Count == 0) {
                return;
            }
            expiredKeys.Clear();
            uint now = Main.GameUpdateCount;
            foreach (KeyValuePair<NetworkNPCIdentity, uint> pair in table) {
                if (pair.Value <= now) {
                    expiredKeys.Add(pair.Key);
                }
            }
            foreach (NetworkNPCIdentity key in expiredKeys) {
                table.Remove(key);
            }
        }

        //==================== 状态与归属 ====================

        internal static GsSentryLocal StateOf(Projectile proj)
            => proj.TryGetGlobalProjectile(out GodSmithProjRouter router)
                ? router.GetOrCreateState<GsSentryLocal>()
                : orphanState;

        internal static bool IsOverdriven(GsSentryLocal st) => st.OverdriveExpire > Main.GameUpdateCount;

        /// <summary>弹体出生时的归属塔：全场最近的同 owner 同系塔（哨兵驻场固定，出生点即炮口）</summary>
        internal static Projectile FindHomeTower(Projectile bolt, SentryKit kit) {
            Projectile best = null;
            float bestDist = float.MaxValue;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != bolt.owner || kit.TierOf(proj.type) < 0) {
                    continue;
                }
                float dist = proj.Center.Distance(bolt.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = proj;
                }
            }
            return best;
        }

        /// <summary>按弹体状态解析所属塔（快取校验 + identity 重扫），塔亡返回 null</summary>
        internal static Projectile ResolveHomeTower(Projectile bolt, GsSentryLocal st) {
            if (st.HomeTowerIdentity < 0) {
                return null;
            }
            if (st.HomeTowerWhoAmI >= 0 && st.HomeTowerWhoAmI < Main.maxProjectiles) {
                Projectile cached = Main.projectile[st.HomeTowerWhoAmI];
                if (cached.active && cached.identity == st.HomeTowerIdentity
                    && cached.type == st.HomeTowerType && cached.owner == bolt.owner) {
                    return cached;
                }
            }
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == bolt.owner && proj.identity == st.HomeTowerIdentity
                    && proj.type == st.HomeTowerType) {
                    st.HomeTowerWhoAmI = proj.whoAmI;
                    return proj;
                }
            }
            return null;
        }

        /// <summary>owner 端补发弹的出生登记：跳过第一帧初始化并继承归属与超频态，防补发递归</summary>
        internal static void MarkSpawnHandled(Projectile spawned, Projectile tower, bool overdriveShot) {
            GsSentryLocal st = StateOf(spawned);
            st.SpawnHandled = true;
            st.OverdriveShot = overdriveShot;
            if (tower != null) {
                st.HomeTowerIdentity = tower.identity;
                st.HomeTowerType = tower.type;
                st.HomeTowerWhoAmI = tower.whoAmI;
            }
        }

        //==================== 充能与超频 ====================

        /// <summary>
        /// 充能记账（只在 owner 端调用生效）。满充：手动系亮就绪提示，AutoOverdrive 系直接触发。
        /// 排气冷却期（超频窗+120 帧）不积能，防高频命中系超频常驻
        /// </summary>
        internal static void AddCharge(Projectile tower, SentryKit kit, int amount) {
            if (!tower.IsOwnedByLocalPlayer()) {
                return;
            }
            GsSentryLocal st = StateOf(tower);
            uint now = Main.GameUpdateCount;
            if (st.ChargeVentUntil > now) {
                return;
            }
            int max = kit.ChargeMaxOf(kit.TierOf(tower.type));
            bool wasFull = st.Charge >= max;
            st.Charge = Math.Min(st.Charge + amount, max);
            if (st.Charge < max || wasFull) {
                return;
            }
            if (kit.AutoOverdrive) {
                TriggerOverdrive(tower, kit);
                return;
            }
            //满充里程碑：owner 本地读数（个人提示，远端只看超频真弹幕）
            if (!VaultUtils.isServer && tower.owner == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.7f, Pitch = 0.4f }, tower.Center);
                CombatText.NewText(tower.Hitbox, GameModeTheme.GodSmithEmber, GsOverdriveProj.ChargeReadyText.Value);
            }
        }

        /// <summary>触发超频：清充能、开排气窗、owner 生成全端可见的超频光环弹幕</summary>
        internal static void TriggerOverdrive(Projectile tower, SentryKit kit) {
            if (!tower.IsOwnedByLocalPlayer()) {
                return;
            }
            GsSentryLocal st = StateOf(tower);
            st.Charge = 0;
            st.ChargeVentUntil = Main.GameUpdateCount + (uint)kit.OverdriveDuration + 120;
            //初始状态全走 NewProjectile 形参：生成包先于一切后续赋值发出
            Projectile.NewProjectile(Main.player[tower.owner].GetSource_Misc("GsSentry"),
                tower.Center, Vector2.Zero, ModContent.ProjectileType<GsOverdriveProj>(),
                0, 0f, tower.owner, tower.identity, tower.type, kit.OverdriveDuration);
        }

        /// <summary>右键手动超频：光标 90px 内待超频哨兵优先，否则最近待超频；30 帧节流</summary>
        internal static void TryManualOverdrive(Player player, int familyIdx) {
            uint now = Main.GameUpdateCount;
            if (manualUseCd > now) {
                return;
            }
            manualUseCd = now + 30;
            Projectile cursorPick = null, nearPick = null;
            SentryKit cursorKit = null, nearKit = null;
            float cursorDist = 90f, nearDist = float.MaxValue;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI || !kitByTower.TryGetValue(proj.type, out SentryKit kit)
                    || kit.FamilyIdx != familyIdx) {
                    continue;
                }
                GsSentryLocal st = StateOf(proj);
                if (IsOverdriven(st) || st.Charge < kit.ChargeMaxOf(kit.TierOf(proj.type))) {
                    continue;
                }
                float toCursor = proj.Center.Distance(Main.MouseWorld);
                if (toCursor <= cursorDist) {
                    cursorDist = toCursor;
                    cursorPick = proj;
                    cursorKit = kit;
                }
                float toPlayer = proj.Center.Distance(player.Center);
                if (toPlayer < nearDist) {
                    nearDist = toPlayer;
                    nearPick = proj;
                    nearKit = kit;
                }
            }
            Projectile target = cursorPick ?? nearPick;
            SentryKit pickedKit = cursorPick != null ? cursorKit : nearKit;
            if (target == null) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.4f });
                CombatText.NewText(player.Hitbox, Color.Gray, GsOverdriveProj.NotReadyText.Value);
                return;
            }
            TriggerOverdrive(target, pickedKit);
        }

        //==================== 殉爆网（爆炸陷阱） ====================

        /// <summary>
        /// 殉爆传播：从爆点找 ≤200px 内、90 帧未参与过的自家陷阱塔，在其位置生成殉爆弹
        /// （15 帧引信后起爆）。链深从 1 数起封顶 4；只在 owner 端调用，不碰原版陷阱 AI 与冷却
        /// </summary>
        internal static void PropagateChain(Vector2 from, int owner, int depth, int damage) {
            if (depth > 4 || damage <= 0) {
                return;
            }
            uint now = Main.GameUpdateCount;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != owner || !kitByTower.TryGetValue(proj.type, out SentryKit kit)
                    || kit.FamilyIdx != GsSentryFamilyIdx.ExplosiveTrap) {
                    continue;
                }
                if (proj.Center.Distance(from) > 200f) {
                    continue;
                }
                GsSentryLocal st = StateOf(proj);
                if (st.LastChainTick + 90 > now && st.LastChainTick != 0) {
                    continue;
                }
                st.LastChainTick = now;
                Projectile.NewProjectile(Main.player[owner].GetSource_Misc("GsSentry"),
                    proj.Center, Vector2.Zero,
                    ModContent.ProjectileType<Projectiles.GsSentryChainBlastProj>(),
                    damage, 6f, owner, depth, 90f);
            }
        }

        /// <summary>陷阱右键：手动提前引爆一轮殉爆检查（源塔自吃一发，邻雷跟进），无需充能</summary>
        internal static void TryManualChainCheck(Player player) {
            uint now = Main.GameUpdateCount;
            if (manualUseCd > now) {
                return;
            }
            manualUseCd = now + 30;
            Projectile pick = null;
            float bestDist = float.MaxValue;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI || !kitByTower.TryGetValue(proj.type, out SentryKit kit)
                    || kit.FamilyIdx != GsSentryFamilyIdx.ExplosiveTrap) {
                    continue;
                }
                float toCursor = proj.Center.Distance(Main.MouseWorld);
                if (toCursor < bestDist) {
                    bestDist = toCursor;
                    pick = proj;
                }
            }
            if (pick == null || StateOf(pick).LastChainTick + 90 > now && StateOf(pick).LastChainTick != 0) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.4f });
                CombatText.NewText(player.Hitbox, Color.Gray, GsOverdriveProj.NotReadyText.Value);
                return;
            }
            //不预写源塔节流：传播会选中源塔自身（距离 0），手动引爆自带本体演出
            PropagateChain(pick.Center, player.whoAmI, 1, (int)(pick.damage * 0.8f));
        }

        //==================== 敌怪标记（曝光/感电/交叉火力，owner 本地） ====================

        private static void Mark(Dictionary<NetworkNPCIdentity, uint> table, NPC npc, int frames) {
            if (NetworkNPCIdentity.TryCapture(npc, out NetworkNPCIdentity id)) {
                table[id] = Main.GameUpdateCount + (uint)frames;
            }
        }

        private static bool IsMarked(Dictionary<NetworkNPCIdentity, uint> table, NPC npc)
            => table.Count > 0 && NetworkNPCIdentity.TryCapture(npc, out NetworkNPCIdentity id)
                && table.TryGetValue(id, out uint until) && until > Main.GameUpdateCount;

        /// <summary>眼犬曝光：命中挂 90 帧，链内哨兵对其 +10%</summary>
        internal static void MarkExposed(NPC npc) => Mark(exposeUntil, npc, 90);

        internal static bool IsExposed(NPC npc) => IsMarked(exposeUntil, npc);

        /// <summary>光环感电：过载力场 tick 挂 90 帧，链内其他哨兵对其 +10%</summary>
        internal static void MarkShocked(NPC npc) => Mark(shockUntil, npc, 90);

        internal static bool IsShocked(NPC npc) => IsMarked(shockUntil, npc);

        /// <summary>
        /// 弩炮交叉火力判定：45 帧内第二发弩矢命中同目标即就绪，触发后该目标 90 帧冷却。
        /// 只在 owner 命中路径调用
        /// </summary>
        internal static bool CrossFireReady(NPC target) {
            if (!NetworkNPCIdentity.TryCapture(target, out NetworkNPCIdentity id)) {
                return false;
            }
            uint now = Main.GameUpdateCount;
            if (crossCdUntil.TryGetValue(id, out uint cd) && cd > now) {
                return false;
            }
            bool ready = lastBoltHitTick.TryGetValue(id, out uint last) && now - last <= 45;
            lastBoltHitTick[id] = now;
            if (ready) {
                crossCdUntil[id] = now + 90;
                lastBoltHitTick.Remove(id);
            }
            return ready;
        }

        //==================== 命中加成汇总（owner 端 ModifyHit 统一入口） ====================

        /// <summary>
        /// 链边 +4%/条（至 3 条）+ 月门枢纽 +8%，加法段封顶 +20%；
        /// 曝光/感电各自 ×1.10（要求本塔与标记源系成链）
        /// </summary>
        internal static void ApplySentryHitBonus(Projectile tower, NPC target, ref NPC.HitModifiers modifiers) {
            GsSentryLocal st = StateOf(tower);
            float add = 0.04f * Math.Min(st.LinkCount, 3);
            if (st.HubLinked) {
                add += 0.08f;
            }
            float mult = 1f + Math.Min(add, 0.20f);
            if ((st.LinkMask & 1 << GsSentryFamilyIdx.Houndius) != 0 && IsExposed(target)) {
                mult *= 1.10f;
            }
            if ((st.LinkMask & 1 << GsSentryFamilyIdx.LightningAura) != 0 && IsShocked(target)) {
                mult *= 1.10f;
            }
            if (mult > 1f) {
                modifiers.FinalDamage *= mult;
            }
        }

        /// <summary>
        /// 击杀里程碑（owner 端命中后调用）：曝光目标被链内哨兵击杀时，
        /// 最近成链眼犬免费充能 +3（组合技回馈）
        /// </summary>
        internal static void NotifySentryKill(Projectile tower, NPC target) {
            if (tower == null || target.life > 0) {
                return;
            }
            GsSentryLocal st = StateOf(tower);
            if ((st.LinkMask & 1 << GsSentryFamilyIdx.Houndius) == 0 || st.LinkedTowers == null || !IsExposed(target)) {
                return;
            }
            Projectile bestEye = null;
            float bestDist = float.MaxValue;
            foreach (int who in st.LinkedTowers) {
                if (who < 0 || who >= Main.maxProjectiles) {
                    continue;
                }
                Projectile other = Main.projectile[who];
                if (!other.active || other.owner != tower.owner || other.type != ProjectileID.HoundiusShootius) {
                    continue;
                }
                float dist = other.Center.Distance(tower.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    bestEye = other;
                }
            }
            if (bestEye != null && TryGetTowerKit(bestEye.type, out SentryKit eyeKit)) {
                AddCharge(bestEye, eyeKit, 3);
            }
        }

        //==================== 族共享视觉（链线 + 充能辉光） ====================

        /// <summary>
        /// 链线：小 identity 端向大 identity 端画一次，LightShot 拉伸 + 端点微光。
        /// 各端由同一确定性图重画；月门参与的链染月青。呼吸相位用 identity 哈希去同相
        /// </summary>
        internal static void DrawTowerLinks(Projectile tower, GsSentryLocal st) {
            if (st.LinkedTowers == null || st.LinkedTowers.Count == 0) {
                return;
            }
            Texture2D line = CWRAsset.LightShot?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (line == null || glow == null) {
                return;
            }
            foreach (int who in st.LinkedTowers) {
                if (who < 0 || who >= Main.maxProjectiles) {
                    continue;
                }
                Projectile other = Main.projectile[who];
                if (!other.active || other.owner != tower.owner
                    || !kitByTower.ContainsKey(other.type) || other.identity <= tower.identity) {
                    continue;
                }
                bool lunar = tower.type == ProjectileID.MoonlordTurret || other.type == ProjectileID.MoonlordTurret;
                Color tint = lunar ? new Color(120, 190, 235) : GameModeTheme.GodSmithAccent;
                float pulse = 0.55f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f
                    + (tower.identity * 0.83f + other.identity * 0.47f) % 6.28f);
                Color c = tint * (0.34f * pulse);
                c.A = 0;
                Vector2 span = other.Center - tower.Center;
                float len = span.Length();
                Main.EntitySpriteDraw(line, tower.Center - Main.screenPosition, null, c,
                    span.ToRotation(), new Vector2(0f, line.Height * 0.5f),
                    new Vector2(len / line.Width, 6f / line.Height), SpriteEffects.None, 0);
                Color end = tint * (0.5f * pulse);
                end.A = 0;
                Main.EntitySpriteDraw(glow, tower.Center - Main.screenPosition, null, end, 0f,
                    glow.Size() * 0.5f, 0.22f, SpriteEffects.None, 0);
            }
        }

        /// <summary>充能辉光：owner 全量（远端无数据不画，里程碑另由超频弹幕承载）</summary>
        internal static void DrawTowerCharge(Projectile tower, SentryKit kit, GsSentryLocal st) {
            if (tower.owner != Main.myPlayer || IsOverdriven(st)) {
                return;
            }
            int max = kit.ChargeMaxOf(kit.TierOf(tower.type));
            float ratio = Math.Clamp(st.Charge / (float)max, 0f, 1f);
            if (ratio <= 0.01f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            bool full = st.Charge >= max;
            //满充呼吸脉动，未满恒稳弱光；相位按 identity 去同相
            float pulse = full
                ? 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + tower.identity * 0.91f)
                : 1f;
            Color c = Color.Lerp(GameModeTheme.GodSmithAccent, GameModeTheme.GodSmithEmber, ratio)
                * ((0.10f + 0.24f * ratio) * pulse);
            c.A = 0;
            Main.EntitySpriteDraw(glow, tower.Center - Main.screenPosition, null, c, 0f,
                glow.Size() * 0.5f, tower.width / 34f + 0.9f, SpriteEffects.None, 0);
        }

        /// <summary>满充待机粒子：owner 端每 20 帧一粒上升光屑（TowerPostAI 调）</summary>
        internal static void EmitFullChargeIdle(Projectile tower, SentryKit kit, GsSentryLocal st) {
            if (VaultUtils.isServer || tower.owner != Main.myPlayer || IsOverdriven(st)) {
                return;
            }
            if (st.Charge < kit.ChargeMaxOf(kit.TierOf(tower.type)) || Main.GameUpdateCount % 20 != 0) {
                return;
            }
            PRTLoader.NewParticle<PRT_Light>(
                tower.Center + Main.rand.NextVector2Circular(tower.width * 0.4f, 8f),
                new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.1f)),
                GameModeTheme.GodSmithEmber, Main.rand.NextFloat(0.08f, 0.13f))?.Configure(16, 0.75f);
        }
    }
}
