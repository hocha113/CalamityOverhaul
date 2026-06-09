using CalamityOverhaul.Content.Items.Melee.StormGoddessSpears;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj;
using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Players
{
    internal class PlayerRippers : PlayerOverride, ICWRLoader
    {
        public static List<int> noRippersProj = [];
        void ICWRLoader.SetupData() {
            noRippersProj = [
                ModContent.ProjectileType<MuraExecutionCut>(),
                ModContent.ProjectileType<StormGoddessSpearHeld>(),
                ModContent.ProjectileType<StormArc>(),
                ModContent.ProjectileType<StormLightning>(),
            ];
        }

        void ICWRLoader.UnLoadData() {
            noRippersProj?.Clear();
        }

        public override bool On_ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            if (Player.GetPlayerAdrenalineMode()) {
                if (noRippersProj.Contains(proj.type)) {
                    return false;
                }
            }
            return base.On_ModifyHitNPCWithProj(proj, target, ref modifiers);
        }
    }
}
