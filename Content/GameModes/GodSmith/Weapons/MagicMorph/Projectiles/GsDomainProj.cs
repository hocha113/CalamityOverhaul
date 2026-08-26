using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles
{
    /// <summary>
    /// MagicMorph 族小领域基类：短时驻场弹幕（真弹幕承载，全端可见）。<br/>
    /// 寿命在 SetDefaults 定死（各端出生即一致，杜绝服务端直改 timeLeft 不入包）；
    /// 判定为以弹幕中心为圆心的圆，与可见边界环同源；
    /// tick 节奏走 usesLocalNPCImmunity + localNPCHitCooldown；
    /// 同类领域全场最多一座：再放走 <see cref="TryMigrate{T}"/> 旧域迁移（不叠不刷不续命）
    /// </summary>
    internal abstract class GsDomainProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 子类参数 ====================

        /// <summary>判定半径（px），与可见边界同源</summary>
        protected abstract int DomainRadius { get; }

        /// <summary>寿命（帧），SetDefaults 写死后不得再改</summary>
        protected abstract int DomainLife { get; }

        /// <summary>域内命中冷却（帧）</summary>
        protected virtual int DomainTickRate => 12;

        /// <summary>域本体是否携带接触判定（false=纯位置标记/产物承伤型）</summary>
        protected virtual bool DealsContactDamage => true;

        /// <summary>边界环三色：波前亮缘</summary>
        protected abstract Color RingBright { get; }
        /// <summary>边界环三色：环带主体</summary>
        protected abstract Color RingMain { get; }
        /// <summary>边界环三色：内侧残波</summary>
        protected abstract Color RingDeep { get; }

        /// <summary>边界环 Y 透视压缩，贴地域用 0.4~0.5，悬空域用 1</summary>
        protected virtual float RingSquish => 1f;

        //==================== 生命周期 ====================

        public sealed override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = DomainTickRate;
            Projectile.timeLeft = DomainLife;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            SetDomainDefaults();
        }

        /// <summary>子类的 SetDefaults 扩展点（禁改 timeLeft）</summary>
        protected virtual void SetDomainDefaults() { }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => DealsContactDamage ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 c = Projectile.Center;
            Vector2 nearest = new(
                MathHelper.Clamp(c.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(c.Y, targetHitbox.Top, targetHitbox.Bottom));
            return c.DistanceSQ(nearest) <= (float)DomainRadius * DomainRadius;
        }

        /// <summary>入场 12t 淡入、离场 20t 淡出的确定函数（timeLeft 驱动，各端一致）</summary>
        protected float LifeFade {
            get {
                int lived = DomainLife - Projectile.timeLeft;
                float fadeIn = MathHelper.Clamp(lived / 12f, 0f, 1f);
                float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
                return fadeIn * fadeOut;
            }
        }

        private Vector2 prevCenter;

        public sealed override void AI() {
            //模式关闭时在场领域即刻消散（世界旗标全端同步，各端 Kill 一致）
            if (!GameModeSystem.GodSmithActive) {
                Projectile.Kill();
                return;
            }
            //迁移瞬间的跨端可见反馈：位置突变时两端各撒少量粒子
            if (prevCenter != Vector2.Zero && !VaultUtils.isServer
                && Projectile.Center.DistanceSQ(prevCenter) > 100f * 100f) {
                OnMigrateVisual(prevCenter);
            }
            prevCenter = Projectile.Center;
            DomainAI();
            if (!VaultUtils.isServer) {
                EmitAmbient();
            }
        }

        /// <summary>子类领域逻辑（各端都会执行；权威改动守 IsOwnedByLocalPlayer，服务端可写 NPC 位移）</summary>
        protected virtual void DomainAI() { }

        /// <summary>域内环境粒子（仅客户端；预算 ≤4/帧）</summary>
        protected virtual void EmitAmbient() { }

        /// <summary>迁移瞬间的旧址消散反馈（仅客户端）</summary>
        protected virtual void OnMigrateVisual(Vector2 oldCenter) { }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            float fade = LifeFade;
            if (fade <= 0.02f) {
                return false;
            }
            //边界慢环：半径呼吸微动，identity 定相（绘制路径禁 Main.rand）
            float breathe = 1f + 0.018f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f + Projectile.identity * 0.83f);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, DomainRadius * breathe, 7f,
                RingBright, RingMain, RingDeep, 0.45f * fade,
                squish: RingSquish, innerGlow: 0.12f, timeSeed: Projectile.identity * 0.37f);
            DrawDomainInner(fade);
            return false;
        }

        /// <summary>域内自定义绘制层（已处于实体批；黑底贴图记得色批 A=0）</summary>
        protected virtual void DrawDomainInner(float fade) { }

        //==================== 迁移 helper ====================

        /// <summary>
        /// 同类领域已在场则迁移到目标点并返回 true（不重置寿命），否则返回 false 由调用方新建。
        /// 仅本地玩家路径调用；位置改动随 netUpdate 过线
        /// </summary>
        internal static bool TryMigrate<T>(Player player, Vector2 target) where T : GsDomainProj {
            int type = ModContent.ProjectileType<T>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && proj.owner == player.whoAmI) {
                    proj.Center = target;
                    proj.netUpdate = true;
                    return true;
                }
            }
            return false;
        }
    }
}
