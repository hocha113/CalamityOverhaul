using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using CalamityOverhaul.Content.TileProcessors;
using InnoVault.GameSystem;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.ModifySupCalNPCs
{
    internal class ModifySCalAltar : TileOverride
    {
        public override int TargetID => CWRID.Tile_SCalAltar;
        public override bool IsLoadingEnabled(Mod mod) => TargetID > 0;

        public static void HitEffctByPlayer(Player player) {
            for (int z = 0; z < 40; z++) {
                float angle = MathHelper.TwoPi * z / 40f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 12f);

                Dust dust = Dust.NewDustPerfect(
                    player.Center,
                    CWRID.Dust_Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.8f, 3f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1.4f;
            }

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.7f,
                Pitch = -0.4f
            }, player.Center);
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with {
                Volume = 0.6f,
                Pitch = -0.5f
            }, player.Center);
            PlayerDeathReason pd = PlayerDeathReason.ByCustomReason(BloodAltarTP.SacrificeDeathReason.ToNetworkText(player.name));
            player.Hurt(pd, 250, 0);
        }

        public static bool? Click() {
            if (EbnEffect.IsActive) {
                return false;
            }

            if (EbnState.IsConquered(Main.LocalPlayer)) {
                if (!InWorldBossPhase.Downed29.Invoke()) {
                    if (++SCalAltarScenario.Count > 2) {
                        HitEffctByPlayer(Main.LocalPlayer);
                        return false;
                    }

                    NarrativeRouter.Begin<SCalAltarScenario>();
                    return false;
                }
            }

            return null;
        }

        public override bool? RightClick(int i, int j, Tile tile) => Click();
    }

    internal class ModifySCalAltarLarge : TileOverride
    {
        public override int TargetID => CWRID.Tile_SCalAltarLarge;
        public override bool IsLoadingEnabled(Mod mod) => TargetID > 0;
        public override bool? RightClick(int i, int j, Tile tile) => ModifySCalAltar.Click();
    }
}
