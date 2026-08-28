using CalamityOverhaul.Content.NPCs.SeaShrimp;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Armor.Deepcrystals
{
    /// <summary>
    /// 渊晶四职业头盔共性:低防高伤玻璃大炮,套装开启聚泡引爆(见 <see cref="DeepcrystalPlayer"/>)。
    /// 子类只提供职业词条与引爆归类
    /// </summary>
    internal abstract class DeepcrystalHeadBase : ModItem
    {
        public sealed override string Texture => CWRConstant.Item + "Armor/" + Name;

        /// <summary>引爆演出与套装增伤走的职业</summary>
        public abstract DamageClass SetClass { get; }

        public sealed override bool IsArmorSet(Item head, Item body, Item legs) {
            return body.type == ModContent.ItemType<DeepcrystalBreastplate>()
                && legs.type == ModContent.ItemType<DeepcrystalGreaves>();
        }

        public sealed override void UpdateArmorSet(Player player) {
            player.setBonus = this.GetLocalization("SetBonus").Value;
            //狂战士代价:受到的伤害加深
            player.endurance -= 0.08f;
            player.GetDamage(SetClass) += 0.10f;
            DeepcrystalPlayer dcp = player.GetModPlayer<DeepcrystalPlayer>();
            dcp.SetActive = true;
            dcp.SetClass = SetClass;
        }

        public sealed override void AddRecipes() => DeepcrystalRecipe.Add(this, 4, 6, 4, 6, 8, 3, 5);
    }

    /// <summary>渊晶战盔:近战</summary>
    [AutoloadEquip(EquipType.Head)]
    internal class DeepcrystalWarhelm : DeepcrystalHeadBase
    {
        public override DamageClass SetClass => DamageClass.Melee;

        public override void SetDefaults() {
            Item.width = 30;
            Item.height = 26;
            Item.value = Item.sellPrice(0, 3);
            Item.rare = ItemRarityID.Lime;
            Item.defense = 9;
        }

        public override void UpdateEquip(Player player) {
            player.GetDamage(DamageClass.Melee) += 0.12f;
            player.GetAttackSpeed(DamageClass.Melee) += 0.10f;
            player.GetCritChance(DamageClass.Melee) += 6f;
        }
    }

    /// <summary>渊晶目镜:远程</summary>
    [AutoloadEquip(EquipType.Head)]
    internal class DeepcrystalVisor : DeepcrystalHeadBase
    {
        public override DamageClass SetClass => DamageClass.Ranged;

        public override void SetDefaults() {
            Item.width = 22;
            Item.height = 20;
            Item.value = Item.sellPrice(0, 3);
            Item.rare = ItemRarityID.Lime;
            Item.defense = 7;
        }

        public override void UpdateEquip(Player player) {
            player.GetDamage(DamageClass.Ranged) += 0.12f;
            player.GetCritChance(DamageClass.Ranged) += 8f;
            player.ammoCost80 = true;
        }
    }

    /// <summary>渊晶法冠:魔法</summary>
    [AutoloadEquip(EquipType.Head)]
    internal class DeepcrystalCrown : DeepcrystalHeadBase
    {
        public override DamageClass SetClass => DamageClass.Magic;

        public override void SetDefaults() {
            Item.width = 22;
            Item.height = 26;
            Item.value = Item.sellPrice(0, 3);
            Item.rare = ItemRarityID.Lime;
            Item.defense = 7;
        }

        public override void UpdateEquip(Player player) {
            player.GetDamage(DamageClass.Magic) += 0.12f;
            player.GetCritChance(DamageClass.Magic) += 6f;
            player.manaCost -= 0.10f;
        }
    }

    /// <summary>渊晶触冠:召唤</summary>
    [AutoloadEquip(EquipType.Head)]
    internal class DeepcrystalFeelers : DeepcrystalHeadBase
    {
        public override DamageClass SetClass => DamageClass.Summon;

        public override void SetDefaults() {
            Item.width = 26;
            Item.height = 30;
            Item.value = Item.sellPrice(0, 3);
            Item.rare = ItemRarityID.Lime;
            Item.defense = 7;
        }

        public override void UpdateEquip(Player player) {
            player.GetDamage(DamageClass.Summon) += 0.14f;
            player.maxMinions += 2;
        }
    }

    /// <summary>渊晶胸甲:通职业输出件</summary>
    [AutoloadEquip(EquipType.Body)]
    internal class DeepcrystalBreastplate : ModItem
    {
        public override string Texture => CWRConstant.Item + "Armor/DeepcrystalBreastplate";

        public override void SetDefaults() {
            Item.width = 30;
            Item.height = 20;
            Item.value = Item.sellPrice(0, 5);
            Item.rare = ItemRarityID.Lime;
            Item.defense = 12;
        }

        public override void UpdateEquip(Player player) {
            player.GetDamage(DamageClass.Generic) += 0.08f;
            player.GetCritChance(DamageClass.Generic) += 4f;
        }

        public override void AddRecipes() => DeepcrystalRecipe.Add(this, 6, 10, 6, 8, 12, 5, 8);
    }

    /// <summary>渊晶护腿:机动件,湿身提速</summary>
    [AutoloadEquip(EquipType.Legs)]
    internal class DeepcrystalGreaves : ModItem
    {
        public override string Texture => CWRConstant.Item + "Armor/DeepcrystalGreaves";

        public override void SetDefaults() {
            Item.width = 22;
            Item.height = 18;
            Item.value = Item.sellPrice(0, 4);
            Item.rare = ItemRarityID.Lime;
            Item.defense = 9;
        }

        public override void UpdateEquip(Player player) {
            player.moveSpeed += 0.15f;
            player.accFlipper = true;
            if (player.wet) {
                player.moveSpeed += 0.10f;
            }
        }

        public override void AddRecipes() => DeepcrystalRecipe.Add(this, 5, 8, 5, 7, 10, 4, 6);
    }

    /// <summary>渊晶配方:灾厄在场走深渊材料,否则走原版替代;两条都吃渊晶虾壳</summary>
    internal static class DeepcrystalRecipe
    {
        public static void Add(ModItem item, int shell, int chlorophyte, int lumenyl, int cells, int voidstone, int fin, int soul) {
            if (CWRID.Item_Lumenyl > 0 && CWRID.Item_Voidstone > 0 && CWRID.Item_DepthCells > 0) {
                item.CreateRecipe().
                    AddIngredient<SeaShrimpShell>(shell).
                    AddIngredient(CWRID.Item_Lumenyl, lumenyl).
                    AddIngredient(CWRID.Item_DepthCells, cells).
                    AddIngredient(CWRID.Item_Voidstone, voidstone).
                    AddIngredient(ItemID.ChlorophyteBar, chlorophyte).
                    AddTile(TileID.MythrilAnvil).
                    Register();
                return;
            }
            item.CreateRecipe().
                AddIngredient<SeaShrimpShell>(shell).
                AddIngredient(ItemID.ChlorophyteBar, chlorophyte + 2).
                AddIngredient(ItemID.SharkFin, fin).
                AddIngredient(ItemID.SoulofMight, soul).
                AddTile(TileID.MythrilAnvil).
                Register();
        }
    }
}
