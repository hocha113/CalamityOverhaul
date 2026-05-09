using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.Generator;
using CalamityOverhaul.Content.Items.Modifys;
using CalamityOverhaul.Content.LegendWeapon;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
using InnoVault.GameSystem;
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
    public class CWRItem : GlobalItem
    {
        #region Data
        public override bool InstancePerEntity => true;
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
        /// 在背包中的时间
        /// </summary>
        public int InventoryTimer;
        /// <summary>
        /// 如果该物品被一个收集者视作为目标，那么该值会被设置为对应手臂的的弹幕索引
        /// </summary>
        internal int TargetByCollector = -1;
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
        /// 需要锁定的弹药类型
        /// </summary>
        public Item TargetLockAmmo;
        /// <summary>
        /// 使用的染色物品ID
        /// </summary>
        public int DyeItemID;
        /// <summary>
        /// 历史物品转化目标ID，如果大于0，则会试图转化
        /// </summary>
        public int LegacyItemTranslationID;
        #endregion
        public override void Load() {
            ItemRebuildLoader.PreSetDefaultsEvent += PreSetDefaults;
            ItemRebuildLoader.PostSetDefaultsEvent += PostSetDefaults;
            ItemRebuildLoader.PreModifyTooltipsEvent += OverModifyTooltip;
        }
        public override void Unload() {
            ItemRebuildLoader.PreSetDefaultsEvent -= PreSetDefaults;
            ItemRebuildLoader.PostSetDefaultsEvent -= PostSetDefaults;
            ItemRebuildLoader.PreModifyTooltipsEvent -= OverModifyTooltip;
        }
        public override GlobalItem Clone(Item from, Item to) => CloneCWRItem((CWRItem)base.Clone(from, to));
        public CWRItem CloneCWRItem(CWRItem cwr) {
            cwr.ai = ai;
            cwr.isHeldItem = isHeldItem;
            cwr.IsHeldSwing = IsHeldSwing;
            cwr.heldProjType = heldProjType;
            cwr.hasHeldNoCanUseBool = hasHeldNoCanUseBool;
            cwr.InventoryTimer = InventoryTimer;
            cwr.StorageUE = StorageUE;
            cwr.UEValue = UEValue;
            cwr.ConsumeUseUE = ConsumeUseUE;
            cwr.OmigaSnyContent = OmigaSnyContent;
            cwr.IsShootCountCorlUse = IsShootCountCorlUse;
            cwr.LegendData = LegendData;
            cwr.DyeItemID = DyeItemID;
            cwr.LegacyItemTranslationID = LegacyItemTranslationID;
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
        public static void PreSetDefaults(Item item) {
            CWRItem cwrItem = item.CWR();
            cwrItem.ai = new float[MaxAISlot];
            cwrItem.TargetLockAmmo = new Item();
            SmiperItemSet(item);
            CWRLoad.SetAmmoItem(item);
        }
        //调用在 ItemRebuildLoader.SetDefaults 之后
        public static void PostSetDefaults(Item item) {
            CWRItem cwrItem = item.CWR();

            if (cwrItem.MaxUEValue <= 0) {
                cwrItem.MaxUEValue = cwrItem.ConsumeUseUE;
            }
            if (cwrItem.MaxUEValue <= 0) {
                cwrItem.MaxUEValue = 20;
            }

            if (CWRLoad.AddMaxStackItemsIn64.Contains(item.type)) {
                item.maxStack = 64;
            }
        }

        #region NetWork
        public override void NetSend(Item item, BinaryWriter writer) {
            LegendData?.NetSend(item, writer);

            ai ??= new float[MaxAISlot];
            for (int i = 0; i < MaxAISlot; i++) {
                writer.Write(ai[i]);
            }

            writer.Write(DyeItemID);
            writer.Write(StorageUE);
            writer.Write(UEValue);

            writer.Write(TargetByCollector);
        }

        public override void NetReceive(Item item, BinaryReader reader) {
            LegendData?.NetReceive(item, reader);

            ai ??= new float[MaxAISlot];
            for (int i = 0; i < MaxAISlot; i++) {
                ai[i] = reader.ReadSingle();
            }

            DyeItemID = reader.ReadInt32();
            StorageUE = reader.ReadBoolean();
            UEValue = reader.ReadSingle();

            TargetByCollector = reader.ReadInt32();
        }
        #endregion

        public override void SplitStack(Item destination, Item source, int numToTransfer) {
            if (destination.type != ItemID.None && source.type != ItemID.None) {
                CWRItem cwrDestination = destination.CWR();
                CWRItem cwrSource = source.CWR();
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
                CWRItem cwrDestination = destination.CWR();
                CWRItem cwrSource = source.CWR();
                if (cwrDestination.StorageUE && cwrSource.StorageUE) {
                    float addUE = Math.Min(cwrSource.UEValue, cwrSource.MaxUEValue) * numToTransfer;
                    if (cwrSource.UEValue < addUE) {
                        addUE = 0;
                    }
                    cwrSource.UEValue -= addUE;
                    cwrDestination.UEValue += addUE;
                }
            }
        }

        public override void OnConsumeItem(Item item, Player player) {
            if (item.type != ItemID.None) {
                CWRItem cwrItem = item.CWR();
                if (cwrItem.StorageUE) {
                    cwrItem.UEValue -= cwrItem.ConsumeUseUE;
                    cwrItem.UEValue = MathHelper.Clamp(cwrItem.UEValue, 0, int.MaxValue);
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

        //有意思的是，在数次令角色死亡死后，我确认当角色死亡时，该函数会被加载一次
        public override void SaveData(Item item, TagCompound tag) {
            if (DyeItemID > ItemID.None) {
                tag.Add("_DyeItemID", DyeItemID);
            }

            try {
                //存储操作使用StorageOperation上下文，静默升级不弹窗
                LegendData?.DoUpdate(item, LegendUpdateContext.StorageOperation);
                LegendData?.SaveData(item, tag);
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[LegendData:SaveData] an error has occurred:{ex.Message}");
            }

            if (StorageUE) {
                tag["UEValue"] = UEValue;
            }
        }

        public override void LoadData(Item item, TagCompound tag) {
            if (!tag.TryGet("_DyeItemID", out DyeItemID)) {
                DyeItemID = 0;
            }

            try {
                HalibutData.IsLegacyItem(item, tag);
                //加载数据
                LegendData?.LoadData(item, tag);
                //加载操作使用StorageOperation上下文，静默升级不弹窗
                LegendData?.DoUpdate(item, LegendUpdateContext.StorageOperation);
                //转化历史物品
                if (LegacyItemTranslationID > ItemID.None) {
                    item.ChangeItemType(LegacyItemTranslationID);
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[LegendData:LoadData] an error has occurred:{ex.Message}");
            }

            if (StorageUE) {
                if (!tag.TryGet("UEValue", out UEValue)) {
                    UEValue = 0;
                }
            }
        }

        public override void HoldItem(Item item, Player player) {
            //玩家手持物品，使用 PlayerHolding 上下文。把 player 一并传入用于 owner 校验，
            //避免多人模式下 A 玩家的物品在 B 玩家屏幕上弹出确认 UI
            LegendData?.DoUpdate(item, player, LegendUpdateContext.PlayerHolding);
            if (heldProjType > 0) {
                //使用GetProjectileHasNum即时检测，而不是使用ownedProjectileCounts，这样获得的弹幕数量最为保险
                if (player.CountProjectilesOfID(heldProjType) <= 0 && Main.myPlayer == player.whoAmI) {//player.ownedProjectileCounts[heldProjType] == 0
                    Projectile.NewProjectileDirect(item.GetSource_FromThis(), player.Center, Vector2.Zero
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
            //转化历史物品
            if (LegacyItemTranslationID > ItemID.None) {
                item.ChangeItemType(LegacyItemTranslationID);
            }
        }

        public override void UpdateInventory(Item item, Player player) {
            //玩家背包中的物品，使用 PlayerInventory 上下文。把 player 一并传入用于 owner 校验，
            //避免多人模式下 A 玩家的物品在 B 玩家屏幕上弹出确认 UI
            LegendData?.DoUpdate(item, player, LegendUpdateContext.PlayerInventory);
            RecoverUnloadedItem.UpdateInventory(item);
            if (InventoryTimer < int.MaxValue)
                InventoryTimer++;
            if (LegacyItemTranslationID > ItemID.None) {
                item.ChangeItemType(LegacyItemTranslationID);
            }
        }

        public override void Update(Item item, ref float gravity, ref float maxFallSpeed) {
            //世界掉落物，使用WorldItem上下文，静默升级不弹窗
            LegendData?.DoUpdate(item, LegendUpdateContext.WorldItem);
            //转化历史物品
            if (LegacyItemTranslationID > ItemID.None) {
                item.ChangeItemType(LegacyItemTranslationID);
            }
        }

        public static void OverModifyTooltip(Item item, List<TooltipLine> tooltips) {
            bool inRItemIndsDict = ItemOverride.ByID.ContainsKey(item.type);

            if (CWRLoad.ItemIsGun[item.type]) {
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
                    tooltips.Clear(); //清空原 tooltips 集合
                    tooltips.AddRange(newTooltips); //添加修改后的 newTooltips 集合
                    tooltips.AddRange(prefixTooltips);
                }
            }

            if (ItemOverride.TryFetchByID(item.type, out Dictionary<Type, ItemOverride> itemOverrides)) {
                foreach (var rItem in itemOverrides.Values) {
                    if (!rItem.CanLoadLocalization || rItem.Mod != CWRMod.Instance) {
                        continue;
                    }
                    CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, rItem.Tooltip);
                }
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

            HalibutUIPanel.FishSkillTooltip(item, tooltips);

            if (item.CWR().LegendData != null && item.CWR().LegendData.DontUpgradeName == SaveWorld.WorldFullName) {
                var line = new TooltipLine(CWRMod.Instance, "LegendItemUpgradeDisable", CWRLocText.Instance.LegendItemUpgradeDisable.Value);
                line.OverrideColor = VaultUtils.MultiStepColorLerp(Main.LocalPlayer.miscCounter % 100 / 100f
                    , Color.Yellow, Color.Goldenrod, Color.Gold, Color.Goldenrod, Color.Yellow);
                tooltips.Add(line);
            }
        }

        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset) {
            if (line.Mod == "CalamityMod" && line.Name == "CalamityCharge") {
                Texture2D value = CWRAsset.DraedonContactPanel.Value;
                VaultUtils.DrawBorderedRectangle(Main.spriteBatch, value, 4
                    , new Vector2(line.X, line.Y), 200, 28, Color.White, Color.White * 0, 1);
                Color color = VaultUtils.MultiStepColorLerp(CWRRef.ChargeRatio(item), Color.Red, Color.SeaGreen);
                VaultUtils.DrawBorderedRectangle(Main.spriteBatch, value, 4
                    , new Vector2(line.X, line.Y), (int)(200 * CWRRef.ChargeRatio(item)), 28, Color.White * 0, color, 1);
                ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, line.Font, line.Text, new Vector2(line.X + 16, line.Y + 6)
                , Color.White, line.Rotation, line.Origin, line.BaseScale, line.MaxWidth, line.Spread);
                return false;
            }

            if (line.Name == "ItemName" && line.Mod == "Terraria" && DyeItemID > ItemID.None) {
                item.BeginDyeEffectForUI(DyeItemID);
            }
            return base.PreDrawTooltipLine(item, line, ref yOffset);
        }

        public override void PostDrawTooltipLine(Item item, DrawableTooltipLine line) {
            if (line.Name == "ItemName" && line.Mod == "Terraria" && DyeItemID > ItemID.None) {
                item.EndDyeEffectForUI();
            }
        }

        public override void PostDrawTooltip(Item item, ReadOnlyCollection<DrawableTooltipLine> lines) {
            if (!ItemOverride.TryFetchByID(item.type, out Dictionary<Type, ItemOverride> itemOverrides)) {
                return;
            }

            bool result = true;
            foreach (var rItem in itemOverrides.Values) {
                result = rItem.DrawingInfo;
            }

            if (result) {
                Main.spriteBatch.Draw(CWRAsset.icon_small.Value, Main.MouseScreen - new Vector2(0, -26), null, Color.Gold, 0
                , CWRAsset.icon_small.Value.Size() / 2, MathF.Sin(Main.GameUpdateCount * 0.05f) * 0.05f + 0.7f, SpriteEffects.None, 0);
            }
        }

        public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            item.BeginDyeEffectForUI(DyeItemID);
            return true;
        }

        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            item.EndDyeEffectForUI();
        }

        public override bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            item.BeginDyeEffectForWorld(DyeItemID);
            return true;
        }

        public override void PostDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            item.EndDyeEffectForWorld();
        }
    }
}
