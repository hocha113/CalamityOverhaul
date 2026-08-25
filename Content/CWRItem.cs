using CalamityOverhaul.Content.Industrials.Generator;
using CalamityOverhaul.Content.Items.Modifys;
using CalamityOverhaul.Content.LegendWeapon;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI.Chat;

namespace CalamityOverhaul.Content
{
    public class CWRItem : GlobalItem, ILocalizedModType
    {
        public string LocalizationCategory => "Items.CWRItem";

        public static LocalizedText TemperatureText { get; private set; }
        public static LocalizedText InternalStoredEnergy { get; private set; }
        public static LocalizedText DeathModeItemText { get; private set; }
        public static LocalizedText LegendItemUpgradeDisable { get; private set; }
        public static LocalizedText ItemLegendOnMouseLang { get; private set; }

        public override void SetStaticDefaults() {
            TemperatureText = this.GetLocalization(nameof(TemperatureText), () => "燃烧温度");
            InternalStoredEnergy = this.GetLocalization(nameof(InternalStoredEnergy), () => "能量存储");
            DeathModeItemText = this.GetLocalization(nameof(DeathModeItemText), () => "死亡模式");
            LegendItemUpgradeDisable = this.GetLocalization(nameof(LegendItemUpgradeDisable), () =>
                """
                这把传奇武器在当前世界中已被设定为无法升级
                若需要解除禁用状态，请重新进入世界
                """);
            ItemLegendOnMouseLang = this.GetLocalization(nameof(ItemLegendOnMouseLang), () => "按下'Shift'聆听故事...");
        }

        #region Data
        public override bool InstancePerEntity => true;
        /// <summary>手持物品标记，与 <see cref="heldProjType"/> &gt;0 等价</summary>
        public bool isHeldItem;
        /// <summary>手持弹幕 Type，0=无</summary>
        public int heldProjType;
        /// <summary>true 时有手持弹幕则禁用原使用，配合 <see cref="heldProjType"/></summary>
        public bool hasHeldNoCanUseBool;
        /// <summary>背包内停留帧数</summary>
        public int InventoryTimer;
        /// <summary>收集者手臂弹幕索引，-1 无</summary>
        internal int TargetByCollector = -1;
        /// <summary>收集者锁剩余帧，归零清 <see cref="TargetByCollector"/>（Actor 无死亡钩子防幽灵锁）</summary>
        internal int CollectorLockTime;
        /// <summary>是否存储 UE</summary>
        public bool StorageUE;
        /// <summary>UE 储能</summary>
        public float UEValue;
        /// <summary>UE 容量上限，0 时回落 <see cref="ConsumeUseUE"/> 再 20</summary>
        public float MaxUEValue;
        /// <summary>单次消耗 UE</summary>
        public float ConsumeUseUE;
        /// <summary>传奇升级数据</summary>
        public LegendData LegendData;
        /// <summary>死亡模式专属</summary>
        public bool DeathModeItem;
        /// <summary>锁定弹药</summary>
        public Item TargetLockAmmo;
        /// <summary>染色物品 ID</summary>
        public int DyeItemID;
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
        public override GlobalItem Clone(Item from, Item to) => CloneCWRItem((CWRItem)base.Clone(from, to), to);
        public CWRItem CloneCWRItem(CWRItem cwr, Item to) {
            //LegendData 引用型，浅拷会共写
            cwr.isHeldItem = isHeldItem;
            cwr.heldProjType = heldProjType;
            cwr.hasHeldNoCanUseBool = hasHeldNoCanUseBool;
            cwr.InventoryTimer = InventoryTimer;
            cwr.StorageUE = StorageUE;
            cwr.UEValue = UEValue;
            cwr.ConsumeUseUE = ConsumeUseUE;
            cwr.LegendData = LegendData?.Clone(to);
            cwr.DyeItemID = DyeItemID;
            return cwr;
        }

        public override void OnCreated(Item item, ItemCreationContext context) {
            if (context is not JourneyDuplicationItemCreationContext) {
                return;
            }
            if (OnikiriData.TryGet(item) is OnikiriData data) {
                data.RenewIdentity();
            }
            if (KikasaData.TryGet(item) is KikasaData kikasaData) {
                kikasaData.RenewIdentity();
            }
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

        //TODO:时机受限，属性可能错过，优先在 ItemRebuildLoader 改
        public override void SetDefaults(Item item) { }

        //ItemRebuildLoader.SetDefaults 之前
        public static void PreSetDefaults(Item item) {
            CWRItem cwrItem = item.CWR();
            cwrItem.TargetLockAmmo = new Item();
            SmiperItemSet(item);
            CWRLoad.SetAmmoItem(item);
        }
        //ItemRebuildLoader.SetDefaults 之后
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

            writer.Write(DyeItemID);
            writer.Write(StorageUE);
            writer.Write(UEValue);

            writer.Write(TargetByCollector);
        }

        public override void NetReceive(Item item, BinaryReader reader) {
            LegendData receivedLegend = LegendData?.Clone(item);
            receivedLegend?.NetReceive(item, reader);
            if (LegendData is OnikiriData currentOnikiri
                && receivedLegend is OnikiriData receivedOnikiri
                && currentOnikiri.InstanceId == receivedOnikiri.InstanceId
                && receivedOnikiri.EditRevision < currentOnikiri.EditRevision) {
                receivedOnikiri.PreserveEditedStateFrom(currentOnikiri);
            }
            //鬼伞同款：迟到的旧修订同步不许吃掉本机更新的挂符
            if (LegendData is KikasaData currentKikasa
                && receivedLegend is KikasaData receivedKikasa
                && currentKikasa.InstanceId == receivedKikasa.InstanceId
                && receivedKikasa.EditRevision < currentKikasa.EditRevision) {
                receivedKikasa.PreserveEditedStateFrom(currentKikasa);
            }
            if (item.type == OnikiriOverride.ID && receivedLegend != null) {
                receivedLegend.Level = OnikiriOverride.ClampLevel(receivedLegend.Level);
            }

            int receivedDyeItemID = reader.ReadInt32();
            bool receivedStorageUE = reader.ReadBoolean();
            float receivedUEValue = reader.ReadSingle();
            int receivedTargetByCollector = reader.ReadInt32();

            LegendData = receivedLegend;
            DyeItemID = receivedDyeItemID;
            StorageUE = receivedStorageUE;
            UEValue = receivedUEValue;
            TargetByCollector = receivedTargetByCollector;
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
            if (heldProjType > 0 && hasHeldNoCanUseBool) {
                return false;
            }
            return true;
        }

        //死亡时也会调一次 SaveData
        public override void SaveData(Item item, TagCompound tag) {
            if (DyeItemID > ItemID.None) {
                tag.Add("_DyeItemID", DyeItemID);
            }

            try {
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
                LegendData?.LoadData(item, tag);
                //StorageOperation，静默不弹窗
                LegendData?.DoUpdate(item, LegendUpdateContext.StorageOperation);
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
            //PlayerHolding+owner 校验，防 MP 旁观端弹窗
            LegendData?.DoUpdate(item, player, LegendUpdateContext.PlayerHolding);
            if (heldProjType > 0) {
                //CountProjectilesOfID，不用 ownedProjectileCounts
                if (player.CountProjectilesOfID(heldProjType) <= 0 && Main.myPlayer == player.whoAmI) {
                    Projectile.NewProjectileDirect(item.GetSource_FromThis(), player.Center, Vector2.Zero
                        , heldProjType, item.damage, item.knockBack, player.whoAmI);
                }
            }
        }

        public override void UpdateInventory(Item item, Player player) {
            //PlayerInventory+owner 校验，防 MP 旁观端弹窗
            LegendData?.DoUpdate(item, player, LegendUpdateContext.PlayerInventory);
            RecoverUnloadedItem.UpdateInventory(item);
            if (InventoryTimer < int.MaxValue)
                InventoryTimer++;
        }

        public override void Update(Item item, ref float gravity, ref float maxFallSpeed) {
            //锁超时过期，防泄漏
            if (TargetByCollector >= 0 && --CollectorLockTime <= 0) {
                TargetByCollector = -1;
            }
            //WorldItem，静默不弹窗
            LegendData?.DoUpdate(item, LegendUpdateContext.WorldItem);
        }

        public static void OverModifyTooltip(Item item, List<TooltipLine> tooltips) {
            bool inRItemIndsDict = ItemOverride.ByID.ContainsKey(item.type);

            if (CWRLoad.ItemIsGun[item.type]) {
                if (!inRItemIndsDict) {
                    List<TooltipLine> newTooltips = new(tooltips);
                    List<TooltipLine> prefixTooltips = [];
                    List<TooltipLine> tooltip = [];
                    foreach (TooltipLine line in tooltips.ToList()) {
                        if (CWRUtils.IsTooltipBodyLine(line)) {
                            tooltip.Add(line.Clone());
                            line.Hide();
                        }
                        if (line.Name.Contains("Prefix")) {
                            prefixTooltips.Add(line.Clone());
                            line.Hide();
                        }
                    }
                    newTooltips.AddRange(tooltip);
                    tooltips.Clear();
                    tooltips.AddRange(newTooltips);
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
                var line = new TooltipLine(CWRMod.Instance, "FuelItem", $"{TemperatureText.Value}: {value * 4}°C");
                line.OverrideColor = Color.Orange;
                tooltips.Add(line);
            }

            if (item.CWR().StorageUE) {
                var line = new TooltipLine(CWRMod.Instance, "UEValue", $"{InternalStoredEnergy.Value}: {(int)item.CWR().UEValue}UE");
                line.OverrideColor = VaultUtils.MultiStepColorLerp(Main.LocalPlayer.miscCounter % 300 / 300f
                    , Color.Yellow, Color.White, Color.Yellow);
                tooltips.Add(line);
            }

            if (item.CWR().DeathModeItem) {
                var line = new TooltipLine(CWRMod.Instance, "DeathModeItem", $"--{DeathModeItemText.Value}--");
                line.OverrideColor = VaultUtils.MultiStepColorLerp(Main.LocalPlayer.miscCounter % 100 / 100f
                    , Color.Gold, Color.Red, Color.DarkRed, Color.Red, Color.Gold);
                tooltips.Add(line);
            }

            HalibutSkillTips.FishSkillTooltip(item, tooltips);

            if (item.CWR().LegendData != null && item.CWR().LegendData.DontUpgradeName == SaveWorld.WorldFullName) {
                var line = new TooltipLine(CWRMod.Instance, "LegendItemUpgradeDisable", LegendItemUpgradeDisable.Value);
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
