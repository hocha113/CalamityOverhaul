using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class FlarefrostBladeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "FlarefrostBlade";
        public override void SetDefaults() {
            Item.SetCalamitySD<FlarefrostBlade>();
            Item.SetKnifeHeld<FlarefrostBladeHeld>();
        }
    }

    internal class RFlarefrostBlade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<FlarefrostBlade>();
        public override int ProtogenesisID => ModContent.ItemType<FlarefrostBladeEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<FlarefrostBladeHeld>();
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class FlarefrostBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<FlarefrostBlade>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "FlarefrostBlade_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 56;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 30;
            drawTrailCount = 8;
            Length = 68;
            SwingData.baseSwingSpeed = 3.65f;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            ShootSpeed = 11;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ModContent.ProjectileType<Flarefrost>(), Projectile.damage
                , Projectile.knockBack, Owner.whoAmI, 0f, 0);
        }

        public override bool PreInOwnerUpdate() {
            int dustChoice = Main.rand.Next(2);
            dustChoice = dustChoice == 0 ? 67 : 6;
            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustChoice);
            }
            return base.PreInOwnerUpdate();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 180);
            target.AddBuff(BuffID.Frostburn2, 180);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire3, 180);
            target.AddBuff(BuffID.Frostburn2, 180);
        }
    }
}
