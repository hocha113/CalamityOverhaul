using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes
{
    /// <summary>
    /// 切磋生成老公爵的弹幕载体
    /// <para>利用弹幕自带的全端同步机制，在AI中先设置WannaToFight=true，再由服务端/单人生成NPC</para>
    /// <para>这样保证所有客户端在NPC到达前就已知道这是切磋模式，
    /// 避免NPC首帧AI因ShouldLeaveAfterCooperation()而消失</para>
    /// </summary>
    internal class SpawnOldDukeWannaToFight : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

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
            //所有端先设置切磋标记，保证NPC同步到达时各端已知道是切磋模式
            OldDukeCampsite.WannaToFight = true;

            //仅在服务端或单人模式下生成NPC，避免重复生成
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
