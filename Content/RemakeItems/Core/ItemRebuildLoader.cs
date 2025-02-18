using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using static CalamityOverhaul.Content.RemakeItems.Core.ItemOverride;

namespace CalamityOverhaul.Content.RemakeItems.Core
{
    //关于物品重制节点的钩子均挂载于此处
    internal class ItemRebuildLoader : GlobalItem, ICWRLoader
    {
        #region NetWork
        public static void ResetValueByWorld(int type, Player player) {
            //重新设置一次物品的属性
            foreach (var item in player.inventory) {
                if (item.type != type) {
                    continue;
                }
                item.SetDefaults(type);
            }

            //清理掉可能的手持弹幕
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.hostile || proj.ModProjectile == null || proj.owner != player.whoAmI) {
                    continue;
                }
                if (proj.ModProjectile is BaseHeldProj held) {
                    held.Projectile.Kill();
                    held.Projectile.netUpdate = true;
                }
            }
        }

        public static void SendModifiIntercept(Item handItem, Player player) {
            if (!CWRServerConfig.Instance.ModifiIntercept) {
                return;
            }

            int type = handItem.type;
            CanOverrideByID[type] = !CanOverrideByID[type];

            SoundEngine.PlaySound(SoundID.DD2_BetsySummon);

            ResetValueByWorld(type, player);

            if (VaultUtils.isClient) {
                if (CanOverrideByID[type]) {
                    VaultUtils.Text(player.name + " Modify item enabled " + handItem.ToString(), Color.Goldenrod);
                }
                else {
                    VaultUtils.Text(player.name + " The modified item was blocked " + handItem.ToString(), Color.Red);
                }

                ModPacket modPacket = CWRMod.Instance.GetPacket();
                modPacket.Write((byte)CWRMessageType.ModifiIntercept_InGame);
                modPacket.Write(type);
                modPacket.Write(CanOverrideByID[type]);
                modPacket.Send();
            }
        }

        public static void NetModifiIntercept_InGame(BinaryReader reader, int whoAmI) {
            int key = reader.ReadInt32();
            bool value = reader.ReadBoolean();

            if (CanOverrideByID.ContainsKey(key)) {
                CanOverrideByID[key] = value;
            }

            ResetValueByWorld(key, Main.LocalPlayer);

            if (VaultUtils.isServer) {
                ModPacket modPacket = CWRMod.Instance.GetPacket();
                modPacket.Write((byte)CWRMessageType.ModifiIntercept_InGame);
                modPacket.Write(key);
                modPacket.Write(value);
                modPacket.Write(whoAmI);//在服务端上的whoAmI指向发生改动的玩家索引，所以这里自然要记录一下
                modPacket.Send(-1, whoAmI);
            }
            else {//如果是客户端，则打印来源端的消息
                int fromePlayer = reader.ReadInt32();//这里接收来自服务端记录的来源玩家索引
                if (CanOverrideByID[key]) {
                    VaultUtils.Text(Main.player[fromePlayer].name + " Modify item enabled " + new Item(key).ToString(), Color.Goldenrod);
                }
                else {
                    VaultUtils.Text(Main.player[fromePlayer].name + " The modified item was blocked " + new Item(key).ToString(), Color.Red);
                }
            }
        }

        public static void NetModifiInterceptEnterWorld_Server(BinaryReader reader, int whoAmI) {
            if (!VaultUtils.isServer) {
                return;
            }
            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.ModifiIntercept_EnterWorld_ToClient);
            modPacket.Write(CanOverrideByID.Count);
            foreach (var pair in CanOverrideByID) {
                modPacket.Write(pair.Key);
                modPacket.Write(pair.Value);
            }
            modPacket.Send(whoAmI);
        }

        public static void NetModifiInterceptEnterWorld_Client(BinaryReader reader, int whoAmI) {
            if (!VaultUtils.isClient) {
                return;
            }
            CanOverrideByID = [];
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++) {
                CanOverrideByID.Add(reader.ReadInt32(), reader.ReadBoolean());
            }
        }

        public static void ModifiIntercept_OnEnterWorld() {
            if (!CWRServerConfig.Instance.ModifiIntercept || !VaultUtils.isClient) {
                return;
            }

            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.ModifiIntercept_EnterWorld_Request);
            modPacket.Send();
        }
        #endregion

        #region On and IL
        internal delegate bool On_Item_Dalegate(Item item);
        internal delegate bool On_AllowPrefix_Dalegate(Item item, int pre);
        internal delegate void On_SetDefaults_Dalegate(Item item, bool createModItem = true);
        internal delegate bool On_Shoot_Dalegate(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback, bool defaultResult = true);
        internal delegate void On_ModifyShootStats_Delegate(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback);
        internal delegate void On_HitNPC_Delegate(Item item, Player player, NPC target, in NPC.HitInfo hit, int damageDone);
        internal delegate void On_HitPvp_Delegate(Item item, Player player, Player target, Player.HurtInfo hurtInfo);
        internal delegate void On_ModifyHitNPC_Delegate(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers);
        internal delegate bool On_CanUseItem_Delegate(Item item, Player player);
        internal delegate bool On_PreDrawInInventory_Delegate(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale);
        internal delegate bool? On_UseItem_Delegate(Item item, Player player);
        internal delegate void On_UseAnimation_Delegate(Item item, Player player);
        internal delegate void On_ModifyWeaponCrit_Delegate(Item item, Player player, ref float crit);
        internal delegate void On_ModifyItemLoot_Delegate(Item item, ItemLoot itemLoot);
        internal delegate bool On_CanConsumeAmmo_Delegate(Item weapon, Item ammo, Player player);
        internal delegate void On_ModifyWeaponDamage_Delegate(Item item, Player player, ref StatModifier damage);
        internal delegate void On_UpdateAccessory_Delegate(Item item, Player player, bool hideVisual);
        internal delegate bool On_AltFunctionUse_Delegate(Item item, Player player);
        internal delegate void On_ModItem_ModifyTooltips_Delegate(object obj, List<TooltipLine> list);
        internal delegate string On_GetItemNameValue_Delegate(int id);
        internal delegate string On_GetItemName_get_Delegate(Item item);

        public static Type itemLoaderType;
        public static MethodBase onMeleePrefixMethod;
        public static MethodBase onRangedPrefixMethod;
        public static MethodBase onAllowPrefixMethod;
        public static MethodBase onSetDefaultsMethod;
        public static MethodBase onShootMethod;
        public static MethodBase onModifyShootStatsMethod;
        public static MethodBase onHitNPCMethod;
        public static MethodBase onHitPvpMethod;
        public static MethodBase onModifyHitNPCMethod;
        public static MethodBase onCanUseItemMethod;
        public static MethodBase onPreDrawInInventoryMethod;
        public static MethodBase onUseItemMethod;
        public static MethodBase onUseAnimationMethod;
        public static MethodBase onModifyWeaponCritMethod;
        public static MethodBase onModifyItemLootMethod;
        public static MethodBase onCanConsumeAmmoMethod;
        public static MethodBase onModifyWeaponDamageMethod;
        public static MethodBase onUpdateAccessoryMethod;
        public static MethodBase onAltFunctionUseMethod;
        public static MethodBase onGetItemNameValueMethod;
        public static MethodBase onItemNamePropertyGetMethod;
        public static MethodBase onAffixNameMethod;

        void ICWRLoader.LoadAsset() {
            TextureAssets.Item[ItemID.IceSickle] = CWRUtils.GetT2DAsset(CWRConstant.Item_Melee + "IceSickle");
        }

        void ICWRLoader.SetupData() {
            CWRMod.Instance.Logger.Info($"{ByID.Count} key pair is loaded into the RItemIndsDict");
        }

        void ICWRLoader.LoadData() {
            Instances ??= [];
            Instances.Clear();
            ByID ??= [];
            ByID.Clear();

            itemLoaderType = typeof(ItemLoader);
            onSetDefaultsMethod = itemLoaderType.GetMethod("SetDefaults", BindingFlags.NonPublic | BindingFlags.Static);
            onShootMethod = itemLoaderType.GetMethod("Shoot", BindingFlags.Public | BindingFlags.Static);
            onModifyShootStatsMethod = itemLoaderType.GetMethod("ModifyShootStats", BindingFlags.Public | BindingFlags.Static);
            onHitNPCMethod = itemLoaderType.GetMethod("OnHitNPC", BindingFlags.Public | BindingFlags.Static);
            onHitPvpMethod = itemLoaderType.GetMethod("OnHitPvp", BindingFlags.Public | BindingFlags.Static);
            onModifyHitNPCMethod = itemLoaderType.GetMethod("ModifyHitNPC", BindingFlags.Public | BindingFlags.Static);
            onCanUseItemMethod = itemLoaderType.GetMethod("CanUseItem", BindingFlags.Public | BindingFlags.Static);
            onPreDrawInInventoryMethod = itemLoaderType.GetMethod("PreDrawInInventory", BindingFlags.Public | BindingFlags.Static);
            onUseItemMethod = itemLoaderType.GetMethod("UseItem", BindingFlags.Public | BindingFlags.Static);
            onUseAnimationMethod = itemLoaderType.GetMethod("UseAnimation", BindingFlags.Public | BindingFlags.Static);
            onModifyWeaponCritMethod = itemLoaderType.GetMethod("ModifyWeaponCrit", BindingFlags.Public | BindingFlags.Static);
            onModifyItemLootMethod = itemLoaderType.GetMethod("ModifyItemLoot", BindingFlags.Public | BindingFlags.Static);
            onCanConsumeAmmoMethod = itemLoaderType.GetMethod("CanConsumeAmmo", BindingFlags.Public | BindingFlags.Static);
            onModifyWeaponDamageMethod = itemLoaderType.GetMethod("ModifyWeaponDamage", BindingFlags.Public | BindingFlags.Static);
            onUpdateAccessoryMethod = itemLoaderType.GetMethod("UpdateAccessory", BindingFlags.Public | BindingFlags.Static);
            onAltFunctionUseMethod = itemLoaderType.GetMethod("AltFunctionUse", BindingFlags.Public | BindingFlags.Static);
            onAllowPrefixMethod = itemLoaderType.GetMethod("AllowPrefix", BindingFlags.Public | BindingFlags.Static);
            onMeleePrefixMethod = itemLoaderType.GetMethod("MeleePrefix", BindingFlags.NonPublic | BindingFlags.Static);
            onRangedPrefixMethod = itemLoaderType.GetMethod("RangedPrefix", BindingFlags.NonPublic | BindingFlags.Static);
            onGetItemNameValueMethod = typeof(Lang).GetMethod("GetItemNameValue", BindingFlags.Public | BindingFlags.Static);
            onItemNamePropertyGetMethod = typeof(Item).GetProperty("Name", BindingFlags.Instance | BindingFlags.Public).GetGetMethod();
            onAffixNameMethod = typeof(Item).GetMethod("AffixName", BindingFlags.Instance | BindingFlags.Public);
            //这个钩子的挂载最终还是被废弃掉，因为会与一些二次继承了ModItem类的第三方模组发生严重的错误，我目前无法解决这个，所以放弃了这个钩子的挂载
            //if (onSetDefaultsMethod != null && !ModLoader.HasMod("MagicBuilder")) {
            //    CWRHook.Add(onSetDefaultsMethod, OnSetDefaultsHook);
            //}
            if (onShootMethod != null) {
                CWRHook.Add(onShootMethod, OnShootHook);
            }
            if (onModifyShootStatsMethod != null) {
                CWRHook.Add(onModifyShootStatsMethod, OnModifyShootStatsHook);
            }
            if (onHitNPCMethod != null) {
                CWRHook.Add(onHitNPCMethod, OnHitNPCHook);
            }
            if (onHitPvpMethod != null) {
                CWRHook.Add(onHitPvpMethod, OnHitPvpHook);
            }
            if (onModifyHitNPCMethod != null) {
                CWRHook.Add(onModifyHitNPCMethod, OnModifyHitNPCHook);
            }
            if (onCanUseItemMethod != null) {
                CWRHook.Add(onCanUseItemMethod, OnCanUseItemHook);
            }
            if (onPreDrawInInventoryMethod != null) {
                CWRHook.Add(onPreDrawInInventoryMethod, OnPreDrawInInventoryHook);
            }
            if (onUseItemMethod != null) {
                CWRHook.Add(onUseItemMethod, OnUseItemHook);
            }
            if (onUseAnimationMethod != null) {
                CWRHook.Add(onUseAnimationMethod, OnUseAnimationHook);
            }
            if (onModifyWeaponCritMethod != null) {
                CWRHook.Add(onModifyWeaponCritMethod, OnModifyWeaponCritHook);
            }
            if (onModifyItemLootMethod != null) {
                CWRHook.Add(onModifyItemLootMethod, OnModifyItemLootHook);
            }
            if (onCanConsumeAmmoMethod != null) {
                CWRHook.Add(onCanConsumeAmmoMethod, OnCanConsumeAmmoHook);
            }
            if (onModifyWeaponDamageMethod != null) {
                CWRHook.Add(onModifyWeaponDamageMethod, OnModifyWeaponDamageHook);
            }
            if (onUpdateAccessoryMethod != null) {
                CWRHook.Add(onUpdateAccessoryMethod, OnUpdateAccessoryHook);
            }
            if (onAltFunctionUseMethod != null) {
                CWRHook.Add(onAltFunctionUseMethod, OnAltFunctionUseHook);
            }
            if (onMeleePrefixMethod != null) {
                CWRHook.Add(onMeleePrefixMethod, OnMeleePrefixHook);
            }
            if (onRangedPrefixMethod != null) {
                CWRHook.Add(onRangedPrefixMethod, OnRangedPrefixHook);
            }
            if (onAllowPrefixMethod != null) {
                CWRHook.Add(onAllowPrefixMethod, OnAllowPrefixHook);
            }
            if (onGetItemNameValueMethod != null) {
                //CWRHook.Add(onGetItemNameValueMethod, OnGetItemNameValueHook);
            }
            if (onItemNamePropertyGetMethod != null) {
                CWRHook.Add(onItemNamePropertyGetMethod, On_Name_Get_Hook);
            }
            if (onAffixNameMethod != null) {
                CWRHook.Add(onAffixNameMethod, OnAffixNameHook);
            }
        }

        void ICWRLoader.UnLoadData() {
            Instances?.Clear();
            ByID?.Clear();

            itemLoaderType = null;
            onSetDefaultsMethod = null;
            onShootMethod = null;
            onHitNPCMethod = null;
            onHitPvpMethod = null;
            onModifyHitNPCMethod = null;
            onCanUseItemMethod = null;
            onPreDrawInInventoryMethod = null;
            onUseItemMethod = null;
            onUseAnimationMethod = null;
            onModifyWeaponCritMethod = null;
            onModifyItemLootMethod = null;
            onCanConsumeAmmoMethod = null;
            onModifyWeaponDamageMethod = null;
            onUpdateAccessoryMethod = null;
            onAltFunctionUseMethod = null;
            onMeleePrefixMethod = null;
            onRangedPrefixMethod = null;
            onAllowPrefixMethod = null;
            onGetItemNameValueMethod = null;
            onItemNamePropertyGetMethod = null;
            onAffixNameMethod = null;
        }

        //public string OnGetItemNameValueHook(On_GetItemNameValue_Delegate orig, int id) {
        //    if (ItemIDToOverrideDic.TryGetValue(id, out var itemOverride)) {
        //        return itemOverride.DisplayName.Value;
        //    }
        //    return orig.Invoke(id);
        //}

        /// <summary>
        /// <br>这个钩子非常危险，未来很可能移除，因为它钩的是属性的get行为，这可能会带来较大的性能开销和适配性问题，同时，编写代码时也得非常小心，否则可能引起无限迭代让游戏闪退</br>
        /// <br>为什么修改物品名字不使用 Item.SetNameOverride() ？因为这会导致一个难以解决的问题，详情见<see href="https://github.com/tModLoader/tModLoader/issues/4467#issuecomment-2623220787"/> </br>
        /// <br>所以我使用了两个钩子来解决这个名称的覆盖显示，On_Name_Get_Hook改变了Item.Name返回值，因为Name被使用的地方非常多，所以这个钩子需要多加考察才能确认其安全性</br> 
        /// <br>OnAffixNameHook用于改变UI获取物品名字的方式，(不知道为何，明明AffixName的返回值是基于Item.Name的，但On_Name_Get_Hook的修改没能在这上面起作用)</br> 
        /// <br>直观来讲，一个负责改变UI上显示的名字(OnAffixNameHook)，一个负责改变逻辑数据，使其在搜索框之类的功能中能够被以新名字检索到(OnAffixNameHook)</br> 
        /// </summary>
        public string On_Name_Get_Hook(On_GetItemName_get_Delegate orig, Item item) {
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                return ritem.DisplayName.Value;
            }
            return orig.Invoke(item);
        }

        public string OnAffixNameHook(On_GetItemName_get_Delegate orig, Item item) {
            bool onOverd = false;
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                item.SetNameOverride(ritem.DisplayName.Value);
                onOverd = true;
            }
            string forgtName = orig.Invoke(item);//这是个很取巧的办法，保证了兼容性
            if (onOverd) {
                item.ClearNameOverride();
            }
            return forgtName;
        }

        public bool OnAllowPrefixHook(On_AllowPrefix_Dalegate orig, Item item, int pre) {
            if (item.type == ItemID.None) {
                return false;
            }
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                bool? rasg = ritem.On_AllowPreFix(item, pre);
                if (rasg.HasValue) {
                    return rasg.Value;
                }
            }
            if (item.CWR().GetAllowPrefix) {
                return true;
            }
            return orig.Invoke(item, pre);
        }

        public bool OnMeleePrefixHook(On_Item_Dalegate orig, Item item) {
            if (item.type == ItemID.None) {
                return false;
            }
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                bool? rasg = ritem.On_MeleePreFix(item);
                if (rasg.HasValue) {
                    return rasg.Value;
                }
            }
            if (item.CWR().GetMeleePrefix) {
                return true;
            }
            return orig.Invoke(item);
        }

        public bool OnRangedPrefixHook(On_Item_Dalegate orig, Item item) {
            if (item.type == ItemID.None) {
                return false;
            }
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                bool? rasg = ritem.On_RangedPreFix(item);
                if (rasg.HasValue) {
                    return rasg.Value;
                }
            }
            if (item.CWR().GetRangedPrefix) {
                return true;
            }
            return orig.Invoke(item);
        }

        /// <summary>
        /// 提前于TML的方法执行，这样继承重写<br/><see cref="ItemOverride.On_AltFunctionUse"/><br/>便拥有可以阻断TML后续方法运行的能力，用于进行一些高级修改
        /// </summary>
        public bool OnAltFunctionUseHook(On_AltFunctionUse_Delegate orig, Item item, Player player) {
            if (item.IsAir) {
                return false;
            }
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                bool? rasg = ritem.On_AltFunctionUse(item, player);
                if (rasg.HasValue) {
                    return rasg.Value;
                }
            }
            return orig.Invoke(item, player);
        }
        /// <summary>
        /// 提前于TML的方法执行，这样继承重写<br/><see cref="ItemOverride.On_UpdateAccessory"/><br/>便拥有可以阻断TML后续方法运行的能力，用于进行一些高级修改
        /// </summary>
        public void OnUpdateAccessoryHook(On_UpdateAccessory_Delegate orig, Item item, Player player, bool hideVisual) {
            if (item.IsAir) {
                return;
            }
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                bool rasg = ritem.On_UpdateAccessory(item, player, hideVisual);
                if (!rasg) {
                    return;
                }
            }
            orig.Invoke(item, player, hideVisual);
        }
        /// <summary>
        /// 提前于TML的方法执行，这样继承重写<br/><see cref="ItemOverride.On_CanConsumeAmmo"/><br/>便拥有可以阻断TML后续方法运行的能力，用于进行一些高级修改
        /// </summary>
        public void OnModifyWeaponDamageHook(On_ModifyWeaponDamage_Delegate orig, Item item, Player player, ref StatModifier damage) {
            if (item.IsAir) {
                return;
            }
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                bool rasg = ritem.On_ModifyWeaponDamage(item, player, ref damage);
                if (!rasg) {
                    return;
                }
            }
            orig.Invoke(item, player, ref damage);
        }

        /// <summary>
        /// 提前于TML的方法执行，这样继承重写<br/><see cref="ItemOverride.On_CanConsumeAmmo"/><br/>便拥有可以阻断TML后续方法运行的能力，用于进行一些高级修改
        /// </summary>
        public bool OnCanConsumeAmmoHook(On_CanConsumeAmmo_Delegate orig, Item item, Item ammo, Player player) {
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                bool? rasg = ritem.On_CanConsumeAmmo(item, ammo, player);
                if (rasg.HasValue) {
                    return rasg.Value;
                }
            }

            return orig.Invoke(item, ammo, player);
        }
        /// <summary>
        /// 这个钩子用于挂载一个提前于TML方法的<see cref="ItemLoader.ModifyItemLoot"/>，以此来进行一些高级的修改
        /// </summary>
        /// <param name="orig"></param>
        /// <param name="item"></param>
        /// <param name="player"></param>
        /// <returns></returns>
        public void OnModifyItemLootHook(On_ModifyItemLoot_Delegate orig, Item item, ItemLoot itemLoot) {
            bool? result = null;
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                result = ritem.On_ModifyItemLoot(item, itemLoot);
            }
            if (result.HasValue) {
                if (result.Value) {
                    item.ModItem?.ModifyItemLoot(itemLoot);
                    return;
                }
                else {
                    return;
                }
            }
            orig.Invoke(item, itemLoot);
        }
        /// <summary>
        /// 这个钩子用于挂载一个提前于TML方法的<see cref="ItemLoader.ModifyWeaponCrit"/>，以此来进行一些高级的修改
        /// </summary>
        /// <param name="orig"></param>
        /// <param name="item"></param>
        /// <param name="player"></param>
        /// <returns></returns>
        public void OnModifyWeaponCritHook(On_ModifyWeaponCrit_Delegate orig, Item item, Player player, ref float crit) {
            bool? result = null;
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                result = ritem.On_ModifyWeaponCrit(item, player, ref crit);
            }
            if (result.HasValue) {
                if (result.Value) {
                    item.ModItem?.ModifyWeaponCrit(player, ref crit);
                    return;
                }
                else {
                    return;
                }
            }
            orig.Invoke(item, player, ref crit);
        }
        /// <summary>
        /// 这个钩子用于挂载一个提前于TML方法的<see cref="ItemLoader.UseAnimation(Item, Player)"/>，以此来进行一些高级的修改
        /// </summary>
        /// <param name="orig"></param>
        /// <param name="item"></param>
        /// <param name="player"></param>
        /// <returns></returns>
        public void OnUseAnimationHook(On_UseAnimation_Delegate orig, Item item, Player player) {
            bool? result = null;
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                result = ritem.On_UseAnimation(item, player);
            }
            if (result.HasValue) {
                if (result.Value) {
                    item.ModItem?.UseAnimation(player);
                    return;
                }
                else {
                    return;
                }
            }
            orig.Invoke(item, player);
        }
        /// <summary>
        /// 这个钩子用于挂载一个提前于TML方法的<see cref="ItemLoader.UseItem(Item, Player)"/>，以此来进行一些高级的修改
        /// </summary>
        /// <param name="orig"></param>
        /// <param name="item"></param>
        /// <param name="player"></param>
        /// <returns></returns>
        public bool? OnUseItemHook(On_UseItem_Delegate orig, Item item, Player player) {
            bool? result = null;
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                result = ritem.On_UseItem(item, player);
            }
            return result.HasValue ? result.Value : orig(item, player);
        }

        /// <summary>
        /// 这个钩子用于挂载一个提前于TML方法的SetDefaults，以此来进行一些高级的修改
        /// </summary>
        [Obsolete]
        public void OnSetDefaultsHook(On_SetDefaults_Dalegate orig, Item item, bool createModItem) {
            orig.Invoke(item, true);
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                ritem.On_PostSetDefaults(item);
            }
        }
        /// <summary>
        /// 提前于TML的方法执行，这样继承重写<br/><see cref="ItemOverride.On_Shoot"/><br/>便拥有可以阻断TML后续方法运行的能力，用于进行一些高级修改
        /// </summary>
        public bool OnShootHook(On_Shoot_Dalegate orig, Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback, bool defaultResult) {
            bool? rest;
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                rest = ritem.On_Shoot(item, player, source, position, velocity, type, damage, knockback);
                if (rest.HasValue) {
                    return rest.Value;
                }
            }

            if (player.Calamity().bladeArmEnchant) {//我不知道为什么需要这行代码
                return false;
            }

            if (!CWRLoad.ItemIsHeldSwing[item.type]) {//手持挥舞类的物品不能直接调用gItem的Shoot，所以这里判断一下
                foreach (var g in RangedLoader.ItemLoader_Shoot_Hook.Enumerate(item)) {
                    rest = g.Shoot(item, player, source, position, velocity, type, damage, knockback);
                }
            }

            rest = ProcessRemakeAction(item, (inds) => inds.Shoot(item, player, source, position, velocity, type, damage, knockback));

            if ((!rest.HasValue || rest.Value) && CWRLoad.ItemIsHeldSwing[item.type] && !CWRLoad.ItemIsHeldSwingDontStopOrigShoot[item.type]) {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
                return false;
            }

            if (rest.HasValue) {
                return rest.Value;
            }

            return orig.Invoke(item, player, source, position, velocity, type, damage, knockback);
        }
        /// <summary>
        /// 提前于TML的方法执行，这样继承重写<br/><see cref="ItemOverride.On_ModifyShootStats(Item, Player, ref Vector2, ref Vector2, ref int, ref int, ref float)"/><br/>便拥有可以阻断TML后续方法运行的能力，用于进行一些高级修改
        /// </summary>
        public void OnModifyShootStatsHook(On_ModifyShootStats_Delegate orig, Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {//
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                bool rasg = ritem.On_ModifyShootStats(item, player, ref position, ref velocity, ref type, ref damage, ref knockback);
                if (!rasg) {
                    return;
                }
            }

            orig.Invoke(item, player, ref position, ref velocity, ref type, ref damage, ref knockback);
        }
        /// <summary>
        /// 提前于TML的方法执行，这个钩子可以用来做到<see cref="GlobalItem.CanUseItem"/>无法做到的修改效果，比如让一些原本不可使用的物品可以使用，
        /// <br/>继承重写<see cref="ItemOverride.On_CanUseItem(Item, Player)"/>来达到这些目的，用于进行一些高级修改
        /// </summary>
        public bool OnCanUseItemHook(On_CanUseItem_Delegate orig, Item item, Player player) {
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                //这个钩子的运作原理有些不同，因为这个目标函数的返回值应该直接起到作用，而不是简单的返回Void类型
                bool? rasg = ritem.On_CanUseItem(item, player);//运行OnUseItem获得钩子函数的返回值，这应该起到传递的作用
                if (rasg.HasValue) {//如果rasg不为空，那么直接返回这个值让钩子的传递起效
                    return rasg.Value;//如果rasg不包含实际值，那么在这次枚举中就什么都不做
                }
            }

            return orig.Invoke(item, player);
        }

        public void OnHitNPCHook(On_HitNPC_Delegate orig, Item item, Player player, NPC target, in NPC.HitInfo hit, int damageDone) {
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                //这个钩子的运作原理有些不同，因为这个目标函数的返回值应该直接起到作用，而不是简单的返回Void类型
                bool? rasg = ritem.On_OnHitNPC(item, player, target, hit, damageDone);//运行OnUseItem获得钩子函数的返回值，这应该起到传递的作用
                if (rasg.HasValue) {//如果rasg不为空，那么直接返回这个值让钩子的传递起效
                    if (!rasg.Value) {
                        return;
                    }
                }
            }

            orig.Invoke(item, player, target, hit, damageDone);
        }

        public void OnHitPvpHook(On_HitPvp_Delegate orig, Item item, Player player, Player target, Player.HurtInfo hurtInfo) {
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                bool? rasg = ritem.On_OnHitPvp(item, player, target, hurtInfo);
                if (rasg.HasValue) {
                    if (!rasg.Value) {
                        return;
                    }
                }
            }

            orig.Invoke(item, player, target, hurtInfo);
        }

        public void OnModifyHitNPCHook(On_ModifyHitNPC_Delegate orig, Item item, Player player, NPC target, ref NPC.HitModifiers modifiers) {
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                bool? rasg = ritem.On_ModifyHitNPC(item, player, target, ref modifiers);
                if (rasg.HasValue) {
                    if (rasg.Value) {//如果返回了true，那么执行原物品的该方法
                        item.ModItem?.ModifyHitNPC(player, target, ref modifiers);
                        return;
                    }
                    else {//否则返回false，那么后续的就什么都不执行
                        return;
                    }
                }
            }

            orig.Invoke(item, player, target, ref modifiers);
        }

        public bool OnPreDrawInInventoryHook(On_PreDrawInInventory_Delegate orig, Item item, SpriteBatch spriteBatch
            , Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                bool rasg = ritem.On_PreDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale);
                if (!rasg) {
                    return false;
                }
            }

            return orig.Invoke(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale);
        }
        #endregion

        #region Loader Item Hook
        public static void ProcessRemakeAction(Item item, Action<ItemOverride> action) {
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                action(ritem);
            }
        }

        public static bool? ProcessRemakeAction(Item item, Func<ItemOverride, bool?> action) {
            bool? result = null;
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                result = action(ritem);
            }
            return result;
        }

        public override void SetDefaults(Item item) {
            ProcessRemakeAction(item, (inds) => inds.SetDefaults(item));
        }

        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            ProcessRemakeAction(item, (inds) => inds.PostDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale));
        }

        public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.PreDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale));
            return rest ?? base.PreDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale);
        }

        public override bool AllowPrefix(Item item, int pre) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.AllowPrefix(item, pre));
            return rest ?? base.AllowPrefix(item, pre);
        }

        public override bool AltFunctionUse(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.AltFunctionUse(item, player));
            return rest ?? base.AltFunctionUse(item, player);
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) {
            bool? rest = ProcessRemakeAction(equippedItem, (inds) => inds.CanAccessoryBeEquippedWith(equippedItem, incomingItem, player));
            return rest ?? base.CanAccessoryBeEquippedWith(equippedItem, incomingItem, player);
        }

        public override bool? CanBeChosenAsAmmo(Item ammo, Item weapon, Player player) {
            bool? rest = ProcessRemakeAction(ammo, (inds) => inds.CanBeChosenAsAmmo(ammo, weapon, player));
            return rest ?? base.CanBeChosenAsAmmo(ammo, weapon, player);
        }

        public override bool CanBeConsumedAsAmmo(Item ammo, Item weapon, Player player) {
            bool? rest = ProcessRemakeAction(ammo, (inds) => inds.CanBeConsumedAsAmmo(ammo, weapon, player));
            return rest ?? base.CanBeConsumedAsAmmo(ammo, weapon, player);
        }

        public override bool? CanCatchNPC(Item item, NPC target, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanCatchNPC(item, target, player));
            return rest ?? base.CanCatchNPC(item, target, player);
        }

        public override bool CanEquipAccessory(Item item, Player player, int slot, bool modded) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanEquipAccessory(item, player, slot, modded));
            return rest ?? base.CanEquipAccessory(item, player, slot, modded);
        }

        public override bool? CanHitNPC(Item item, Player player, NPC target) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanHitNPC(item, player, target));
            return rest ?? base.CanHitNPC(item, player, target);
        }

        public override bool CanHitPvp(Item item, Player player, Player target) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanHitPvp(item, player, target));
            return rest ?? base.CanHitPvp(item, player, target);
        }

        public override bool? CanMeleeAttackCollideWithNPC(Item item, Rectangle meleeAttackHitbox, Player player, NPC target) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanMeleeAttackCollideWithNPC(item, meleeAttackHitbox, player, target));
            return rest ?? base.CanMeleeAttackCollideWithNPC(item, meleeAttackHitbox, player, target);
        }

        public override bool CanPickup(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanPickup(item, player));
            return rest ?? base.CanPickup(item, player);
        }

        public override bool CanReforge(Item item) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanReforge(item));
            return rest ?? base.CanReforge(item);
        }

        public override bool CanResearch(Item item) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanResearch(item));
            return rest ?? base.CanResearch(item);
        }

        public override bool CanRightClick(Item item) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanRightClick(item));
            return rest ?? base.CanRightClick(item);
        }

        public override bool CanShoot(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanShoot(item, player));
            return rest ?? base.CanShoot(item, player);
        }

        public override bool CanStack(Item destination, Item source) {
            bool? rest = ProcessRemakeAction(destination, (inds) => inds.CanStack(destination, source));
            return rest ?? base.CanStack(destination, source);
        }

        public override bool CanStackInWorld(Item destination, Item source) {
            bool? rest = ProcessRemakeAction(destination, (inds) => inds.CanStackInWorld(destination, source));
            return rest ?? base.CanStackInWorld(destination, source);
        }

        public override bool CanUseItem(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanUseItem(item, player));
            return rest ?? base.CanUseItem(item, player);
        }

        public override bool ConsumeItem(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.ConsumeItem(item, player));
            return rest ?? base.ConsumeItem(item, player);
        }

        public override void HoldItem(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.HoldItem(item, player));
        }

        public override void HoldItemFrame(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.HoldItemFrame(item, player));
        }

        public override void LoadData(Item item, TagCompound tag) {
            ProcessRemakeAction(item, (inds) => inds.LoadData(item, tag));
        }

        public override void MeleeEffects(Item item, Player player, Rectangle hitbox) {
            ProcessRemakeAction(item, (inds) => inds.MeleeEffects(item, player, hitbox));
        }

        public override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers) {
            NPC.HitModifiers hitNPCModifier = modifiers;
            ProcessRemakeAction(item, (inds) => inds.ModifyHitNPC(item, player, target, ref hitNPCModifier));
            modifiers = hitNPCModifier;
        }

        public override void ModifyHitPvp(Item item, Player player, Player target, ref Player.HurtModifiers modifiers) {
            Player.HurtModifiers hitPlayerModifier = modifiers;
            ProcessRemakeAction(item, (inds) => inds.ModifyHitPvp(item, player, target, ref hitPlayerModifier));
            modifiers = hitPlayerModifier;
        }

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            ProcessRemakeAction(item, (inds) => inds.ModifyItemLoot(item, itemLoot));
        }

        public override void ModifyItemScale(Item item, Player player, ref float scale) {
            float slp = scale;
            ProcessRemakeAction(item, (inds) => inds.ModifyItemScale(item, player, ref slp));
            scale = slp;
        }

        public override void ModifyManaCost(Item item, Player player, ref float reduce, ref float mult) {
            float newReduce = reduce;
            float newMult = mult;
            ProcessRemakeAction(item, (inds) => inds.ModifyManaCost(item, player, ref newReduce, ref newMult));
            reduce = newReduce;
            mult = newMult;
        }

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            ShootStats stats = new() {
                Position = position,
                Velocity = velocity,
                Type = type,
                Damage = damage,
                Knockback = knockback
            };
            ProcessRemakeAction(item, (inds) => inds.ModifyShootStats(item, player, ref stats));
            position = stats.Position;
            velocity = stats.Velocity;
            type = stats.Type;
            damage = stats.Damage;
            knockback = stats.Knockback;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            if (TryFetchByID(item.type, out ItemOverride ritem)) {
                CWRItems.OverModifyTooltip(item, tooltips);
                ritem.ModifyTooltips(item, tooltips);
            }
        }

        public override void ModifyWeaponCrit(Item item, Player player, ref float crit) {
            float safeCrit = crit;
            ProcessRemakeAction(item, (inds) => inds.ModifyWeaponCrit(item, player, ref safeCrit));
            crit = safeCrit;
        }

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            StatModifier safeDamage = damage;
            ProcessRemakeAction(item, (inds) => inds.ModifyWeaponDamage(item, player, ref safeDamage));
            damage = safeDamage;
        }

        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback) {
            StatModifier safeKnockback = knockback;
            ProcessRemakeAction(item, (inds) => inds.ModifyWeaponKnockback(item, player, ref safeKnockback));
            knockback = safeKnockback;
        }

        public override void OnConsumeAmmo(Item weapon, Item ammo, Player player) {
            ProcessRemakeAction(ammo, (inds) => inds.OnConsumeAmmo(weapon, ammo, player));
        }

        public override void OnConsumedAsAmmo(Item ammo, Item weapon, Player player) {
            ProcessRemakeAction(ammo, (inds) => inds.OnConsumedAsAmmo(ammo, weapon, player));
        }

        public override void OnConsumeItem(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.OnConsumeItem(item, player));
        }

        public override void OnConsumeMana(Item item, Player player, int manaConsumed) {
            ProcessRemakeAction(item, (inds) => inds.OnConsumeMana(item, player, manaConsumed));
        }

        public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            ProcessRemakeAction(item, (inds) => inds.OnHitNPC(item, player, target, hit, damageDone));
        }

        public override void OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo) {
            ProcessRemakeAction(item, (inds) => inds.OnHitPvp(item, player, target, hurtInfo));
        }

        public override void OnMissingMana(Item item, Player player, int neededMana) {
            ProcessRemakeAction(item, (inds) => inds.OnMissingMana(item, player, neededMana));
        }

        public override bool OnPickup(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.OnPickup(item, player));
            return rest ?? base.OnPickup(item, player);
        }

        public override void OnSpawn(Item item, IEntitySource source) {
            ProcessRemakeAction(item, (inds) => inds.OnSpawn(item, source));
        }

        public override void OnStack(Item destination, Item source, int numToTransfer) {
            ProcessRemakeAction(destination, (inds) => inds.OnStack(destination, source, numToTransfer));
        }

        public override void PickAmmo(Item weapon, Item ammo, Player player, ref int type, ref float speed, ref StatModifier damage, ref float knockback) {
            int safeType = type;
            float safeSpeed = speed;
            float safeKnockback = knockback;
            StatModifier safeDamage = damage;
            ProcessRemakeAction(weapon, (inds) => inds.PickAmmo(weapon, ammo, player, ref safeType, ref safeSpeed, ref safeDamage, ref safeKnockback));
            type = safeType;
            speed = safeSpeed;
            knockback = safeKnockback;
            safeDamage = damage;
        }

        public override void RightClick(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.RightClick(item, player));
        }

        public override void SaveData(Item item, TagCompound tag) {
            ProcessRemakeAction(item, (inds) => inds.SaveData(item, tag));
        }

        //public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
        //    , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        //    if (player.Calamity().bladeArmEnchant) {//我不知道为什么需要这行代码
        //        return false;
        //    }

        //    bool? rest = ProcessRemakeAction(item, (inds) => inds.Shoot(item, player, source, position, velocity, type, damage, knockback));
        //    if ((!rest.HasValue || rest.Value) && item.type != ItemID.None && CWRLoad.ItemIsHeldSwing[item.type] && !CWRLoad.ItemIsHeldSwingDontStopOrigShoot[item.type]) {
        //        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
        //        return false;
        //    }

        //    return rest ?? base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        //}

        public override void SplitStack(Item destination, Item source, int numToTransfer) {
            ProcessRemakeAction(destination, (inds) => inds.SplitStack(destination, source, numToTransfer));
        }

        public override void Update(Item item, ref float gravity, ref float maxFallSpeed) {
            float safeGravity = gravity;
            float safeMaxFallSpeed = maxFallSpeed;
            ProcessRemakeAction(item, (inds) => inds.Update(item, ref safeGravity, ref safeMaxFallSpeed));
            gravity = safeGravity;
            maxFallSpeed = safeMaxFallSpeed;
        }

        public override void UpdateAccessory(Item item, Player player, bool hideVisual) {
            ProcessRemakeAction(item, (inds) => inds.UpdateAccessory(item, player, hideVisual));
        }

        public override void UpdateInventory(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.UpdateInventory(item, player));
        }

        public override void UseAnimation(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.UseAnimation(item, player));
        }

        public override bool? UseItem(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.UseItem(item, player));
            return rest ?? base.UseItem(item, player);
        }

        public override void UseItemFrame(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.UseItemFrame(item, player));
        }

        public override void UseItemHitbox(Item item, Player player, ref Rectangle hitbox, ref bool noHitbox) {
            Rectangle safeHitbox = hitbox;
            bool safeNoHitbox = noHitbox;
            ProcessRemakeAction(item, (inds) => inds.UseItemFrame(item, player));
            hitbox = safeHitbox;
            noHitbox = safeNoHitbox;
        }

        public override void UseStyle(Item item, Player player, Rectangle heldItemFrame) {
            ProcessRemakeAction(item, (inds) => inds.UseStyle(item, player, heldItemFrame));
        }

        #endregion
    }
}
