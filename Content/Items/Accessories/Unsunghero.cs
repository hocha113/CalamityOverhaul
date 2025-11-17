using CalamityMod;
using CalamityMod.Balancing;
using CalamityMod.CalPlayer;
using InnoVault.GameSystem;
using System;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class Unsunghero : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Accessorie + "Unsunghero";
        void ICWRLoader.LoadData() {
            var math = typeof(CalamityPlayer).GetMethod("ProvideStealthStatBonuses", BindingFlags.Instance | BindingFlags.NonPublic);
            VaultHook.Add(math, OnProvideStealthStatBonusesHook);
        }
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(0, 15, 22, 0);
            Item.rare = ItemRarityID.Orange;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.CWR().IsUnsunghero = true;
            player.Calamity().rogueStealthMax += 0.1f;
        }

        private static void OnProvideStealthStatBonusesHook(Action<CalamityPlayer> orig, CalamityPlayer calamityPlayer) {
            if (calamityPlayer.Player.CWR().IsUnsunghero) {
                if (!calamityPlayer.wearingRogueArmor || calamityPlayer.rogueStealthMax <= 0) {
                    return;
                }

                Item item = calamityPlayer.Player.GetItem();
                int realUseTime = Math.Max(item.useTime, item.useAnimation);
                double useTimeFactor = 0.75 + 0.75 * Math.Log(realUseTime + 2D, 4D);
                //直接使用固定的基础时间，固定为 4 秒
                double stealthGenFactor = Math.Max(Math.Pow(4f, 2D / 3D), 1.5);

                double stealthAddedDamage = calamityPlayer.rogueStealth * BalancingConstants.UniversalStealthStrikeDamageFactor * useTimeFactor * stealthGenFactor;
                calamityPlayer.stealthDamage += (float)stealthAddedDamage;

                calamityPlayer.Player.aggro -= (int)(calamityPlayer.rogueStealth * 300f);

                return;
            }

            orig.Invoke(calamityPlayer);
        }
    }
}
