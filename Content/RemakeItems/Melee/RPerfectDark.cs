using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RPerfectDark : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<PerfectDark>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<PerfectDarkHeld>();
    }

    internal class PerfectDarkHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<PerfectDark>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "PerfectDark_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 40;
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 4f;
            drawTrailBtommWidth = 30;
            distanceToOwner = 14;
            drawTrailTopWidth = 20;
            Length = 50;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, Owner.Center, ShootVelocity
                , ModContent.ProjectileType<DarkBall>(), Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<BrainRot>(), 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<BrainRot>(), 300);
        }
    }
}
