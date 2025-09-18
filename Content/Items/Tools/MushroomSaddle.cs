using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.Modifys;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class MushroomSaddle : ModItem
    {
        [VaultLoaden(CWRConstant.Item_Tools)]
        public static Asset<Texture2D> MushroomSaddlePlace;
        public ModifyCrabulon ModifyCrabulon;
        public static LocalizedText UseCombat;
        public override string Texture => CWRConstant.Item + "Tools/MushroomSaddle";
        public override void SetStaticDefaults() => 
            UseCombat = this.GetLocalization(nameof(UseCombat), () => "Can only be used for Crabulon");
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = Item.useAnimation = 12;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.consumable = true;
            Item.value = 25800;
            Item.rare = ItemRarityID.Cyan;
        }

        public override bool CanUseItem(Player player) {
            if (!ModifyCrabulon.npc.Alives() || !ModifyCrabulon.npc.Hitbox.Intersects(Main.MouseWorld.GetRectangle(1))) {
                ModifyCrabulon = null;
                CombatText text = Main.combatText[CombatText.NewText(player.Hitbox, Color.GreenYellow, UseCombat.Value)];
                text.text = UseCombat.Value;
                text.lifeTime = 120;
                return false;
            }

            ModifyCrabulon.SaddleItem = Item.Clone();
            return true;
        }

        public override bool? UseItem(Player player) {
            SoundEngine.PlaySound(CWRSound.PutSaddle, player.Center);
            return true;
        }
    }
}
