using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles
{
    internal class SpwanTextProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        internal Action text;
        public static void New(Player player, Action action, int time = 180) {
            Projectile proj = Projectile.NewProjectileDirect(player.GetSource_FromAI(), player.Center
                    , Vector2.Zero, ModContent.ProjectileType<SpwanTextProj>(), 0, 0, player.whoAmI);
            proj.timeLeft = time;
            SpwanTextProj spwanTextProj = (SpwanTextProj)proj.ModProjectile;
            spwanTextProj.text = action;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
        }
        public override bool ShouldUpdatePosition() => false;
        public override bool? CanDamage() => false;
        public override bool PreDraw(ref Color lightColor) => false;
        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                text.Invoke();
            }
        }
    }
}
