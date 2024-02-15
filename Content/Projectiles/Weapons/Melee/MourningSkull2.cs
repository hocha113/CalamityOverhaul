using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class MourningSkull2 : MourningSkull
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "MourningSkull";
        public override void AI() {
            if (Projectile.ai[0] < 0f) {
                Projectile.alpha = 0;
            }
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 50;
            }
            if (Projectile.alpha < 0) {
                Projectile.alpha = 0;
            }

            if (Projectile.velocity.X < 0f) {
                Projectile.spriteDirection = -1;
                Projectile.rotation = (float)Math.Atan2((double)-(double)Projectile.velocity.Y, (double)-(double)Projectile.velocity.X);
            }
            else {
                Projectile.spriteDirection = 1;
                Projectile.rotation = (float)Math.Atan2(Projectile.velocity.Y, Projectile.velocity.X);
            }

            if (Projectile.timeLeft < 570) {
                NPC target = Projectile.Center.InPosClosestNPC(600);
                if (target != null) {
                    int npcTracker = target.whoAmI;
                    if (Main.npc[npcTracker].active) {
                        Vector2 projPos = new(Projectile.position.X + (Projectile.width * 0.5f), Projectile.position.Y + (Projectile.height * 0.5f));
                        float npcXDist = Main.npc[npcTracker].position.X - projPos.X;
                        float npcYDist = Main.npc[npcTracker].position.Y - projPos.Y;
                        float npcDistance = (float)Math.Sqrt((double)((npcXDist * npcXDist) + (npcYDist * npcYDist)));
                        npcDistance = 8f / npcDistance;
                        npcXDist *= npcDistance;
                        npcYDist *= npcDistance;
                        Projectile.velocity.X = ((Projectile.velocity.X * 14f) + npcXDist) / 15f;
                        Projectile.velocity.Y = ((Projectile.velocity.Y * 14f) + npcYDist) / 15f;
                    }
                    else {
                        float homingRange = 1000f;
                        int inc;
                        for (int j = 0; j < Main.maxNPCs; j = inc + 1) {
                            if (Main.npc[j].CanBeChasedBy(Projectile, false)) {
                                float targetX = Main.npc[j].position.X + Main.npc[j].width / 2;
                                float targetY = Main.npc[j].position.Y + Main.npc[j].height / 2;
                                float targetDist = Math.Abs(Projectile.position.X + Projectile.width / 2 - targetX) + Math.Abs(Projectile.position.Y + Projectile.height / 2 - targetY);
                                if (targetDist < homingRange && Collision.CanHit(Projectile.position, Projectile.width, Projectile.height, Main.npc[j].position, Main.npc[j].width, Main.npc[j].height)) {
                                    homingRange = targetDist;
                                    Projectile.ai[0] = j;
                                }
                            }
                            inc = j;
                        }
                    }

                    if (Projectile.velocity.X < 0f) {
                        Projectile.spriteDirection = -1;
                        Projectile.rotation = (float)Math.Atan2((double)-(double)Projectile.velocity.Y, (double)-(double)Projectile.velocity.X);
                    }
                    else {
                        Projectile.spriteDirection = 1;
                        Projectile.rotation = (float)Math.Atan2(Projectile.velocity.Y, Projectile.velocity.X);
                    }

                    int eightConst = 8;
                    int mourningDust = Dust.NewDust(new Vector2(Projectile.position.X + eightConst, Projectile.position.Y + eightConst), Projectile.width - (eightConst * 2), Projectile.height - (eightConst * 2), Main.rand.NextBool() ? 5 : 6, 0f, 0f, 0, default, 1f);
                    Dust dust = Main.dust[mourningDust];
                    dust.velocity *= 0.5f;
                    dust = Main.dust[mourningDust];
                    dust.velocity += Projectile.velocity * 0.5f;
                    Main.dust[mourningDust].noGravity = true;
                    Main.dust[mourningDust].noLight = true;
                    Main.dust[mourningDust].scale = 1.4f;
                    return;
                }
            }
        }
    }
}
