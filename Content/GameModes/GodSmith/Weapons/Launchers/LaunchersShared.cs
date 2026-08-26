using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 发射器族方案基类。族级三轴：弹药定制、引爆时机、区域压制。<br/>
    /// 右键统一范式：<see cref="GsAltFunctionUse"/> 放行右键，使用链在
    /// <see cref="GsCanUseItem"/> 的 altFunctionUse==2 分支被截断（返回 false），
    /// 因此右键永不进入 use 流程：不耗弹、不播使用动画、零网络包；
    /// 动作本体只在 myPlayer 端执行，冷却由 <see cref="GsLaunchersPlayer.altCooldown"/> 把门。<br/>
    /// 引爆总架构：不替换原版火箭弹幕，owner 遍历自己已打标的弹幕走
    /// <see cref="GsDetonate"/>。爆炸类压 timeLeft=3 进原版爆窗（这正是原版碰撞起爆的
    /// 原生入口，Resize 判伤、置液、撒子雷全部保真），非爆炸类直接 Kill；
    /// 弹幕死亡由 tML 原生同步，联机零自建包
    /// </summary>
    internal abstract class GsLauncherScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "Launchers";

        /// <summary>右键动作冷却（tick）</summary>
        public virtual int AltActionCooldown => 30;

        /// <summary>本族武器一律放行右键</summary>
        public override bool? GsAltFunctionUse(Item item, Player player) => true;

        /// <summary>
        /// 右键分流已封死：altFunctionUse==2 时在 myPlayer 端执行 <see cref="OnAltAction"/>
        /// 并全端返回 false 压掉本次使用；左键分支走 <see cref="GsLeftCanUse"/>
        /// </summary>
        public sealed override bool? GsCanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (player.whoAmI == Main.myPlayer) {
                    GsLaunchersPlayer mp = player.GetModPlayer<GsLaunchersPlayer>();
                    if (mp.altCooldown <= 0) {
                        mp.altCooldown = AltActionCooldown;
                        OnAltAction(item, player, mp);
                    }
                }
                return false;
            }
            return GsLeftCanUse(item, player);
        }

        /// <summary>右键动作本体，只在 myPlayer 端被调用，冷却已由基类扣过</summary>
        protected virtual void OnAltAction(Item item, Player player, GsLaunchersPlayer mp) { }

        /// <summary>左键使用许可（替代 GsCanUseItem 的子类入口）</summary>
        protected virtual bool? GsLeftCanUse(Item item, Player player) => null;

        /// <summary>
        /// 子弹幕承签默认处理：第二打标槽在本族是「主弹私旗」（遥控旗/弹药 ID/嵌入态等），
        /// 子弹幕一律清零防旗号误继承。子类覆写做弹片增强时先调用 base
        /// </summary>
        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) => router.MarkData2 = 0f;

        //==================== 引爆帮手 ====================

        /// <summary>
        /// 单弹引爆。爆炸类（aiStyle 16 或 Explosive 集）压 timeLeft=3 进原版爆窗：
        /// Resize 判伤、爆炸视觉、置液、集束撒子雷全走原版路径；速度清零并 netUpdate，
        /// 保证远端同步到定点后再收死亡包，爆点各端一致。非爆炸类直接 Kill 走死亡路径
        /// </summary>
        internal static void GsDetonate(Projectile proj) {
            if (proj.aiStyle == ProjAIStyleID.Explosive || ProjectileID.Sets.Explosive[proj.type]) {
                if (proj.timeLeft > 3) {
                    proj.velocity = Vector2.Zero;
                    proj.timeLeft = 3;
                    proj.netUpdate = true;
                }
                return;
            }
            proj.Kill();
        }

        /// <summary>
        /// 遍历该玩家名下被本方案打标的弹幕执行引爆。只应在 myPlayer 路径调用。
        /// filter 过滤目标，before 在引爆前回调（打旗等），返回引爆数量
        /// </summary>
        protected int DetonateMarked(Player player,
            Func<Projectile, GodSmithProjRouter, bool> filter = null,
            Action<Projectile, GodSmithProjRouter> before = null) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI
                    || !proj.TryGetGlobalProjectile(out GodSmithProjRouter router)
                    || router.MarkScheme != this) {
                    continue;
                }
                if (filter != null && !filter(proj, router)) {
                    continue;
                }
                before?.Invoke(proj, router);
                GsDetonate(proj);
                count++;
            }
            return count;
        }

        /// <summary>数一遍该玩家名下被本方案打标且满足条件的弹幕</summary>
        protected int CountMarked(Player player, Func<Projectile, GodSmithProjRouter, bool> filter = null) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI
                    || !proj.TryGetGlobalProjectile(out GodSmithProjRouter router)
                    || router.MarkScheme != this) {
                    continue;
                }
                if (filter == null || filter(proj, router)) {
                    count++;
                }
            }
            return count;
        }

        //==================== 出手与爆点演出 ====================

        /// <summary>
        /// 发射器出手统一演出：向后烟锥 + 枪口光 + 后坐冲量。
        /// 只应在 GsShoot（owner 端）调用；坐骑减半、空中加成 1.5 倍
        /// </summary>
        protected static void LaunchPresentation(Player player, Vector2 muzzle, Vector2 velocity,
            float recoil, Color smokeTint) {
            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX);
            if (recoil > 0f) {
                float mul = player.mount?.Active == true ? 0.5f : 1f;
                if (player.velocity.Y != 0f) {
                    mul *= 1.5f;
                }
                player.velocity -= aim * recoil * mul;
            }
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(muzzle - aim * 10f,
                    (-aim).RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(1.8f, 4.5f),
                    smokeTint, Main.rand.NextFloat(0.45f, 0.8f))
                    ?.Configure(Main.rand.Next(18, 30), 0.42f, Main.rand.NextFloat(-0.05f, 0.05f));
            }
            PRTLoader.NewParticle<PRT_Light>(muzzle, Vector2.Zero, smokeTint, 0.16f)?.Configure(8, 0.8f);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Spark>(muzzle,
                    aim.RotatedBy(Main.rand.NextFloat(-0.35f, 0.35f)) * Main.rand.NextFloat(3f, 6f),
                    smokeTint, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        /// <summary>
        /// 爆点统一余痕：殉爆光团 + 焦痕（活得比弹幕久）+ 外溅火花 + 升腾烟。
        /// 各端非服务器都执行（GsProjOnKill 全端回调），单次 ≤11 粒守预算
        /// </summary>
        internal static void ExplosionAftermath(Vector2 center, Color warm, float scale = 1f) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_MechExplosion>(center, Vector2.Zero, warm,
                0.75f * scale)?.Configure(26, warm);
            PRTLoader.NewParticle<PRT_DefScorch>(center + Main.rand.NextVector2Circular(6f, 6f),
                Vector2.Zero, warm * 0.65f, Main.rand.NextFloat(0.55f, 0.85f) * scale)
                ?.Configure(Main.rand.Next(70, 110));
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 7f) * scale,
                    warm, Main.rand.NextFloat(0.3f, 0.55f))?.Configure(true, Main.rand.Next(14, 26));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(center + Main.rand.NextVector2Circular(8f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.5f, 1.4f)),
                    Color.Lerp(warm, Color.DimGray, 0.6f), Main.rand.NextFloat(0.5f, 0.9f) * scale)
                    ?.Configure(Main.rand.Next(24, 40), 0.4f);
            }
        }

        /// <summary>本地战斗文本提示（只在 myPlayer 端冒字）</summary>
        protected static void LocalTip(Player player, LocalizedText text, Color color) {
            if (player.whoAmI == Main.myPlayer && !VaultUtils.isServer) {
                CombatText.NewText(player.getRect(), color, text.Value);
            }
        }

        /// <summary>identity 确定性散列（0~1），绘制与散布路径禁 Main.rand 时用</summary>
        internal static float IdentityHash01(int identity, float salt = 0f)
            => MathF.Abs(MathF.Sin(identity * 12.9898f + salt * 78.233f) * 43758.5453f) % 1f;
    }

    /// <summary>
    /// 发射器族每玩家状态。全部字段只在 myPlayer 路径读写（右键动作/射击链/PostUpdate
    /// 都发生在 owner 端逻辑流），联机零同步需求；远端玩家看到的引爆由弹幕死亡自然呈现
    /// </summary>
    internal class GsLaunchersPlayer : ModPlayer
    {
        /// <summary>右键动作冷却</summary>
        internal int altCooldown;
        /// <summary>当前手持物类型，变化即重置模式槽</summary>
        internal int heldType;
        /// <summary>通用模式槽（引信/运载/编排循环）</summary>
        internal int fuzeMode;

        /// <summary>粘附雷落位序号发号器（榴弹发射器）</summary>
        internal int grenadeSeq;
        /// <summary>地雷布设序号发号器</summary>
        internal int mineSeq;
        /// <summary>地雷回收返还预算（每分钟回满 8）</summary>
        internal int mineRecycleBudget = 8;
        private int mineRecycleTimer;

        /// <summary>多米诺连锁调度队列：(弹幕槽位, identity 校验, 延迟)</summary>
        internal readonly List<(int index, int identity, int delay)> dominoQueue = [];

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (altCooldown > 0) {
                altCooldown--;
            }
            int held = Player.HeldItem?.type ?? ItemID.None;
            if (held != heldType) {
                heldType = held;
                fuzeMode = 0;
            }
            if (++mineRecycleTimer >= 3600) {
                mineRecycleTimer = 0;
                mineRecycleBudget = 8;
            }
            UpdateDomino();
        }

        /// <summary>多米诺连锁推进：延迟到点逐雷引爆，槽位重用由 identity 校验兜底</summary>
        private void UpdateDomino() {
            if (dominoQueue.Count == 0) {
                return;
            }
            for (int i = dominoQueue.Count - 1; i >= 0; i--) {
                (int index, int identity, int delay) = dominoQueue[i];
                if (--delay > 0) {
                    dominoQueue[i] = (index, identity, delay);
                    continue;
                }
                dominoQueue.RemoveAt(i);
                if (index < 0 || index >= Main.maxProjectiles) {
                    continue;
                }
                Projectile proj = Main.projectile[index];
                if (proj.active && proj.identity == identity && proj.owner == Player.whoAmI) {
                    GsLauncherScheme.GsDetonate(proj);
                }
            }
        }
    }

    /// <summary>
    /// 高抛轨道共享地基（雪人炮「暴雪轨道」与星炮「星落」共用）：
    /// 升空、横移至光标上空、俯冲三相。只改运载轨迹，不碰爆炸；
    /// 爆窗（timeLeft&lt;=3）一律交回原版 AI。散布用 identity 散列，各端确定一致
    /// </summary>
    internal static class GsOrbitalHelper
    {
        /// <summary>每弹幕轨道状态（LocalState 本地包，可从同步量推导）</summary>
        internal class OrbitalState
        {
            public int age;
            public int phase;
        }

        /// <summary>
        /// 在 GsProjPreAI 里调用（调用方已确认走轨道模式且不在爆窗）。
        /// 返回 false 表示已接管本帧（调用方应 return false 压掉原版 AI）
        /// </summary>
        internal static bool RunOrbital(Projectile proj, GodSmithProjRouter router,
            float riseSpeed, float diveSpeed, float spreadPx) {
            OrbitalState st = router.GetOrCreateState<OrbitalState>();
            st.age++;
            //接管期原版 AI 不跑，出生 alpha 自清防隐形
            if (proj.alpha > 0) {
                proj.alpha = Math.Max(0, proj.alpha - 25);
            }
            float spread = (GsLauncherScheme.IdentityHash01(proj.identity) - 0.5f) * 2f * spreadPx;
            float targetX = router.MarkData2 + spread;
            switch (st.phase) {
                case 0:
                    //升空：拉起并加速向上，穿越头顶障碍
                    proj.tileCollide = false;
                    proj.velocity = Vector2.Lerp(proj.velocity, new Vector2(0f, -riseSpeed), 0.085f);
                    if ((proj.velocity.Y < -riseSpeed * 0.8f && st.age > 18) || st.age > 90) {
                        st.phase = 1;
                    }
                    break;
                case 1:
                    //横移：压住纵向，水平赶往落点上空
                    proj.tileCollide = false;
                    proj.velocity.Y *= 0.90f;
                    float dx = targetX - proj.Center.X;
                    proj.velocity.X = MathHelper.Clamp(proj.velocity.X + Math.Sign(dx) * 0.9f, -17f, 17f);
                    if (Math.Abs(dx) < 56f || st.age > 240) {
                        st.phase = 2;
                    }
                    break;
                default:
                    //俯冲：恢复碰撞，直插落点，命中砖/敌即走原版起爆
                    proj.tileCollide = true;
                    proj.velocity.X = MathHelper.Clamp((targetX - proj.Center.X) * 0.045f, -6f, 6f);
                    proj.velocity.Y = Math.Min(proj.velocity.Y + 1.15f, diveSpeed);
                    break;
            }
            proj.rotation = proj.velocity.ToRotation() + MathHelper.PiOver2;
            return false;
        }
    }
}
