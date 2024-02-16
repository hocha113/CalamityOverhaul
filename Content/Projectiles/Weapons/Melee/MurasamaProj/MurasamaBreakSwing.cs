using CalamityMod;
using CalamityMod.Dusts;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.NPCs.VanillaNPCOverrides.Bosses;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using Humanizer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class MurasamaBreakSwing : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "MurasamaBreakSwing";
        private Player Owner => Main.player[Projectile.owner];
        private Item murasama => Owner.ActiveItem();
        public override void SetDefaults() {
            Projectile.width = 432;
            Projectile.height = 432;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 20;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = InWorldBossPhase.Instance.Level() >= 5 ? 5 : -1;
        }

        int level => InWorldBossPhase.Instance.Level();

        public override void PostAI() => CWRUtils.ClockFrame(ref Projectile.frame, 3, 6);

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Color.IndianRed.ToVector3() * 2.2f);
            
            Projectile.scale += (0.05f + level * 0.002f);
            Projectile.position += Owner.velocity;
            Projectile.position.Y -= (0.02f + level * 0.005f);
            Owner.direction = Math.Sign(Owner.Center.To(Projectile.Center).X);
        }

        private void strikeToFly(NPC npc) {
            Vector2 flyVr = new Vector2(Projectile.velocity.X, -16 + (InWorldBossPhase.Instance.Level() * 0.3f));

            void spanDust(int maxdustNum, int dustID) {
                for (int i = 0; i < maxdustNum; i++) {
                    Dust.NewDust(npc.position, npc.width, npc.height, dustID
                        , flyVr.X * Main.rand.NextFloat(1.1f), flyVr.Y + Main.rand.NextFloat(-1, 1));
                }
            }

            if (CWRIDs.targetNpcTypes17.Contains(npc.type)) {
                foreach (NPC over in Main.npc) {
                    if (over.type == CWRIDs.RavagerBody) {
                        over.velocity += flyVr;
                        break;
                    }
                }
                return;
            }
            if (npc.type == NPCID.GolemHead || npc.type == NPCID.GolemFistLeft || npc.type == NPCID.GolemFistRight) {
                foreach (NPC over in Main.npc) {
                    if (over.type == NPCID.Golem) {
                        over.velocity += flyVr;
                        break;
                    }
                }
                return;
            }
            if (npc.type == NPCID.TargetDummy) {
                spanDust(33, Main.rand.NextBool() ? DustID.Grass : DustID.JungleGrass);
                return;
            }
            if (npc.type == NPCID.WallofFlesh || npc.type == NPCID.WallofFleshEye) {
                spanDust(133, DustID.Blood);
                Owner.velocity += new Vector2(flyVr.X * -2, -1);
                return;
            }
            if (npc.type == ModContent.NPCType<BrimstoneHeart>()) {
                spanDust(33, DustID.Blood);
                return;
            }
            if (CWRIDs.targetNpcTypes8.Contains(npc.type)) {
                spanDust(33, DustID.BlueTorch);
                return;
            }
            if (CWRIDs.targetNpcTypes9.Contains(npc.type)) {
                spanDust(33, DustID.Sand);
                return;
            }
            if (CWRIDs.targetNpcTypes10.Contains(npc.type)) {
                spanDust(33, (int)CalamityDusts.PurpleCosmilite);
                return;
            }
            if (CWRIDs.targetNpcTypes11.Contains(npc.type)) {
                spanDust(13, DustID.Sand);
                return;
            }
            if (CWRIDs.targetNpcTypes14.Contains(npc.type)) {
                spanDust(33, DustID.BlueTorch);
                return;
            }
            if (CWRIDs.targetNpcTypes15.Contains(npc.type)) {
                spanDust(33, DustID.Electric);
                return;
            }

            npc.CWR().MurasamabrBeatBackBool = true;
            npc.CWR().oldNPCPos = npc.position;
            npc.CWR().MurasamabrBeatBackVr = flyVr;
            npc.CWR().MurasamabrBeatBackAttenuationForce = 0.99f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!CWRUtils.isServer) {
                for (int i = 0; i < 13; i++) {
                    SparkleParticle impactParticle = new SparkleParticle(target.Center + Main.rand.NextVector2Circular(target.width * 0.75f, target.height * 0.75f)
                    , Vector2.Zero, Color.LightCoral, Color.Red, Main.rand.NextFloat(1.1f, 1.7f), 8, 0, 2.5f);
                    GeneralParticleHandler.SpawnParticle(impactParticle);
                }
                for (int j = 0; j < 33; j++) {
                    AltSparkParticle spark = new AltSparkParticle(
                        Projectile.Center + Main.rand.NextVector2Circular(Projectile.width * 0.5f, Projectile.height * 0.5f)
                        , Projectile.velocity.RotatedBy(0.5f * Math.Sign(Projectile.velocity.X)) * 3.2f
                        , false, 13, Main.rand.NextFloat(1.3f), Main.rand.NextBool(3) ? Color.Red : Color.IndianRed);
                    GeneralParticleHandler.SpawnParticle(spark);
                }
            }
            
            if (Projectile.numHits == 0) {
                SoundEngine.PlaySound(Murasama.OrganicHit with { Pitch = 0.15f }, Projectile.Center);
                strikeToFly(target);

                //设置玩家的不可击退性并给予玩家短暂的无敌帧
                Owner.GivePlayerImmuneState(30);
                //设置时期对应的升龙冷却
                Owner.CWR().RisingDragonCoolDownTime = Murasama.GetOnRDCD;

                Vector2 ver = target.Center.To(Owner.Center).UnitVector();

                if (ContentConfig.Instance.MurasamaScreenVibration) {
                    PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center, Projectile.velocity.UnitVector(), 12f, 10, 20, -1, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                if (Projectile.IsOwnedByLocalPlayer()) {
                    //给玩家一个合适的远离被击中目标的初始速度
                    Owner.velocity += ver * 10;

                    //进行武器充能的操作
                    murasama.initialize();
                    murasama.CWR().ai[0]++;
                    if (murasama.CWR().ai[0] > 10) {
                        murasama.CWR().ai[0] = 10;
                    }
                }

                //如果充能已经满了10点，并且该技能已经解锁，那么进行处决技的释放
                if (murasama.CWR().ai[0] == 10 && Murasama.UnlockSkill3) {
                    SoundEngine.PlaySound(CWRSound.EndSilkOrbSpanSound with {Volume = 0.7f }, Projectile.Center);
                    if (Projectile.IsOwnedByLocalPlayer()) {//同样的，释放衍生弹幕和进行自我充能清零的操作只能交由主人玩家执行
                        int maxSpanNum = 13 + level;
                        for (int i = 0; i < maxSpanNum; i++) {
                            Vector2 spanPos = Projectile.Center + CWRUtils.randVr(1380, 2200);
                            Vector2 vr = spanPos.To(Projectile.Center + CWRUtils.randVr(180, 320 + level * 12)).UnitVector() * 12;
                            Projectile.NewProjectile(Owner.parent(), spanPos, vr, ModContent.ProjectileType<MurasamaEndSkillOrbOnSpan>(), Projectile.damage, 0, Owner.whoAmI);
                        }
                        //生成一个制造终结技核心效果的弹幕，这样的程序设计是为了减少耦合度
                        Projectile.NewProjectile(Owner.parent(), Owner.Center, Vector2.Zero,
                            ModContent.ProjectileType<EndSkillEffectStart>(), Projectile.damage, 0, Owner.whoAmI, 0, Owner.Center.X, Owner.Center.Y);

                        murasama.CWR().ai[0] = 0;//清零充能
                        CombatText.NewText(target.Hitbox, Color.Gold, "Finishing Blow!!!", true);
                    }

                    if (ContentConfig.Instance.MurasamaScreenVibration) {
                        PunchCameraModifier modifier2 = new PunchCameraModifier(Projectile.Center, new Vector2(0, Main.rand.NextFloat(-2, 2)), 10f, 30f, 20, 1000f, FullName);
                        Main.instance.CameraModifiers.Add(modifier2);
                    }

                    return;
                }

                CombatText.NewText(target.Hitbox, Main.rand.NextBool(3) ? Color.Red : Color.IndianRed, $"{murasama.CWR().ai[0]}!", true);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
           if (target.type == NPCID.SkeletronHand) {
                modifiers.FinalDamage /= 2;
           }
           if (target.type == NPCID.WallofFleshEye || target.type == NPCID.WallofFlesh) {
                modifiers.FinalDamage *= 0.25f;
            }
           if (target.type == NPCID.PrimeCannon || target.type == NPCID.PrimeSaw || target.type == NPCID.PrimeVice || target.type == NPCID.PrimeLaser) {
                modifiers.FinalDamage *= 0.75f;
           }
           if (target.type == CWRIDs.AquaticScourgeBody) {
                modifiers.FinalDamage *= 0.3f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Projectile.frame, 6)
            , Murasama.NameIsVergil(Owner) ? Color.Blue : Color.White
            , Projectile.rotation + MathHelper.ToRadians(100 * Math.Sign(Projectile.velocity.X))
            , CWRUtils.GetOrig(value, 6), Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }
}
