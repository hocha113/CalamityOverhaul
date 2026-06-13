using InnoVault.GameSystem;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.RangedModify
{
    public class RangedLoader : ICWRLoader
    {
        public delegate Item On_ChooseAmmo_Delegate(object obj, Item weapon);
        void ICWRLoader.LoadData() {
            MethodBase chooseAmmoMethod = typeof(Player).GetMethod("ChooseAmmo", BindingFlags.Public | BindingFlags.Instance);
            VaultHook.Add(chooseAmmoMethod, OnChooseAmmoHook);
            ItemRebuildLoader.PreShootEvent += PreShootHook;
        }
        void ICWRLoader.UnLoadData() {
            ItemRebuildLoader.PreShootEvent -= PreShootHook;
        }

        private static void ModifyInBow(Item weapon, ref Item ammo) {
            if ((!GlobalBow.IsBow && !GlobalBow.IsArrow) || weapon.type == ItemID.None) {
                return;
            }

            Item targetLockAmmo = weapon.CWR().TargetLockAmmo;

            if (targetLockAmmo == null
                || targetLockAmmo.type == ItemID.None
                || targetLockAmmo.ammo != AmmoID.Arrow) {
                return;
            }

            foreach (var item in Main.LocalPlayer.inventory) {
                if (item.type != targetLockAmmo.type) {
                    continue;
                }
                ammo = item;
                break;
            }
        }

        public static Item OnChooseAmmoHook(On_ChooseAmmo_Delegate orig, object obj, Item weapon) {
            Item ammo = null;

            ModifyInBow(weapon, ref ammo);

            if (ammo == null) {
                ammo = orig.Invoke(obj, weapon);
            }

            return ammo;
        }

        private static bool PreShootHook(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback, bool defaultResult = true) {
            bool? rest;
            if (ItemOverride.TryFetchByID(item.type, out ItemOverride ritem)) {
                rest = ritem.On_Shoot(item, player, source, position, velocity, type, damage, knockback);
                if (rest.HasValue) {
                    return rest.Value;
                }
            }

            if (player.GetPlayerBladeArmEnchant()) {//我不知道为什么需要这行代码
                return false;
            }

            foreach (var g in ItemRebuildLoader.ItemLoader_Shoot_Hook.Enumerate(item)) {
                rest = g.Shoot(item, player, source, position, velocity, type, damage, knockback);
            }

            rest = ItemRebuildLoader.ProcessRemakeAction(item, (inds)
                => inds.Shoot(item, player, source, position, velocity, type, damage, knockback));

            if (rest.HasValue) {
                return rest.Value;
            }

            return true;
        }
    }
}
