using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core
{
    /// <summary>
    /// 一个通用的用于制造外置效果的实体栈，其中Projectile.ai[0]默认作为时间计数器
    /// 而Projectile.ai[1]用于存储需要跟随的弹幕的索引
    /// </summary>
    internal abstract class BaseOnSpanNoDraw : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
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
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public virtual void SpanProj() {

        }

        public override void AI() {
            Projectile.MaxUpdates = 2;
            BaseOnSpanProj.FlowerAI(Projectile);
            if (Projectile.IsOwnedByLocalPlayer()) {
                SpanProj();
            }
            if (Projectile.ai[2] == 0) {
                if (!DownRight || DownLeft) {
                    Projectile.Kill();
                }
            }
            else {
                if (!DownLeft || DownRight) {
                    Projectile.Kill();
                }
            }
        }
    }
}
