using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;

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
    }
}
