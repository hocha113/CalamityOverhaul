using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using CalamityOverhaul.Content.RemakeItems;
using CalamityOverhaul.OtherMods.ImproveGame;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.RangedModify
{
    public class RangedLoader : ICWRLoader
    {
        public delegate Item On_ChooseAmmo_Delegate(object obj, Item weapon);
        public static event Func<Item, Player, bool> IsAmmunitionUnlimitedEvent;
        public static List<GlobalRanged> GlobalRangeds { get; private set; } = [];
        public static Dictionary<Type, Asset<Texture2D>> TypeToGlowAsset { get; private set; } = [];
        void ICWRLoader.LoadData() {
            GlobalRangeds = VaultUtils.GetDerivedInstances<GlobalRanged>();
            MethodBase chooseAmmoMethod = typeof(Player).GetMethod("ChooseAmmo", BindingFlags.Public | BindingFlags.Instance);
            VaultHook.Add(chooseAmmoMethod, OnChooseAmmoHook);
            ItemRebuildLoader.PreShootEvent += PreShootHook;
        }
        void ICWRLoader.LoadAsset() {
            var indss = VaultUtils.GetDerivedInstances<BaseHeldRanged>();
            TypeToGlowAsset = [];
            foreach (var ranged in indss) {
                if (ranged.GlowTexPath != "") {
                    TypeToGlowAsset.Add(ranged.GetType(), CWRUtils.GetT2DAsset(ranged.GlowTexPath));
                }
            }
            indss.Clear();
        }
        void ICWRLoader.UnLoadData() {
            GlobalRangeds?.Clear();
            TypeToGlowAsset?.Clear();
            IsAmmunitionUnlimitedEvent = null;
            ItemRebuildLoader.PreShootEvent -= PreShootHook;
        }

        /// <summary>
        /// 判断该弹药物品是否应该被视为无限弹药
        /// </summary>
        /// <param name="ammoItem">要检查的弹药物品</param>
        /// <param name="owner">要检查的玩家</param>
        /// <returns>如果弹药物品是无限的，返回<see langword="true"/>；否则返回<see langword="false"/></returns>
        public static bool IsAmmunitionUnlimited(Item ammoItem, Player owner = null) {
            if (!ammoItem.consumable) {
                return true;
            }

            owner ??= Main.LocalPlayer;
            Item weapon = owner.GetItem();
            if (weapon.type == ItemID.None) {
                return false;
            }

            if (ImproveRef.LuiAFKSetAmmoIsNoConsume(ammoItem)) {//适配LuiAFK
                return true;
            }

            if (ImproveRef.ImproveGameSetAmmoIsNoConsume(ammoItem)) {//适配ImproveGame
                return true;
            }

            bool reset = false;
            if (IsAmmunitionUnlimitedEvent != null) {
                reset = IsAmmunitionUnlimitedEvent.Invoke(ammoItem, owner);
            }

            return reset;
        }

        private static void ModifyInCartridgeGun(Item weapon, ref Item ammo) {
            if (!CWRLoad.ItemIsGun[weapon.type]) {
                return;
            }
            CWRItem cwrItem = weapon.CWR();
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

            ModifyInCartridgeGun(weapon, ref ammo);
            ModifyInBow(weapon, ref ammo);

            if (ammo == null) {
                ammo = orig.Invoke(obj, weapon);
            }

            return ammo;
        }

        private static bool PreShootHook(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback, bool defaultResult = true) {
            if (HandlerCanOverride.CanOverrideByID.TryGetValue(item.type, out bool value) && !value) {
                //return orig.Invoke(item, player, source, position, velocity, type, damage, knockback);
                return true;
            }

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

            if (!CWRLoad.ItemIsHeldSwing[item.type]) {//手持挥舞类的物品不能直接调用gItem的Shoot，所以这里判断一下
                foreach (var g in ItemRebuildLoader.ItemLoader_Shoot_Hook.Enumerate(item)) {
                    rest = g.Shoot(item, player, source, position, velocity, type, damage, knockback);
                }
            }

            rest = ItemRebuildLoader.ProcessRemakeAction(item, (inds)
                => inds.Shoot(item, player, source, position, velocity, type, damage, knockback));

            if ((!rest.HasValue || rest.Value)
                && CWRLoad.ItemIsHeldSwing[item.type] && !CWRLoad.ItemIsHeldSwingDontStopOrigShoot[item.type]) {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
                return false;
            }

            if (rest.HasValue) {
                return rest.Value;
            }

            return true;
        }
    }
}
