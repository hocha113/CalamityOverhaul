using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Schemes
{
    /// <summary>
    /// 哨兵族方案基类：把类型通道回调统一翻译成「塔/弹体 + kit + 状态包」语义。<br/>
    /// 增强全部走按弹幕类型注册的通道（不依赖源物品打标），模式关闭当帧停发全部加法层；
    /// 不 override 原版哨兵 SetDefaults、不占用原版 ai[]，逐弹幕状态放 LocalState。<br/>
    /// DD2 三档系：仅 kit 宿主档（T1）实现 <see cref="BuildKit"/> 返回非 null 并注册通道，
    /// 其余档返回 null 只保留物品面（tooltip/右键/数值行），档位差异在虚点内按 tier 分支
    /// </summary>
    internal abstract class GsSentryScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "SummonSentries";

        /// <summary>本系序号（GsSentryFamilyIdx）</summary>
        protected abstract int FamilyIdx { get; }

        /// <summary>构造本系 kit；非宿主档返回 null（DD2 的 T2/T3）</summary>
        protected abstract SentryKit BuildKit();

        public sealed override void GsSetStaticDefaults() {
            SentryKit kit = BuildKit();
            if (kit == null) {
                return;
            }
            kit.Host = this;
            kit.FamilyIdx = FamilyIdx;
            SentryGrid.RegisterKit(kit);
            GsRegisterProjChannel([.. kit.TowerTypes, .. kit.BoltTypes]);
        }

        //==================== 右键 = 手动超频（各端压掉使用，owner 端判定） ====================

        public sealed override bool? GsAltFunctionUse(Item item, Player player) => true;

        public sealed override bool? GsCanUseItem(Item item, Player player) {
            if (player.altFunctionUse != 2) {
                return null;
            }
            //右键分支：owner 守门做超频判定；全端返回 false 压掉本次使用（不放哨兵不耗魔）
            if (player.whoAmI == Main.myPlayer) {
                HandleRightClick(player);
            }
            return false;
        }

        /// <summary>右键行为，默认手动超频；爆炸陷阱改为手动引爆殉爆检查</summary>
        protected virtual void HandleRightClick(Player player)
            => SentryGrid.TryManualOverdrive(player, FamilyIdx);

        //==================== 类型通道回调统一翻译 ====================

        public sealed override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (!SentryGrid.TryGetKit(proj.type, out SentryKit kit, out bool isTower)) {
                return;
            }
            GsSentryLocal st = router.GetOrCreateState<GsSentryLocal>();
            if (!st.SpawnHandled) {
                st.SpawnHandled = true;
                FirstFrame(proj, kit, isTower, st);
            }
            if (isTower) {
                SentryGrid.EmitFullChargeIdle(proj, kit, st);
                TowerPostAI(proj, kit, st);
            }
            else {
                BoltPostAI(proj, kit, st);
            }
        }

        /// <summary>
        /// 第一帧初始化：塔 = owner 预充能 25%（快速布防补偿，充能随实例走不存档）；
        /// 弹体 = 各端记归属塔并判定超频窗，owner 端执行出膛升格（改动 netUpdate 随包同步）
        /// </summary>
        private void FirstFrame(Projectile proj, SentryKit kit, bool isTower, GsSentryLocal st) {
            if (isTower) {
                if (proj.IsOwnedByLocalPlayer()) {
                    st.Charge = kit.ChargeMaxOf(kit.TierOf(proj.type)) / 4;
                }
                return;
            }
            Projectile tower = SentryGrid.FindHomeTower(proj, kit);
            if (tower == null) {
                return;
            }
            st.HomeTowerIdentity = tower.identity;
            st.HomeTowerType = tower.type;
            st.HomeTowerWhoAmI = tower.whoAmI;
            st.OverdriveShot = SentryGrid.IsOverdriven(SentryGrid.StateOf(tower));
            if (st.OverdriveShot && proj.IsOwnedByLocalPlayer()) {
                OnOverdriveBoltSpawn(proj, tower, kit.TierOf(tower.type));
                proj.netUpdate = true;
            }
            OnBoltFirstFrame(proj, tower, st);
        }

        public sealed override void GsProjModifyHitNPC(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (!SentryGrid.TryGetKit(proj.type, out SentryKit kit, out bool isTower)) {
                return;
            }
            GsSentryLocal st = router.GetOrCreateState<GsSentryLocal>();
            Projectile tower = isTower ? proj : SentryGrid.ResolveHomeTower(proj, st);
            if (tower == null) {
                return;
            }
            SentryGrid.ApplySentryHitBonus(tower, target, ref modifiers);
            ModifySentryHit(proj, tower, target, ref modifiers, st);
        }

        public sealed override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //命中钩子只在攻击方端执行，天然 owner 路径
            if (!SentryGrid.TryGetKit(proj.type, out SentryKit kit, out bool isTower)) {
                return;
            }
            GsSentryLocal st = router.GetOrCreateState<GsSentryLocal>();
            Projectile tower = isTower ? proj : SentryGrid.ResolveHomeTower(proj, st);
            if (tower != null) {
                if (!kit.ChargeOnBoltKill) {
                    SentryGrid.AddCharge(tower, kit, 1);
                }
                //九头蛇联动：与其成链的哨兵命中附霜（原版减益骑原版同步）
                if ((SentryGrid.StateOf(tower).LinkMask & 1 << GsSentryFamilyIdx.FrostHydra) != 0) {
                    target.AddBuff(Terraria.ID.BuffID.Frostburn, 120);
                }
                SentryGrid.NotifySentryKill(tower, target);
            }
            OnSentryHit(proj, tower, target, hit, damageDone, st);
        }

        public sealed override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //OnKill 各端都跑，权威副作用守 owner
            if (!proj.IsOwnedByLocalPlayer()
                || !SentryGrid.TryGetKit(proj.type, out SentryKit kit, out bool isTower) || isTower) {
                return;
            }
            GsSentryLocal st = router.GetOrCreateState<GsSentryLocal>();
            Projectile tower = SentryGrid.ResolveHomeTower(proj, st);
            if (kit.ChargeOnBoltKill && tower != null) {
                SentryGrid.AddCharge(tower, kit, 1);
            }
            OnBoltKilled(proj, tower, st);
        }

        public sealed override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            if (!SentryGrid.TryGetKit(proj.type, out SentryKit kit, out bool isTower)) {
                return;
            }
            GsSentryLocal st = router.GetOrCreateState<GsSentryLocal>();
            if (isTower) {
                SentryGrid.DrawTowerLinks(proj, st);
                SentryGrid.DrawTowerCharge(proj, kit, st);
                DrawTowerExtra(proj, kit, st, lightColor);
            }
            else {
                DrawBoltExtra(proj, kit, st, lightColor);
            }
        }

        //==================== 子类虚点 ====================

        /// <summary>超频窗内弹体出膛升格（owner 生成端；补发用 SpawnBoltHandled 防递归）</summary>
        protected virtual void OnOverdriveBoltSpawn(Projectile bolt, Projectile tower, int tier) { }

        /// <summary>弹体第一帧（各端；归属已解析）</summary>
        protected virtual void OnBoltFirstFrame(Projectile bolt, Projectile tower, GsSentryLocal st) { }

        /// <summary>塔每帧后置（原版 AI 之后；粒子守 !VaultUtils.isServer）</summary>
        protected virtual void TowerPostAI(Projectile tower, SentryKit kit, GsSentryLocal st) { }

        /// <summary>弹体每帧后置</summary>
        protected virtual void BoltPostAI(Projectile bolt, SentryKit kit, GsSentryLocal st) { }

        /// <summary>命中附加效果（owner 端；tower 可能为 null）</summary>
        protected virtual void OnSentryHit(Projectile proj, Projectile tower, NPC target,
            NPC.HitInfo hit, int damageDone, GsSentryLocal st) { }

        /// <summary>命中伤害额外乘区（owner 端；链边/曝光/感电已由框架先行套用）</summary>
        protected virtual void ModifySentryHit(Projectile proj, Projectile tower, NPC target,
            ref NPC.HitModifiers modifiers, GsSentryLocal st) { }

        /// <summary>弹体消亡（owner 端；殉爆链入口）</summary>
        protected virtual void OnBoltKilled(Projectile bolt, Projectile tower, GsSentryLocal st) { }

        /// <summary>塔追加绘制（副头/背门等；充能辉光与链线已由框架画好）</summary>
        protected virtual void DrawTowerExtra(Projectile tower, SentryKit kit, GsSentryLocal st, Color lightColor) { }

        /// <summary>弹体追加绘制（超频弹特效等）</summary>
        protected virtual void DrawBoltExtra(Projectile bolt, SentryKit kit, GsSentryLocal st, Color lightColor) { }

        /// <summary>超频周期技（GsOverdriveProj 每帧驱动，owner 端；age 从 1 数起）</summary>
        internal virtual void OverdrivePulse(Projectile tower, Projectile odProj, int age) { }

        //==================== 族内小工具 ====================

        /// <summary>衍生弹统一出生源（Misc 源不入承签链，防打标通道串扰）</summary>
        protected static IEntitySource SentrySource(Projectile anchor)
            => Main.player[anchor.owner].GetSource_Misc("GsSentry");

        /// <summary>owner 端补发原版类型弹并登记（跳过第一帧升格防递归，继承归属与超频视觉态）</summary>
        protected static Projectile SpawnBoltHandled(Projectile tower, Vector2 pos, Vector2 vel,
            int type, int damage, float knockback, bool overdriveShot = true) {
            int idx = Projectile.NewProjectile(SentrySource(tower), pos, vel, type,
                damage, knockback, tower.owner);
            if (idx < 0 || idx >= Main.maxProjectiles) {
                return null;
            }
            Projectile spawned = Main.projectile[idx];
            SentryGrid.MarkSpawnHandled(spawned, tower, overdriveShot);
            return spawned;
        }
    }
}
