using CalamityMod.Buffs.StatDebuffs;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class AbsoluteZeroHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<AbsoluteZero>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "AbsoluteZero_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 60;
            distanceToOwner = -22;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            Length = 56;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center + UnitToMouseV * 42
                , UnitToMouseV * 9, ModContent.ProjectileType<DarkIceZeros>()
                , (int)(Projectile.damage * 0.8f), Projectile.knockBack * 0.8f, Owner.whoAmI);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 300);
            target.AddBuff(ModContent.BuffType<GlacialState>(), 60);
            var source = Owner.GetSource_ItemUse(Item);
            int p = Projectile.NewProjectile(source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<DarkIceZeros>(), (int)(Item.damage * 1.25f), 12f, Owner.whoAmI);
            Main.projectile[p].timeLeft = 12;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Frostburn2, 300);
            target.AddBuff(ModContent.BuffType<GlacialState>(), 60);
            var source = Owner.GetSource_ItemUse(Item);
            int p = Projectile.NewProjectile(source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<DarkIceZeros>(), (int)(Item.damage * 1.25f), 12f, Owner.whoAmI);
            Main.projectile[p].timeLeft = 12;
        }
    }
}
