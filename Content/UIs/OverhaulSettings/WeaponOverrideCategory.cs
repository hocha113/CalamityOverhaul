using CalamityOverhaul.Content.RemakeItems;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;
using SettingToggle = CalamityOverhaul.Content.UIs.OverhaulSettings.OverhaulSettingsUI.SettingToggle;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    /// <summary>
    /// 武器修改管理分类：显示所有注册在 HandlerCanOverride.CanOverrideByID 中的武器，
    /// 允许逐个启用或禁用其修改覆写
    /// </summary>
    internal class WeaponOverrideCategory : SettingsCategory
    {
        public override string Title => OverhaulSettingsUI.WeaponOverrideText?.Value ?? "武器修改管理";

        public override void Initialize() {
            foreach (var pair in HandlerCanOverride.CanOverrideByID) {
                int itemType = pair.Key;
                if (itemType <= 0) continue;

                //使用物品的内部名称作为属性名
                Item sample = new(itemType);
                string displayName = sample.Name ?? itemType.ToString();

                AddToggle(displayName,
                    () => HandlerCanOverride.CanOverrideByID.TryGetValue(itemType, out bool v) && v,
                    v => HandlerCanOverride.CanOverrideByID[itemType] = v,
                    false);

                //在最后一个Toggle上存储物品类型ID，方便绘制图标
                Toggles[^1].ItemType = itemType;
            }
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
            //这里只是改变字典值，不需要额外操作
            //如果未来需要实时生效可以调用 ResetValueByWorld
        }

        public override float GetLabelOffsetX(float scale) => 28f * scale;

        public override void DrawRowExtra(SpriteBatch spriteBatch, SettingToggle toggle,
            Rectangle rect, float alpha, float scale) {
            if (toggle.ItemType <= 0) return;

            //在开关盒子右边、标签左边绘制物品小图标
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
