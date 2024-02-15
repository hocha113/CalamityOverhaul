using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common.Interfaces
{
    public abstract class CustomItems : ModItem
    {
        public abstract override void SetStaticDefaults();
        public abstract override void SetDefaults();
        public abstract override string Texture { get; }
        public abstract override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback);
        public abstract override bool CanUseItem(Player player);
        public abstract override bool? UseItem(Player player);
        public abstract override void Update(ref float gravity, ref float maxFallSpeed);
        public abstract override void UpdateInventory(Player player);
        public abstract override void HoldItem(Player player);
    }
}
