using CalamityOverhaul.Content.RemakeItems;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using static CalamityOverhaul.Content.UIs.OverhaulSettings.OverhaulSettingsUI;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    /// <summary>
    /// 武器覆写设置的持久化存储，保存被禁用的武器ID列表
    /// </summary>
    internal class WeaponOverrideSave : SaveMod
    {
        public override void SetStaticDefaults() {
            if (!HasSave) {
                DoSave<WeaponOverrideSave>();
            }
            DoLoad<WeaponOverrideSave>();
        }

        public override void SaveData(TagCompound tag) {
            List<string> disabledList = [];
            foreach (var pair in CWRItemOverride.CanOverrideByID) {
                if (!pair.Value) {
                    string fullName = pair.Key < ItemID.Count
                        ? pair.Key.ToString()
                        : ItemLoader.GetItem(pair.Key)?.FullName;
                    if (!string.IsNullOrEmpty(fullName)) {
                        disabledList.Add(fullName);
                    }
                }
            }
            tag["DisabledWeaponNames"] = disabledList;
        }

        public override void LoadData(TagCompound tag) {
            if (tag.TryGet<List<string>>("DisabledWeaponNames", out var disabledNames)) {
                foreach (string fullName in disabledNames) {
                    int id = VaultUtils.GetItemTypeFromFullName(fullName);
                    if (id > 0 && CWRItemOverride.CanOverrideByID.ContainsKey(id)) {
                        CWRItemOverride.CanOverrideByID[id] = false;
                    }
                }
                return;
            }
        }

        public static void Save() => DoSave<WeaponOverrideSave>();
    }

    /// <summary>
    /// 武器修改管理分类：显示所有注册在 CWRItemOverride.CanOverrideByID 中的武器，
    /// 允许逐个启用或禁用其修改覆写
    /// </summary>
    internal class WeaponOverrideCategory : SettingsCategory
    {
        public override string Title => WeaponOverrideText?.Value ?? "武器修改管理";

        public override void Initialize() {
            foreach (var pair in CWRItemOverride.CanOverrideByID) {
                int itemType = pair.Key;
                if (itemType <= 0) continue;
                Item item = ContentSamples.ItemsByType[itemType];
                if (!item.Alives()) continue;
                if (item.damage <= 0) continue;
                if (item.ModItem is not null && item.ModItem.Mod == CWRMod.Instance) continue;
                if (item.CWR().LegendData is not null) continue;
                if (ItemOverride.TryFetchByID(item.type, out ItemOverride itemOverride) && !itemOverride.DrawingInfo) continue;

                Item sample = new(itemType);
                string displayName = sample.Name ?? itemType.ToString();

                AddToggle(displayName,
                    () => CWRItemOverride.CanOverrideByID.TryGetValue(itemType, out bool v) && v,
                    v => CWRItemOverride.CanOverrideByID[itemType] = v,
                    false);

                Toggles[^1].ItemType = itemType;
            }

            //添加操作按钮
            ActionButtons.Add(new ActionButton {
                Label = EnableAllText?.Value ?? "启用全部",
                OnClick = EnableAll
            });
            ActionButtons.Add(new ActionButton {
                Label = DisableAllText?.Value ?? "禁用全部",
                OnClick = DisableAll
            });
        }

        private void EnableAll() {
            foreach (var toggle in Toggles) {
                toggle.Setter(true);
            }
            WeaponOverrideSave.Save();
        }

        private void DisableAll() {
            foreach (var toggle in Toggles) {
                toggle.Setter(false);
            }
            WeaponOverrideSave.Save();
        }

        public override string GetLabel(SettingToggle toggle) {
            if (toggle.ItemType > 0) {
                Item sample = new(toggle.ItemType);
                return sample.Name ?? toggle.ConfigPropertyName;
            }
            return toggle.ConfigPropertyName;
        }

        public override string GetTooltip(SettingToggle toggle) {
            if (toggle.ItemType > 0) {
                if (ItemOverride.TryFetchByID(toggle.ItemType, out ItemOverride rItem)
                    && rItem.Mod == CWRMod.Instance && rItem.CanLoadLocalization) {
                    string key = $"Mods.CalamityOverhaul.Items.{rItem.Name}.Tooltip";
                    string value = Language.GetTextValue(key);
                    if (value != key) return value;
                }
            }
            return "";
        }

        public override void OnToggleChanged(SettingToggle toggle, bool newValue) {
            WeaponOverrideSave.Save();
        }

        public override float GetLabelOffsetX(float scale) => 28f * scale;

        public override void DrawRowExtra(SpriteBatch spriteBatch, SettingToggle toggle,
            Rectangle rect, float alpha, float scale) {
            if (toggle.ItemType <= 0) return;

            float boxSize = ToggleBoxSize * scale;
            float iconX = rect.X + 12f * scale + boxSize + 4f * scale;
            float iconY = rect.Y + rect.Height / 2f;
            float iconSize = 22f * scale;

            VaultUtils.SafeLoadItem(toggle.ItemType);
            VaultUtils.SimpleDrawItem(spriteBatch, toggle.ItemType,
                new Vector2(iconX + iconSize / 2f, iconY),
                itemWidth: (int)iconSize, size: 1f, rotation: 0f, color: Color.White * alpha);
        }
    }
}
