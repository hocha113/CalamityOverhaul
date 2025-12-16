using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 包含AI的绘制和动画逻辑
    /// </summary>
    internal partial class HeadPrimeAI
    {
        internal static void DrawArm(SpriteBatch spriteBatch, NPC rCurrentNPC, Vector2 screenPos) {
            NPC head = Main.npc[(int)rCurrentNPC.ai[1]];

            if (setPosingStarmCount > 0) {
                return;
            }

            if ((head.ai[1] == 1 || head.ai[1] == 2 || head.GetOverride<HeadPrimeAI>().ai[10] > 0) && setPosingStarmCount <= 0) {
                float rCurrentNPCRotation = rCurrentNPC.rotation;
                Vector2 drawPos = rCurrentNPC.Center + (rCurrentNPCRotation + MathHelper.PiOver2).ToRotationVector2() * -120;
                Rectangle drawRec = BSPRAM.Value.GetRectangle();
                Vector2 drawOrig = drawRec.Size() / 2;
                SpriteEffects spriteEffects = SpriteEffects.None;
                float rotation7 = rCurrentNPCRotation;
                Color color7 = Lighting.GetColor((int)drawPos.X / 16, (int)(drawPos.Y / 16f));
                drawPos.X -= Main.screenPosition.X;
                drawPos.Y -= Main.screenPosition.Y;
                spriteBatch.Draw(BSPRAM.Value, drawPos, drawRec, color7, rotation7, drawOrig, 1f, spriteEffects, 0f);
                spriteBatch.Draw(BSPRAMGlow.Value, drawPos, drawRec, Color.White, rotation7, drawOrig, 1f, spriteEffects, 0f);

                int num24 = Dust.NewDust(rCurrentNPC.Center, 10, 10, DustID.FireworkFountain_Red, 0, 0, 0, Color.Gold, 0.5f);
                Main.dust[num24].noGravity = false;
                return;
            }

            Vector2 vector7 = new Vector2(rCurrentNPC.position.X + rCurrentNPC.width * 0.5f - 5f * rCurrentNPC.ai[0], rCurrentNPC.position.Y + 20f);
            for (int k = 0; k < 2; k++) {
                float num21 = head.position.X + head.width / 2 - vector7.X;
                float num22 = head.position.Y + head.height / 2 - vector7.Y;
                float num23;

                if (k == 0) {
                    num21 -= 200f * rCurrentNPC.ai[0];
                    num22 += 130f;
                    num23 = (float)Math.Sqrt(num21 * num21 + num22 * num22);
                    num23 = 92f / num23;
                    vector7.X += num21 * num23;
                    vector7.Y += num22 * num23;
                }
                else {
                    num21 -= 50f * rCurrentNPC.ai[0];
                    num22 += 80f;
                    num23 = (float)Math.Sqrt(num21 * num21 + num22 * num22);
                    num23 = 60f / num23;
                    vector7.X += num21 * num23;
                    vector7.Y += num22 * num23;
                }

                float rotation7 = (float)Math.Atan2(num22, num21) - 1.57f;
                Color color7 = Lighting.GetColor((int)vector7.X / 16, (int)(vector7.Y / 16f));

                Texture2D value = BSPRAM.Value;
                Texture2D glow = BSPRAMGlow.Value;
                if (k == 0) {
                    value = BSPRAM_Forearm.Value;
                    glow = BSPRAM_ForearmGlow.Value;
                }

                Vector2 drawPos = new Vector2(vector7.X - screenPos.X, vector7.Y - screenPos.Y);
                Vector2 drawOrig = new Vector2(TextureAssets.BoneArm.Width() * 0.5f, TextureAssets.BoneArm.Height() * 0.5f);
                Rectangle drawRec = new Rectangle(0, 0, TextureAssets.BoneArm.Width(), TextureAssets.BoneArm.Height());
                SpriteEffects spriteEffects = k == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

                spriteBatch.Draw(value, drawPos, drawRec, color7, rotation7, drawOrig, 1f, spriteEffects, 0f);
                spriteBatch.Draw(glow, drawPos, drawRec, Color.White, rotation7, drawOrig, 1f, spriteEffects, 0f);

                if (k == 0) {
                    vector7.X += num21 * num23 / 2f;
                    vector7.Y += num22 * num23 / 2f;
                }
                else if (Main.instance.IsActive) {
                    vector7.X += num21 * num23 - 16f;
                    vector7.Y += num22 * num23 - 6f;
                    int num24 = Dust.NewDust(new Vector2(vector7.X, vector7.Y), 30, 10
                        , DustID.FireworkFountain_Red, num21 * 0.02f, num22 * 0.02f, 0, Color.Gold, 0.5f);
                    Main.dust[num24].noGravity = true;
                }
            }
        }

        internal static bool SetArmRot(NPC arm, NPC head, int type) {
            if (DontReform()) {
                return false;
            }

            if (type == NPCID.PrimeLaser) {
                type = 0;
            }
            else if (type == NPCID.PrimeCannon) {
                type = 1;
            }
            else if (type == NPCID.PrimeSaw) {
                type = 2;
            }
            else if (type == NPCID.PrimeVice) {
                type = 3;
            }

            NPCOverride pCOverride = head.GetOverride<HeadPrimeAI>();
            for (int i = 0; i < arm.buffImmune.Length; i++) {
                arm.buffImmune[i] = true;
            }
            arm.damage = arm.defDamage;
            if (pCOverride.ai[10] > 0) {
                arm.damage = 0;
            }
            if (setPosingStarmCount > 0 || pCOverride.ai[10] > 0) {
                float rot2 = MathHelper.TwoPi / 4 * type + head.rotation;
                Vector2 toPoint2 = head.Center + rot2.ToRotationVector2() * head.width;
                arm.Center = Vector2.Lerp(arm.Center, toPoint2, 0.2f);
                arm.rotation = head.Center.To(arm.Center).ToRotation() - MathHelper.PiOver2;
                arm.velocity = Vector2.Zero;
                arm.position += head.velocity;
                arm.dontTakeDamage = true;
                arm.damage = 0;
                return true;
            }
            if (head.ai[1] != 1 && head.ai[1] != 2) {
                return false;
            }
            float rot = pCOverride.ai[9] * 0.2f + MathHelper.TwoPi / 4 * type;
            Vector2 toPoint = head.Center + rot.ToRotationVector2() * head.width * 2;
            float origeRot = head.Center.To(arm.Center).ToRotation();
            arm.Center = Vector2.Lerp(arm.Center, toPoint, 0.5f);
            arm.rotation = origeRot - MathHelper.PiOver2;
            arm.velocity = Vector2.Zero;
            arm.position += head.velocity;
            arm.dontTakeDamage = true;
            arm.damage = 0;
            if (!VaultUtils.isClient && NPC.IsMechQueenUp && pCOverride.ai[9] % 6 == 0 && setPosingStarmCount <= 0 && pCOverride.ai[10] <= 0) {
                int projType = ProjectileID.DeathLaser;
                Vector2 ver = origeRot.ToRotationVector2() * 6;
                Projectile.NewProjectile(arm.GetSource_FromAI(), arm.Center, ver, projType, 36, 2);
            }
            return true;
        }

        public override bool FindFrame(int frameHeight) {
            if (++frameCount <= 10) {
                return false;
            }

            if (npc.ai[1] == 0) {
                if (noArm && ai9 > 2) {
                    if (++frame > 11) {
                        frame = 8;
                    }
                }
                else {
                    if (++frame > 3) {
                        frame = 0;
                    }
                }
            }
            else if (npc.ai[1] == 1) {
                if (++frame > 7) {
                    frame = 4;
                }
            }

            frameCount = 0;
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => DontReform();

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (npc.ai[1] == 4) {
                drawColor = Color.Red;
            }
            if (DontReform()) {
                return true;
            }
            Texture2D mainValue = HandAsset.Value;
            Texture2D glowValue = HandAssetGlow.Value;
            Rectangle rectangle = mainValue.GetRectangle(frame, 12);
            Vector2 orig = rectangle.Size() / 2;

            float sengs = 0.2f;
            for (int i = 0; i < npc.oldPos.Length; i++) {
                Vector2 drawOldPos = npc.oldPos[i] + npc.Size / 2 - Main.screenPosition;
                Main.EntitySpriteDraw(mainValue, drawOldPos, rectangle, Color.White * sengs
                    , npc.rotation, orig, npc.scale * (0.8f + sengs), SpriteEffects.None, 0);
                sengs *= 0.8f;
            }

            Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, rectangle
                , drawColor, npc.rotation, orig, npc.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glowValue, npc.Center - Main.screenPosition, rectangle
                , Color.White, npc.rotation, orig, npc.scale, SpriteEffects.None, 0);

            if (player != null && noEye && npc.ai[0] == 3) {
                Vector2 toD = player.Center.To(npc.Center);
                Vector2 origpos = player.Center - Main.screenPosition;
                float alp = toD.Length() / 400f;
                if (alp > 1) {
                    alp = 1;
                }
                Vector2 drawPos1 = new Vector2(-toD.X, toD.Y) + origpos;
                Main.EntitySpriteDraw(mainValue, drawPos1, rectangle
                , drawColor * alp, npc.rotation, orig, npc.scale, SpriteEffects.None, 0);
                Vector2 drawPos2 = new Vector2(-toD.X, -toD.Y) + origpos;
                Main.EntitySpriteDraw(mainValue, drawPos2, rectangle
                , drawColor * alp, npc.rotation, orig, npc.scale, SpriteEffects.None, 0);
                Vector2 drawPos3 = new Vector2(toD.X, -toD.Y) + origpos;
                Main.EntitySpriteDraw(mainValue, drawPos3, rectangle
                , drawColor * alp, npc.rotation, orig, npc.scale, SpriteEffects.None, 0);
            }

            return false;
        }
    }
}
