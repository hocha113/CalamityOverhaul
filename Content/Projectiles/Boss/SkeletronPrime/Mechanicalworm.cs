using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    internal class Mechanicalworm : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 10000;
        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.scale = 2f;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 380;
            Projectile.alpha = 0;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Math.Abs(Projectile.velocity.X) > 13 || Math.Abs(Projectile.velocity.Y) > 13) {
                Projectile.velocity *= 0.99f;
            }
        }

        public override void OnKill(int timeLeft) {

        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D[] worm = [
                CWRUtils.GetT2DValue(CWRConstant.NPC + "BTD/Head")
                , CWRUtils.GetT2DValue(CWRConstant.NPC + "BTD/Body")
                , CWRUtils.GetT2DValue(CWRConstant.NPC + "BTD/Tail")];
            if (HeadPrimeAI.DontReform()) {
                Main.instance.LoadNPC(NPCID.TheDestroyer);
                Main.instance.LoadNPC(NPCID.TheDestroyerBody);
                Main.instance.LoadNPC(NPCID.TheDestroyerTail);
                worm = [TextureAssets.Npc[NPCID.TheDestroyer].Value
                , TextureAssets.Npc[NPCID.TheDestroyerBody].Value, TextureAssets.Npc[NPCID.TheDestroyerTail].Value];
            }
            Vector2 toD = (Projectile.rotation).ToRotationVector2();
            for (int i = 0; i < 122; i++) {
                Texture2D body = worm[1];
                if (i == 121) {
                    body = worm[2];
                }
                Main.EntitySpriteDraw(body, Projectile.Center - Main.screenPosition + toD * -(30 + i * 60) * Projectile.scale, null, Color.White
                , Projectile.rotation + MathHelper.PiOver2, body.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(worm[0], Projectile.Center - Main.screenPosition, null, Color.White
                , Projectile.rotation + MathHelper.PiOver2, worm[0].Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
