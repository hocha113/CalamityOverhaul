using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles.Vanilla
{
    internal class EarlierSwordHeld : BaseKnife
    {
        public override int TargetID => Item.type;
        public override void SetKnifeProperty() {
            SwingData.baseSwingSpeed = 4;
            Length = 30;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            Projectile.usesLocalNPCImmunity = false;
        }

        public override void Initialize() {
            base.Initialize();
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
