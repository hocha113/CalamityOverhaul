using CalamityOverhaul.Content.Players.Core;
using Terraria;

namespace CalamityOverhaul.Content.Players
{
    internal class PlayerControl : PlayerSet
    {
        //void ICWRLoader.LoadData() {
        //    IL_Player.Update += il => {
        //        try {
        //            var c = new ILCursor(il);
        //            c.GotoNext(MoveType.After,
        //                i => i.MatchStfld<Player>(nameof(Player.slideDir)),
        //                i => i.Match(Ldc_I4_0),
        //                i => i.Match(Stloc_S),
        //                i => i.Match(Ldarg_0),
        //                i => i.MatchLdfld<Player>(nameof(Player.controlDown)),
        //                i => i.Match(Stloc_S)
        //            );

        //            var label = il.DefineLabel();

        //            c.Emit(Ldloc_S, (byte)14);
        //            c.Emit(Brfalse_S, label);
        //            c.Emit(Ldc_I4_1);
        //            c.Emit(Stloc_S, (byte)13);

        //            c.MarkLabel(label);
        //        } 
        //        catch (Exception e) {
        //            CWRMod.Instance.Logger.Info(e);
        //        }
        //    };
        //}

        public override bool? CanSwitchWeapon(Player player) {
            if (player.CWR().DontSwitchWeaponTime > 0) {
                return false;
            }
            return null;
        }
    }
}
