using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class DevastationEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Devastation";
        public override void SetDefaults() {
            Item.SetCalamitySD<Devastation>();
            Item.SetKnifeHeld<DevastationHeld>();
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(6, 11));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }
    }

    internal class RDevastation : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Devastation>();
        public override int ProtogenesisID => ModContent.ItemType<DevastationEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<DevastationHeld>();
    }

    internal class DevastationHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Devastation>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "CatastropheClaymore_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 20;
            drawTrailCount = 3;
            Length = 82;
            SwingData.starArg = 68;
            SwingData.baseSwingSpeed = 3.5f;
            ShootSpeed = 12;
            CuttingFrmeInterval = 5;
            AnimationMaxFrme = 11;
        }

        public override bool PreInOwnerUpdate() {
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {

        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {

        }
    }
}
