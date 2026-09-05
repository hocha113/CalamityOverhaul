using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles
{
    /// <summary>
    /// MagicMorph 族小领域基类：短时驻场弹幕（真弹幕承载，全端可见）。<br/>
    /// 寿命在 SetDefaults 定死（各端出生即一致，杜绝服务端直改 timeLeft 不入包）；
    /// 判定为以弹幕中心为圆心的圆，范围提示为原版贴图按半径缩放的一笔；
    /// tick 节奏走 usesLocalNPCImmunity + localNPCHitCooldown；
    /// 同类领域全场最多一座：再放走 <see cref="TryMigrate{T}"/> 旧域迁移（不叠不刷不续命）。
    /// 子类须覆写 <see cref="ModProjectile.Texture"/> 指向原版贴图
    /// </summary>
    internal abstract class GsDomainProj : ModProjectile
    {
        //==================== 子类参数 ====================

        /// <summary>判定半径（px），与范围提示同源</summary>
        protected abstract int DomainRadius { get; }

        /// <summary>寿命（帧），SetDefaults 写死后不得再改</summary>
        protected abstract int DomainLife { get; }

        /// <summary>域内命中冷却（帧）</summary>
        protected virtual int DomainTickRate => 12;

        /// <summary>域本体是否携带接触判定（false=纯位置标记/产物承伤型）</summary>
        protected virtual bool DealsContactDamage => true;

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

        public sealed override void AI() {
            //模式关闭时在场领域即刻消散（世界旗标全端同步，各端 Kill 一致）
            if (!GameModeSystem.GodSmithActive) {
                Projectile.Kill();
                return;
            }
            DomainAI();
        }

        /// <summary>子类领域逻辑（各端都会执行；权威改动守 IsOwnedByLocalPlayer，服务端可写 NPC 位移）</summary>
        protected virtual void DomainAI() { }

        //==================== 绘制 ====================

        /// <summary>范围提示：原版贴图首帧按判定半径缩放画一笔（lightColor 着色），随寿命淡入淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            float fade = LifeFade;
            if (fade <= 0.02f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frame = tex.Frame(1, Main.projFrames[Type], 0, 0);
            float scale = DomainRadius * 2f / frame.Width;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, lightColor * fade, 0f,
                frame.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }

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
