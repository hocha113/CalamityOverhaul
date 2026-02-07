using CalamityOverhaul.Content.UIs.SupertableUIs;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 天国极乐，与万魔殿相对的教皇权杖
    /// </summary>
    internal class Elysium : ModItem
    {
        //合成配方材料，与万魔殿同级
        public readonly static string[] FullItems = ["0", "0", "0", "0", "CalamityMod/AshesofAnnihilation", "0", "0", "0", "0",
            "0", "0", "0", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "0", "0", "0",
            "0", "0", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "0", "0",
            "0", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/DivineGeode", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "0",
            "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/Rock", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation",
            "0", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/Apotheosis", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "0",
            "0", "0", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "0", "0",
            "0", "0", "0", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "CalamityMod/AshesofAnnihilation", "0", "0", "0",
            "0", "0", "0", "0", "CalamityMod/AshesofAnnihilation", "0", "0", "0", "0",
            "CalamityOverhaul/Elysium"
        ];

        public override bool IsLoadingEnabled(Mod mod) => false;

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
            SupertableUI.ModCall_OtherRpsData_StringList.Add(FullItems);
        }

        public override void SetDefaults() {
            Item.damage = 320;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 20;
            Item.width = 50;
            Item.height = 50;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 6;
            Item.value = Item.sellPrice(platinum: 10);
            Item.rare = CWRID.Rarity_BurnishedAuric;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<ElysiumHeld>();
            Item.shootSpeed = 12f;
            Item.channel = true;
            Item.CWR().OmigaSnyContent = FullItems;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                //右键：转化NPC为门徒
                Item.mana = 50;
                Item.useTime = Item.useAnimation = 30;
                Item.channel = false;
                return true;
            }
            else {
                //左键：化蛇术
                Item.mana = 20;
                Item.useTime = Item.useAnimation = 25;
                Item.channel = true;
                return player.ownedProjectileCounts[ModContent.ProjectileType<ElysiumHeld>()] == 0;
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            //显示当前门徒数量
            if (Main.LocalPlayer.TryGetModPlayer<ElysiumPlayer>(out var ep)) {
                int count = ep.GetDiscipleCount();
                string discipleInfo = count > 0 ? $"当前门徒: {count}/12" : "尚无门徒追随";
                tooltips.Add(new TooltipLine(Mod, "DiscipleCount", discipleInfo));

                if (count == 12) {
                    tooltips.Add(new TooltipLine(Mod, "JudasWarning", "[c/FF4444:警告: 犹大的背叛已潜伏于你的身边]"));
                }
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                //右键：尝试转化最近的NPC为门徒
                if (player.TryGetModPlayer<ElysiumPlayer>(out var ep)) {
                    ep.TryConvertNearestNPC(player);
                }
                return false;
            }
            else {
                //左键：生成手持权杖弹幕
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
                return false;
            }
        }
    }
}
