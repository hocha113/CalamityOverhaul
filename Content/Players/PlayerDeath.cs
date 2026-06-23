using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
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
                return false;//八音盒诅咒
            }

            if (Player.CountProjectilesOfID<RestartEffectProj>() > 0) {
                return false;//正在重启，阻止死亡
            }

            if (Player.CountProjectilesOfID<YourLevelIsTooLowProj>() > 0) {
                return false;//无限重启，不死
            }

            return null;
        }
    }
}
