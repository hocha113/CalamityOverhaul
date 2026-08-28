using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke
{
    internal class SulfuricacidExplosion : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 60;
        }

        public override void AI() {
            if (Projectile.timeLeft < 30 && (CWRWorld.Asura || CWRWorld.MasterMode)) {
                int index = NPC.FindFirstNPC(CWRID.NPC_OldDuke);
                if (index.TryGetNPC(out var boss) && boss.type == CWRID.NPC_OldDuke && !boss.friendly) {
                    Projectile.friendly = false;
                    Projectile.hostile = true;
                    Projectile.damage = 100;
                }
            }
        }
    }
}
