using CalamityOverhaul.Content.Items.Summon.Deepclaws;
using CalamityOverhaul.Content.NPCs.SeaShrimp;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Armor.Deepcrystals
{
    /// <summary>
    /// 渊晶头盔。套装奖励:受击迸发空化反震(复用 <see cref="DeepclawSnapBurst"/>),冷却见 <see cref="DeepcrystalPlayer"/>
    /// </summary>
    [AutoloadEquip(EquipType.Head)]
    internal class DeepcrystalHelmet : ModItem
    {
        public override string Texture => CWRConstant.Item + "Armor/DeepcrystalHelmet";

        public static LocalizedText SetBonusText { get; private set; }

        public override void SetStaticDefaults() {
            SetBonusText = this.GetLocalization("SetBonus");
        }

        public override void SetDefaults() {
            Item.width = 22;
            Item.height = 26;
            Item.value = Item.sellPrice(0, 3);
            Item.rare = ItemRarityID.Lime;
            Item.defense = 12;
        }

        public override void UpdateEquip(Player player) {
            player.GetDamage(DamageClass.Generic) += 0.06f;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs) {
            return body.type == ModContent.ItemType<DeepcrystalBreastplate>()
                && legs.type == ModContent.ItemType<DeepcrystalGreaves>();
        }

        public override void UpdateArmorSet(Player player) {
            player.setBonus = SetBonusText.Value;
            player.GetModPlayer<DeepcrystalPlayer>().SetActive = true;
        }

        public override void AddRecipes() => DeepcrystalRecipe.Add(this, 4, 6, 4, 6, 8, 3, 5);
    }

    /// <summary>渊晶胸甲</summary>
    [AutoloadEquip(EquipType.Body)]
    internal class DeepcrystalBreastplate : ModItem
    {
        public override string Texture => CWRConstant.Item + "Armor/DeepcrystalBreastplate";

        public override void SetDefaults() {
            Item.width = 30;
            Item.height = 20;
            Item.value = Item.sellPrice(0, 5);
            Item.rare = ItemRarityID.Lime;
            Item.defense = 20;
        }

        public override void UpdateEquip(Player player) {
            player.endurance += 0.06f;
        }

        public override void AddRecipes() => DeepcrystalRecipe.Add(this, 6, 10, 6, 8, 12, 5, 8);
    }

    /// <summary>渊晶护腿</summary>
    [AutoloadEquip(EquipType.Legs)]
    internal class DeepcrystalGreaves : ModItem
    {
        public override string Texture => CWRConstant.Item + "Armor/DeepcrystalGreaves";

        public override void SetDefaults() {
            Item.width = 22;
            Item.height = 18;
            Item.value = Item.sellPrice(0, 4);
            Item.rare = ItemRarityID.Lime;
            Item.defense = 16;
        }

        public override void UpdateEquip(Player player) {
            player.moveSpeed += 0.12f;
            player.accFlipper = true;
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

    /// <summary>渊晶套装每玩家状态:空化反震冷却。owner 侧触发,不入库</summary>
    internal class DeepcrystalPlayer : ModPlayer
    {
        private const int BurstCooldown = 240;
        private const int BurstBaseDamage = 160;

        public bool SetActive;
        private uint burstReadyAt;

        public override void ResetEffects() => SetActive = false;

        public override void OnHurt(Player.HurtInfo info) {
            if (!SetActive || Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (Main.GameUpdateCount < burstReadyAt) {
                return;
            }
            burstReadyAt = Main.GameUpdateCount + BurstCooldown;
            int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(BurstBaseDamage);
            Projectile.NewProjectile(Player.GetSource_Misc("DeepcrystalSetBonus"), Player.Center, Vector2.Zero
                , ModContent.ProjectileType<DeepclawSnapBurst>(), damage, 8f, Player.whoAmI, 1.2f);
        }
    }
}
