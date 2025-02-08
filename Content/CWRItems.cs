using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon;
using CalamityOverhaul.Content.RangedModify.UI.AmmoView;
using CalamityOverhaul.Content.RemakeItems;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

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
        #region Date
        public override bool InstancePerEntity => true;
        /// <summary>
        /// 用于存储物品的状态值，对这个数组的使用避免了额外类成员的创建
        /// (自建类成员数据对于修改物品而言总是令人困惑)
        /// 这个数组不会自动的网络同步，需要在合适的时机下调用同步指令
        /// </summary>
        public float[] ai = [0, 0, 0];
        /// <summary>
        /// 是否强制AllowPrefix返回true，这个属性的优先级低于<see cref="ItemOverride.On_AllowPreFix(Item, int)"/>
        /// </summary>
        public bool GetAllowPrefix;
        /// <summary>
        /// 是否强制MeleePrefix返回true，这个属性的优先级低于<see cref="ItemOverride.On_MeleePreFix(Item)"/>
        /// </summary>
        public bool GetMeleePrefix;
        /// <summary>
        /// 是否强制RangedPrefix返回true，这个属性的优先级低于<see cref="ItemOverride.On_RangedPreFix(Item)"/>
        /// </summary>
        public bool GetRangedPrefix;
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
        internal Item[] MagazineContents;
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
        /// 该物品是否可以开启侦察
        /// </summary>
        public bool Scope;
        /// <summary>
        /// 退弹时，该物品是否返还
        /// </summary>
        public bool AmmoProjectileReturn;
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
        /// 用于存储一个手持挥舞类的原生射弹ID
        /// </summary>
        internal int SetHeldSwingOrigShootID;
        /// <summary>
        /// 表示这个物品是否经过了<see cref="CWRUtils.SetKnifeHeld{T}(Item, bool)"/>的设置
        /// </summary>
        internal bool WeaponInSetKnifeHeld;
        /// <summary>
        /// 这个物品所属的终焉合成内容，这决定了它的物品简介是否绘制终焉合成表格
        /// </summary>
        internal string[] OmigaSnyContent;
        /// <summary>
        /// 用于动态开关该合成指示UI的变量
        /// </summary>
        internal bool DrawOmigaSnyUIBool;
        /// <summary>
        /// 是一把弓
        /// </summary>
        public bool IsBow;
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
        /// 被传奇武器所使用，用于保存一些数据
        /// </summary>
        public LegendData LegendData;
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
            cwr.isInfiniteItem = isInfiniteItem;
            cwr.NoDestruct = NoDestruct;
            cwr.destructTime = destructTime;
            cwr.OmigaSnyContent = OmigaSnyContent;
            cwr.DrawOmigaSnyUIBool = DrawOmigaSnyUIBool;
            cwr.IsBow = IsBow;
            cwr.IsShootCountCorlUse = IsShootCountCorlUse;
            cwr.LegendData = LegendData;
            return cwr;
        }

        private void SmiperItemSet(Item item) {
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

            if (CWRServerConfig.Instance.BiologyOverhaul) {
                if (item.type == ItemID.TwinsBossBag 
                    || item.type == ItemID.DestroyerBossBag 
                    || item.type == ItemID.SkeletronPrimeBossBag) {
                    item.CWR().DeathModeItem = true;
                }
            }
        }

        public override void SetDefaults(Item item) {
            if (isInfiniteItem) {
                destructTime = 5;
            }
            if (AmmoCapacity == 0) {
                AmmoCapacity = 1;
            }

            InitializeMagazine();
            SmiperItemSet(item);
            CWRLoad.SetAmmoItem(item);
            LegendData.Create(item);

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
            bool isUnlimited = CWRUtils.IsAmmunitionUnlimited(addAmmo);

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
                newAmmo.CWR().AmmoProjectileReturn = !isUnlimited;
            }

            List<Item> newMagazine = MagazineContents.ToList();
            bool onAdds = false;
            foreach (var item in newMagazine) {
                if (item.type == newAmmo.type && item.stack < item.maxStack) {
                    item.stack += newAmmo.stack;
                    onAdds = true;
                }
            }
            if (!onAdds) {
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
        }

        public override void HoldItem(Item item, Player player) {
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
            RecoverUnloadedItem.UpdateInventory(item, player);
        }

        public static void OverModifyTooltip(Item item, List<TooltipLine> tooltips) {
            bool inRItemIndsDict = CWRMod.ItemIDToOverrideDic.ContainsKey(item.type);

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

            if (inRItemIndsDict) {
                string path = $"Mods.CalamityOverhaul.RemakeItems.{CWRMod.ItemIDToOverrideDic[item.type].GetType().Name}.Tooltip";
                CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, Language.GetText(path));
            }

            if (item.CWR().DeathModeItem) {
                var line = new TooltipLine(CWRMod.Instance, "DeathModeItem", $"--{CWRLocText.Instance.DeathModeItem.Value}--");
                line.OverrideColor = VaultUtils.MultiStepColorLerp(Main.LocalPlayer.miscCounter % 100 / 100f, Color.Gold, Color.Red, Color.DarkRed, Color.Red, Color.Gold);
                tooltips.Add(line);
            }
        }

        public override void ModifyWeaponCrit(Item item, Player player, ref float crit) {
            CWRPlayer modPlayer = player.CWR();
            if (modPlayer.LoadMuzzleBrakeLevel > 0) {
                if (item.DamageType.CountsAsClass(DamageClass.Ranged)) {
                    if (modPlayer.LoadMuzzleBrakeLevel == 1) {
                        crit += 5;
                    }
                    else if (modPlayer.LoadMuzzleBrakeLevel == 2) {
                        crit += 10;
                    }
                    else if (modPlayer.LoadMuzzleBrakeLevel == 3) {
                        crit += 15;
                    }
                    else if (modPlayer.LoadMuzzleBrakeLevel == 4) {
                        crit += 100;
                    }
                }
            }
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
            if (CWRServerConfig.Instance.WeaponOverhaul && CWRMod.ItemIDToOverrideDic.TryGetValue(item.type, out ItemOverride baseRItem) && baseRItem.DrawingInfo) {
                Main.spriteBatch.Draw(CWRAsset.icon_small.Value, Main.MouseScreen - new Vector2(0, -26), null, Color.Gold, 0
                    , CWRAsset.icon_small.Value.Size() / 2, MathF.Sin(Main.GameUpdateCount * 0.05f) * 0.05f + 0.7f, SpriteEffects.None, 0);
            }
        }
    }
}
