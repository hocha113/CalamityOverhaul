using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class PhoenixFlameBarrageEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "PhoenixFlameBarrage";
        public override void SetDefaults() {
            Item.SetItemCopySD<PhoenixFlameBarrage>();
            Item.shoot = ModContent.ProjectileType<PhantomPhoenix>();
            Item.CWR().heldProjType = ModContent.ProjectileType<PhoenixFlameBarrageHeld>();
            Item.CWR().hasHeldNoCanUseBool = true;
        }
    }
}
