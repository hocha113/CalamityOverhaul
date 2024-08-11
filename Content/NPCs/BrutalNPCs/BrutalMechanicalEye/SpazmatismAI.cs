using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye
{
    internal class SpazmatismAI : NPCCoverage
    {
        public override int TargetID => NPCID.Spazmatism;
        public static bool Accompany;
        public static Color textColor1 => new(155, 255, 255);
        public static Color textColor2 => new(213, 4, 11);
        public static int[] ai = new int[8];
        private int frame;
        private static int frameIndex;
        private static int frameCount;
        public override void SetProperty() {
            SetAccompany(npc, out Accompany);
            ai = new int[8];
            frameIndex = 3;
        }
        public static void SetAccompany(NPC npc, out bool accompany) {
            accompany = false;
            foreach (var n in Main.npc) {
                if (!n.active) {
                    continue;
                }
                if (n.type == NPCID.SkeletronPrime) {
                    accompany = true;
                }
            }
            if (accompany) {
                npc.lifeMax *= 2;
                npc.life = npc.lifeMax;
                for (int i = 0; i < npc.buffImmune.Length; i++) {
                    npc.buffImmune[i] = true;
                }
                if (npc.type == NPCID.Spazmatism) {
                    NPC eye2 = CWRUtils.FindNPC(NPCID.Retinazer);
                    if (eye2.Alives()) {
                        npc.realLife = eye2.whoAmI;
                    }
                }
            }
        }

        public static void NetAISend(NPC eye) {
            if (CWRUtils.isServer) {
                var netMessage = CWRMod.Instance.GetPacket();
                netMessage.Write((byte)CWRMessageType.BrutalTwinsAI);

                bool isSpazmatism = eye.type == NPCID.Spazmatism;
                netMessage.Write(isSpazmatism);

                int[] aiArray = RetinazerAI.ai;
                if (isSpazmatism) {
                    aiArray = ai;
                }

                foreach (var num in aiArray) {
                    netMessage.Write(num);
                }

                netMessage.Send();
            }
        }

        public static void SetEyeValue(NPC eye, Player player, Vector2 toPoint, Vector2 toTarget) {
            float roting = toTarget.ToRotation() - MathHelper.PiOver2;
            eye.damage = 0;
            eye.position += player.velocity;
            eye.Center = Vector2.Lerp(eye.Center, toPoint, 0.1f);
            eye.velocity = toTarget.UnitVector() * 0.01f;
            eye.EntityToRot(roting, 0.2f);
        }

        public static bool AccompanyAI(NPC eye, ref int[] ai, bool accompany) {
            if (!accompany) {
                return false;
            }

            float lifeRog = eye.life / (float)eye.lifeMax;
            bool isSpazmatism = eye.type == NPCID.Spazmatism;
            NPC skeletronPrime = CWRUtils.FindNPC(NPCID.SkeletronPrime);
            if (!skeletronPrime.Alives() || skeletronPrime?.ai[1] == 3 || lifeRog < 0.8f) {
                eye.dontTakeDamage = true;
                eye.position += new Vector2(0, -26);
                if (ai[6] == 0) {
                    if (isSpazmatism && !CWRUtils.isServer) {
                        CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text3"), textColor1);
                        CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text4"), textColor2);
                    }
                    for (int i = 0; i < 13; i++) {
                        Item.NewItem(eye.GetSource_FromAI(), eye.Hitbox, ItemID.Heart);
                    }
                }
                if (ai[6] > 380) {
                    eye.active = false;
                }
                ai[6]++;
                return true;
            }

            if (ai[0] == 0 && !CWRUtils.isServer && isSpazmatism) {
                CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text1"), textColor1);
                CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text2"), textColor2);
                ai[0]++;
                NetAISend(eye);
            }

            Player player = Main.player[skeletronPrime.target];
            Vector2 toTarget = eye.Center.To(player.Center);
            Vector2 toPoint = skeletronPrime.Center;
            eye.damage = eye.defDamage;

            Lighting.AddLight(eye.Center, (isSpazmatism ? Color.OrangeRed : Color.BlueViolet).ToVector3());

            bool skeletronPrimeInSprint = skeletronPrime.ai[1] == 1;
            bool LaserWall = BrutalSkeletronPrimeAI.ai4 == 2;
            bool isDestroyer = false;
            bool isIdle = BrutalSkeletronPrimeAI.ai11 > 0;

            foreach (var p in Main.projectile) {
                if (!p.active) {
                    continue;
                }
                if (p.type == ModContent.ProjectileType<SetPosingStarm>()) {
                    isDestroyer = true;
                }
            }

            if (isIdle) {
                toPoint = skeletronPrime.Center + new Vector2(isSpazmatism ? 50 : -50, -100);
                SetEyeValue(eye, player, toPoint, toTarget);
                return true;
            }

            if (LaserWall) {
                toPoint = player.Center + new Vector2(isSpazmatism ? 450 : -450, -400);
                SetEyeValue(eye, player, toPoint, toTarget);
                return true;
            }

            if (isDestroyer) {
                toPoint = player.Center + (ai[4] * 0.04f + MathHelper.TwoPi / 2 * (isSpazmatism ? 1 : 2)).ToRotationVector2() * 760;
                if (++ai[5] > 10) {
                    if (!CWRUtils.isClient) {
                        int type = isSpazmatism ? ModContent.ProjectileType<Fireball>() : ProjectileID.EyeLaser;
                        Projectile.NewProjectile(eye.GetSource_FromAI(), eye.Center, toTarget.UnitVector() * 9, type, 32, 0);
                    }
                    ai[5] = 0;
                    NetAISend(eye);
                }
                ai[4]++;
                SetEyeValue(eye, player, toPoint, toTarget);
                return true;
            }

            if (skeletronPrimeInSprint || ai[7] > 0) {
                switch (ai[1]) {
                    case 0:
                        toPoint = player.Center + new Vector2(isSpazmatism ? 300 : -300, -450);
                        if (ai[2] == 30 && !CWRUtils.isClient) {
                            int type = ProjectileID.EyeLaser;
                            if (isSpazmatism) {
                                type = ModContent.ProjectileType<Fireball>();
                            }
                            for (int i = 0; i < 6; i++) {
                                Projectile.NewProjectile(eye.GetSource_FromAI(), eye.Center
                                    , (MathHelper.TwoPi / 6 * i).ToRotationVector2() * 9, type, 32, 0);
                            }
                        }
                        if (ai[2] > 60) {
                            ai[7] = 10;
                            ai[1] = 1;
                            ai[2] = 0;
                            NetAISend(eye);
                        }
                        ai[2]++;
                        break;
                    case 1:
                        toPoint = player.Center + new Vector2(isSpazmatism ? 600 : -600, 0);
                        if (++ai[2] > 15) {
                            if (!CWRUtils.isClient) {
                                int type = ProjectileID.EyeLaser;
                                if (isSpazmatism) {
                                    type = ModContent.ProjectileType<Fireball>();
                                }
                                Projectile.NewProjectile(eye.GetSource_FromAI(), eye.Center, toTarget.UnitVector() * 11, type, 32, 0);
                            }
                            ai[3]++;
                            ai[2] = 0;
                            NetAISend(eye);
                        }

                        if (ai[3] > 6) {
                            ai[3] = 0;
                            ai[2] = 0;
                            ai[1] = 0;
                            ai[7] = 0;
                            NetAISend(eye);
                        }
                        else if (ai[7] < 2) {
                            ai[7] = 2;
                        }
                        break;
                }

                SetEyeValue(eye, player, toPoint, toTarget);
                return true;
            }

            if (ai[7] > 0) {
                ai[7]--;
            }

            return false;
        }

        public override bool AI() {
            if (++frameCount > 5) {
                if (++frameIndex > 5) {
                    frameIndex = 3;
                }
                frameCount = 0;
            }

            npc.dontTakeDamage = false;
            if (AccompanyAI(npc, ref ai, Accompany)) {
                return false;
            }
            
            ai[0]++;
            return true;
        }

        internal static bool IsCCK(NPC eye, int[] ai) {
            NPC skeletronPrime = CWRUtils.FindNPC(NPCID.SkeletronPrime);
            
            if (!skeletronPrime.Alives()) {
                return false;
            }
            if (ai[7] > 0 || skeletronPrime.ai[1] == 1) {
                return true;
            }

            int num = 0;
            foreach (var p in Main.projectile) {
                if (!p.active) {
                    continue;
                }
                if (p.type == ModContent.ProjectileType<SetPosingStarm>()) {
                    num++;
                }
            }

            return num > 0;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Accompany && IsCCK(npc, ai)) {
                Main.instance.LoadNPC(npc.type);
                Texture2D mainValue = TextureAssets.Npc[npc.type].Value;
                Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, CWRUtils.GetRec(mainValue, frameIndex, 6)
                , drawColor, npc.rotation, CWRUtils.GetOrig(mainValue, 6), npc.scale, SpriteEffects.None, 0);
                return false;
            }
            return true;
        }
    }
}
