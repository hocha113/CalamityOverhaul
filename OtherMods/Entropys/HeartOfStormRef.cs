using CalamityOverhaul.Content.Items.Melee.StormGoddessSpears;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.Entropys
{
    internal class HeartOfStormPlayer : ModPlayer
    {
        /// <summary>
        /// 玩家是否拥有风暴之心
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public static bool GetHeartOfStorm(Player player) {
            if (CWRMod.Instance.calamityEntropy == null)
                return false;

            try {
                object result = CWRMod.Instance.calamityEntropy.Call(
                    "GetPlayerData",
                    player,
                    "heartOfStorm"
                );

                return result is bool value && value;
            } catch {
                return false;
            }
        }

        public override void PostUpdateEquips() {
            if (!GetHeartOfStorm(Player)) {
                return;
            }
            //生成风暴女神
            if (Player.whoAmI != Main.myPlayer || Player.CountProjectilesOfID<StormGoddess>() != 0) {
                return;
            }
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
