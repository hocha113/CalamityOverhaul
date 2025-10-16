﻿using CalamityMod;
using CalamityMod.Dusts;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.NPCs.AquaticScourge;
using CalamityMod.NPCs.CeaselessVoid;
using CalamityMod.NPCs.Crabulon;
using CalamityMod.NPCs.Cryogen;
using CalamityMod.NPCs.DesertScourge;
using CalamityMod.NPCs.HiveMind;
using CalamityMod.NPCs.Leviathan;
using CalamityMod.NPCs.NormalNPCs;
using CalamityMod.NPCs.OldDuke;
using CalamityMod.NPCs.Polterghast;
using CalamityMod.NPCs.ProfanedGuardians;
using CalamityMod.NPCs.Providence;
using CalamityMod.NPCs.SlimeGod;
using CalamityMod.NPCs.StormWeaver;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.NPCs.Yharon;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
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
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
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

        internal static void StrikeToFly(Vector2 velocity, NPC npc, Player owner, int level) {
            Vector2 flyVr = new Vector2(velocity.X, -16 + level * 0.3f);
            float modef = npc.Center.To(owner.Center).Length() / (300 + level * 30);
            if (modef > 1) {
                modef = 1f;
            }
            flyVr *= 1 - modef;

            void spanDust(int maxdustNum, int dustID) {
                for (int i = 0; i < maxdustNum; i++) {
                    Dust.NewDust(npc.position, npc.width, npc.height, dustID
                        , flyVr.X * Main.rand.NextFloat(1.1f), flyVr.Y + Main.rand.NextFloat(-1, 1));
                }
            }

            if (CWRLoad.targetNpcTypes16.Contains(npc.type)) {
                foreach (NPC over in Main.npc) {
                    if (over.type == CWRLoad.RavagerBody) {
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

            if (CWRLoad.targetNpcTypes4.Contains(npc.type)) {
                spanDust(33, DustID.Blood);
                return;
            }
            if (CWRLoad.targetNpcTypes5.Contains(npc.type)) {
                spanDust(33, DustID.Blood);
                return;
            }
            if (CWRLoad.targetNpcTypes17.Contains(npc.type)) {
                spanDust(33, DustID.Blood);
                return;
            }
            if (npc.type == ModContent.NPCType<Yharon>()) {
                spanDust(33, DustID.Blood);
                return;
            }
            if (npc.type == NPCID.TargetDummy) {
                spanDust(33, Main.rand.NextBool() ? DustID.Grass : DustID.JungleGrass);
                return;
            }
            if (npc.type == ModContent.NPCType<HiveMind>()) {
                ;
                return;
            }
            if (npc.type == NPCID.WallofFlesh || npc.type == NPCID.WallofFleshEye) {
                spanDust(133, DustID.Blood);
                owner.velocity += new Vector2(flyVr.X * -2, -1);
                return;
            }
            if (npc.type == ModContent.NPCType<BrimstoneHeart>()) {
                spanDust(33, DustID.Blood);
                return;
            }
            if (CWRLoad.targetNpcTypes2.Contains(npc.type)) {
                spanDust(11, DustID.Blood);
                return;
            }
            if (CWRLoad.targetNpcTypes8.Contains(npc.type)) {
                spanDust(33, DustID.BlueTorch);
                return;
            }
            if (npc.type == ModContent.NPCType<CeaselessVoid>()) {
                spanDust(33, DustID.BlueTorch);
                return;
            }
            if (CWRLoad.targetNpcTypes9.Contains(npc.type)) {
                spanDust(33, DustID.Sand);
                return;
            }
            if (CWRLoad.targetNpcTypes10.Contains(npc.type)) {
                spanDust(33, (int)CalamityDusts.PurpleCosmilite);
                return;
            }
            if (npc.type == NPCID.MoonLordCore) {
                spanDust(33, DustID.Blood);
                return;
            }
            if (CWRLoad.targetNpcTypes11.Contains(npc.type)) {
                spanDust(33, DustID.Blood);
                return;
            }
            if (CWRLoad.targetNpcTypes14.Contains(npc.type)) {
                spanDust(33, DustID.BlueTorch);
                return;
            }
            if (CWRLoad.targetNpcTypes15.Contains(npc.type)) {
                spanDust(33, DustID.Electric);
                return;
            }
            if (npc.type == ModContent.NPCType<ProfanedRocks>()) {
                spanDust(33, (int)CalamityDusts.ProfanedFire);
                return;
            }
            if (npc.type == ModContent.NPCType<Providence>()) {
                spanDust(33, (int)CalamityDusts.ProfanedFire);
                return;
            }
            if (npc.ModNPC?.FullName == "CatalystMod/Astrageldon") {
                spanDust(33, (int)CalamityDusts.Nightwither);
                return;
            }
            if (npc.ModNPC?.FullName == "StarsAbove/StarfarerBoss") {
                spanDust(33, (int)CalamityDusts.Nightwither);
                return;
            }
            if (CWRLoad.targetNpcTypes7.Contains(npc.type)) {
                spanDust(33, (int)CalamityDusts.Nightwither);
                return;
            }
            if (npc.type == NPCID.LunarTowerVortex) {
                spanDust(33, DustID.LunarRust);
                return;
            }
            if (npc.type == NPCID.LunarTowerSolar) {
                spanDust(33, DustID.FireworkFountain_Red);
                return;
            }
            if (npc.type == NPCID.LunarTowerNebula) {
                spanDust(33, DustID.NorthPole);
                return;
            }
            if (npc.type == NPCID.LunarTowerStardust) {
                spanDust(33, DustID.StarRoyale);
                return;
            }
            if (npc.type == ModContent.NPCType<SupremeCalamitas>() || npc.type == ModContent.NPCType<SupremeCataclysm>() || npc.type == ModContent.NPCType<SupremeCatastrophe>()) {
                return;
            }
            if (npc.type == ModContent.NPCType<SuperDummyNPC>()) {
                return;
            }

            //执行击飞效果的具体代码
            npc.CWR().OverBeatBackBool = true;
            npc.CWR().OldNPCPos = npc.position;
            npc.CWR().OverBeatBackVr = flyVr;
            npc.CWR().OverBeatBackAttenuationForce = npc.IsWormBody() ? 0.96f : 0.99f;
            CWRNpc.OverBeatBackSend(npc);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
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
                _ = !CWRLoad.NPCValue.ISTheofSteel(target)
                    ? SoundEngine.PlaySound(Murasama.OrganicHit with { Pitch = 0.15f }, Projectile.Center)
                    : SoundEngine.PlaySound(Murasama.InorganicHit with { Pitch = 0.15f }, Projectile.Center);

                //设置玩家的不可击退性并给予玩家短暂的无敌帧
                Owner.GivePlayerImmuneState(30);

                Vector2 ver = target.Center.To(Owner.Center).UnitVector();

                if (CWRServerConfig.Instance.ScreenVibration) {
                    PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center, Projectile.velocity.UnitVector(), 12f, 10, 20, -1, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                //如果充能已经满了10点，并且该技能已经解锁，那么进行处决技的释放
                if (Item.CWR().ai[0] == 10 && MurasamaOverride.UnlockSkill3(Item)) {
                    SoundEngine.PlaySound(CWRSound.EndSilkOrbSpanSound with { Volume = 0.7f }, Projectile.Center);
                    if (Projectile.IsOwnedByLocalPlayer()) {//同样的，释放衍生弹幕和进行自我充能清零的操作只能交由主人玩家执行
                        int maxSpanNum = 13 + Level;
                        for (int i = 0; i < maxSpanNum; i++) {
                            Vector2 spanPos = Projectile.Center + VaultUtils.RandVr(1380, 2200);
                            Vector2 vr = spanPos.To(Projectile.Center + VaultUtils.RandVr(180, 320 + Level * 12)).UnitVector() * 12;
                            Projectile.NewProjectile(Projectile.GetSource_FromAI(), spanPos, vr, ModContent.ProjectileType<MuraExecutionCutOnSpan>(), Projectile.damage / 2, 0, Owner.whoAmI);
                        }

                        //生成一个制造终结技核心效果的弹幕，这样的程序设计是为了减少耦合度
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center, Vector2.Zero,
                            ModContent.ProjectileType<EndSkillEffectStart>(), (int)(Projectile.damage * 0.7f), 0, Owner.whoAmI, 0, Owner.Center.X, Owner.Center.Y);

                        Item.CWR().ai[0] = 0;//清零充能
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
                    //给玩家一个合适的远离被击中目标的初始速度
                    Owner.velocity += ver * 10;

                    //进行武器充能的操作
                    Item.Initialize();
                    Item.CWR().ai[0]++;
                    if (Item.CWR().ai[0] > 10) {
                        Item.CWR().ai[0] = 10;
                    }
                }
            }

            if (!OnHitNPCs.Contains(target)) {
                StrikeToFly(Projectile.velocity, target, Owner, Level);
                OnHitNPCs.Add(target);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            int level = MurasamaOverride.GetLevel(Item);
            if (target.type == ModContent.NPCType<Crabulon>() || target.type == ModContent.NPCType<CrabShroom>()) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == ModContent.NPCType<DesertScourgeBody>()) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == ModContent.NPCType<DesertScourgeHead>() || target.type == ModContent.NPCType<DesertScourgeTail>()) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == ModContent.NPCType<DesertNuisanceHead>()) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == ModContent.NPCType<DesertNuisanceHead>() || target.type == ModContent.NPCType<DesertNuisanceTail>()) {
                modifiers.FinalDamage *= 2.5f;
            }
            if (target.type == ModContent.NPCType<DesertNuisanceHeadYoung>()) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == ModContent.NPCType<DesertNuisanceHeadYoung>() || target.type == ModContent.NPCType<DesertNuisanceTailYoung>()) {
                modifiers.FinalDamage *= 2.5f;
            }
            if (target.type == NPCID.KingSlime) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == NPCID.EyeofCthulhu) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == NPCID.BrainofCthulhu) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == NPCID.Creeper) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == ModContent.NPCType<BloodlettingServant>() || target.type == NPCID.ServantofCthulhu) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == ModContent.NPCType<KingSlimeJewelRuby>() || target.type == ModContent.NPCType<KingSlimeJewelEmerald>() || target.type == ModContent.NPCType<KingSlimeJewelSapphire>()) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == NPCID.EaterofWorldsBody || target.type == NPCID.EaterofWorldsHead || target.type == NPCID.EaterofWorldsTail) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == ModContent.NPCType<HiveBlob>() || target.type == ModContent.NPCType<HiveBlob2>() || target.type == ModContent.NPCType<DarkHeart>() || target.type == ModContent.NPCType<DankCreeper>()) {
                modifiers.FinalDamage *= 1.75f;
            }
            if (target.type == NPCID.QueenBee) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == NPCID.SkeletronHead) {
                modifiers.FinalDamage *= 1.25f;
                modifiers.SetMaxDamage((int)(target.lifeMax * (0.2f + level * 0.075f)));
            }
            if (target.type == NPCID.SkeletronHand) {
                modifiers.FinalDamage *= 1.1f;
                modifiers.SetMaxDamage((int)(target.lifeMax * (0.2f + level * 0.075f)));
            }
            if (target.type == ModContent.NPCType<CorruptSlimeSpawn>() || target.type == ModContent.NPCType<CorruptSlimeSpawn2>() || target.type == ModContent.NPCType<CrimsonSlimeSpawn>() || target.type == ModContent.NPCType<CrimsonSlimeSpawn2>()) {
                modifiers.FinalDamage *= 1.75f;
            }
            if (target.type == NPCID.WallofFlesh) {
                modifiers.FinalDamage *= 0.65f;
            }
            if (target.type == NPCID.WallofFleshEye) {
                modifiers.FinalDamage *= 0.45f;
            }
            if (target.type == ModContent.NPCType<Cryogen>()) {
                modifiers.FinalDamage *= 1.2f;
            }
            if (target.type == ModContent.NPCType<CryogenShield>()) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == ModContent.NPCType<AquaticScourgeBody>()) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (target.type == ModContent.NPCType<AquaticScourgeHead>() || target.type == ModContent.NPCType<AquaticScourgeTail>()) {
                modifiers.FinalDamage *= 1.15f;
            }
            if (target.type == NPCID.PrimeCannon || target.type == NPCID.PrimeSaw || target.type == NPCID.PrimeVice || target.type == NPCID.PrimeLaser) {
                modifiers.FinalDamage *= 0.75f;
            }
            if (target.type == NPCID.Retinazer || target.type == NPCID.Spazmatism) {
                modifiers.FinalDamage *= 1.75f;
            }
            if (target.type == ModContent.NPCType<Leviathan>() || target.type == ModContent.NPCType<Anahita>()) {
                modifiers.FinalDamage *= 1.75f;
            }
            if (target.type == NPCID.GolemHeadFree || target.type == NPCID.Golem) {
                modifiers.FinalDamage *= 1.75f;
            }
            if (target.type == NPCID.DukeFishron) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == CWRLoad.CosmicGuardianTail || target.type == CWRLoad.CosmicGuardianHead
                || target.type == CWRLoad.DevourerofGodsHead || target.type == CWRLoad.DevourerofGodsTail
                || target.type == CWRLoad.AstrumDeusHead || target.type == CWRLoad.AstrumDeusTail
                || target.type == NPCID.TheDestroyer || target.type == NPCID.TheDestroyerTail
                || target.type == CWRLoad.StormWeaverHead || target.type == CWRLoad.StormWeaverTail) {
                modifiers.FinalDamage *= 1.33f;
            }
            if (target.type == ModContent.NPCType<StormWeaverBody>()) {
                modifiers.FinalDamage *= 0.35f;
            }
            if (target.type == NPCID.MoonLordFreeEye || target.type == NPCID.MoonLordHand || target.type == NPCID.MoonLordHead || target.type == NPCID.MoonLordCore) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == NPCID.Retinazer || target.type == NPCID.Spazmatism) {
                modifiers.FinalDamage *= 1.1f;
                modifiers.SetMaxDamage((int)(target.lifeMax * (0.3f + level * 0.1f)));
            }
            if (target.type == ModContent.NPCType<SplitEbonianPaladin>() || target.type == ModContent.NPCType<SplitCrimulanPaladin>()) {
                modifiers.FinalDamage *= 0.45f;
                modifiers.SetMaxDamage((int)(target.lifeMax * (0.2f + level * 0.1f)));
            }
            if (target.type == CWRLoad.PlaguebringerGoliath) {
                modifiers.FinalDamage *= 2f;
            }
            if (target.type == CWRLoad.RavagerBody) {
                modifiers.FinalDamage *= 3f;
            }
            if (target.type == CWRLoad.RavagerClawLeft || target.type == CWRLoad.RavagerClawRight || target.type == CWRLoad.RavagerHead
                || target.type == CWRLoad.RavagerLegLeft || target.type == CWRLoad.RavagerLegRight) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (CWRLoad.targetNpcTypes7_1.Contains(target.type)) {
                modifiers.SetMaxDamage(target.lifeMax / 4);
            }
            if (target.type == CWRLoad.Apollo || target.type == CWRLoad.Artemis) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == ModContent.NPCType<ProfanedGuardianHealer>() || target.type == ModContent.NPCType<ProfanedGuardianDefender>()) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == ModContent.NPCType<ProfanedGuardianCommander>()) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == ModContent.NPCType<ProfanedRocks>()) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == ModContent.NPCType<Providence>()) {
                modifiers.FinalDamage *= 0.8f;
            }
            if (target.type == ModContent.NPCType<DarkEnergy>()) {
                modifiers.FinalDamage *= 0.6f;
            }
            if (target.type == ModContent.NPCType<PolterghastHook>()) {
                modifiers.FinalDamage *= 1.33f;
            }
            if (target.type == CWRLoad.StormWeaverTail || target.type == CWRLoad.StormWeaverHead) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == CWRLoad.Polterghast) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == ModContent.NPCType<OldDukeToothBall>() || target.type == ModContent.NPCType<SulphurousSharkron>()) {
                modifiers.FinalDamage *= 2f;
            }
            if (target.type == CWRLoad.DevourerofGodsHead) {
                modifiers.FinalDamage *= 1.33f;
            }
            if (target.type == CWRLoad.DevourerofGodsBody) {
                modifiers.FinalDamage *= 0.75f;
            }
            if (target.type == CWRLoad.AresBody || target.type == CWRLoad.AresGaussNuke || target.type == CWRLoad.AresLaserCannon || target.type == CWRLoad.AresPlasmaFlamethrower || target.type == CWRLoad.AresTeslaCannon
                || target.type == CWRLoad.Apollo || target.type == CWRLoad.Artemis) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == CWRLoad.ThanatosBody1 || target.type == CWRLoad.ThanatosBody2) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (target.type == CWRLoad.ThanatosHead) {
                modifiers.FinalDamage *= 5.71f;
            }
            if (target.type == ModContent.NPCType<SupremeCataclysm>() || target.type == ModContent.NPCType<SupremeCatastrophe>()) {
                modifiers.FinalDamage *= 2f;
            }
            if (target.type == ModContent.NPCType<SupremeCalamitas>()) {
                modifiers.FinalDamage *= 1.666666f;
            }
            if (target.type == ModContent.NPCType<BrimstoneHeart>()) {
                modifiers.FinalDamage *= 1.4f;
            }
            //饿鬼(被触手连接在肉山身上的状态)
            if (target.type == NPCID.TheHungry) {
                modifiers.FinalDamage *= 0.7f;
            }

            if (!target.IsWormBody()) {
                modifiers.DefenseEffectiveness *= 0.5f;
            }
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
