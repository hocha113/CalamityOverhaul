using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Core
{
    /// <summary>
    /// 神赋物品数据（逐实例 GlobalItem），挂在可重铸单件（武器或饰品）上。<br/>
    /// roll：PostReforge 只在交互端执行（owner 权威），按新词缀的神赋池加权 roll；
    /// 数据走 SaveData/LoadData 持久化、NetSend/NetReceive 随物品同步
    /// （背包改动由 tML 每帧 diff 自动发 SyncEquipment，无需自定义包）。<br/>
    /// 模式关闭时数据保留但全部钩子惰性；钩子分发全部以 GodSmithActive 为闸
    /// </summary>
    internal class GodSmithItem : GlobalItem
    {
        public override bool InstancePerEntity => true;

        //maxStack==1 且（武器或饰品）才可能持有神赋；lateInstantiation 保证 SetDefaults 已跑完
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => lateInstantiation && entity.maxStack == 1 && (entity.damage > 0 || entity.accessory);

        /// <summary>神赋稳定键（= 神赋类名）；null = 无。键未注册时数据保留、效果与 tooltip 静默</summary>
        public string EndowKey;

        private string cachedKey;
        private GodSmithEndow cachedEndow;

        /// <summary>解析当前神赋；无神赋或键未注册返回 null</summary>
        public GodSmithEndow Endow {
            get {
                if (string.IsNullOrEmpty(EndowKey)) {
                    return null;
                }
                if (cachedKey != EndowKey) {
                    GodSmithEndow.TryGet(EndowKey, out cachedEndow);
                    cachedKey = EndowKey;
                }
                return cachedEndow;
            }
        }

        /// <summary>取生效神赋：模式开启且键可解析；分发钩子统一走这里</summary>
        private GodSmithEndow ActiveEndow => GameModeSystem.GodSmithActive ? Endow : null;

        public override void PostReforge(Item item) {
            //词缀已换，旧神赋一律作废；模式开启时按新词缀重 roll（本钩子只在交互端执行）
            EndowKey = null;
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            EndowKey = GodSmithEndow.RollFor(item.prefix)?.Key;
        }

        //==================== 数据往返（string 键防加载顺序变化） ====================

        public override void SaveData(Item item, TagCompound tag) {
            if (!string.IsNullOrEmpty(EndowKey)) {
                tag["EndowKey"] = EndowKey;
            }
        }

        public override void LoadData(Item item, TagCompound tag) {
            EndowKey = tag.TryGet("EndowKey", out string key) && !string.IsNullOrEmpty(key) ? key : null;
        }

        public override void NetSend(Item item, BinaryWriter writer) {
            writer.Write(EndowKey ?? string.Empty);
        }

        public override void NetReceive(Item item, BinaryReader reader) {
            //定长负载先读净再落地，保持共享流对齐
            string key = reader.ReadString();
            EndowKey = key.Length == 0 ? null : key;
        }

        //==================== 钩子分发（全部以 GodSmithActive 为闸，档位已按当前词缀算好） ====================

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            if (ActiveEndow is GodSmithEndow endow) {
                endow.ModifyWeaponDamage(item, player, ref damage, endow.TierScaleFor(item.prefix));
            }
        }

        public override void ModifyWeaponCrit(Item item, Player player, ref float crit) {
            if (ActiveEndow is GodSmithEndow endow) {
                endow.ModifyWeaponCrit(item, player, ref crit, endow.TierScaleFor(item.prefix));
            }
        }

        public override float UseSpeedMultiplier(Item item, Player player) {
            return ActiveEndow is GodSmithEndow endow
                ? endow.UseSpeedMultiply(item, player, endow.TierScaleFor(item.prefix)) : 1f;
        }

        public override void UseAnimation(Item item, Player player) {
            if (ActiveEndow is GodSmithEndow endow) {
                endow.OnUseAnimation(item, player, endow.TierScaleFor(item.prefix));
            }
        }

        public override void UpdateAccessory(Item item, Player player, bool hideVisual) {
            if (ActiveEndow is not GodSmithEndow endow) {
                return;
            }
            GodSmithPlayer state = player.GetModPlayer<GodSmithPlayer>();
            state.RegisterActiveEndowAcc(item, endow);
            endow.UpdateAccessory(item, player, hideVisual, state, endow.TierScaleFor(item.prefix));
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            if (ActiveEndow is not GodSmithEndow endow) {
                return;
            }
            tooltips.Add(new TooltipLine(CWRMod.Instance, "CWR_GodSmithEndowName",
                GameModeText.GodSmithEndowPrefix.Value + endow.EndowName.Value) {
                OverrideColor = GodSmithTooltip.TitleGold
            });
            object[] args = endow.DescFormatArgs(item);
            string desc = args == null ? endow.EndowDesc?.Value : endow.EndowDesc?.Format(args);
            GodSmithTooltip.AddBodyLines(tooltips, "CWR_GodSmithEndowDesc", desc);
        }
    }
}
