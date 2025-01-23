using CalamityMod.Events;
using CalamityMod.World;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.Projectiles.Boss.Eye;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye
{
    internal class SpazmatismAI : NPCOverride
    {
        public override int TargetID => NPCID.Spazmatism;
        public static Color TextColor1 => new(155, 215, 215);
        public static Color TextColor2 => new(200, 54, 91);
        Player player;
        public bool accompany;
        protected int frameIndex;
        protected int frameCount;
        public override void SetProperty() {
            npc.realLife = -1;

            for (int i = 0; i < ai.Length; i++) {
                ai[i] = 0;
            }

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
                //for (int i = 0; i < npc.buffImmune.Length; i++) {
                //    npc.buffImmune[i] = true;
                //}
                ai[11] = 0;
                NPC skeletronPrime = CWRUtils.FindNPCFromeType(NPCID.SkeletronPrime);
                if (skeletronPrime.Alives()) {
                    ai[11] = skeletronPrime.ai[0] != 3 ? 1 : 0;
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

        public static void SetEyeRealLife(NPC eye) {
            if (eye.realLife > 0) {
                return;
            }
            if (eye.type == NPCID.Spazmatism) {
                NPC retinazer = CWRUtils.FindNPCFromeType(NPCID.Retinazer);
                if (retinazer.Alives()) {
                    eye.realLife = retinazer.whoAmI;
                }
            }
            else {
                NPC spazmatism = CWRUtils.FindNPCFromeType(NPCID.Spazmatism);
                if (spazmatism.Alives()) {
                    eye.realLife = spazmatism.whoAmI;
                }
            }
        }

        public bool AccompanyAI() {
            if (!accompany) {
                return false;
            }

            NPC skeletronPrime = CWRUtils.FindNPCFromeType(NPCID.SkeletronPrime);
            float lifeRog = npc.life / (float)npc.lifeMax;
            bool bossRush = BossRushEvent.BossRushActive;
            bool death = CalamityWorld.death || bossRush;
            bool isSpazmatism = npc.type == NPCID.Spazmatism;
            bool lowBloodVolume = lifeRog < 0.7f;
            bool skeletronPrimeIsDead = !skeletronPrime.Alives();
            bool skeletronPrimeIsTwo = skeletronPrimeIsDead ? false : (skeletronPrime.ai[0] == 3);
            bool isSpawnFirstStage = ai[11] == 1;
            bool isSpawnFirstStageFromeExeunt = false;
            if (!skeletronPrimeIsDead && isSpawnFirstStage) {
                isSpawnFirstStageFromeExeunt = ((skeletronPrime.life / (float)skeletronPrime.lifeMax) < 0.6f);
            }

            int projType = isSpazmatism ? ModContent.ProjectileType<Fireball>() : ProjectileID.EyeLaser;
            int projDamage = 36;

            player = skeletronPrimeIsDead ? Main.player[npc.target] : Main.player[skeletronPrime.target];

            Lighting.AddLight(npc.Center, (isSpazmatism ? Color.OrangeRed : Color.BlueViolet).ToVector3());

            if (ai[0] == 0) {
                if (!VaultUtils.isServer && isSpazmatism) {
                    CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text1"), TextColor1);
                    CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text2"), TextColor2);
                }
                ai[0] = 1;
                NetAISend(npc);
            }

            if (ai[0] == 1) {
                if (Debut()) {
                    return true;
                }
            }

            if (IsSecondPhase()) {
                npc.HitSound = SoundID.NPCHit4;
            }

            if (skeletronPrimeIsDead || skeletronPrime?.ai[1] == 3 || lowBloodVolume || isSpawnFirstStageFromeExeunt) {
                npc.dontTakeDamage = true;
                npc.position += new Vector2(0, -36);
                if (ai[6] == 0) {
                    if (isSpazmatism && !VaultUtils.isServer) {
                        if (lowBloodVolume) {
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text3"), TextColor1);
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text4"), TextColor2);
                        }
                        else if (skeletronPrime?.ai[1] == 3) {
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), TextColor1);
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), TextColor2);
                        }
                        else if (isSpawnFirstStageFromeExeunt) {
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text6"), TextColor1);
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text6"), TextColor2);
                        }
                        else {
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text7"), TextColor1);
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text7"), TextColor2);
                        }
                    }
                    for (int i = 0; i < 13; i++) {
                        Item.NewItem(npc.GetSource_FromAI(), npc.Hitbox, ItemID.Heart);
                    }
                }
                if (ai[6] > 120) {
                    npc.active = false;
                }
                ai[6]++;
                return true;
            }

            Vector2 toTarget = npc.Center.To(player.Center);
            Vector2 toPoint = skeletronPrime.Center;
            npc.damage = npc.defDamage;
            bool skeletronPrimeInSprint = skeletronPrime.ai[1] == 1;
            bool LaserWall = skeletronPrime.CWR().NPCOverride.ai[3] == 2;
            bool isDestroyer = BrutalSkeletronPrimeAI.setPosingStarmCount > 0;
            bool isIdle = skeletronPrime.CWR().NPCOverride.ai[10] > 0;

            if (isIdle) {
                toPoint = skeletronPrime.Center + new Vector2(isSpazmatism ? 50 : -50, -100);
                SetEyeValue(npc, player, toPoint, toTarget);
                return true;
            }

            if (LaserWall) {
                toPoint = player.Center + new Vector2(isSpazmatism ? 450 : -450, -400);
                SetEyeValue(npc, player, toPoint, toTarget);
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
                    fireTime = death ? 5 : 8;
                    toTarget = npc.Center.To(projectile.Center);
                    float speedRot = death ? 0.02f : 0.03f;
                    int modelong = death ? 1060 : 1160;
                    toPoint = projectile.Center + (ai[4] * speedRot + MathHelper.TwoPi / 2 * (isSpazmatism ? 1 : 2)).ToRotationVector2() * 1060;
                }
                else {
                    toPoint = player.Center + (ai[4] * 0.04f + MathHelper.TwoPi / 2 * (isSpazmatism ? 1 : 2)).ToRotationVector2() * 760;
                }

                if (++ai[5] > fireTime && ai[4] > 30) {//这里需要停一下，不要立即开火
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(npc.GetSource_FromAI()
                            , npc.Center, toTarget.UnitVector() * 9, projType, projDamage, 0);
                    }
                    ai[5] = 0;
                    NetAISend(npc);
                }
                ai[4]++;
                SetEyeValue(npc, player, toPoint, toTarget);
                return true;
            }

            if (skeletronPrimeInSprint || ai[7] > 0) {
                switch (ai[1]) {
                    case 0:
                        toPoint = player.Center + new Vector2(isSpazmatism ? 600 : -600, -650);
                        if (death) {
                            toPoint = player.Center + new Vector2(isSpazmatism ? 500 : -500, -650);
                        }
                        if (ai[2] == 30 && !VaultUtils.isClient) {
                            float shootSpeed = death ? 8 : 6;
                            for (int i = 0; i < 6; i++) {
                                Vector2 ver = (MathHelper.TwoPi / 6f * i).ToRotationVector2() * shootSpeed;
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, ver, projType, projDamage, 0);
                            }
                        }
                        if (ai[2] > 60) {
                            ai[7] = 10;
                            ai[1] = 1;
                            ai[2] = 0;
                            NetAISend(npc);
                        }
                        ai[2]++;
                        break;
                    case 1:
                        toPoint = player.Center + new Vector2(isSpazmatism ? 700 : -700, ai[9]);
                        if (++ai[2] > 20) {
                            if (!VaultUtils.isClient) {
                                if (skeletronPrimeIsTwo) {
                                    for (int i = 0; i < 3; i++) {
                                        Vector2 ver = toTarget.RotatedBy((-1 + i) * 0.06f).UnitVector() * 5;
                                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, ver, projType, projDamage, 0);
                                    }
                                }
                                else {
                                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, toTarget.UnitVector() * 6, projType, projDamage, 0);
                                }
                            }
                            ai[3]++;
                            ai[2] = 0;
                            NetAISend(npc);
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
                                if (!VaultUtils.isClient) {
                                    ai[9] = Main.rand.Next(140, 280) * (Main.rand.NextBool() ? -1 : 1);
                                }
                                NetAISend(npc);
                            }
                        }

                        if (ai[3] > 6) {
                            ai[3] = 0;
                            ai[2] = 0;
                            ai[1] = 0;
                            ai[7] = 0;
                            NetAISend(npc);
                        }
                        else if (ai[7] < 2) {
                            ai[7] = 2;
                        }
                        break;
                }

                SetEyeValue(npc, player, toPoint, toTarget);
                return true;
            }

            if (ai[7] > 0) {
                ai[7]--;
            }

            npc.VanillaAI();
            SetEyeRealLife(npc);
            return true;
        }

        public bool ProtogenesisAI() {
            float lifeRog = npc.life / (float)npc.lifeMax;
            bool isSpazmatism = npc.type == NPCID.Spazmatism;
            bool bossRush = BossRushEvent.BossRushActive;
            bool death = CalamityWorld.death || bossRush;
            player = Main.player[npc.target];
            npc.dontTakeDamage = false;
            npc.damage = npc.defDamage;
            if (!player.Alives()) {
                npc.TargetClosest();
                player = Main.player[npc.target];
                if (!player.Alives()) {
                    ai[0] = 4;
                    NetAISend(npc);
                }
            }
            int projDamage = death ? 36 : 30;
            int projType = 0;
            switch (ai[0]) {
                case 0:
                    //这里应该做点什么，比如初始化
                    ai[0] = 1;
                    NetAISend(npc);
                    break;
                case 1:
                    Debut();
                    break;
                case 2:
                    if (!CalamityWorld.death && !BossRushEvent.BossRushActive) {
                        ai[0] = 3;
                        break;
                    }
                    //ai1作为子阶段计数
                    //ai2作为一个全局时间点滴
                    //ai3作为位置关键值
                    //ai4作为一个攻击计数器
                    //ai5作为一个镜像索引
                    //ai6作为阶段切换的计数，从0切换到1会加一次值，用于判定是否切换到冲刺阶段
                    //ai7在这里仅仅用作一个备用计数器
                    Vector2 toPoint = player.Center;
                    Vector2 offset = Vector2.Zero;
                    Vector2 toTarget = npc.Center.To(player.Center);

                    if (ai[5] == 0) {
                        ai[5] = 1;
                    }
                    if (ai[1] == 0) {
                        npc.damage = 0;
                        offset = isSpazmatism ? new Vector2(600, ai[3]) : new Vector2(-600, ai[3]);
                        offset.X *= ai[5];
                        projType = isSpazmatism ? ProjectileID.DD2BetsyFireball : ProjectileID.DeathLaser;
                        if (!VaultUtils.isClient && ai[2] > 30) {
                            if (isSpazmatism) {
                                for (int i = 0; i < 6; i++) {
                                    Vector2 origVer = toTarget.UnitVector() * 8;
                                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center
                                    , origVer + new Vector2(origVer.X * 0.2f * i, -0.1f * i), projType, projDamage, 0);
                                }
                            }
                            else {
                                for (int i = 0; i < ai[4]; i++) {
                                    Vector2 origVer = toTarget.UnitVector() * 8;
                                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center
                                    , origVer.RotatedBy((ai[4] / -2 + i) * 0.1f), projType, projDamage, 0);
                                }
                            }

                            ai[2] = 0;
                            if (isSpazmatism) {
                                if (ai[3] == 0) {
                                    ai[3] = -860;
                                }
                                ai[3] += 160;
                            }
                            else {
                                if (ai[3] == 0) {
                                    ai[3] = 420;
                                }
                            }
                            if (++ai[4] > 7) {
                                ai[4] = 0;
                                ai[3] = 0;
                                ai[2] = 0;
                                ai[1] = 1;
                                ai[5] *= -1;
                                ai[6]++;
                            }
                            NetAISend(npc);
                        }
                        SetEyeValue(npc, player, toPoint + offset, toTarget);
                    }
                    else if (ai[1] == 1) {
                        npc.damage = 0;
                        offset = isSpazmatism ? new Vector2(600 + ai[3] / 2, -500 + ai[3]) : new Vector2(-600 - ai[3] / 2, -500 + ai[3]);
                        offset.X *= ai[5];
                        projType = isSpazmatism ? ModContent.ProjectileType<Fireball>() : ProjectileID.EyeLaser;
                        float maxNum = 6f;
                        if (ai[2] > 30) {
                            for (int i = 0; i < maxNum; i++) {
                                Vector2 ver = (MathHelper.TwoPi / maxNum * i).ToRotationVector2() * (4 + ai[4] * 0.8f);
                                Projectile.NewProjectile(npc.GetSource_FromAI()
                                    , npc.Center, ver, projType, projDamage, 0);
                            }
                            ai[2] = 0;
                            ai[3] += 160;
                            if (++ai[4] > 6) {
                                ai[4] = 0;
                                ai[3] = 0;
                                ai[2] = 0;
                                ai[1] = 0;
                                if (ai[6] >= 2) {
                                    ai[1] = 2;//如果轮回了2次，那么就切换到2吧，开始冲刺
                                }
                            }
                            NetAISend(npc);
                        }
                        SetEyeValue(npc, player, toPoint + offset, toTarget);
                    }
                    else if (ai[1] == 2) {
                        if (ai[2] < 40) {
                            npc.damage = 0;
                            if (ai[2] == 2) {
                                SoundEngine.PlaySound(SoundID.Roar);
                            }
                            offset = isSpazmatism ? new Vector2(600, -500) * ai[5] : new Vector2(-600, -500) * ai[5];
                            SetEyeValue(npc, player, toPoint + offset, toTarget);
                        }
                        else if (ai[2] < 90) {
                            npc.damage = (int)(npc.defDamage * 1.25f);
                            if (ai[2] == 42) {
                                SoundEngine.PlaySound(SoundID.ForceRoar);
                                npc.velocity = toTarget.UnitVector() * (death ? 30 : 25);
                                npc.rotation = toTarget.ToRotation() - MathHelper.PiOver2;
                            }
                        }
                        else {
                            npc.damage = npc.defDamage;
                            npc.VanillaAI();
                            if (ai[2] > 160) {
                                ai[3] = 0;
                                ai[2] = 0;
                                ai[1] = 3;
                                ai[5] *= -1;
                                ai[6] = 0;
                                npc.damage = 0;
                                NetAISend(npc);
                            }
                        }
                    }
                    else if (ai[1] == 3) {
                        if (ai[2] < 40) {
                            npc.damage = 0;
                            if (ai[2] == 2) {
                                SoundEngine.PlaySound(SoundID.Roar);
                            }

                            if (ai[5] == 1) {
                                offset = isSpazmatism ? new Vector2(600, -500) : new Vector2(-600, 500);
                            }
                            else {
                                offset = isSpazmatism ? new Vector2(600, 500) : new Vector2(-600, -500);
                            }

                            SetEyeValue(npc, player, toPoint + offset, toTarget);
                        }
                        else if (ai[2] < 90) {
                            npc.damage = (int)(npc.defDamage * 1.25f);
                            if (ai[2] == 42) {
                                SoundEngine.PlaySound(SoundID.ForceRoar);
                                npc.velocity = toTarget.UnitVector() * (death ? 32 : 26);
                                npc.rotation = toTarget.ToRotation() - MathHelper.PiOver2;
                            }
                        }
                        else {
                            npc.damage = npc.defDamage;
                            npc.VanillaAI();
                            if (ai[2] > 380) {
                                ai[4]++;
                                ai[3] = 0;
                                ai[2] = 0;
                                ai[1] = 2;
                                ai[6] = 0;
                                if (ai[4] >= 2) {
                                    ai[1] = 0;
                                    ai[4] = 0;
                                }
                                npc.damage = 0;
                                NetAISend(npc);
                            }
                        }
                    }

                    ai[2]++;
                    break;
                case 3:
                    return false;
                case 4:
                    if (isSpazmatism && !VaultUtils.isServer && ai[2] == 2) {
                        CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), TextColor1);
                        CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), TextColor2);
                    }
                    npc.dontTakeDamage = true;
                    npc.damage = 0;
                    npc.velocity = new Vector2(0, -33);
                    if (++ai[2] > 200) {
                        npc.active = false;
                    }
                    break;
            }
            if (lifeRog < 0.6f && ai[0] == 2) {
                ai[6] = 0;
                ai[5] = 0;
                ai[4] = 0;
                ai[3] = 0;
                ai[2] = 0;
                ai[1] = 0;
                ai[0] = 3;
                NetAISend(npc);
            }
            return true;
        }

        private bool Debut() {
            ref float ai1 = ref ai[1];
            if (ai1 == 0) {
                npc.life = 1;
                npc.Center = player.Center;
                npc.Center += npc.type == NPCID.Spazmatism ? new Vector2(-1200, 1000) : new Vector2(1200, 1000);
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
                if (ai1 == 90 && !VaultUtils.isServer && !accompany) {
                    SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow, Main.LocalPlayer.Center);
                }
                if (ai1 > 90) {
                    int addNum = (int)(npc.lifeMax / 80f);
                    if (npc.life >= npc.lifeMax) {
                        npc.life = npc.lifeMax;
                    }
                    else {
                        Lighting.AddLight(npc.Center, (npc.type == NPCID.Spazmatism ? Color.OrangeRed : Color.BlueViolet).ToVector3());
                        npc.life += addNum;
                        CombatText.NewText(npc.Hitbox, CombatText.HealLife, addNum);
                    }
                }
            }

            if (ai1 > 180) {
                if (!VaultUtils.isServer && !accompany) {
                    SoundEngine.PlaySound(CWRSound.SpawnArmMgs, Main.LocalPlayer.Center);
                }
                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage;
                ai[0] = 2;
                ai1 = 0;
                NetAISend(npc);
                return false;
            }

            npc.Center = Vector2.Lerp(npc.Center, toPoint, 0.065f);

            ai1++;

            return true;
        }

        public override bool AI() {
            if (++frameCount > 5) {
                if (++frameIndex > 3) {
                    frameIndex = 0;
                }
                frameCount = 0;
            }
            npc.dontTakeDamage = false;
            npc.spriteDirection = Math.Sign((npc.rotation + MathHelper.PiOver2).ToRotationVector2().X);

            if (AccompanyAI()) {
                return false;
            }

            if (ProtogenesisAI()) {
                return false;
            }

            return true;
        }

        internal bool IsSecondPhase() => ai[0] == 2;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(CWRConstant.NPC + "BEYE/Spazmatism");
            Rectangle rectangle = CWRUtils.GetRec(mainValue, frameIndex, 4);
            SpriteEffects spriteEffects = npc.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = npc.rotation + MathHelper.PiOver2;
            if ((accompany && IsSecondPhase())) {
                mainValue = CWRUtils.GetT2DValue(CWRConstant.NPC + "BEYE/SpazmatismAlt");
                rectangle = CWRUtils.GetRec(mainValue, frameIndex, 4);
                Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, rectangle
                , Color.White, drawRot, rectangle.Size() / 2, npc.scale, spriteEffects, 0);
                return false;
            }
            Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, rectangle
            , Color.White, drawRot, rectangle.Size() / 2, npc.scale, spriteEffects, 0);
            return false;
        }
    }
}
