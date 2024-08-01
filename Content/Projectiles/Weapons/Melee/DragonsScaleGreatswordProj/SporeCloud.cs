using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DragonsScaleGreatswordProj
{
    internal class SporeCloud : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "SporeCloud";

        private int startCanHitCooldown;//弹幕堆叠所会造成极高伤害的问题始终存在，所以使用这个控制开始造成伤害的时机来错开伤害阶段

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = Projectile.height = 24;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.timeLeft = 60;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 25;
            startCanHitCooldown = Main.rand.Next(Projectile.localNPCHitCooldown);
        }

        public override void AI() {
            Projectile.velocity *= 0.985f;
            Projectile.scale += 0.013f;
            Projectile.rotation += Projectile.velocity.X * 0.05f;
        }

        public override bool? CanHitNPC(NPC target) {
            return Projectile.timeLeft >= 90 - startCanHitCooldown ? false : base.CanHitNPC(target);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 1200);
            Projectile.timeLeft -= 15;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 4);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Projectile.frame, 5), lightColor * (Projectile.timeLeft / 30f)
                , Projectile.rotation, CWRUtils.GetOrig(value, 5), Projectile.scale * 0.8f, 0, 0);
            return false;
        }
    }
}
