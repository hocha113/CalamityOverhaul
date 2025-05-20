﻿using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.Generator;
using CalamityOverhaul.Content.LegendWeapon;
using CalamityOverhaul.Content.RangedModify;
using CalamityOverhaul.Content.RangedModify.UI.AmmoView;
using CalamityOverhaul.Content.RemakeItems;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI.Chat;

namespace CalamityOverhaul.Content
{
    public enum SpecialAmmoStateEnum
    {
        /// <summary>
        /// 普通，默认值，即无特殊效果
        /// </summary>
        ordinary,
        /// <summary>
        /// 燃烧弹
        /// </summary>
        napalmBomb,
        /// <summary>
        /// 破甲
        /// </summary>
        armourPiercer,
        /// <summary>
        /// 高爆
        /// </summary>
        highExplosive,
        /// <summary>
        /// 龙息
        /// </summary>
        dragonBreath
    }

    public class CWRItems : GlobalItem
    {
        #region Data
        public override bool InstancePerEntity => true;
        /// <summary>
        /// 如果不为<see langword="null"/>，则强行接收所有前缀
        /// </summary>
        public static Dictionary<int, bool?> ItemAllowPrefixDic { get; set; } = [];
        /// <summary>
        /// 如果不为<see langword="null"/>，则强行接收所有近战前缀
        /// </summary>
        public static Dictionary<int, bool?> ItemMeleePrefixDic { get; set; } = [];
        /// <summary>
        /// 如果不为<see langword="null"/>，则强行接收所有远程前缀
        /// </summary>
        public static Dictionary<int, bool?> ItemRangedPrefixDic { get; set; } = [];
        /// <summary>
        /// AI槽位数量
        /// </summary>
        public const int MaxAISlot = 3;
        /// <summary>
        /// 用于存储物品的状态值，对这个数组的使用避免了额外类成员的创建
        /// (自建类成员数据对于修改物品而言总是令人困惑)
        /// 这个数组不会自动的网络同步，需要在合适的时机下调用同步指令
        /// </summary>
        public float[] ai = new float[MaxAISlot];
        /// <summary>
        /// 是否正在真近战
        /// </summary>
        public bool closeCombat;
        /// <summary>
        /// 一般用于近战类武器的充能值
        /// </summary>
        public float MeleeCharge;
        /// <summary>
        /// 是否是一个手持物品，改判定与<see cref="heldProjType"/> >0 具有同样的功效，都会被系统认定为手持物品
        /// </summary>
        public bool isHeldItem;
        /// <summary>
        /// 设置这个物品的手持弹幕Type，默认为0，如果是0便认定为无手持
        /// </summary>
        public int heldProjType;
        /// <summary>
        /// 在有手持弹幕存在时还可以使用武器吗？和<see cref="heldProjType"/>配合使用，
        /// 设置为<see langword="true"/>在拥有手持弹幕时禁止物品使用，
        /// 设置为<see langword="false"/>默认物品的原使用
        /// </summary>
        public bool hasHeldNoCanUseBool;
        /// <summary>
        /// 该物品是否可以开启侦察
        /// </summary>
        public bool Scope;
        /// <summary>
        /// 是否是一个无尽物品，这个的设置决定物品是否会受到湮灭机制的影响
        /// </summary>
        internal bool isInfiniteItem;
        /// <summary>
        /// 是否湮灭，在设置<see cref="isInfiniteItem"/>为<see langword="true"/>后启用，决定无尽物品是否湮灭
        /// </summary>
        internal bool NoDestruct;
        /// <summary>
        /// 湮灭倒计时间
        /// </summary>
        internal int destructTime;
        /// <summary>
        /// 是否存储UE
        /// </summary>
        public bool StorageUE;
        /// <summary>
        /// UE储能
        /// </summary>
        public float UEValue;
        /// <summary>
        /// 单件物品的最大UE电力容量，如果为默认0，则会自动设置为<see cref="ConsumeUseUE"/>的值，如果ConsumeUseUE也为0，则设置为20
        /// </summary>
        public float MaxUEValue;
        /// <summary>
        /// 当这个物品被消耗时，会消耗的UE值
        /// </summary>
        public float ConsumeUseUE;
        /// <summary>
        /// 用于存储一个手持挥舞类的原生射弹ID
        /// </summary>
        public int SetHeldSwingOrigShootID;
        /// <summary>
        /// 表示这个物品是否经过了<see cref="CWRUtils.SetKnifeHeld{T}(Item, bool)"/>的设置
        /// </summary>
        public bool WeaponInSetKnifeHeld;
        /// <summary>
        /// 这个物品所属的终焉合成内容，这决定了它的物品简介是否绘制终焉合成表格
        /// </summary>
        public string[] OmigaSnyContent;
        /// <summary>
        /// 是否自动装填终焉合成配方？如果的话，默认为<see langword="true"/>，这个属性只有在设置了<see cref="OmigaSnyContent"/>时才有意义
        /// </summary>
        public bool AutoloadingOmigaSnyRecipe = true;
        /// <summary>
        /// 被传奇武器所使用，用于保存一些数据
        /// </summary>
        public LegendData LegendData;
        /// <summary>
        /// 是否被抛射物控制使用，优先级高于<see cref="hasHeldNoCanUseBool"/>，且不受<see cref="heldProjType"/>影响
        /// </summary>
        public bool IsShootCountCorlUse;
        /// <summary>
        /// 该物品是否是一个手持挥舞类，一般由<see cref="CWRUtils.SetKnifeHeld{T}(Item)"/>来设置，如果为<see langword="true"/>，那么就会阻断原方法的发射逻辑
        /// </summary>
        public bool IsHeldSwing;
        /// <summary>
        /// 对于手持挥舞来说，是否不阻断原射击方式
        /// </summary>
        public bool IsHeldSwingDontStopOrigShoot;
        /// <summary>
        /// 是否是死亡模式专属物品
        /// </summary>
        public bool DeathModeItem;
        /// <summary>
        /// 该物品对应的剩余子弹数量，一般用于给手持枪械弹幕访问
        /// </summary>
        public int NumberBullets;
        /// <summary>
        /// 该物品的弹容量
        /// </summary>
        public int AmmoCapacity;
        /// <summary>
        /// 大于0时不可以装弹
        /// </summary>
        public int NoKreLoadTime;
        /// <summary>
        /// 退弹时，该物品是否返还
        /// </summary>
        public bool AmmoProjectileReturn;
        /// <summary>
        /// 来源是无限的弹药
        /// </summary>
        public bool FromUnlimitedAmmo;
        /// <summary>
        /// 是否已经装好了弹药，一般来讲，该字段用于存储可装弹式手持弹幕的装弹状态
        /// </summary>
        public bool IsKreload;
        /// <summary>
        /// 该物品是否具有弹夹系统
        /// </summary>
        public bool HasCartridgeHolder;
        /// <summary>
        /// 使用的弹匣UI类型
        /// </summary>
        public CartridgeUIEnum CartridgeType;
        /// <summary>
        /// 特种状态
        /// </summary>
        public SpecialAmmoStateEnum SpecialAmmoState;
        /// <summary>
        /// 弹匣内容，管理装填后的弹药部分
        /// </summary>
        public Item[] MagazineContents;
        /// <summary>
        /// 需要锁定的弹药类型
        /// </summary>
        public Item TargetLockAmmo;
        #endregion
        public override GlobalItem Clone(Item from, Item to) => CloneCWRItem((CWRItems)base.Clone(from, to));
        public CWRItems CloneCWRItem(CWRItems cwr) {
            cwr.ai = ai;
            cwr.closeCombat = closeCombat;
            cwr.MeleeCharge = MeleeCharge;
            cwr.isHeldItem = isHeldItem;
            cwr.IsHeldSwing = IsHeldSwing;
            cwr.heldProjType = heldProjType;
            cwr.hasHeldNoCanUseBool = hasHeldNoCanUseBool;
            cwr.IsKreload = IsKreload;
            cwr.HasCartridgeHolder = HasCartridgeHolder;
            cwr.CartridgeType = CartridgeType;
            cwr.SpecialAmmoState = SpecialAmmoState;
            cwr.MagazineContents = MagazineContents;
            cwr.NumberBullets = NumberBullets;
            cwr.AmmoCapacity = AmmoCapacity;
            cwr.NoKreLoadTime = NoKreLoadTime;
            cwr.Scope = Scope;
            cwr.AmmoProjectileReturn = AmmoProjectileReturn;
            cwr.FromUnlimitedAmmo = FromUnlimitedAmmo;
            cwr.isInfiniteItem = isInfiniteItem;
            cwr.NoDestruct = NoDestruct;
            cwr.destructTime = destructTime;
            cwr.StorageUE = StorageUE;
            cwr.UEValue = UEValue;
            cwr.ConsumeUseUE = ConsumeUseUE;
            cwr.OmigaSnyContent = OmigaSnyContent;
            cwr.IsShootCountCorlUse = IsShootCountCorlUse;
            cwr.LegendData = LegendData;
            return cwr;
        }

        internal static void SmiperItemSet(Item item) {
            int type = item.type;
            if (type == ItemID.Zenith) {
                item.damage = 105;
            }
            else if (type == ItemID.FallenStar) {
                item.shootSpeed = 13;
                item.damage = 6;
                item.knockBack = 2;
                item.useStyle = ItemUseStyleID.Swing;
            }
            else if (type == ItemID.Coal) {
                item.maxStack = 9999;
                item.value = Item.buyPrice(0, 0, 0, 15);
            }
        }
        //TODO:这里的设置受到时效性的影响，可能会让一些属性错过设置实际，最好是在 ItemRebuildLoader 中编辑代码
        public override void SetDefaults(Item item) { }
        //调用在 ItemRebuildLoader.SetDefaults 之前
        public void PreSetDefaults(Item item) {
            ai = new float[MaxAISlot];
            TargetLockAmmo = new Item();
            InitializeMagazine();
            SmiperItemSet(item);
            CWRLoad.SetAmmoItem(item);
        }
        //调用在 ItemRebuildLoader.SetDefaults 之后
        public void PostSetDefaults(Item item) {
            if (isInfiniteItem) {
                destructTime = 5;
            }
            if (AmmoCapacity == 0) {
                AmmoCapacity = 1;
            }

            if (MaxUEValue <= 0) {
                MaxUEValue = ConsumeUseUE;
            }
            if (MaxUEValue <= 0) {
                MaxUEValue = 20;
            }

            if (CWRLoad.AddMaxStackItemsIn64.Contains(item.type)) {
                item.maxStack = 64;
            }
        }

        #region Magazine
        /// <summary>
        /// 安全获取选定的弹匣弹药内容
        /// </summary>
        /// <returns></returns>
        public Item GetSelectedBullets() {
            if (MagazineContents == null || MagazineContents.Length <= 0 || MagazineContents[^1] == null) {
                InitializeMagazine();
            }
            return MagazineContents[^1];//倒着读，让子弹先进先出
        }

        /// <summary>
        /// 装填弹匣，该行为不考虑弹匣上限问题，使用默认添加数量可以自动计算装填数量
        /// </summary>
        /// <param name="addAmmo"></param>
        /// <param name="addStack"></param>
        public void LoadenMagazine(Item addAmmo, int addStack = 0) {
            CalculateNumberBullet();
            bool isUnlimited = RangedLoader.IsAmmunitionUnlimited(addAmmo);

            if (addAmmo.type != ItemID.None && !addAmmo.CWR().AmmoProjectileReturn) {
                isUnlimited = true;//如果加入的物品本身设置就是不返还，就强行判定为无限弹药
            }

            if (addStack == 0) {
                addStack = AmmoCapacity - NumberBullets;
                if (addStack > addAmmo.stack && !isUnlimited) {
                    addStack = addAmmo.stack;
                }
            }

            if (!isUnlimited) {
                addAmmo.stack -= addStack;
            }

            Item newAmmo;
            if (VaultUtils.ProjectileToSafeAmmoMap.TryGetValue(addAmmo.shoot, out int trueAmmoType)) {
                newAmmo = new Item(trueAmmoType);
            }
            else {
                newAmmo = addAmmo.Clone();
            }

            newAmmo.stack = addStack;
            if (newAmmo.type != ItemID.None) {
                CWRItems cwrAmmo = newAmmo.CWR();
                cwrAmmo.AmmoProjectileReturn = !isUnlimited;
            }

            List<Item> newMagazine = [.. MagazineContents];
            bool onAdds = false;
            foreach (var item in newMagazine) {
                if (item.type == newAmmo.type && item.stack < item.maxStack
                    && item.CWR().AmmoProjectileReturn == newAmmo.CWR().AmmoProjectileReturn) {
                    item.stack += newAmmo.stack;
                    onAdds = true;
                }
            }
            if (!onAdds) {
                if (addAmmo.type > ItemID.None && addAmmo.CWR().FromUnlimitedAmmo) {
                    newAmmo.CWR().AmmoProjectileReturn = false;
                }
                newMagazine.Add(newAmmo);
            }
            SetMagazine(newMagazine);
        }

        /// <summary>
        /// 设置弹匣内容，自动处理冗余内容、UI更新、剩余子弹数量等机制，如果输入非法的值，将直接初始化弹匣
        /// </summary>
        /// <param name="magazineArray"></param>
        public void SetMagazine(Item[] magazineArray) {
            if (magazineArray == null || magazineArray.Length <= 0 || magazineArray[0] == null) {
                InitializeMagazine();//如果输入的是非法的弹匣内容，直接初始化弹匣，并返回
                return;
            }
            SetMagazine(magazineArray.ToList());
        }

        /// <summary>
        /// 设置弹匣内容，自动处理冗余内容、UI更新、剩余子弹数量等机制，如果输入非法的值，将直接初始化弹匣
        /// </summary>
        /// <param name="magazineList"></param>
        public void SetMagazine(List<Item> magazineList) {
            if (magazineList == null || magazineList.Count <= 0 || magazineList[0] == null) {
                InitializeMagazine();//如果输入的是非法的弹匣内容，直接初始化弹匣，并返回
                return;
            }

            magazineList.RemoveAll(ammo => ammo == null || ammo.type == ItemID.None || ammo.stack <= 0);
            MagazineContents = magazineList.ToArray();
            if (MagazineContents.Length > 0) {
                CalculateNumberBullet();
                AmmoViewUI.Instance.LoadAmmos(this);
            }
            else {
                InitializeMagazine();
            }
        }

        /// <summary>
        /// 计算并更新弹匣剩余子弹的数量
        /// </summary>
        public void CalculateNumberBullet() {
            int ammoCount = 0;
            foreach (var ammo in MagazineContents) {
                if (ammo.type == ItemID.None || ammo.stack <= 0) {
                    continue;
                }
                ammoCount += ammo.stack;
            }
            NumberBullets = ammoCount;
        }

        /// <summary>
        /// 将枪械的弹匣数据初始化
        /// </summary>
        public void InitializeMagazine() {
            AmmoProjectileReturn = true;
            IsKreload = false;
            NumberBullets = 0;
            NoKreLoadTime = 10;
            MagazineContents = new Item[AmmoCapacity];
            for (int i = 0; i < MagazineContents.Length; i++) {
                MagazineContents[i] = new Item();
            }
            if (!CWRServerConfig.Instance.MagazineSystem) {
                IsKreload = true;
            }
            SpecialAmmoState = SpecialAmmoStateEnum.ordinary;
            AmmoViewUI.Instance.LoadAmmos(this);
        }
        #endregion

        #region NetWork
        public override void NetSend(Item item, BinaryWriter writer) {
            LegendData?.NetSend(item, writer);

            ai ??= new float[MaxAISlot];
            for (int i = 0; i < MaxAISlot; i++) {
                writer.Write(ai[i]);
            }

            writer.Write(StorageUE);
            writer.Write(UEValue);

            if (HasCartridgeHolder) {
                if (MagazineContents == null) {
                    InitializeMagazine();
                }
                writer.Write(NumberBullets);
                writer.Write(NoKreLoadTime);
                writer.Write(AmmoProjectileReturn);
                writer.Write(IsKreload);
                writer.Write((byte)SpecialAmmoState);
                int count = 0;
                foreach (var ammo in MagazineContents) {
                    if (ammo.type == ItemID.None || ammo.stack <= 0) {
                        continue;
                    }
                    count++;
                }
                writer.Write(count);
                foreach (var ammo in MagazineContents) {
                    if (ammo.type == ItemID.None || ammo.stack <= 0) {
                        continue;
                    }

                    writer.Write(ammo.type);
                    writer.Write(ammo.stack);
                    writer.Write(ammo.CWR().AmmoProjectileReturn);
                }
            }
        }

        public override void NetReceive(Item item, BinaryReader reader) {
            LegendData?.NetReceive(item, reader);

            ai ??= new float[MaxAISlot];
            for (int i = 0; i < MaxAISlot; i++) {
                ai[i] = reader.ReadSingle();
            }

            StorageUE = reader.ReadBoolean();
            UEValue = reader.ReadSingle();

            if (HasCartridgeHolder) {
                NumberBullets = reader.ReadInt32();
                NoKreLoadTime = reader.ReadInt32();
                AmmoProjectileReturn = reader.ReadBoolean();
                IsKreload = reader.ReadBoolean();
                SpecialAmmoState = (SpecialAmmoStateEnum)reader.ReadByte();
                List<Item> list = new List<Item>();
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++) {
                    Item ammo = new Item(reader.ReadInt32(), reader.ReadInt32());
                    ammo.CWR().AmmoProjectileReturn = reader.ReadBoolean();
                    list.Add(ammo);
                }
                MagazineContents = list.ToArray();
            }
        }
        #endregion

        public override void SplitStack(Item destination, Item source, int numToTransfer) {
            if (destination.type != ItemID.None && source.type != ItemID.None) {
                CWRItems cwrDestination = destination.CWR();
                CWRItems cwrSource = source.CWR();
                if (cwrDestination.StorageUE && cwrSource.StorageUE) {
                    cwrDestination.UEValue = cwrSource.UEValue;
                    cwrDestination.UEValue = MathHelper.Clamp(cwrDestination.UEValue, 0, cwrDestination.ConsumeUseUE);
                    cwrSource.UEValue -= cwrSource.ConsumeUseUE;
                    cwrSource.UEValue = MathHelper.Clamp(cwrSource.UEValue, 0, int.MaxValue);
                }
            }
        }

        public override void OnStack(Item destination, Item source, int numToTransfer) {
            if (destination.type != ItemID.None && source.type != ItemID.None) {
                CWRItems cwrDestination = destination.CWR();
                CWRItems cwrSource = source.CWR();
                if (cwrDestination.StorageUE && cwrSource.StorageUE) {
                    cwrDestination.UEValue += cwrSource.UEValue;
                }
            }
        }

        public override void OnConsumeItem(Item item, Player player) {
            if (item.type != ItemID.None) {
                CWRItems cwrItem = item.CWR();
                if (cwrItem.StorageUE) {
                    cwrItem.UEValue -= cwrItem.ConsumeUseUE;
                    cwrItem.UEValue = MathHelper.Clamp(cwrItem.UEValue, 0, int.MaxValue);
                }
            }
        }

        //有意思的是，在数次令角色死亡死后，我确认当角色死亡时，该函数会被加载一次
        public override void SaveData(Item item, TagCompound tag) {
            tag.Add("_MeleeCharge", MeleeCharge);
            tag.Add("_NoDestruct", NoDestruct);
            if (HasCartridgeHolder) {
                if (MagazineContents != null && MagazineContents.Length > 0) {
                    Item[] safe_MagazineContent = MagazineContents.ToArray();//这里需要一次安全的保存中转
                    for (int i = 0; i < safe_MagazineContent.Length; i++) {
                        if (safe_MagazineContent[i] == null) {
                            safe_MagazineContent[i] = new Item(ItemID.None);
                        }
                        if (safe_MagazineContent[i].type != ItemID.None) {
                            if (!safe_MagazineContent[i].CWR().AmmoProjectileReturn) {
                                safe_MagazineContent[i] = new Item(ItemID.None);
                            }
                        }
                    }
                    tag.Add("_MagazineContents", safe_MagazineContent);
                }
                tag.Add("_IsKreload", IsKreload);
            }

            LegendData?.SaveData(item, tag);

            if (StorageUE) {
                tag["UEValue"] = UEValue;
            }
        }

        public override void LoadData(Item item, TagCompound tag) {
            if (!tag.TryGet("_MeleeCharge", out MeleeCharge)) {
                MeleeCharge = 0;
            }
            if (!tag.TryGet("_NoDestruct", out NoDestruct)) {
                NoDestruct = false;
            }

            if (HasCartridgeHolder) {
                if (tag.ContainsKey("_MagazineContents")) {
                    Item[] magazineContents = tag.Get<Item[]>("_MagazineContents");
                    for (int i = 0; i < magazineContents.Length; i++) {
                        if (magazineContents[i] == null) {
                            magazineContents[i] = new Item(ItemID.None);
                        }
                    }
                    MagazineContents = tag.Get<Item[]>("_MagazineContents");
                }
                int ammoValue = 0;
                foreach (Item i in MagazineContents) {
                    if (i.type != ItemID.None) {
                        ammoValue += i.stack;
                    }
                }
                NumberBullets = ammoValue;
                if (tag.ContainsKey("_IsKreload")) {
                    IsKreload = tag.GetBool("_IsKreload");
                }
            }

            LegendData?.LoadData(item, tag);

            if (StorageUE) {
                if (!tag.TryGet("UEValue", out UEValue)) {
                    UEValue = 0;
                }
            }
        }

        public override void HoldItem(Item item, Player player) {
            LegendData?.DoUpdate();
            if (heldProjType > 0) {
                //使用GetProjectileHasNum即时检测，而不是使用ownedProjectileCounts，这样获得的弹幕数量最为保险
                if (player.GetProjectileHasNum(heldProjType) <= 0 && Main.myPlayer == player.whoAmI) {//player.ownedProjectileCounts[heldProjType] == 0
                    Projectile.NewProjectileDirect(player.GetSource_FromThis(), player.Center, Vector2.Zero
                        , heldProjType, item.damage, item.knockBack, player.whoAmI);
                }
                if (CWRLoad.ItemIsRanged[item.type]) {
                    bool lDown = player.PressKey();
                    bool rDown = player.PressKey(false);
                    if (lDown || (rDown && !lDown && CWRLoad.ItemIsRangedAndCanRightClickFire[item.type] && !player.cursorItemIconEnabled)) {
                        player.CWR().HeldStyle = 0;
                    }
                }
            }
        }

        public override void UpdateInventory(Item item, Player player) {
            LegendData?.DoUpdate();
            RecoverUnloadedItem.UpdateInventory(item, player);
        }

        public static void OverModifyTooltip(Item item, List<TooltipLine> tooltips) {
            bool inRItemIndsDict = ItemOverride.ByID.ContainsKey(item.type);

            if (CWRLoad.ItemIsGun[item.type]) {
                if (CWRLoad.ItemIsGunAndMustConsumeAmmunition[item.type] && item.CWR().HasCartridgeHolder && CWRServerConfig.Instance.MagazineSystem) {
                    tooltips.Add(new TooltipLine(CWRMod.Instance, "CWRGun_MustCA", CWRLocText.GetTextValue("CWRGun_MustCA_Text")));
                }
                if (item.CWR().HasCartridgeHolder && CWRServerConfig.Instance.MagazineSystem) {
                    string newText = CWRLocText.GetTextValue("CWRGun_KL_Text").Replace("[KL]", CWRKeySystem.KreLoad_Key.TooltipHotkeyString());
                    tooltips.Add(new TooltipLine(CWRMod.Instance, "CWRGun_KL", newText));
                }
                if (item.CWR().Scope) {
                    string newText = CWRLocText.GetTextValue("CWRGun_Scope_Text").Replace("[Scope]", CWRKeySystem.ADS_Key.TooltipHotkeyString());
                    tooltips.Add(new TooltipLine(CWRMod.Instance, "CWRGun_Scope", newText));
                }
                if (CWRServerConfig.Instance.ActivateGunRecoil && CWRLoad.ItemIsGunAndGetRecoilValue[item.type] > 0) {
                    string newText3 = CWRLocText.GetTextValue("CWRGun_Recoil_Text").Replace("[Recoil]", CWRLocText.GetTextValue(CWRLoad.ItemIsGunAndGetRecoilLocKey[item.type]));
                    tooltips.Add(new TooltipLine(CWRMod.Instance, "CWRGun_Recoil", newText3));
                }

                if (!inRItemIndsDict) {
                    List<TooltipLine> newTooltips = new(tooltips);
                    List<TooltipLine> prefixTooltips = [];
                    List<TooltipLine> tooltip = [];
                    foreach (TooltipLine line in tooltips.ToList()) {//复制 tooltips 集合，以便在遍历时修改
                        for (int i = 0; i < 9; i++) {
                            if (line.Name == "Tooltip" + i) {
                                tooltip.Add(line.Clone());
                                line.Hide();
                            }
                        }
                        if (line.Name.Contains("Prefix")) {
                            prefixTooltips.Add(line.Clone());
                            line.Hide();
                        }
                    }
                    newTooltips.AddRange(tooltip);
                    tooltips.Clear(); // 清空原 tooltips 集合
                    tooltips.AddRange(newTooltips); // 添加修改后的 newTooltips 集合
                    tooltips.AddRange(prefixTooltips);
                }
            }

            if (ItemOverride.TryFetchByID(item.type, out var rItem) && rItem.CanLoadLocalization) {
                CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, rItem.Tooltip);
            }

            if (Main.LocalPlayer.CWR().ThermalGenerationActiveTime > 0 && FuelItems.FuelItemToCombustion.TryGetValue(item.type, out int value)) {
                var line = new TooltipLine(CWRMod.Instance, "FuelItem", $"{CWRLocText.Instance.TemperatureText}: {value * 4}°C");
                line.OverrideColor = Color.Orange;
                tooltips.Add(line);
            }

            if (item.CWR().StorageUE) {
                var line = new TooltipLine(CWRMod.Instance, "UEValue", $"{CWRLocText.Instance.InternalStoredEnergy.Value}: {(int)item.CWR().UEValue}UE");
                line.OverrideColor = VaultUtils.MultiStepColorLerp(Main.LocalPlayer.miscCounter % 300 / 300f
                    , Color.Yellow, Color.White, Color.Yellow);
                tooltips.Add(line);
            }

            if (item.CWR().DeathModeItem) {
                var line = new TooltipLine(CWRMod.Instance, "DeathModeItem", $"--{CWRLocText.Instance.DeathModeItem.Value}--");
                line.OverrideColor = VaultUtils.MultiStepColorLerp(Main.LocalPlayer.miscCounter % 100 / 100f
                    , Color.Gold, Color.Red, Color.DarkRed, Color.Red, Color.Gold);
                tooltips.Add(line);
            }
        }

        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset) {
            if (line.Mod == "CalamityMod" && line.Name == "CalamityCharge") {
                Texture2D value = CWRAsset.DraedonContactPanel.Value;
                VaultUtils.DrawBorderedRectangle(Main.spriteBatch, value, 4
                    , new Vector2(line.X, line.Y), 200, 28, Color.White, Color.White * 0, 1);
                Color color = VaultUtils.MultiStepColorLerp(item.Calamity().ChargeRatio, Color.Red, Color.SeaGreen);
                VaultUtils.DrawBorderedRectangle(Main.spriteBatch, value, 4
                    , new Vector2(line.X, line.Y), (int)(200 * item.Calamity().ChargeRatio), 28, Color.White * 0, color, 1);
                ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, line.Font, line.Text, new Vector2(line.X + 16, line.Y + 6)
                , Color.White, line.Rotation, line.Origin, line.BaseScale, line.MaxWidth, line.Spread);
                return false;
            }
            return base.PreDrawTooltipLine(item, line, ref yOffset);
        }

        public override bool CanUseItem(Item item, Player player) {
            if (IsShootCountCorlUse) {
                return player.ownedProjectileCounts[item.shoot] <= 0;
            }
            if (heldProjType > 0 && hasHeldNoCanUseBool) {
                return false;
            }
            return true;
        }

        public override void PostDrawTooltip(Item item, ReadOnlyCollection<DrawableTooltipLine> lines) {
            if (ItemOverride.TryFetchByID(item.type, out ItemOverride ritem) && ritem.DrawingInfo) {
                Main.spriteBatch.Draw(CWRAsset.icon_small.Value, Main.MouseScreen - new Vector2(0, -26), null, Color.Gold, 0
                    , CWRAsset.icon_small.Value.Size() / 2, MathF.Sin(Main.GameUpdateCount * 0.05f) * 0.05f + 0.7f, SpriteEffects.None, 0);
            }
        }
    }
}
