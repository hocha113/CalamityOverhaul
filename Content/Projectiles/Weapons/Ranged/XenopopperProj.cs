using CalamityMod;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class XenopopperProj : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "Xenopopper";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.timeLeft = 45;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void AI() {
            Projectile.velocity.X *= 0.95f;
            Projectile.velocity.Y *= 0.99f;
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 vr = Projectile.Center.To(Main.MouseWorld).UnitVector() * Projectile.localAI[1];
                if (Projectile.ai[0] != 0) {
                    NPC target = Projectile.Center.FindClosestNPC(1300);
                    if (target != null) {
                        vr = Projectile.Center.To(target.Center).UnitVector() * Projectile.localAI[1];
                    }
                }
                Projectile.NewProjectileDirect(new EntitySource_ItemUse(Main.player[Projectile.owner], Main.player[Projectile.owner].ActiveItem())
                    , Projectile.Center, vr, (int)Projectile.localAI[0], Projectile.damage, Projectile.knockBack, Projectile.owner, 0);
            }
        }
    }
}
