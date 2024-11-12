using InnoVault.UIHandles;
using Terraria;

namespace CalamityOverhaul.Content.UIs
{
    internal class CWRUI : GlobalUIHandle
    {
        public override void PostUIHanderElementUpdate(UIHandle handle) {
            if (Main.LocalPlayer.active && !Main.dedServ) {
                Main.LocalPlayer.CWR().setUIMouseInterface(Main.LocalPlayer.mouseInterface);
            }
        }
    }
}
