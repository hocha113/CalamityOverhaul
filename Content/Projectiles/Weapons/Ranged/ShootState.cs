using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    public struct ShootState{
        public int AmmoTypes;
        public float ScaleFactor;
        public int WeaponDamage;
        public float WeaponKnockback;
        public int UseAmmoItemType;
        public EntitySource_ItemUse_WithAmmo Source;
    }
}
