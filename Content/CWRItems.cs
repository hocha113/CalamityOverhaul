using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GunCustomization.UI.AmmoView;
using CalamityOverhaul.Content.Items;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Terraria;
using Terraria.ID;
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
        /// 是否强制AllowPrefix返回true，这个属性的优先级低于<see cref="BaseRItem.On_AllowPreFix(Item, int)"/>
        /// </summary>
        public bool GetAllowPrefix;
        /// <summary>
        /// 是否强制MeleePrefix返回true，这个属性的优先级低于<see cref="BaseRItem.On_MeleePreFix(Item)"/>
        /// </summary>
        public bool GetMeleePrefix;
        /// <summary>
        /// 是否强制RangedPrefix返回true，这个属性的优先级低于<see cref="BaseRItem.On_RangedPreFix(Item)"/>
        /// </summary>
        public bool GetRangedPrefix;
        /// <summary>
        /// 是否是一个重制物品，在基类为<see cref="EctypeItem"/>时自动启用
        /// </summary>
        public bool remakeItem;
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
        internal bool noDestruct;
        /// <summary>
        /// 湮灭倒计时间
        /// </summary>
        internal int destructTime;
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
        #endregion

        public override GlobalItem Clone(Item from, Item to) => CloneCWRItem((CWRItems)base.Clone(from, to));
        public CWRItems CloneCWRItem(CWRItems cwr) {
            cwr.ai = ai;
            cwr.remakeItem = remakeItem;
            cwr.closeCombat = closeCombat;
            cwr.MeleeCharge = MeleeCharge;
            cwr.isHeldItem = isHeldItem;
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
            cwr.noDestruct = noDestruct;
            cwr.destructTime = destructTime;
            cwr.OmigaSnyContent = OmigaSnyContent;
            cwr.DrawOmigaSnyUIBool = DrawOmigaSnyUIBool;
            cwr.IsBow = IsBow;
            cwr.IsShootCountCorlUse = IsShootCountCorlUse;
            return cwr;
        }

        private void SmiperItemSet(Item item) {
            int type = item.type;
            if (type == ModContent.ItemType<Nadir>()) {
                item.damage = 190;
                item.useTime = item.useAnimation = 18;
            }
            else if (type == ItemID.Zenith) {
                item.damage = 105;
            }
        }

        public override void SetDefaults(Item item) {
            if (isInfiniteItem) {
                destructTime = 5;
            }
            if (AmmoCapacity == 0) {
                AmmoCapacity = 1;
            }

            remakeItem = (item.ModItem as EctypeItem) != null;
            InitializeMagazine();
            SmiperItemSet(item);
            CWRLoad.SetAmmoItem(item);

            if (CWRLoad.AddMaxStackItemsIn64.Contains(item.type)) {
                item.maxStack = 64;
            }
            //TODO:我忘了为什么要加这个，猜测是当时要禁止切枪用的一个属性，但已经用另一个方法实现了，所以这段处理是无用的，暂时注释掉试试
            //if (CWRLoad.OnLoadContentBool && CWRLoad.ItemIsRanged[item.type]) {
            //    item.noUseGraphic = true;
            //}
        }

        public void InitializeMagazine() {
            AmmoProjectileReturn = true;
            IsKreload = false;
            NumberBullets = 0;
            NoKreLoadTime += 10;
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

        //有意思的是，在数次令角色死亡死后，我确认当角色死亡时，该函数会被加载一次
        public override void SaveData(Item item, TagCompound tag) {
            tag.Add("_MeleeCharge", MeleeCharge);
            tag.Add("_noDestruct", noDestruct);
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
        }

        public override void LoadData(Item item, TagCompound tag) {
            MeleeCharge = tag.GetFloat("_MeleeCharge");
            noDestruct = tag.GetBool("_noDestruct");
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

            OwnerByDir(item, player);
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            if (remakeItem) {
                TooltipLine nameLine = tooltips.FirstOrDefault((x) => x.Name == "ItemName" && x.Mod == "Terraria");
                if (nameLine != null) {
                    string overText = $" ([c/fff08c:{CWRLocText.GetTextValue("CWRItem_IsRemakeItem_TextContent")}])";
                    nameLine.Text += overText;
                }
            }
        }

        public static void OverModifyTool(Item item, List<TooltipLine> tooltips) {
            bool inRItemIndsDict = CWRMod.RItemIndsDict.ContainsKey(item.type);
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
                if (CWRServerConfig.Instance.ActivateGunRecoil) {
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

            if (CWRServerConfig.Instance.WeaponOverhaul && inRItemIndsDict) {
                string key = CWRMod.RItemIndsDict[item.type].TargetToolTipItemName;
                if (key != "") {
                    if (CWRMod.RItemIndsDict[item.type].IsVanilla) {
                        CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, CWRLocText.GetText(key));
                    }
                    else {
                        CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, key);
                    }
                }
            }
        }

        public override void ModifyWeaponCrit(Item item, Player player, ref float crit) {
            CWRPlayer modPlayer = player.CWR();
            if (modPlayer.LoadMuzzleBrake) {
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

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            CWRPlayer modPlayer = player.CWR();
            if (modPlayer.LoadMuzzleBrake) {
                if (item.DamageType.CountsAsClass(DamageClass.Ranged)) {
                    if (modPlayer.LoadMuzzleBrakeLevel == 1) {
                        damage *= 0.85f;
                    }
                    else if (modPlayer.LoadMuzzleBrakeLevel == 2) {
                        damage *= 0.9f;
                    }
                    else if (modPlayer.LoadMuzzleBrakeLevel == 3) {
                        damage *= 0.95f;
                    }
                    else if (modPlayer.LoadMuzzleBrakeLevel == 4) {
                        damage *= 2;
                    }
                }
            }

        }

        private void OwnerByDir(Item item, Player player) {
            if ((player.PressKey() || player.PressKey(false)) && player.whoAmI == Main.myPlayer) {
                if (item.type > ItemID.None && item.useStyle == ItemUseStyleID.Swing
                    && (item.createTile == -1 && item.createWall == -1)
                    && item.CWR().heldProjType == 0
                    && !player.CWR().uiMouseInterface && !player.cursorItemIconEnabled) {
                    player.direction = Math.Sign(player.position.To(Main.MouseWorld).X);
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

        internal static void drawIconSmall() {
            Main.spriteBatch.Draw(CWRAsset.icon_small.Value, Main.MouseScreen - new Vector2(0, -26), null, Color.Gold, 0
                , CWRAsset.icon_small.Value.Size() / 2, MathF.Sin(Main.GameUpdateCount * 0.05f) * 0.05f + 0.7f, SpriteEffects.None, 0);
        }

        public override void PostDrawTooltip(Item item, ReadOnlyCollection<DrawableTooltipLine> lines) {
            if (CWRServerConfig.Instance.WeaponOverhaul
                && CWRMod.RItemIndsDict.TryGetValue(item.type, out BaseRItem baseRItem)) {
                if (baseRItem.DrawingInfo) {
                    drawIconSmall();
                }
            }
        }
    }
}
