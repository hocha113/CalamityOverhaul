using CalamityOverhaul.Content.Items.Melee.StormGoddessSpears;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.Entropys
{
    internal class HeartOfStormPlayer : ModPlayer
    {
        //每玩家帧戳缓存：装备更新与风暴女神 AI 每帧各读一次，Mod.Call 一次后整帧复用
        private uint heartFrame = uint.MaxValue;
        private bool heartCache;

        /// <summary>是否持有风暴之心</summary>
        public static bool GetHeartOfStorm(Player player) {
            if (CWRMod.Instance.calamityEntropy == null)
                return false;

            HeartOfStormPlayer mp = player.GetModPlayer<HeartOfStormPlayer>();
            if (mp.heartFrame == Main.GameUpdateCount) {
                return mp.heartCache;
            }
            mp.heartFrame = Main.GameUpdateCount;

            try {
                object result = CWRMod.Instance.calamityEntropy.Call(
                    "GetPlayerData",
                    player,
                    "heartOfStorm"
                );

                mp.heartCache = result is bool value && value;
            } catch {
                mp.heartCache = false;
            }
            return mp.heartCache;
        }

        public override void PostUpdateEquips() {
            if (!GetHeartOfStorm(Player)) {
                return;
            }
            //生成风暴女神（玩家更新阶段 ownedProjectileCounts 是上一拍完整快照，
            //生成后下一拍即计数，不会重复生成）
            if (Player.whoAmI != Main.myPlayer
                || Player.ownedProjectileCounts[ModContent.ProjectileType<StormGoddess>()] != 0) {
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
