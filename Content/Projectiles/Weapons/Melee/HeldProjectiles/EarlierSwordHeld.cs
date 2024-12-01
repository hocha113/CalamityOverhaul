using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class EarlierSwordHeld : BaseKnife
    {
        public override int TargetID => Item.type;
        public override void SetKnifeProperty() {
            //drawTrailHighlight = false;
            //canDrawSlashTrail = true;
            //distanceToOwner = 8;
            //drawTrailBtommWidth = 8;
            //drawTrailTopWidth = 22;
            //drawTrailCount = 4;
            SwingData.baseSwingSpeed = 4;
            Length = 30;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            Projectile.usesLocalNPCImmunity = false;
        }

        internal static void Set(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<EarlierSwordHeld>();
        }

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.1f, phase1SwingSpeed: 3.2f, phase2SwingSpeed: 6f, drawSlash: false);
            return base.PreInOwnerUpdate();
        }

        public override void KnifeInitialize() {
            if (TargetID == ItemID.CactusSword) {
                Projectile.width = Projectile.height = 40;
                Length = 50;
                SwingData.minClampLength = 55;
                SwingData.maxClampLength = 60;
                unitOffsetDrawZkMode = -2;
            }
        }
    }
}
