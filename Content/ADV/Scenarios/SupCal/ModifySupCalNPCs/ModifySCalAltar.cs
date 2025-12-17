using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using InnoVault.GameSystem;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.ModifySupCalNPCs
{
    internal class ModifySCalAltar : TileOverride
    {
        public override int TargetID => CWRID.Tile_SCalAltar;
        public override bool IsLoadingEnabled(Mod mod) => CWRRef.Has;
        public static void HitEffctByPlayer(Player player) {
            //硫磺火粒子爆发，使用Brimstone粒子
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
            //硫磺火召唤音效
            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.7f,
                Pitch = -0.4f
            }, player.Center);
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with {
                Volume = 0.6f,
                Pitch = -0.5f
            }, player.Center);
            PlayerDeathReason pd = PlayerDeathReason.ByCustomReason(CWRLocText.Instance.BloodAltar_Text3.ToNetworkText(player.name));
            player.Hurt(pd, 250, 0);
        }
        public static bool? Click() {
            if (EbnEffect.IsActive) {
                return false;//永恒燃烧的现在结局激活时无法使用灾厄祭坛
            }
            if (EbnPlayer.IsConquered(Main.LocalPlayer)) {
                if (!InWorldBossPhase.Downed29.Invoke()) {//没有击败星流巨械
                    if (++SCalAltarScenario.Count > 2) {
                        HitEffctByPlayer(Main.LocalPlayer);
                        return false;//无法使用灾厄祭坛
                    }
                    ScenarioManager.Reset<SCalAltarScenario>();
                    ScenarioManager.Start<SCalAltarScenario>();
                    return false;//无法使用灾厄祭坛
                }
            }
            return null;
        }
        public override bool? RightClick(int i, int j, Tile tile) => Click();
    }

    internal class ModifySCalAltarLarge : TileOverride
    {
        public override int TargetID => CWRID.Tile_SCalAltarLarge;
        public override bool IsLoadingEnabled(Mod mod) => CWRRef.Has;
        public override bool? RightClick(int i, int j, Tile tile) => ModifySCalAltar.Click();
    }
}
