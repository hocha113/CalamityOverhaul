using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 净神者之焰
    /// </summary>
    internal class CleansingBlazeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CleansingBlaze";
        public override void SetDefaults() {
            Item.SetCalamitySD<CleansingBlaze>();
            Item.SetCartridgeGun<CleansingBlazeHeldProj>(160);
            Item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Item.DrawItemGlowmaskSingleFrame(spriteBatch, rotation, ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Ranged/CleansingBlazeGlow").Value);
        }
    }
}
