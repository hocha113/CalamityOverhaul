using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.DataStructures;
using Terraria;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class EutrophicScimitarEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "EutrophicScimitar";
        public override void SetDefaults() {
            Item.SetCalamitySD<EutrophicScimitar>();
            Item.SetKnifeHeld<EutrophicScimitarHeld>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }
    }
}
