using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Tools;
using CalamityOverhaul.Content.RemakeItems;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class ModifyInfinitePick : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<InfinitePick>();
        public override bool DrawingInfo => false;
        public override bool CanLoadLocalization => false;
        //在某些不应该的情况下，武器会被禁止使用，使用这个钩子来防止这种事情的发生
        public override bool? On_CanUseItem(Item item, Player player) => true;
    }

    internal class InfinitePick : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item + "Tools/Pickaxe";
        private static bool IsPick = true;
        [VaultLoaden(CWRConstant.Item + "Tools/Pickaxe")]
        private static Asset<Texture2D> Pickaxe = null;
        [VaultLoaden(CWRConstant.Item + "Tools/Hammer")]
        private static Asset<Texture2D> Hammer = null;
        private bool rDown;
        private bool oldRDown;
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 4));
        }
        public override void SetDefaults() {
            Item.damage = 9999;
            Item.knockBack = 6;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 1;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.DamageType = EndlessDamageClass.Instance;
            Item.value = Item.buyPrice(gold: 999);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.pick = 9999;
            Item.tileBoost = 64;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_InfinitePick;
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame) => player.itemLocation = player.GetPlayerStabilityCenter();

        public override bool AltFunctionUse(Player player) => true;

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit = 9999;

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) => damage = damage.Scale(0);

        public override void HoldItem(Player player) {
            if (Main.myPlayer != player.whoAmI) {
                return;
            }

            if (IsPick) {
                Item.pick = 9999;
                Item.hammer = 0;
                Item.useTime = 1;
                Item.useAnimation = 10;

            }
            else {
                Item.pick = 0;
                Item.hammer = 9999;
                Item.useTime = 2;
                Item.useAnimation = 10;
            }

            if (CWRKeySystem.InfinitePickSkillKey.JustPressed) {
                IsPick = !IsPick;
                SoundEngine.PlaySound(!IsPick ? CWRSound.Pecharge : CWRSound.Peuncharge, player.Center);
                TextureAssets.Item[Type] = IsPick ? Pickaxe : Hammer;
            }

            rDown = player.PressKey(false);
            bool justRDown = rDown && !oldRDown;
            oldRDown = rDown;

            if (justRDown && !player.CWR().UIMouseInterface && !player.cursorItemIconEnabled && player.cursorItemIconID == ItemID.None) {
                Projectile.NewProjectile(player.FromObjectGetParent(), player.GetPlayerStabilityCenter()
                    , player.Center.To(Main.MouseWorld).UnitVector() * 32, ModContent.ProjectileType<InfinitePickProj>()
                    , Item.damage, 0, player.whoAmI, IsPick ? 1 : 0, Main.MouseWorld.X, Main.MouseWorld.Y);
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            if (IsPick) {
                TooltipLine cumstops = tooltips.FirstOrDefault((TooltipLine x) => x.Name == "PickPower" && x.Mod == "Terraria");
                if (cumstops != null) {
                    string typeV = Language.GetTextValue("LegacyTooltip.26");
                    cumstops.Text = $"{int.MaxValue}{typeV}";
                }
            }
            else {
                TooltipLine cumstops = tooltips.FirstOrDefault((TooltipLine x) => x.Name == "HammerPower" && x.Mod == "Terraria");
                if (cumstops != null) {
                    string typeV = Language.GetTextValue("LegacyTooltip.28");
                    cumstops.Text = $"{int.MaxValue}{typeV}";
                }
            }

            tooltips.IntegrateHotkey(CWRKeySystem.InfinitePickSkillKey);
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) {
            if ((line.Name == "ItemName" || line.Name == "Damage" || line.Name == "PickPower" || line.Name == "HammerPower")
                && line.Mod == "Terraria") {
                InfiniteIngot.DrawColorText(Main.spriteBatch, line);
                return false;
            }
            return true;
        }
    }
}
