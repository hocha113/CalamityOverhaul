using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GunCustomization
{
    public class RangedLoader : ICWRLoader
    {
        public delegate Item On_ChooseAmmo_Delegate(object obj, Item weapon);
        public static List<GlobalRanged> GlobalRangeds { get; private set; } = [];
        void ICWRLoader.LoadData() {
            GlobalRangeds = VaultUtils.GetSubclassInstances<GlobalRanged>();
            MethodBase chooseAmmoMethod = typeof(Player).GetMethod("ChooseAmmo", BindingFlags.Public | BindingFlags.Instance);
            CWRHook.Add(chooseAmmoMethod, OnChooseAmmoHook);
        }
        void ICWRLoader.UnLoadData() {
            GlobalRangeds?.Clear();
        }

        private static void ModifyInCartridgeGun(Item weapon, ref Item ammo) {
            if (!CWRLoad.ItemIsGun[weapon.type]) {
                return;
            }
            CWRItems cwrItem = weapon.CWR();
            if (!cwrItem.HasCartridgeHolder) {
                return;
            }
            //这个部分用于修复弹匣系统的伤害判定，原版只考虑背包内的弹药，所以这里需要进行拦截修改使其考虑到弹匣供弹
            if (CWRServerConfig.Instance.MagazineSystem) {
                Item newAmmo = cwrItem.GetSelectedBullets();
                if (newAmmo.type > ItemID.None) {
                    ammo = newAmmo;
                }
            }

            foreach (var gGun in GlobalRangeds) {
                Item gAmmo = gGun.ChooseAmmo(weapon);
                if (gAmmo != null) {
                    ammo = gAmmo;
                }
            }
        }

        private static void ModifyInBow(Item weapon, ref Item ammo) {
            if (!CWRLoad.ItemIsBow[weapon.type] && !CWRLoad.ItemIsCrossBow[weapon.type]) {
                return;
            }
            if (ArrowHolderUI.targetAmmo == null
                || ArrowHolderUI.targetAmmo.type == ItemID.None
                || ArrowHolderUI.targetAmmo.ammo != AmmoID.Arrow) {
                return;
            }
            foreach (var item in Main.LocalPlayer.inventory) {
                if (item.type != ArrowHolderUI.targetAmmo.type) {
                    continue;
                }
                ammo = item;
                break;
            }
        }

        public static Item OnChooseAmmoHook(On_ChooseAmmo_Delegate orig, object obj, Item weapon) {
            Item ammo = null;

            ModifyInCartridgeGun(weapon, ref ammo);
            ModifyInBow(weapon, ref ammo);

            if (ammo == null) {
                ammo = orig.Invoke(obj, weapon);
            }

            return ammo;
        }
    }
}
