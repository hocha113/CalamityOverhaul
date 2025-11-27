using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// <summary>
    /// 寰宇咏叹调
    /// </summary>
    internal class AriaofTheCosmos : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "AriaofTheCosmos";
        public readonly static string[] FullItems = ["0", "0", "0", "0", "CalamityOverhaul/StarflowPlatedBlock", "0", "0", "0", "0",
            "0", "0", "0", "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock", "0", "0", "0",
            "0", "0", "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock", "0", "0",
            "0", "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock", "0",
            "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/Rock", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock",
            "0", "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock", "0",
            "0", "0", "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock", "0", "0",
            "0", "0", "0", "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock", "0", "0", "0",
            "0", "0", "0", "0", "CalamityOverhaul/StarflowPlatedBlock", "0", "0", "0", "0",
            "CalamityOverhaul/AriaofTheCosmos"
        ];

        public override void SetStaticDefaults() {
            SupertableUI.ModCall_OtherRpsData_StringList.Add(FullItems);
        }

        public override void SetDefaults() {
            Item.damage = 1285;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 20;
            Item.width = 52;
            Item.height = 52;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 5f;
            Item.value = Item.buyPrice(gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = null;
            Item.autoReuse = false;
            Item.shoot = ModContent.ProjectileType<AccretionDisk>();
            Item.shootSpeed = 0f;
            Item.channel = true;
            Item.SetHeldProj<AriaofTheCosmosHeld>();
            Item.CWR().OmigaSnyContent = FullItems;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.InsertHotkeyBinding(CWRKeySystem.AriaofTheCosmos_Q, "AriaofTheCosmosQSkill", CWRLocText.Instance.Notbound.Value);
            tooltips.InsertHotkeyBinding(CWRKeySystem.AriaofTheCosmos_R, "AriaofTheCosmosRSkill", CWRLocText.Instance.Notbound.Value);
        }
    }
}
