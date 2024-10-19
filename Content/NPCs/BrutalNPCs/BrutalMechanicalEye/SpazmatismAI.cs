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
        public static bool Accompany;
        public static Color textColor1 => new(155, 215, 215);
        public static Color textColor2 => new(200, 54, 91);
        private static int frameIndex;
        private static int frameCount;
        public override void SetProperty() => SetAccompany(npc, ref ai, out Accompany);
        public static void SetAccompany(NPC npc, ref float[] ai, out bool accompany) {
            npc.realLife = -1;

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

        public static bool AccompanyAI(NPC eye, ref float[] ai, bool accompany) {
            if (!accompany) {
                return false;
            }

            NPC skeletronPrime = CWRUtils.FindNPCFromeType(NPCID.SkeletronPrime);
            float lifeRog = eye.life / (float)eye.lifeMax;
            bool bossRush = BossRushEvent.BossRushActive;
            bool death = CalamityWorld.death || bossRush;
            bool isSpazmatism = eye.type == NPCID.Spazmatism;
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

            if (skeletronPrimeIsDead || skeletronPrime?.ai[1] == 3 || lowBloodVolume || isSpawnFirstStageFromeExeunt) {
                eye.dontTakeDamage = true;
                eye.position += new Vector2(0, -36);
                if (ai[6] == 0) {
                    if (isSpazmatism && !CWRUtils.isServer) {
                        if (lowBloodVolume) {
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text3"), textColor1);
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text4"), textColor2);
                        }
                        else if (skeletronPrime?.ai[1] == 3) {
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), textColor1);
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), textColor2);
                        }
                        else if (isSpawnFirstStageFromeExeunt) {
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text6"), textColor1);
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text6"), textColor2);
                        }
                        else {
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text7"), textColor1);
                            CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text7"), textColor2);
                        }
                    }
                    for (int i = 0; i < 13; i++) {
                        Item.NewItem(eye.GetSource_FromAI(), eye.Hitbox, ItemID.Heart);
                    }
                }
                if (ai[6] > 120) {
                    eye.active = false;
                }
                ai[6]++;
                return true;
            }

            Vector2 toTarget = eye.Center.To(player.Center);
            Vector2 toPoint = skeletronPrime.Center;
            eye.damage = eye.defDamage;
            bool skeletronPrimeInSprint = skeletronPrime.ai[1] == 1;
            bool LaserWall = skeletronPrime.CWR().NPCOverride.ai[3] == 2;
            bool isDestroyer = BrutalSkeletronPrimeAI.setPosingStarmCount > 0;
            bool isIdle = skeletronPrime.CWR().NPCOverride.ai[10] > 0;

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
                    fireTime = death ? 5 : 8;
                    toTarget = eye.Center.To(projectile.Center);
                    float speedRot = death ? 0.02f : 0.03f;
                    int modelong = death ? 1060 : 1160;
                    toPoint = projectile.Center + (ai[4] * speedRot + MathHelper.TwoPi / 2 * (isSpazmatism ? 1 : 2)).ToRotationVector2() * 1060;
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
                        toPoint = player.Center + new Vector2(isSpazmatism ? 600 : -600, -650);
                        if (death) {
                            toPoint = player.Center + new Vector2(isSpazmatism ? 500 : -500, -650);
                        }
                        if (ai[2] == 30 && !CWRUtils.isClient) {
                            float shootSpeed = death ? 9 : 7;
                            for (int i = 0; i < 6; i++) {
                                Vector2 ver = (MathHelper.TwoPi / 6f * i).ToRotationVector2() * shootSpeed;
                                Projectile.NewProjectile(eye.GetSource_FromAI(), eye.Center, ver, projType, projDamage, 0);
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
                                if (!CWRUtils.isClient) {
                                    ai[9] = Main.rand.Next(140, 280) * (Main.rand.NextBool() ? -1 : 1);
                                }
                                NetAISend(eye);
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

            eye.VanillaAI();
            SetEyeRealLife(eye);
            return true;
        }

        public static bool ProtogenesisAI(NPC eye, ref float[] ai) {
            float lifeRog = eye.life / (float)eye.lifeMax;
            bool isSpazmatism = eye.type == NPCID.Spazmatism;
            bool bossRush = BossRushEvent.BossRushActive;
            bool death = CalamityWorld.death || bossRush;
            Player player = Main.player[eye.target];
            eye.dontTakeDamage = false;
            eye.damage = eye.defDamage;
            if (!player.Alives()) {
                eye.TargetClosest();
                player = Main.player[eye.target];
                if (!player.Alives()) {
                    ai[0] = 4;
                    NetAISend(eye);
                }
            }
            int projDamage = death ? 36 : 30;
            int projType = 0;
            switch (ai[0]) {
                case 0:
                    //这里应该做点什么，比如初始化
                    ai[0] = 1;
                    NetAISend(eye);
                    break;
                case 1:
                    Debut(eye, player, ref ai);
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
                    Vector2 toTarget = eye.Center.To(player.Center);

                    if (ai[5] == 0) {
                        ai[5] = 1;
                    }
                    if (ai[1] == 0) {
                        eye.damage = 0;
                        offset = isSpazmatism ? new Vector2(600, ai[3]) : new Vector2(-600, ai[3]);
                        offset.X *= ai[5];
                        projType = isSpazmatism ? ProjectileID.DD2BetsyFireball : ProjectileID.DeathLaser;
                        if (!CWRUtils.isClient && ai[2] > 30) {
                            if (isSpazmatism) {
                                for (int i = 0; i < 6; i++) {
                                    Vector2 origVer = toTarget.UnitVector() * 9;
                                    Projectile.NewProjectile(eye.GetSource_FromAI(), eye.Center
                                    , origVer + new Vector2(origVer.X * 0.2f * i, -0.1f * i), projType, projDamage, 0);
                                }
                            }
                            else {
                                for (int i = 0; i < ai[4]; i++) {
                                    Vector2 origVer = toTarget.UnitVector() * 9;
                                    Projectile.NewProjectile(eye.GetSource_FromAI(), eye.Center
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
                            NetAISend(eye);
                        }
                        SetEyeValue(eye, player, toPoint + offset, toTarget);
                    }
                    else if (ai[1] == 1) {
                        eye.damage = 0;
                        offset = isSpazmatism ? new Vector2(600 + ai[3] / 2, -500 + ai[3]) : new Vector2(-600 - ai[3] / 2, -500 + ai[3]);
                        offset.X *= ai[5];
                        projType = isSpazmatism ? ModContent.ProjectileType<Fireball>() : ProjectileID.EyeLaser;
                        float maxNum = 6f;
                        if (ai[2] > 30) {
                            for (int i = 0; i < maxNum; i++) {
                                Vector2 ver = (MathHelper.TwoPi / maxNum * i).ToRotationVector2() * (6 + ai[4]);
                                Projectile.NewProjectile(eye.GetSource_FromAI()
                                    , eye.Center, ver, projType, projDamage, 0);
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
                            NetAISend(eye);
                        }
                        SetEyeValue(eye, player, toPoint + offset, toTarget);
                    }
                    else if (ai[1] == 2) {
                        if (ai[2] < 40) {
                            eye.damage = 0;
                            if (ai[2] == 2) {
                                SoundEngine.PlaySound(SoundID.Roar);
                            }
                            offset = isSpazmatism ? new Vector2(600, -500) * ai[5] : new Vector2(-600, -500) * ai[5];
                            SetEyeValue(eye, player, toPoint + offset, toTarget);
                        }
                        else if (ai[2] < 90) {
                            eye.damage = (int)(eye.defDamage * 1.25f);
                            if (ai[2] == 42) {
                                SoundEngine.PlaySound(SoundID.ForceRoar);
                                eye.velocity = toTarget.UnitVector() * (death ? 30 : 25);
                                eye.rotation = toTarget.ToRotation() - MathHelper.PiOver2;
                            }
                        }
                        else {
                            eye.damage = eye.defDamage;
                            eye.VanillaAI();
                            if (ai[2] > 160) {
                                ai[3] = 0;
                                ai[2] = 0;
                                ai[1] = 3;
                                ai[5] *= -1;
                                ai[6] = 0;
                                eye.damage = 0;
                                NetAISend(eye);
                            }
                        }
                    }
                    else if (ai[1] == 3) {
                        if (ai[2] < 40) {
                            eye.damage = 0;
                            if (ai[2] == 2) {
                                SoundEngine.PlaySound(SoundID.Roar);
                            }

                            if (ai[5] == 1) {
                                offset = isSpazmatism ? new Vector2(600, -500) : new Vector2(-600, 500);
                            }
                            else {
                                offset = isSpazmatism ? new Vector2(600, 500) : new Vector2(-600, -500);
                            }

                            SetEyeValue(eye, player, toPoint + offset, toTarget);
                        }
                        else if (ai[2] < 90) {
                            eye.damage = (int)(eye.defDamage * 1.25f);
                            if (ai[2] == 42) {
                                SoundEngine.PlaySound(SoundID.ForceRoar);
                                eye.velocity = toTarget.UnitVector() * (death ? 32 : 26);
                                eye.rotation = toTarget.ToRotation() - MathHelper.PiOver2;
                            }
                        }
                        else {
                            eye.damage = eye.defDamage;
                            eye.VanillaAI();
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
                                eye.damage = 0;
                                NetAISend(eye);
                            }
                        }
                    }

                    ai[2]++;
                    break;
                case 3:
                    return false;
                case 4:
                    if (isSpazmatism && !CWRUtils.isServer && ai[2] == 2) {
                        CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), textColor1);
                        CWRUtils.Text(CWRLocText.GetTextValue("Spazmatism_Text5"), textColor2);
                    }
                    eye.dontTakeDamage = true;
                    eye.damage = 0;
                    eye.velocity = new Vector2(0, -33);
                    if (++ai[2] > 200) {
                        eye.active = false;
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
                NetAISend(eye);
            }
            return true;
        }

        private static bool Debut(NPC eye, Player player, ref float[] ai) {
            ref float ai1 = ref ai[1];
            if (ai1 == 0) {
                eye.life = 1;
                eye.Center = player.Center;
                eye.Center += eye.type == NPCID.Spazmatism ? new Vector2(-1200, 1000) : new Vector2(1200, 1000);
            }

            eye.damage = 0;
            eye.dontTakeDamage = true;

            Vector2 toTarget = eye.Center.To(player.Center);
            eye.rotation = toTarget.ToRotation() - MathHelper.PiOver2;
            eye.velocity = Vector2.Zero;
            eye.position += player.velocity;
            Vector2 toPoint = player.Center;

            if (ai1 < 60) {
                toPoint = player.Center + new Vector2(eye.type == NPCID.Spazmatism ? 500 : -500, 500);
            }
            else {
                toPoint = player.Center + new Vector2(eye.type == NPCID.Spazmatism ? -500 : 500, -500);
                if (ai1 == 90 && !CWRUtils.isServer && !Accompany) {
                    SoundEngine.PlaySound(CWRSound.MechanicalFullBloodFlow, Main.LocalPlayer.Center);
                }
                if (ai1 > 90) {
                    int addNum = (int)(eye.lifeMax / 80f);
                    if (eye.life >= eye.lifeMax) {
                        eye.life = eye.lifeMax;
                    }
                    else {
                        Lighting.AddLight(eye.Center, (eye.type == NPCID.Spazmatism ? Color.OrangeRed : Color.BlueViolet).ToVector3());
                        eye.life += addNum;
                        CombatText.NewText(eye.Hitbox, CombatText.HealLife, addNum);
                    }
                }
            }

            if (ai1 > 180) {
                if (!CWRUtils.isServer && !Accompany) {
                    SoundEngine.PlaySound(CWRSound.SpawnArmMgs, Main.LocalPlayer.Center);
                }
                eye.dontTakeDamage = false;
                eye.damage = eye.defDamage;
                ai[0] = 2;
                ai1 = 0;
                NetAISend(eye);
                return false;
            }

            eye.Center = Vector2.Lerp(eye.Center, toPoint, 0.065f);

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

        public static Vector2 CatmullRom(float value, params Vector2[] points) {
            if (points == null || points.Length < 4) {
                throw new ArgumentException("Catmull-Rom样条至少需要4个点");
            }

            value = Math.Clamp(value, 0f, 1f);

            int segmentCount = points.Length - 3;

            float t = value * segmentCount;
            int i = (int)t;
            t -= i;

            i = Math.Clamp(i, 0, segmentCount - 1);

            Vector2 p0 = points[i];
            Vector2 p1 = points[i + 1];
            Vector2 p2 = points[i + 2];
            Vector2 p3 = points[i + 3];

            return 0.5f * (
                (2 * p1) +
                (-p0 + p2) * t +
                (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
                (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t
            );
        }

        internal static bool IsCCK(NPC eye, float[] ai) {
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
