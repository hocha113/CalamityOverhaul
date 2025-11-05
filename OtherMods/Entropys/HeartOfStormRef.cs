using CalamityOverhaul.Content.Items.Melee.StormGoddessSpears;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.Entropys
{
    [JITWhenModsEnabled("CalamityEntropy")]
    internal class HeartOfStormRef
    {
        public static bool IsHeartOfStorm(Player player) => false;// player.Entropy().heartOfStorm;
    }

    internal class HeartOfStormPlayer : ModPlayer
    {
        public override void UpdateAutopause() {
            if (EntropyCore.IsHeartOfStorm(Player)) {
                //生成风暴女神
                if (Player.whoAmI == Main.myPlayer && Player.CountProjectilesOfID<StormGoddess>() == 0) {
                    Projectile.NewProjectile(
                            Player.FromObjectGetParent(),
                            Player.Center,
                            Vector2.Zero,
                            ModContent.ProjectileType<StormGoddess>(),
                            0,
                            0f,
                            Player.whoAmI
                        );
                }
            }
        }
    }
}
