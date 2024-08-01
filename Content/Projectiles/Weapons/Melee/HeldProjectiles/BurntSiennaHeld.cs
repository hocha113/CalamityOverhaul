using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Boss;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class BurntSiennaHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<BurntSienna>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "BurntSienna_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 36;
            canDrawSlashTrail = true;
            distanceToOwner = 16;
            drawTrailBtommWidth = 40;
            drawTrailTopWidth = 16;
            drawTrailCount = 8;
            Length = 44;
        }

        public override void Shoot() {
            Vector2 vr = ShootVelocity.RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-15, 15))) * Main.rand.NextFloat(0.75f, 1.12f);
            int proj = Projectile.NewProjectile(Source, ShootSpanPos, vr
                , ModContent.ProjectileType<DesertScourgeSpit>(), Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI);
            Main.projectile[proj].hostile = false;
            Main.projectile[proj].friendly = true;
            NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, proj);
        }

        public override bool PreInOwnerUpdate() {
            return base.PreInOwnerUpdate();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitNPC(target, hit, damageDone);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            base.OnHitPlayer(target, info);
        }
    }
}
