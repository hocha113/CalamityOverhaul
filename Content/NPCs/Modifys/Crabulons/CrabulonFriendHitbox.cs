using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    internal class CrabulonFriendHitbox : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 296;
            Projectile.height = 196;
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.timeLeft = 2;
            Projectile.penetrate = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (((int)Projectile.ai[0]).TryGetNPC(out var npc)) {
                Projectile.Center = npc.Center;
            }
        }
    }
}
