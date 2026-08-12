using CalamityOverhaul.Content.Industrials.MachineModules;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Industrials.UIs
{
    /// <summary>
    /// 机器面板上的模块插座行:一行凹槽插座,封装点击放/取/换(Shift 回背包)、
    /// 宿主种类与同类去重校验、拒绝红闪、空槽键位蚀刻、模块图标绘制与悬停反馈。<br/>
    /// 各机器 UI 持有一个实例,布局自己定,行为全走这里,保证五族面板交互一致
    /// </summary>
    internal class ModuleSocketStrip
    {
        private readonly List<Rectangle> rects = [];
        private int denySlot = -1;
        private int denyTimer;
        private LocalizedText denyReason;

        internal IReadOnlyList<Rectangle> Rects => rects;

        /// <summary>横排布局;每次面板重排时调用</summary>
        internal void Layout(int x, int y, int count, int size = 44, int gap = 10) {
            rects.Clear();
            for (int i = 0; i < count; i++) {
                rects.Add(new Rectangle(x + i * (size + gap), y, size, size));
            }
        }

        internal bool Contains(Point mouse) {
            foreach (Rectangle rect in rects) {
                if (rect.Contains(mouse)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>拒绝闪烁计时;宿主 Update 每帧调用</summary>
        internal void Update() {
            if (denyTimer > 0) {
                denyTimer--;
            }
        }

        /// <summary>
        /// 处理一次左键按下;命中插座返回 true(宿主停止后续点击分发)。
        /// 变更成功后回调 onChanged(宿主负责 MarkDirty + SendData + 自身刷新)
        /// </summary>
        internal bool HandleClick(Point mouse, MachineModuleRack rack, int slotCount, Player player, Action onChanged) {
            for (int i = 0; i < rects.Count; i++) {
                if (rects[i].Contains(mouse)) {
                    ClickSlot(i, rack, slotCount, player, onChanged);
                    return true;
                }
            }
            return false;
        }

        private void ClickSlot(int index, MachineModuleRack rack, int slotCount, Player player, Action onChanged) {
            Item[] slots = rack.EnsureSlots(slotCount);
            if (index >= slots.Length) {
                return;
            }
            Item slot = slots[index];
            Item mouse = Main.mouseItem;

            if (mouse.IsAir && slot.IsAir) {
                return;
            }

            if (!mouse.IsAir) {
                if (!rack.Accepts(mouse)) {
                    Deny(index, MachineModuleText.SocketOnly);
                    return;
                }
                if (rack.HasType(mouse.type, ignoreSlot: index)) {
                    Deny(index, MachineModuleText.SocketDuplicate);
                    return;
                }
                //放入/交换
                Item swap = slot.IsAir ? new Item() : slot.Clone();
                slots[index] = mouse.Clone();
                slots[index].stack = 1;
                Main.mouseItem = swap;
            }
            else {
                //取出:Shift 直接回背包,MP下地面掉落会被队友截走
                if (Main.keyState.PressingShift()) {
                    player.GiveItem(new EntitySource_WorldEvent(), slot.Clone());
                }
                else {
                    Main.mouseItem = slot.Clone();
                }
                slots[index] = new Item();
            }

            SoundEngine.PlaySound(SoundID.Grab);
            rack.MarkDirty();
            onChanged?.Invoke();
        }

        private void Deny(int slotIndex, LocalizedText reason) {
            denySlot = slotIndex;
            denyTimer = 40;
            denyReason = reason;
            SoundEngine.PlaySound(SoundID.MenuClose);
        }

        /// <summary>插座 + 模块图标/键位蚀刻</summary>
        internal void Draw(SpriteBatch sb, MachineModuleRack rack, int slotCount, float alpha, Point mouse) {
            Item[] slots = rack.EnsureSlots(slotCount);
            for (int i = 0; i < rects.Count; i++) {
                Rectangle rect = rects[i];
                bool hover = rect.Contains(mouse);
                float deny = denyTimer > 0 && denySlot == i ? denyTimer / 40f : 0f;

                //插座:凹槽床 + 键槽刻痕 + 黄铜簧片
                IndustrialTerminalRenderer.DrawSocket(sb, rect, alpha, hover ? 1f : 0f, deny);

                Item item = i < slots.Length ? slots[i] : null;
                if (item != null && !item.IsAir) {
                    if (item.ModItem is BaseMachineModule module) {
                        module.DrawIcon(sb, rect.Center.ToVector2(), 15f, alpha);
                    }
                    else {
                        //兜底画原版贴图前先确保纹理已加载
                        Main.instance.LoadItem(item.type);
                        VaultUtils.SimpleDrawItem(sb, item.type, rect.Center.ToVector2(), 32, 1f, 0, Color.White * alpha);
                    }
                }
                else {
                    //空插座:键位蚀刻
                    IndustrialTerminalRenderer.DrawSocketKeyMark(sb, rect.Center.ToVector2(), alpha);
                }
            }
        }

        /// <summary>
        /// 悬停反馈:装了模块给物品 tooltip,拒绝期给红字原因,空槽给放入提示。
        /// 命中返回 true(宿主停止后续悬停分发);showTip 由宿主提供自家提示牌绘制
        /// </summary>
        internal bool DrawHoverTip(SpriteBatch sb, MachineModuleRack rack, int slotCount, Point mouse, Action<string, Color> showTip) {
            Item[] slots = rack.EnsureSlots(slotCount);
            for (int i = 0; i < rects.Count; i++) {
                if (!rects[i].Contains(mouse)) {
                    continue;
                }
                if (denyTimer > 0 && denySlot == i && denyReason != null) {
                    showTip(denyReason.Value, IndustrialTerminalRenderer.WarnRed);
                }
                else if (i < slots.Length && !slots[i].IsAir) {
                    Main.HoverItem = slots[i].Clone();
                    Main.hoverItemName = slots[i].Name;
                }
                else {
                    showTip(MachineModuleText.SocketEmptyHint.Value, IndustrialTerminalRenderer.TextMain);
                }
                return true;
            }
            return false;
        }
    }
}
