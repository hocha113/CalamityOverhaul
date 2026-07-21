using CalamityOverhaul.Content.Scenarios.OldDuke.Campsites;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke
{
    /// <summary>
    /// 切磋生成载体；先置WannaToFight再权威端刷NPC，防首帧ShouldLeave
    /// </summary>
    internal class SpawnOldDukeWannaToFight : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 360;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
        }

        public override void AI() {
            //各端先置切磋标记
            OldDukeCampsite.WannaToFight = true;

            //权威端刷NPC
            if (Projectile.ai[0] == 0 && !VaultUtils.isClient) {
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.WorldData);
                }

                Player player = Main.player[Projectile.owner];
                if (player.Alives() && !NPC.AnyNPCs(CWRID.NPC_OldDuke)) {
                    NPC.NewNPC(NPC.GetBossSpawnSource(player.whoAmI),
                        (int)player.Center.X, (int)player.Center.Y - 200, CWRID.NPC_OldDuke);
                }
            }

            Projectile.ai[0]++;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
