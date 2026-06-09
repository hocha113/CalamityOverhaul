using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.GameSystem;
using System;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Players
{
    internal class PlayerDeath : PlayerOverride
    {
        public bool Doomed { get; set; }

        public override void ResetEffects() {
            Doomed = false;
        }

        public override bool? On_PreKill(double damage, int hitDirection, bool pvp,
            ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            if (Doomed) {
                return true;
            }

            if (Player.GetModPlayer<SirenMusicalBoxPlayer>().IsCursed) {
                if (Player.TryGetOverride(out HalibutPlayer halibutPlayer)
                    && halibutPlayer.ResurrectionSystem.Ratio == 1f) {
                    return true;
                }

                Player.statLife = Math.Clamp(Player.statLife, 1, Player.statLifeMax2);
                return false;
            }

            return null;
        }
    }
}
