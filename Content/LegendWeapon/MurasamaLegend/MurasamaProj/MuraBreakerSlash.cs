using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    /// <summary>
    /// 升龙斩的爆发弹幕刀刃效果
    /// </summary>
    internal class MuraBreakerSlash : BaseHeldProj
    {
        public override string Texture => CWRConstant.Projectile_Melee + "MuraBreakerSlash";
        private List<NPC> OnHitNPCs { get; set; } = [];
        private const int maxFrame = 7;
        private const float baseSize = 0.8f;
        private int Level => MurasamaOverride.GetLevel(Item);

        public override void SetStaticDefaults() => CWRLoad.ProjValue.ImmuneFrozen[Type] = true;

        public override void SetDefaults() {
            Projectile.width = 432;
            Projectile.height = 432;
            Projectile.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override void Initialize() => Projectile.scale = baseSize * MuraSlashDefault.GetMuraSizeInMeleeSengs(Owner) + Level * 0.024f;

        public override void AI() {
            if (++Projectile.ai[1] >= 3) {
                if (++Projectile.ai[2] >= maxFrame) {
                    Projectile.Kill();
                }
                Projectile.ai[1] = 0;
            }

            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.velocity.UnitVector() * 80 * Projectile.scale;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Owner.direction = Math.Sign(Projectile.velocity.X);
            Projectile.ai[0]++;

            if (!Main.dedServ) {
                Lighting.AddLight(Projectile.Center, Color.IndianRed.ToVector3() * 2.2f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SpawnHitParticles(target);
            SpawnHitSparks(Projectile);

            if (Projectile.numHits == 0) {
                PlayHitSound(target);
                Owner.GivePlayerImmuneState(30);
                Vector2 ver = target.Center.To(Owner.Center).UnitVector();

                if (CWRServerConfig.Instance.ScreenVibration) {
                    PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center, Projectile.velocity.UnitVector(), 12f, 10, 20, -1, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                //如果充能已经满了10点，并且该技能已经解锁，那么进行处决技的释放
                if (Item.CWR().ai[0] == 10 && MurasamaOverride.UnlockSkill3(Item)) {
                    SoundEngine.PlaySound(CWRSound.EndSilkOrbSpanSound with { Volume = 0.7f }, Projectile.Center);
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        int maxSpanNum = 13 + Level;
                        for (int i = 0; i < maxSpanNum; i++) {
                            Vector2 spanPos = Projectile.Center + VaultUtils.RandVr(1380, 2200);
                            Vector2 vr = spanPos.To(Projectile.Center + VaultUtils.RandVr(180, 320 + Level * 12)).UnitVector() * 12;
                            Projectile.NewProjectile(Projectile.GetSource_FromAI(), spanPos, vr, ModContent.ProjectileType<MuraExecutionCutOnSpan>(), Projectile.damage / 2, 0, Owner.whoAmI);
                        }

                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center, Vector2.Zero,
                            ModContent.ProjectileType<EndSkillEffectStart>(), (int)(Projectile.damage * 0.7f), 0, Owner.whoAmI, 0, Owner.Center.X, Owner.Center.Y);

                        Item.CWR().ai[0] = 0;
                        CombatText.NewText(target.Hitbox, Color.Gold, "Finishing Blow!!!", true);
                    }

                    CombatText.NewText(target.Hitbox, Main.rand.NextBool(3) ? Color.Red : Color.IndianRed, $"{Item.CWR().ai[0]}!", true);

                    if (CWRServerConfig.Instance.ScreenVibration) {
                        PunchCameraModifier modifier2 = new PunchCameraModifier(Projectile.Center, new Vector2(0, Main.rand.NextFloat(-2, 2)), 10f, 30f, 20, 1000f, FullName);
                        Main.instance.CameraModifiers.Add(modifier2);
                    }

                    return;
                }

                if (Projectile.IsOwnedByLocalPlayer()) {
                    Owner.velocity += ver * 10;
                    Item.Initialize();
                    Item.CWR().ai[0]++;
                    if (Item.CWR().ai[0] > 10) {
                        Item.CWR().ai[0] = 10;
                    }
                }
            }

            if (!OnHitNPCs.Contains(target)) {
                OnHitNPCs.Add(target);
            }
        }

        private void PlayHitSound(NPC target) {
            SoundStyle sound = !CWRLoad.NPCValue.ISTheofSteel(target)
                ? "CalamityMod/Sounds/Item/MurasamaHitOrganic".GetSound()
                : "CalamityMod/Sounds/Item/MurasamaHitInorganic".GetSound();
            SoundEngine.PlaySound(sound with { Pitch = 0.15f, Volume = 0.6f }, Projectile.Center);
        }

        private static void SpawnHitParticles(NPC target) {
            if (Main.dedServ) {
                return;
            }

            for (int i = 0; i < 13; i++) {
                Vector2 particlePosition = target.Center + Main.rand.NextVector2Circular(target.width * 0.75f, target.height * 0.75f);
                float impactParticleScale = Main.rand.NextFloat(0.4f, 0.82f);
                PRT_Sparkle impactParticle = new(particlePosition, Vector2.Zero, Color.LightCoral, Color.Red, impactParticleScale, 8, 0, 2.5f);
                PRTLoader.AddParticle(impactParticle);
            }
        }

        private static void SpawnHitSparks(Projectile projectile) {
            if (Main.dedServ) {
                return;
            }

            for (int j = 0; j < 33; j++) {
                Vector2 sparkPosition = projectile.Center + Main.rand.NextVector2Circular(projectile.width * 0.5f, projectile.height * 0.5f);
                Vector2 sparkVelocity = projectile.velocity.RotatedBy(0.5f * Math.Sign(projectile.velocity.X)) * 3.2f;
                Color sparkColor = Main.rand.NextBool(3) ? Color.Red : Color.IndianRed;

                if (Main.rand.NextBool()) {
                    PRT_SparkAlpha spark = new(sparkPosition, sparkVelocity, false, 13, Main.rand.NextFloat(1.3f), sparkColor);
                    PRTLoader.AddParticle(spark);
                }
                else {
                    PRT_Line spark = new(sparkPosition, sparkVelocity, false, 13, Main.rand.NextFloat(1.3f) * 0.6f, sparkColor);
                    PRTLoader.AddParticle(spark);
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            int level = MurasamaOverride.GetLevel(Item);

            //boss存活时对非Boss单位造成2倍伤害
            MuraSlashDefault.ApplyBaseDamageModifiers(target, ref modifiers);

            //对飞眼怪仅造成15%伤害
            if (target.type == NPCID.Creeper) {
                modifiers.FinalDamage *= 0.15f;
            }

            //对骷髅王之手仅造成50%伤害并限制最大伤害
            if (target.type == NPCID.SkeletronHand) {
                modifiers.FinalDamage *= 0.5f;
                modifiers.SetMaxDamage((int)(target.lifeMax * (0.2f + level * 0.075f)));
            }

            //对史神护卫仅造成50%伤害
            if (target.type == CWRID.NPC_EbonianPaladin || target.type == CWRID.NPC_CrimulanPaladin) {
                modifiers.FinalDamage *= 0.5f;
            }

            //对史神小护卫仅造成25%伤害
            if (target.type == CWRID.NPC_SplitEbonianPaladin || target.type == CWRID.NPC_SplitCrimulanPaladin) {
                modifiers.FinalDamage *= 0.25f;
            }

            //对饿鬼造成3倍伤害
            if (target.type == NPCID.TheHungry || target.type == NPCID.TheHungryII) {
                modifiers.FinalDamage *= 1.5f;
            }

            //对灾眼兄弟仅造成50%伤害
            if (target.type == CWRID.NPC_Cataclysm || target.type == CWRID.NPC_Catastrophe) {
                modifiers.FinalDamage *= 0.5f;
            }

            //对石巨人之拳仅造成50%伤害
            if (target.type == NPCID.GolemFistLeft || target.type == NPCID.GolemFistRight) {
                modifiers.FinalDamage *= 0.5f;
            }

            //对毁灭魔像飞出的头仅造成50%伤害
            if (target.type == CWRID.NPC_RavagerHead2) {
                modifiers.FinalDamage *= 0.5f;
            }

            //对暗能量仅造成50%伤害
            if (target.type == CWRID.NPC_DarkEnergy) {
                modifiers.FinalDamage *= 0.5f;
            }

            //对幽花复制体仅造成75%伤害
            if (target.type == CWRID.NPC_PolterghastHook) {
                modifiers.FinalDamage *= 0.75f;
            }

            //对蠕虫只造成25%伤害
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.25f;
            }

            //对渊海灾虫仅造成20%伤害
            if (CWRLoad.targetNpcTypes11.Contains(target.type)) {
                modifiers.FinalDamage *= 0.8f;
            }

            //对渊海灾虫体节仅造成10%伤害
            if (target.type == CWRID.NPC_AquaticScourgeBodyAlt) {
                modifiers.FinalDamage *= 0.4f;
            }

            //对毁灭者造成50%倍伤害
            if (target.type == NPCID.TheDestroyerBody || target.type == NPCID.TheDestroyer || target.type == NPCID.TheDestroyerTail) {
                modifiers.FinalDamage *= 2f;
            }

            //对塔纳托斯体节造成2倍伤害
            if (target.type == CWRID.NPC_ThanatosBody1 || target.type == CWRID.NPC_ThanatosBody2 || target.type == CWRID.NPC_ThanatosTail) {
                modifiers.FinalDamage *= 2f;
            }

            //对神明吞噬者头尾、风编尾造成4倍伤害
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail || target.type == CWRID.NPC_StormWeaverTail) {
                modifiers.FinalDamage *= 4f;
            }

            //对塔纳托斯头造成11.4倍伤害
            if (target.type == CWRID.NPC_ThanatosHead) {
                modifiers.FinalDamage *= 11.4f;
            }

            //对肉山造成1.5倍伤害
            if (target.type == NPCID.WallofFlesh) {
                modifiers.FinalDamage *= 1.5f;
            }

            //对双子魔眼造成1倍伤害
            if (target.type == NPCID.Retinazer || target.type == NPCID.Spazmatism) {
                modifiers.FinalDamage *= 1f;
            }

            //对毁灭魔像身体部位造成50%伤害
            if (target.type == CWRID.NPC_RavagerClawLeft || target.type == CWRID.NPC_RavagerClawRight || target.type == CWRID.NPC_RavagerHead
                || target.type == CWRID.NPC_RavagerLegLeft || target.type == CWRID.NPC_RavagerLegRight) {
                modifiers.FinalDamage *= 0.5f;
            }

            //对星流双子造成1.33倍伤害
            if (target.type == CWRID.NPC_Apollo || target.type == CWRID.NPC_Artemis) {
                modifiers.FinalDamage *= 1.33f;
            }

            //对终灾造成1.33倍伤害
            if (target.type == CWRID.NPC_SupremeCalamitas) {
                modifiers.FinalDamage *= 1.33f;
            }

            //无视防御
            modifiers.DefenseEffectiveness *= 0f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            SpriteEffects spriteEffects = Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Rectangle rectangle = value.GetRectangle((int)Projectile.ai[2], maxFrame);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, rectangle
            , MurasamaOverride.NameIsVergil(Owner) ? Color.BlueViolet : Color.White, Projectile.rotation
            , rectangle.Size() / 2, Projectile.scale, spriteEffects, 0);
            return false;
        }
    }
}