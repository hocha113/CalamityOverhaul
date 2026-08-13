#if DEBUG
using CalamityOverhaul.Content.HackTimes.Chips;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// 骇入系统专属的调试触发器，独立于根目录的调试便签文件存在。<br/>
    /// D1：撤销本机玩家全部芯片协议持有并把每种协议芯片各发一枚，
    /// 回到"全是暗格 + 手上一串钥匙"的起点，便于反复验收暗格与解锁两态
    /// </summary>
    internal class HackDebugTools : ModSystem
    {
        public override void PostUpdateInput() {
            //if (Pressed(Keys.D1)) {
            //    ResetHackChipDebug(Main.LocalPlayer);
            //}
        }

        //边沿触发：只认"上帧抬起、本帧按下"，避免按住连发
        private static bool Pressed(Keys key)
            => !Main.gameMenu && Main.keyState.IsKeyDown(key) && Main.oldKeyState.IsKeyUp(key);

        /// <summary>
        /// 把所有芯片协议退回未持有，快照一次上报，再把芯片一次发齐。<br/>
        /// 芯片种类已有几十种，背包装不下的会直接落地——调试用途，可接受
        /// </summary>
        private static void ResetHackChipDebug(Player player) {
            if (player == null || !player.TryGetModPlayer(out HackTimePlayer htp)) {
                return;
            }
            List<BaseHackProtocolChip> chips = [.. CWRMod.Instance.GetContent<BaseHackProtocolChip>()];
            if (chips.Count == 0) {
                Main.NewText("No protocol chips are registered.", Color.IndianRed);
                return;
            }

            IEntitySource source = player.GetSource_Misc("HackChipDebug");
            int revoked = 0;
            foreach (BaseHackProtocolChip chip in chips) {
                if (chip.Protocol != null && htp.OwnedProtocols.Remove(chip.Protocol.FullName)) {
                    revoked++;
                }
                player.QuickSpawnItem(source, chip.Type);
            }
            HackTimeNetSync.SendOwnedSnapshot(player);
            Main.NewText($"Hack protocols revoked: {revoked}; chips granted: {chips.Count}.",
                new Color(255, 138, 46));
        }
    }
}
#endif
