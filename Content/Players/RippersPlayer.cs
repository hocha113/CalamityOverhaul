using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj;
using CalamityOverhaul.Content.Players.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Players
{
    internal class RippersPlayer : PlayerSet, ICWRLoader
    {
        public static List<int> noRippersProj = [];

        public static RippersPlayer Instance;

        void ICWRLoader.SetupData() {
            Instance = this;
            noRippersProj = [
                ModContent.ProjectileType<CosmicDischargeFlail>(),
                ModContent.ProjectileType<CosmicIceBurst>(),
                ModContent.ProjectileType<MuraExecutionCut>(),
                ModContent.ProjectileType<StormGoddessSpearProj>(),
                ModContent.ProjectileType<StormArc>(),
                ModContent.ProjectileType<StormLightning>(),
            ];
        }

        void ICWRLoader.UnLoadData() {
            Instance = null;
            noRippersProj?.Clear();
        }

        public override bool On_ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            CalamityPlayer mp = player.Calamity();
            if (mp.adrenalineModeActive) {
                if (noRippersProj.Contains(proj.type)) {
                    return false;
                }
            }
            return base.On_ModifyHitNPCWithProj(player, proj, target, ref modifiers);
        }
    }
}
