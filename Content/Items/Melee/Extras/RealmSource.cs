using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class RealmSource : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "RealmSource";
        public override void SetDefaults() {
            Item.width = Item.height = 54;
            Item.shootSpeed = 9;
            Item.crit = 8;
            Item.damage = 285;
            Item.useTime = 26;
            Item.useAnimation = 26;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(0, 83, 55, 0);
            Item.rare = ItemRarityID.Lime;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.DamageType = DamageClass.Melee;
            Item.shoot = 10;
            Item.CWR().isHeldItem = true;
            Item.SetKnifeHeld<RealmSourceHeld>();
        }
    }

    internal class RealmSourceHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<RealmSource>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "ExaltedOathblade_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 116;
            canDrawSlashTrail = true;
            drawTrailCount = 8;
            distanceToOwner = 80;
            drawTrailTopWidth = 60;
            ownerOrientationLock = true;
            SwingData.baseSwingSpeed = 3.55f;
            IgnoreImpactBoxSize = true;
            Length = 80;
        }

        public override void MeleeEffect() {

        }

        public override void Shoot() {

        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {

        }
    }
}
