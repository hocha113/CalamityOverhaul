using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Audio;
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
        public const int maxAINum = 12;
        public static int[] ai = new int[maxAINum];
        private int frame;
        private static int frameIndex;
        private static int frameCount;
        public override void SetProperty() => SetAccompany(npc, ref ai, out Accompany);
        public static void SetAccompany(NPC npc, ref int[] ai, out bool accompany) {
            ai = new int[maxAINum];
            for (int i = 0; i < ai.Length; i++) {
                ai[i] = 0;
            }
            frameIndex = 3;

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

        public static void NetAIReceive(BinaryReader reader) {
            bool isSpazmatism = reader.ReadBoolean();
            if (isSpazmatism) {
                for (int i = 0; i < ai.Length; i++) {
                    ai[i] = reader.ReadInt32();
                }
            }
            else {
                for (int i = 0; i < RetinazerAI.ai.Length; i++) {
                    RetinazerAI.ai[i] = reader.ReadInt32();
                }
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
            bool skeletronPrimeIsDead = !skeletronPrime.Alives();
            bool skeletronPrimeIsTwo = skeletronPrimeIsDead ? false : (skeletronPrime.ai[0] == 3);
            int projType = isSpazmatism ? ModContent.ProjectileType<Fireball>() : ProjectileID.EyeLaser;
            int projDamage = eye.GetProjectileDamage(projType);  

            Player player = skeletronPrimeIsDead ? Main.player[eye.target] : Main.player[skeletronPrime.target];

            Lighting.AddLight(eye.Center, (isSpazmatism ? Color.OrangeRed : Color.BlueViolet).ToVector3());

            if (ai[0] == 0) {
                if (!CWRUtils.isServer && isSpazmatism) {
                    CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text1"), textColor1);
                    CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text2"), textColor2);
                }                
                ai[0] = 1;
                NetAISend(eye);
            }

            if (ai[0] == 1) {
                if (Debut(eye, player, ref ai)) {
                    return true;
                }
            }

            if (IsCCK(eye, ai)) {
                eye.HitSound = SoundID.NPCHit4;
            }

            if (skeletronPrimeIsDead || skeletronPrime?.ai[1] == 3 || lifeRog < 0.7f) {
                eye.dontTakeDamage = true;
                eye.position += new Vector2(0, -26);
                if (ai[6] == 0) {
                    if (isSpazmatism && !CWRUtils.isServer) {
                        if (lifeRog < 0.7f) {
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text3"), textColor1);
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text4"), textColor2);
                        }
                        else if (skeletronPrime?.ai[1] == 3) {
                            CWRUtils.Text("目标已失去生命体征", textColor1);
                            CWRUtils.Text("目标已失去生命体征", textColor2);
                        }
                        else {
                            CWRUtils.Text("任务失败，尝试从战场撤离...", textColor1);
                            CWRUtils.Text("任务失败，尝试从战场撤离...", textColor2);
                        }
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

            Vector2 toTarget = eye.Center.To(player.Center);
            Vector2 toPoint = skeletronPrime.Center;
            eye.damage = eye.defDamage;
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
                Projectile projectile = null;
                foreach (var p in Main.projectile) {
                    if (!p.active) {
                        continue;
                    }
                    if (p.type == ModContent.ProjectileType<SetPosingStarm>()) {
                        projectile = p;
                    }
                }

                int fireTime = 10;
                if (projectile.Alives()) {
                    fireTime = 5;
                    toTarget = eye.Center.To(projectile.Center);
                    toPoint = projectile.Center + (ai[4] * 0.02f + MathHelper.TwoPi / 2 * (isSpazmatism ? 1 : 2)).ToRotationVector2() * 1060;
                }
                else {
                    toPoint = player.Center + (ai[4] * 0.04f + MathHelper.TwoPi / 2 * (isSpazmatism ? 1 : 2)).ToRotationVector2() * 760;
                }

                if (++ai[5] > fireTime) {
                    if (!CWRUtils.isClient) {
                        Projectile.NewProjectile(eye.GetSource_FromAI()
                            , eye.Center, toTarget.UnitVector() * 9, projType, projDamage, 0);
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
                        toPoint = player.Center + new Vector2(isSpazmatism ? 500 : -500, -650);
                        if (ai[2] == 30 && !CWRUtils.isClient) {
                            for (int i = 0; i < 6; i++) {
                                Projectile.NewProjectile(eye.GetSource_FromAI(), eye.Center
                                    , (MathHelper.TwoPi / 6 * i).ToRotationVector2() * 9, projType, projDamage, 0);
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
                        toPoint = player.Center + new Vector2(isSpazmatism ? 700 : -700, ai[9]);
                        if (++ai[2] > 20) {
                            if (!CWRUtils.isClient) {
                                if (skeletronPrimeIsTwo) {
                                    for (int i = 0; i < 3; i++) {
                                        Vector2 ver = toTarget.RotatedBy((-1 + i) * 0.06f).UnitVector() * 6;
                                        Projectile.NewProjectile(eye.GetSource_FromAI(), eye.Center, ver, projType, projDamage, 0);
                                    }
                                }
                                else {
                                    Projectile.NewProjectile(eye.GetSource_FromAI(), eye.Center, toTarget.UnitVector() * 9, projType, projDamage, 0);
                                }
                                
                            }
                            ai[3]++;
                            ai[2] = 0;
                            NetAISend(eye);
                        }

                        if (ai[2] == 2) {
                            if (skeletronPrimeIsTwo) {
                                if (ai[10] == 0) {
                                    ai[10] = 1;
                                }
                                ai[9] = isSpazmatism ? -600 : 600;
                                ai[9] *= ai[10];
                                ai[10] *= -1;
                            }
                            else {
                                ai[9] = Main.rand.Next(140, 280) * (Main.rand.NextBool() ? -1 : 1);
                            }
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

        public static bool ProtogenesisAI(NPC eye, ref int[] ai) {
            return false;
        }

        private static bool Debut(NPC npc, Player player, ref int[] ai) {
            ref int ai1 = ref ai[1];
            if (ai1 == 0) {
                npc.life = 1;
            }

            npc.damage = 0;
            npc.dontTakeDamage = true;

            Vector2 toTarget = npc.Center.To(player.Center);
            npc.rotation = toTarget.ToRotation() - MathHelper.PiOver2;
            npc.velocity = Vector2.Zero;
            npc.position += player.velocity;
            Vector2 toPoint = player.Center;

            if (ai1 < 60) {
                toPoint = player.Center + new Vector2(npc.type == NPCID.Spazmatism ? 500 : -500, 500);
            }
            else {
                toPoint = player.Center + new Vector2(npc.type == NPCID.Spazmatism ? -500 : 500, -500);
                if (ai1 == 90 && !CWRUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow, Main.LocalPlayer.Center);
                }
                if (ai1 > 90) {
                    int addNum = (int)(npc.lifeMax / 80f);
                    if (npc.life >= npc.lifeMax) {
                        npc.life = npc.lifeMax;
                    }
                    else {
                        npc.life += addNum;
                        CombatText.NewText(npc.Hitbox, CombatText.HealLife, addNum);
                    }
                }
            }

            if (ai1 == 172 && !CWRUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.SpawnArmMgs, Main.LocalPlayer.Center);
            }

            if (ai1 > 220) {
                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage;
                ai[0] = 2;
                ai1 = 0;
                return false;
            }

            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.065f);

            ai1++;

            return true;
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

            if (ProtogenesisAI(npc, ref ai)) {
                return false;
            }

            return true;
        }

        internal static bool IsCCK(NPC eye, int[] ai) {
            /*
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
            */
            return ai[0] == 2;
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
