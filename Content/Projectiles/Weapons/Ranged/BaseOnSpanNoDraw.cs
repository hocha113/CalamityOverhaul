using CalamityOverhaul.Common;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    /// <summary>
    /// 一个通用的用于制造外置效果的实体栈，其中Projectile.ai[0]默认作为时间计数器
    /// 而Projectile.ai[1]用于存储需要跟随的弹幕的索引
    /// </summary>
    internal abstract class BaseOnSpanNoDraw : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public Player Owner => Main.player[Projectile.owner];
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 1;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 40;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.light = 1.2f;
        }

        public virtual void SpanProj() {

        }

        public override void AI() {
            BaseOnSpanProj.FlowerAI(Projectile);
            SpanProj();
        }
    }
}
