using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Spectrometers;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.UIHandles;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials
{
    internal abstract class BaseDyeTP : BaseBattery
    {
        internal Item DyeSlotItem = new();
        internal Item BeDyedItem = new();
        internal Item ResultDyedItem = new();
        public abstract BaseDyeMachineUI DyeMachineUI { get; }
        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            DyeSlotItem ??= new();
            tag["DyeSlotItem"] = ItemIO.Save(DyeSlotItem);
            BeDyedItem ??= new();
            tag["BeDyedItem"] = ItemIO.Save(BeDyedItem);
            ResultDyedItem ??= new();
            tag["ResultDyedItem"] = ItemIO.Save(ResultDyedItem);
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (tag.TryGet<TagCompound>("DyeSlotItem", out var value)) {
                DyeSlotItem = ItemIO.Load(value);
            }
            else {
                DyeSlotItem = new();
            }

            if (tag.TryGet<TagCompound>("BeDyedItem", out var value2)) {
                BeDyedItem = ItemIO.Load(value2);
            }
            else {
                BeDyedItem = new();
            }

            if (tag.TryGet<TagCompound>("ResultDyedItem", out var value3)) {
                ResultDyedItem = ItemIO.Load(value3);
            }
            else {
                ResultDyedItem = new();
            }
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
            ItemIO.Send(DyeSlotItem, data, true, true);
            ItemIO.Send(BeDyedItem, data, true, true);
            ItemIO.Send(ResultDyedItem, data, true, true);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            DyeSlotItem = ItemIO.Receive(reader, true, true);
            BeDyedItem = ItemIO.Receive(reader, true, true);
            ResultDyedItem = ItemIO.Receive(reader, true, true);
            //接收后更新一次UI里面显示的物品
            DyeMachineUI.DyeSlot.Item = DyeSlotItem;
            DyeMachineUI.BeDyedItem.Item = BeDyedItem;
            DyeMachineUI.ResultDyedItem.Item = ResultDyedItem;
        }

        public void RightClick(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }

            SoundEngine.PlaySound(CWRSound.ButtonZero);
            var ui = DyeMachineUI;
            if (ui.DyeTP == this) {
                ui.CanOpen = !DyeMachineUI.CanOpen;
            }
            else {
                ui.DyeTP = this;
                ui.CanOpen = true;
                ui.DyeSlot.Item = DyeSlotItem;
                ui.BeDyedItem.Item = BeDyedItem;
                ui.ResultDyedItem.Item = ResultDyedItem;
            }

            foreach (var otherUI in UIHandleLoader.UIHandles) {
                if (otherUI.ID != DyeMachineUI.ID && otherUI is BaseDyeMachineUI baseDyeMachineUI) {
                    baseDyeMachineUI.CanOpen = false;//关闭其他所有同类UI面板
                }
            }
        }

        public void CloseDyeMachineUI() {
            if (DyeMachineUI.CanOpen && DyeMachineUI.DyeTP.WhoAmI == WhoAmI) {
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.2f });
                DyeMachineUI.CanOpen = false;
            }
        }

        public override void UpdateMachine() {
            if (Main.LocalPlayer.DistanceSQ(CenterInWorld) > 90000) {
                CloseDyeMachineUI();
            }
            DyeMachineUI.DyeSlot.UpdateSlot();
            UpdateDyeMachine();
        }

        public virtual void UpdateDyeMachine() { }

        public override void MachineKill() {
            CloseDyeMachineUI();
            if (VaultUtils.isClient) {
                return;//客户端不处理掉落
            }
            if (DyeSlotItem.Alives()) {
                VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, DyeSlotItem);
            }
            if (BeDyedItem.Alives()) {
                VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, BeDyedItem);
            }
            if (ResultDyedItem.Alives()) {
                VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, ResultDyedItem);
            }
        }
    }
}
