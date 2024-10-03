using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Dusts;
using CalamityMod.Projectiles.Boss;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Projectiles.Summon;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.Particles;

using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Vanilla;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using CosmicFire = CalamityOverhaul.Content.Projectiles.Weapons.Summon.CosmicFire;

namespace CalamityOverhaul.Content
{
    /// <summary>
    /// 用于分组弹幕的发射源，这决定了一些弹幕的特殊行为
    /// </summary>
    public enum SpanTypesEnum : byte
    {
        None,
        DeadWing,
        ClaretCannon,
        Phantom,
        Alluvion,
        Marksman,
        NettlevineGreat,
        TheStorm,
        BarrenBow,
        FetidEmesis,
        AngelicShotgun,
        NailGun,
        RocketLauncher,
        Voidragon,
        CrystalDimming,
        WoodenBow,
        IronBow,
        DemonBow,
        Marrow,
        IceBow,
        TendonBow,
        PulseBow,
        PlatinumBow,
        TungstenBow,
        CopperBow,
        PearlwoodBow,
        SilverBow,
        GoldBow,
        ShadowFlameBow,
        AstralRepeater,
        HalibutCannon,
    }

    public struct HitAttributeStruct
    {
        /// <summary>
        /// 设置为<see langword="true"/>必定暴击
        /// </summary>
        public bool CertainCrit;
        /// <summary>
        /// 设置为<see langword="true"/>必定不暴击，如果启用，会覆盖<see cref="CertainCrit"/>的设置
        /// </summary>
        public bool NeverCrit;
        /// <summary>
        /// 是否无视护甲
        /// </summary>
        public bool OnHitBlindArmor;
        /// <summary>
        /// 是否是一次超级攻击
        /// </summary>
        public bool SuperAttack;
    }

    public class CWRProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public byte SpanTypes;
        public HitAttributeStruct GetHitAttribute;
        public IEntitySource Source;
        public CWRItems cwrItem;
        public bool NotSubjectToSpecialEffects;
        public bool Viscosity;
        private Vector2 offsetHitPos;
        private NPC hitNPC;
        private float offsetHitRot;
        private float oldNPCRot;
        private float npcRotUpdateSengs;
        private int halibutAmmoTime;

        internal static void NetViscositySend(Projectile proj) {
            if (CWRUtils.isSinglePlayer) {
                return;
            }
            var netMessage = CWRMod.Instance.GetPacket();
            CWRProjectile cwrProj = proj.CWR();
            netMessage.Write((byte)CWRMessageType.ProjViscosityData);
            netMessage.Write(proj.whoAmI);
            netMessage.Write(cwrProj.hitNPC.whoAmI);
            netMessage.Write(cwrProj.offsetHitRot);
            netMessage.Write(cwrProj.oldNPCRot);
            netMessage.WriteVector2(cwrProj.offsetHitPos);
            netMessage.Send();
        }

        internal static void NetViscosityReceive(Mod mod, BinaryReader reader, int whoAmI) {
            int projIndex = reader.ReadInt32();
            int npcIndex = reader.ReadInt32();
            float offsetHitRot = reader.ReadSingle();
            float oldNPCRot = reader.ReadSingle();
            Vector2 offsetHitPos = reader.ReadVector2();

            if (projIndex >= 0 && projIndex < Main.maxProjectiles) {
                Projectile targetProj = Main.projectile[projIndex];
                if (targetProj.active) {
                    CWRProjectile cwrProj = targetProj.CWR();
                    if (npcIndex >= 0 && npcIndex < Main.maxNPCs) {
                        cwrProj.hitNPC = Main.npc[npcIndex];
                        cwrProj.offsetHitRot = offsetHitRot;
                        cwrProj.oldNPCRot = oldNPCRot;
                        cwrProj.offsetHitPos = offsetHitPos;
                    }
                }
            }

            if (Main.dedServ) {
                var netMessage = mod.GetPacket();
                netMessage.Write((byte)CWRMessageType.ProjViscosityData);
                netMessage.Write(projIndex);
                netMessage.Write(npcIndex);
                netMessage.Write(offsetHitRot);
                netMessage.Write(oldNPCRot);
                netMessage.WriteVector2(offsetHitPos);
                netMessage.Send(-1, whoAmI);
            }
        }

        public override void SetDefaults(Projectile projectile) {
            if (projectile.type == ProjectileID.Meowmere) {
                projectile.timeLeft = 160;
                projectile.penetrate = 2;
            }
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            Source = source;
            if (source?.Context == "CWRGunShoot") {
                Item heldItem = Main.player[projectile.owner].ActiveItem();
                if (heldItem.type != ItemID.None) {
                    cwrItem = heldItem.CWR();
                }
            }
            if (!projectile.hide && projectile.friendly) {
                CWRPlayer modPlayer = Main.player[projectile.owner].CWR();
                if (projectile.DamageType == DamageClass.Ranged) {
                    if (modPlayer.LoadMuzzleBrake && modPlayer.LoadMuzzleBrakeLevel == 4) {
                        projectile.extraUpdates += 2;
                        if (Main.rand.NextBool(5)) {
                            projectile.scale *= 2.25f;
                        }
                    }
                }
            }
        }

        public override bool PreAI(Projectile projectile) {
            if (Viscosity && projectile.numHits > 0) {
                if (!hitNPC.Alives()) {
                    projectile.Kill();
                    return false;
                }
                npcRotUpdateSengs = oldNPCRot - hitNPC.rotation;
                oldNPCRot = hitNPC.rotation;
                offsetHitRot -= npcRotUpdateSengs;
                projectile.rotation = offsetHitRot;
                offsetHitPos = offsetHitPos.RotatedBy(npcRotUpdateSengs);
                projectile.Center = hitNPC.Center + offsetHitPos;
                return false;
            }
            return base.PreAI(projectile);
        }

        public void SpanTypesPostAI(Projectile projectile) {
            if (projectile.type == ProjectileID.None) {
                return;
            }

            SpanTypesEnum typesEnum = (SpanTypesEnum)SpanTypes;

            switch (typesEnum) {
                case SpanTypesEnum.NettlevineGreat:
                    int dust = Dust.NewDust(projectile.position + projectile.velocity, projectile.width, projectile.height
                        , DustID.GreenTorch, projectile.velocity.X * 0.5f, projectile.velocity.Y * 0.5f);
                    Main.dust[dust].noGravity = true;
                    break;
                case SpanTypesEnum.TheStorm:
                    if (Main.rand.NextBool()) {
                        int sparkier = Dust.NewDust(projectile.position, projectile.width, projectile.height, DustID.UnusedWhiteBluePurple, 0f, 0f, 100, default, 1f);
                        Main.dust[sparkier].scale += 0.3f + (Main.rand.Next(50) * 0.01f);
                        Main.dust[sparkier].noGravity = true;
                        Main.dust[sparkier].velocity *= 0.1f;
                    }
                    break;
                case SpanTypesEnum.FetidEmesis:
                    int dust2 = Dust.NewDust(projectile.position + projectile.velocity, projectile.width, projectile.height
                        , DustID.GemEmerald, projectile.velocity.X * 0.5f, projectile.velocity.Y * 0.5f);
                    Main.dust[dust2].noGravity = true;
                    break;
                case SpanTypesEnum.BarrenBow:
                    Dust.NewDust(projectile.position + projectile.velocity, projectile.width, projectile.height
                        , DustID.Sand, projectile.velocity.X * 0.5f, projectile.velocity.Y * 0.5f);
                    break;
                case SpanTypesEnum.HalibutCannon:
                    if (++halibutAmmoTime > 80) {
                        projectile.Kill();
                    }
                    break;
            }
        }

        public override void PostAI(Projectile projectile) {
            if (projectile.type == ProjectileID.Meowmere) {
                projectile.velocity.X *= 0.98f;
                projectile.velocity.Y += 0.01f;
            }

            if (Source?.Context == "CWRGunShoot" && cwrItem != null) {
                if (cwrItem.SpecialAmmoState == SpecialAmmoStateEnum.armourPiercer) {
                    Color color = Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat(0.3f, 0.64f));
                    BasePRT spark = new PRT_Spark(projectile.Center, projectile.velocity * 0.3f, false, 9, 2.3f, color * 0.1f);
                    PRTLoader.AddParticle(spark);
                }
                else if (cwrItem.SpecialAmmoState == SpecialAmmoStateEnum.highExplosive) {
                    if (Main.rand.NextBool(3)) {
                        int dust = Dust.NewDust(projectile.Center, 1, 1, DustID.FireworkFountain_Red, projectile.velocity.X, projectile.velocity.Y);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].scale *= 0.6f;
                    }
                }
            }

            SpanTypesPostAI(projectile);
        }

        public override void OnKill(Projectile projectile, int timeLeft) {
            RMeowmere.SpanDust(projectile, 0.2f);
            if (projectile.IsOwnedByLocalPlayer()) {
                if (!projectile.friendly) {
                    return;
                }

                if (SpanTypes == (byte)SpanTypesEnum.Marksman) {
                    int proj = Projectile.NewProjectile(projectile.GetSource_FromAI(), projectile.Center, projectile.velocity
                        , ProjectileID.LostSoulFriendly, projectile.damage / 2, projectile.knockBack / 2, projectile.owner, 0);
                    Main.projectile[proj].DamageType = DamageClass.Ranged;
                    Main.projectile[proj].timeLeft = 60;
                    NetMessage.SendData(MessageID.SyncProjectile, -1, projectile.owner, null, proj);
                }
                if (SpanTypes == (byte)SpanTypesEnum.BarrenBow) {
                    _ = Projectile.NewProjectile(projectile.GetSource_FromAI(), projectile.Center, CWRUtils.randVr(6, 9)
                        , ModContent.ProjectileType<BarrenOrb>(), projectile.damage / 2, 0, projectile.owner, 0);
                }
                if (SpanTypes == (byte)SpanTypesEnum.AngelicShotgun) {
                    if (Main.rand.NextBool()) {
                        return;
                    }
                    int proj = Projectile.NewProjectile(projectile.GetSource_FromAI()
                        , projectile.Center + new Vector2(Main.rand.Next(-32, 32), 0), new Vector2(0, -7)
                        , ModContent.ProjectileType<AngelicBeam>(), projectile.damage / 2, 0, projectile.owner, 0);
                    Main.projectile[proj].timeLeft = 90;
                }
            }
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info) {
            RMeowmere.SpanDust(projectile);
            if (SpanTypes == (byte)SpanTypesEnum.NettlevineGreat) {
                target.AddBuff(70, 60);
            }
            if (SpanTypes == (byte)SpanTypesEnum.TheStorm) {
                target.AddBuff(BuffID.Electrified, 120);
            }
        }

        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) {
            if (GetHitAttribute.CertainCrit) {
                modifiers.SetCrit();
            }
            if (GetHitAttribute.NeverCrit) {
                modifiers.DisableCrit();
            }
            if (GetHitAttribute.OnHitBlindArmor) {
                if (modifiers.SuperArmor || target.defense > 999 || target.Calamity().DR >= 0.95f || target.Calamity().unbreakableDR) {
                    return;
                }
                modifiers.DefenseEffectiveness *= 0f;
            }
            if (Source?.Context == "CWRGunShoot" && cwrItem != null) {
                if (cwrItem.SpecialAmmoState == SpecialAmmoStateEnum.armourPiercer) {
                    modifiers.DefenseEffectiveness *= 0.75f;
                }
            }
            if (projectile.CWR().SpanTypes == (byte)SpanTypesEnum.HalibutCannon) {
                HalibutCannonHeldProj.ModifyHalibutAmmoHitNPC(projectile, target, ref modifiers);
            }

            if (projectile.type == CWRLoad.Projectile_ArcZap && target.IsWormBody()) {
                modifiers.FinalDamage /= 2;
            }

            InProjTypeSetHitNPC(projectile, target, ref modifiers);
        }

        private void InProjTypeSetHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) {
            if (projectile.type == ProjectileID.FinalFractal) {
                if (CWRLoad.WormBodys.Contains(target.type)) {
                    modifiers.FinalDamage *= 0.75f;
                }
                if (target.type == CWRLoad.AresLaserCannon || target.type == CWRLoad.AresPlasmaFlamethrower
                    || target.type == CWRLoad.AresTeslaCannon || target.type == CWRLoad.AresGaussNuke) {
                    modifiers.FinalDamage *= 0.7f;
                }
                if (target.type == CWRLoad.DevourerofGodsBody || target.type == CWRLoad.DevourerofGodsHead) {
                    modifiers.FinalDamage *= 0.7f;
                }
                if (target.type == CWRLoad.Polterghast) {
                    modifiers.FinalDamage *= 0.8f;
                }
            }
            else if (projectile.type == ModContent.ProjectileType<CosmicIceBurst>()) {
                if (target.type == CWRLoad.Yharon) {
                    modifiers.FinalDamage *= 0.8f;
                }
            }
        }

        private void SuperAttackOnHitNPC(Projectile projectile, NPC target) {
            if (GetHitAttribute.SuperAttack) {
                if (projectile.type == 961) {
                    if (!target.boss && !CWRLoad.WormBodys.Contains(target.type) && !target.CWR().IceParclose) {
                        Projectile.NewProjectile(CWRUtils.parent(projectile), target.Center, Vector2.Zero
                            , ModContent.ProjectileType<IceParclose>(), 0, 0, projectile.owner, target.whoAmI, target.type, target.rotation);
                    }
                }
                else if (projectile.type == ProjectileID.SnowBallFriendly) {
                    if (projectile.numHits == 0) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 spanPos = projectile.Center + CWRUtils.randVr(1160, 1290);
                            Vector2 vr = spanPos.To(target.Center).UnitVector() * 15;
                            Projectile proj = Projectile.NewProjectileDirect(projectile.GetSource_FromThis(), spanPos, vr
                            , ProjectileID.FrostBeam, projectile.damage / 2, 0, projectile.owner, 1);
                            proj.penetrate = -1;
                            proj.extraUpdates = 6;
                            proj.hostile = false;
                            proj.friendly = true;
                            proj.timeLeft /= 2;
                            proj.usesLocalNPCImmunity = true;
                            proj.localNPCHitCooldown = -1;
                            proj.ArmorPenetration = 15;
                            proj.CWR().GetHitAttribute.NeverCrit = true;
                        }
                    }
                }
            }
        }

        private void RustyMedallionEffect(Player player, Projectile projectile) {
            if (player.CWR().RustyMedallion_Value && Source != null) {
                if (player.ownedProjectileCounts[ModContent.ProjectileType<AcidEtchedTearDrop>()] < 8) {
                    if (Source.Context == "CWRGunShoot" || Source.Context == "CWRBow") {
                        Vector2 startingPosition = Main.MouseWorld - Vector2.UnitY.RotatedByRandom(0.4f) * 1250f;
                        Vector2 directionToMouse = (Main.MouseWorld - startingPosition).SafeNormalize(Vector2.UnitY).RotatedByRandom(0.1f);
                        int newdamage = projectile.damage;
                        newdamage /= 6;
                        int drop = Projectile.NewProjectile(projectile.GetSource_FromAI(), startingPosition, directionToMouse * 15f
                            , ModContent.ProjectileType<AcidEtchedTearDrop>(), newdamage, 0f, player.whoAmI);
                        if (drop.WithinBounds(Main.maxProjectiles)) {
                            Main.projectile[drop].penetrate = 2;
                            Main.projectile[drop].DamageType = DamageClass.Generic;
                        }
                    }
                }
            }
        }

        private void SpanTypesOnHitNPC(Player player, Projectile projectile, NPC target, NPC.HitInfo hit) {
            switch ((SpanTypesEnum)SpanTypes) {
                case SpanTypesEnum.DeadWing: {
                    int types = ModContent.ProjectileType<DeadWave>();

                    if (player.Center.To(target.Center).LengthSquared() < 600 * 600
                        && projectile.type != types
                        && projectile.numHits == 0) {
                        Vector2 vr = player.Center.To(Main.MouseWorld)
                            .RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-15, 15))).UnitVector() * Main.rand.Next(7, 9);
                        Vector2 pos = player.Center + (vr * 10);
                        Projectile.NewProjectileDirect(CWRUtils.parent(player), pos, vr, ModContent.ProjectileType<DeadWave>(),
                            projectile.damage, projectile.knockBack, projectile.owner).rotation = vr.ToRotation();
                    }

                    break;
                }

                case SpanTypesEnum.ClaretCannon: {
                    Projectile projectile1 = Projectile.NewProjectileDirect(CWRUtils.parent(player), target.position, Vector2.Zero,
                        ModContent.ProjectileType<BloodVerdict>(), projectile.damage, projectile.knockBack, projectile.owner);
                    if (projectile1.ModProjectile is BloodVerdict bloodVerdict) {
                        bloodVerdict.offsetVr = new Vector2(Main.rand.Next(target.width), Main.rand.Next(target.height));
                        bloodVerdict.Projectile.ai[1] = target.whoAmI;
                        Vector2[] vrs = new Vector2[3];
                        for (int i = 0; i < 3; i++) {
                            vrs[i] = Main.rand.NextVector2Unit() * Main.rand.Next(16, 19);
                        }

                        bloodVerdict.effusionDirection = vrs;
                    }
                    break;
                }

                case SpanTypesEnum.Phantom: {
                    if (projectile.numHits == 0) {
                        int proj = Projectile.NewProjectile(projectile.GetSource_FromAI(), player.Center + (projectile.velocity.GetNormalVector() * Main.rand.Next(-130, 130))
                            , projectile.Center.To(target.Center).UnitVector() * 13, ModContent.ProjectileType<PhantasmArrow>()
                            , (int)(projectile.damage * 0.8f), projectile.knockBack / 2, player.whoAmI, 0, target.whoAmI);
                        Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() - MathHelper.PiOver2;
                    }
                    break;
                }

                case SpanTypesEnum.Alluvion: {
                    if (projectile.numHits == 0) {
                        Projectile.NewProjectile(projectile.GetSource_FromAI(), player.Center
                            , projectile.velocity.UnitVector() * 16, ModContent.ProjectileType<DeepSeaSharks>()
                            , projectile.damage, projectile.knockBack / 2, player.whoAmI, 0, target.whoAmI);
                    }
                    break;
                }
                    
                   
                case SpanTypesEnum.RocketLauncher: {
                    if (projectile.numHits == 0 && projectile.ai[2] > 0) {
                        if (projectile.ai[2] == 3) {
                            projectile.damage = (int)(projectile.damage * 0.8f);
                        }
                        projectile.ai[2] -= 1;
                        Vector2 velocity0 = target != null ? (target.Center - player.Center).SafeNormalize(Vector2.Zero) * 30f : projectile.velocity;
                        if (player.PressKey()) {
                            int proj = Projectile.NewProjectile(projectile.GetSource_FromAI(), player.Center + ((Main.MouseWorld - player.Center).SafeNormalize(Vector2.Zero) * 40f)
                                , velocity0, projectile.type, projectile.damage / 2, projectile.knockBack, player.whoAmI, 0, target.whoAmI, projectile.ai[2]);
                            Main.projectile[proj].usesLocalNPCImmunity = true;
                            Main.projectile[proj].localNPCHitCooldown = 5;
                            Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.RocketLauncher;
                            Main.projectile[proj].scale *= 0.5f;
                        }
                    }
                    break;
                }
                    
                case SpanTypesEnum.NailGun: {
                    if (projectile.numHits == 0) {
                        int newdamage = projectile.damage;
                        if (hit.Crit == true) {
                            newdamage /= 2;
                        }
                        for (int i = 0; i < 3; i++) {
                            int proj = Projectile.NewProjectile(Source, projectile.Center + new Vector2(0, -target.height)
                                , new Vector2(0, -5).RotatedBy(Main.rand.NextFloat(-0.48f, 0.48f)) * Main.rand.NextFloat(0.7f, 1.5f)
                                , projectile.type, newdamage, projectile.knockBack, player.whoAmI, 0, 0, -1);
                            Main.projectile[proj].extraUpdates += 1;
                        }
                    }
                    break;
                }
                    
                case SpanTypesEnum.CrystalDimming: {
                    bool isSuper = Main.rand.NextBool(projectile.type == ModContent.ProjectileType<Crystal>() ? 4 : 10);
                    for (int i = 0; i < 3; i++) {
                        Vector2 velocity = new(Main.rand.NextFloat(-3, 3), -3);
                        Projectile proj = Projectile.NewProjectileDirect(player.GetShootState().Source
                        , projectile.Bottom + new Vector2(Main.rand.Next(-16, 16), Main.rand.Next(-64, 0)), velocity
                        , 961, projectile.damage / 5, 0f, Main.myPlayer, 0f, Main.rand.NextFloat(0.8f, 1.1f));
                        proj.rotation = velocity.ToRotation();
                        proj.hostile = false;
                        proj.friendly = true;
                        proj.penetrate = -1;
                        proj.usesLocalNPCImmunity = true;
                        proj.localNPCHitCooldown = 20;
                        proj.light = 0.75f;
                        if (isSuper) {
                            proj.CWR().GetHitAttribute.SuperAttack = true;
                        }
                    }
                    break;
                }

                case SpanTypesEnum.Voidragon: {
                    Projectile.NewProjectile(projectile.parent(), target.Center
                    , CWRUtils.randVr(6, 13), ModContent.ProjectileType<RVoidTentacle>()
                    , projectile.damage, projectile.knockBack / 2, player.whoAmI
                    , Main.rand.Next(-160, 160) * 0.001f, Main.rand.Next(-160, 160) * 0.001f);
                    break;
                }
                    
                case SpanTypesEnum.IronBow: {
                    player.AddBuff(BuffID.Ironskin, 60);
                    break;
                }
                    
                case SpanTypesEnum.WoodenBow: {
                    if (projectile.numHits == 0) {
                        Projectile proj = Projectile.NewProjectileDirect(projectile.GetSource_FromThis(), projectile.Center, new Vector2(0, -13).RotatedByRandom(0.2f)
                        , ModContent.ProjectileType<SquirrelSquireAcorn>(), 3, projectile.knockBack, projectile.owner);
                        proj.DamageType = DamageClass.Ranged;
                    }
                    break;
                }
                    
                case SpanTypesEnum.DemonBow: {
                    Projectile proj2 = Projectile.NewProjectileDirect(projectile.GetSource_FromThis(), projectile.Center, CWRUtils.randVr(0.1f)
                        , 974, projectile.damage / 3, projectile.knockBack, projectile.owner, 1);
                    proj2.DamageType = DamageClass.Ranged;
                    break;
                }
                    
                case SpanTypesEnum.Marrow: {
                    Projectile proj3 = Projectile.NewProjectileDirect(projectile.GetSource_FromThis(), projectile.Center, new Vector2(0, -13).RotatedByRandom(0.2f)
                        , ProjectileID.Bone, projectile.damage / 3, projectile.knockBack, projectile.owner, 1);
                    proj3.DamageType = DamageClass.Ranged;
                    proj3.usesLocalNPCImmunity = true;
                    proj3.localNPCHitCooldown = 20;
                    proj3.penetrate = 3;
                    if (projectile.penetrate > 1) {
                        projectile.velocity = projectile.velocity.RotatedByRandom(1f);
                    }
                    break;
                }
                    
                case SpanTypesEnum.IceBow: {
                    Projectile proj4 = Projectile.NewProjectileDirect(projectile.GetSource_FromThis(), projectile.Center, new Vector2(0, -5).RotatedByRandom(0.2f)
                        , ModContent.ProjectileType<IceExplosionFriend>(), projectile.damage / 3, projectile.knockBack, projectile.owner, 1);
                    proj4.scale += Main.rand.NextFloat(-0.3f, 0);
                    projectile.Kill();
                    break;
                }

                case SpanTypesEnum.PulseBow: {
                    if (!NotSubjectToSpecialEffects) {
                        for (int i = 0; i < 2; i++) {
                            Projectile proj5 = Projectile.NewProjectileDirect(projectile.GetSource_FromThis(), projectile.Center - projectile.velocity, projectile.velocity.RotatedByRandom(0.25f)
                            , ProjectileID.PulseBolt, projectile.damage / 2, projectile.knockBack, projectile.owner, 1);
                            proj5.usesLocalNPCImmunity = true;
                            proj5.localNPCHitCooldown = 15;
                            proj5.CWR().NotSubjectToSpecialEffects = true;
                        }
                    }
                    projectile.Kill();
                    break;
                }

                case SpanTypesEnum.CopperBow: {
                    if (projectile.numHits == 0) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 spanPos = projectile.Center + CWRUtils.randVr(560, 690);
                            Vector2 vr = spanPos.To(target.Center).UnitVector() * 13;
                            Projectile proj6 = Projectile.NewProjectileDirect(projectile.GetSource_FromThis(), spanPos, vr
                            , ProjectileID.CopperCoin, 2, projectile.knockBack, projectile.owner, 1);
                            proj6.penetrate = 1;
                            proj6.CWR().GetHitAttribute.NeverCrit = true;
                        }
                    }
                    break;
                }

                case SpanTypesEnum.SilverBow: {
                    if (projectile.numHits == 0) {
                        for (int i = 0; i < 4; i++) {
                            Vector2 spanPos = projectile.Center + CWRUtils.randVr(660, 790);
                            Vector2 vr = spanPos.To(target.Center).UnitVector() * 15;
                            Projectile proj6 = Projectile.NewProjectileDirect(projectile.GetSource_FromThis(), spanPos, vr
                            , ProjectileID.SilverCoin, 2, projectile.knockBack, projectile.owner, 1);
                            proj6.penetrate = 1;
                            proj6.CWR().GetHitAttribute.NeverCrit = true;
                        }
                    }
                    break;
                }

                case SpanTypesEnum.GoldBow: {
                    if (projectile.numHits == 0) {
                        for (int i = 0; i < 5; i++) {
                            Vector2 spanPos = projectile.Center + CWRUtils.randVr(760, 790);
                            Vector2 vr = spanPos.To(target.Center).UnitVector() * 15;
                            Projectile proj6 = Projectile.NewProjectileDirect(projectile.GetSource_FromThis(), spanPos, vr
                            , ProjectileID.GoldCoin, 2, projectile.knockBack, projectile.owner, 1);
                            proj6.penetrate = 1;
                            proj6.extraUpdates = 1;
                            proj6.CWR().GetHitAttribute.NeverCrit = true;
                        }
                    }
                    break;
                }

                case SpanTypesEnum.AstralRepeater: {
                    if (projectile.numHits == 0) {
                        for (int i = 0; i < 2; i++) {
                            Vector2 spanPos = projectile.Center + CWRUtils.randVr(860, 990);
                            Vector2 vr = spanPos.To(target.Center).UnitVector() * 20;
                            Projectile proj7 = Projectile.NewProjectileDirect(projectile.GetSource_FromThis(), spanPos, vr
                            , ModContent.ProjectileType<AstralStar>(), projectile.damage / 4, projectile.knockBack, projectile.owner, 1);
                            proj7.DamageType = DamageClass.Ranged;
                            proj7.extraUpdates = 1;
                            proj7.CWR().GetHitAttribute.NeverCrit = true;
                        }
                        bool blue = Main.rand.NextBool();
                        float multiplier = 1.9f;
                        float angleStart = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                        float var = 0.05f + (2f - multiplier);
                        int dust1 = ModContent.DustType<AstralBlue>();
                        int dust2 = ModContent.DustType<AstralOrange>();
                        for (float angle = 0f; angle < MathHelper.TwoPi; angle += var) {
                            blue = !blue;
                            Vector2 velocity = angle.ToRotationVector2() * (2f + (float)(Math.Sin(angleStart + angle * 3f) + 1) * 2.5f) * Main.rand.NextFloat(0.95f, 1.05f);
                            Dust d = Dust.NewDustPerfect(target.Center, blue ? dust1 : dust2, velocity);
                            d.customData = 0.025f;
                            d.scale = multiplier - 0.75f;
                            d.noLight = false;
                        }
                    }
                    break;
                }

                case SpanTypesEnum.TendonBow: {
                    player.AddBuff(BuffID.Panic, 60);
                    break;
                }

                case SpanTypesEnum.NettlevineGreat: {
                    target.AddBuff(70, 60);
                    break;
                }

                case SpanTypesEnum.TheStorm: {
                    target.AddBuff(BuffID.Electrified, 120);
                    break;
                }

                case SpanTypesEnum.FetidEmesis: {
                    target.AddBuff(ModContent.BuffType<Plague>(), 60);
                    break;
                }
            }
        }

        private void WhipHit(Projectile projectile, NPC target) {
            if (projectile.DamageType == DamageClass.Summon && target.CWR().WhipHitNum > 0) {
                CWRNpc npc = target.CWR();
                WhipHitTypeEnum wTypes = (WhipHitTypeEnum)npc.WhipHitType;
                switch (wTypes) {
                    case WhipHitTypeEnum.WhiplashGalactica:
                        if ((
                            (projectile.numHits % 3 == 0 && projectile.minion == true)
                            || (projectile.numHits == 0 && projectile.minion == false)
                            )
                            && projectile.type != ModContent.ProjectileType<CosmicFire>()
                            ) {
                            float randRot = Main.rand.NextFloat(MathHelper.TwoPi);

                            for (int i = 0; i < 3; i++) {
                                Vector2 vr = ((MathHelper.TwoPi / 3 * i) + randRot).ToRotationVector2() * 10;
                                int proj = Projectile.NewProjectile(projectile.GetSource_FromThis(), target.Center, vr
                                    , ModContent.ProjectileType<GodKillers>(), projectile.damage / 3, 0, projectile.owner, 0, 0, Main.rand.Next(22));
                                Main.projectile[proj].timeLeft = 65;
                                Main.projectile[proj].penetrate = -1;
                                Main.projectile[proj].usesLocalNPCImmunity = true;
                                Main.projectile[proj].localNPCHitCooldown = -1;
                            }
                        }
                        break;
                    case WhipHitTypeEnum.BleedingScourge:
                        _ = Projectile.NewProjectile(CWRUtils.parent(projectile), target.Center, Vector2.Zero,
                                    ModContent.ProjectileType<Projectiles.Weapons.Summon.BloodBlast>(),
                                    projectile.damage / 2, 0, projectile.owner);
                        break;
                    case WhipHitTypeEnum.AzureDragonRage:
                        break;
                    case WhipHitTypeEnum.GhostFireWhip:
                        break;
                    case WhipHitTypeEnum.AllhallowsGoldWhip:
                        break;
                    case WhipHitTypeEnum.ElementWhip:
                        break;
                }

                if (npc.WhipHitNum > 0) {
                    npc.WhipHitNum--;
                }
            }
        }

        private void SpecialAmmoStateOnHitEffect(Player player, Projectile projectile, NPC target, NPC.HitInfo hit) {
            if (Source?.Context == "CWRGunShoot" && cwrItem != null) {
                if (cwrItem.SpecialAmmoState == SpecialAmmoStateEnum.napalmBomb) {
                    target.AddBuff(BuffID.OnFire3, 60);
                    player.ApplyDamageToNPC(target, player.GetShootState().WeaponDamage / 5, 0f, 0, false, DamageClass.Default, true);
                    float thirdDustScale = Main.rand.NextFloat(2, 4);
                    Vector2 dustRotation = (target.rotation - MathHelper.PiOver2).ToRotationVector2();
                    Vector2 dustVelocity = dustRotation * target.velocity.Length();
                    _ = SoundEngine.PlaySound(SoundID.Item14, target.Center);
                    for (int j = 0; j < 40; j++) {
                        int contactDust2 = Dust.NewDust(new Vector2(target.position.X, target.position.Y), target.width, target.height, DustID.InfernoFork, 0f, 0f, 0, default, thirdDustScale);
                        Dust dust = Main.dust[contactDust2];
                        dust.position = target.Center + (Vector2.UnitX.RotatedByRandom(MathHelper.Pi).RotatedBy(target.velocity.ToRotation()) * target.width / 3f);
                        dust.noGravity = true;
                        dust.velocity.Y -= 6f;
                        dust.velocity *= 0.5f;
                        dust.velocity += dustVelocity * (0.6f + (0.6f * Main.rand.NextFloat()));
                    }
                }
                else if (cwrItem.SpecialAmmoState == SpecialAmmoStateEnum.highExplosive) {
                    player.ApplyDamageToNPC(target, player.GetShootState().WeaponDamage / 3, 0f, 0, false, DamageClass.Default, true);
                    for (int i = 0; i < 6; i++) {
                        BasePRT particle = new PRT_Light(projectile.Center, CWRUtils.randVr(3, 16), Main.rand.NextFloat(0.3f, 0.7f), Color.OrangeRed, 2, 0.2f);
                        PRTLoader.AddParticle(particle);
                    }
                }
                else if (cwrItem.SpecialAmmoState == SpecialAmmoStateEnum.dragonBreath) {
                    if (projectile.numHits == 0 && player.ownedProjectileCounts[ModContent.ProjectileType<BMGFIRE>()] < 33) {
                        float newdamage = projectile.damage;
                        int projCount = 1;
                        if (projectile.type > 0 && projectile.type < player.ownedProjectileCounts.Length) {
                            projCount = player.ownedProjectileCounts[projectile.type];
                        }
                        if (newdamage > 1000) {
                            newdamage = 1000;
                        }
                        if (projCount > 5) {
                            newdamage *= 0.95f;
                        }
                        if (projCount > 10) {
                            newdamage *= 0.93f;
                        }
                        if (projCount > 15) {
                            newdamage *= 0.91f;
                        }
                        if (projCount > 20) {
                            newdamage *= 0.9f;
                        }
                        for (int i = 0; i < 4; i++) {
                            Vector2 vr = projectile.velocity.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f)) * Main.rand.NextFloat(0.6f, 1.7f);
                            int proj = Projectile.NewProjectile(projectile.parent(), projectile.Center + (projectile.velocity * -3), vr
                                , ModContent.ProjectileType<BMGFIRE>(), (int)(newdamage * (hit.Crit ? 0.35f : 0.2f)), 0, projectile.owner, Main.rand.Next(23));
                            Main.projectile[proj].timeLeft /= 2;
                        }
                    }
                }
            }
        }

        private void ViscositySD(Projectile projectile, NPC target) {
            if (Viscosity && projectile.numHits == 0) {
                hitNPC = target;
                offsetHitPos = target.Center.To(projectile.Center);
                offsetHitRot = projectile.rotation;
                oldNPCRot = target.rotation;
                NetViscositySend(projectile);
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            Player player = Main.player[projectile.owner];
            ViscositySD(projectile, target);
            SuperAttackOnHitNPC(projectile, target);
            WhipHit(projectile, target);
            RustyMedallionEffect(player, projectile);
            SpanTypesOnHitNPC(player, projectile, target, hit);
            SpecialAmmoStateOnHitEffect(player, projectile, target, hit);
            RMeowmere.SpanDust(projectile);
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor) {
            //我不得不说，冰刺的绘制堪称天才，但是原版对暗影背景的处理出现了小小的问题，这会导致弹幕底部出现一个相对不起眼的黑点，下面这段代码试图解决这个问题
            if (projectile.type == 961) {
                Projectile_961_DeBug(projectile, lightColor);
                return false;
            }
            //drawProjName(projectile);
            return base.PreDraw(projectile, ref lightColor);
        }

        public void drawProjName(Projectile projectile) {
            Vector2 pos = projectile.Center - Main.screenPosition;
            string name = projectile.ToString();
            if (projectile.ModProjectile != null) {
                name = projectile.ModProjectile.FullName;
            }
            Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.ItemStack.Value, name, pos.X + 0, pos.Y - 30, Color.AliceBlue, Color.Black, Vector2.Zero, 1f);
        }

        private void Projectile_961_DeBug(Projectile projectile, Color lightColor) {
            Vector2 drawPos = projectile.Center;
            drawPos.X -= Main.screenPosition.X;
            drawPos.Y -= Main.screenPosition.Y;
            drawPos.Y += projectile.gfxOffY;
            Texture2D value = TextureAssets.Projectile[961].Value;
            Rectangle rec = value.Frame(1, 5, 0, projectile.frame);
            Vector2 origin12 = new(16f, rec.Height / 2);
            Color projectileColor = Lighting.GetColor((int)(projectile.position.X + (projectile.width * 0.5)) / 16, (int)((projectile.position.Y + (projectile.height * 0.5)) / 16.0));
            Color alpha5 = projectile.GetAlpha(projectileColor);
            Vector2 vector39 = new(projectile.scale);
            float lerpValue5 = Utils.GetLerpValue(30f, 25f, projectile.ai[0], clamped: true);
            vector39.Y *= lerpValue5;
            Vector4 vector40 = projectileColor.ToVector4();
            Vector4 vector41 = new Color(67, 17, 17).ToVector4();
            vector41 *= vector40;
            /*问题源自这个暗影背景的绘制，它的形状难以容纳，故而注释
             Main.EntitySpriteDraw(TextureAssets.Extra[98].Value, drawPos - (projectile.velocity * projectile.scale * 0.9f)
            , null, projectile.GetAlpha(new Color(vector41.X, vector41.Y, vector41.Z, vector41.W)) * 1f, projectile.rotation + ((float)Math.PI / 2f)
            , TextureAssets.Extra[98].Value.Size() / 2f, projectile.scale * 0.9f, SpriteEffects.None);
             */
            Color color49 = projectile.GetAlpha(Color.White) * Utils.Remap(projectile.ai[0], 0f, 20f, 0.5f, 0f);
            color49.A = 0;
            for (int num194 = 0; num194 < 4; num194++) {
                Main.EntitySpriteDraw(value, drawPos + (projectile.rotation.ToRotationVector2().RotatedBy((float)Math.PI / 2f * num194) * 2f * vector39)
                    , rec, color49, projectile.rotation, origin12, vector39, SpriteEffects.None);
            }
            Main.EntitySpriteDraw(value, drawPos, rec, alpha5, projectile.rotation, origin12, vector39, SpriteEffects.None);
        }

        private bool ThanatosLaserDrawDeBug(Projectile projectile, ref Color lightColor) {
            ThanatosLaser thanatosLaser = projectile.ModProjectile as ThanatosLaser;
            if (thanatosLaser.TelegraphDelay >= ThanatosLaser.TelegraphTotalTime) {
                lightColor.R = (byte)(255 * projectile.Opacity);
                lightColor.G = (byte)(255 * projectile.Opacity);
                lightColor.B = (byte)(255 * projectile.Opacity);
                Vector2 drawOffset = projectile.velocity.SafeNormalize(Vector2.Zero) * -30f;
                projectile.Center += drawOffset;
                if (projectile.type.ValidateIndex(Main.maxProjectiles)) {
                    CalamityUtils.DrawAfterimagesCentered(projectile, ProjectileID.Sets.TrailingMode[projectile.type], lightColor, 1);
                }

                projectile.Center -= drawOffset;
                return false;
            }

            Texture2D laserTelegraph = ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/LaserWallTelegraphBeam").Value;

            float yScale = 2f;
            if (thanatosLaser.TelegraphDelay < ThanatosLaser.TelegraphFadeTime) {
                yScale = MathHelper.Lerp(0f, 2f, thanatosLaser.TelegraphDelay / 15f);
            }

            if (thanatosLaser.TelegraphDelay > ThanatosLaser.TelegraphTotalTime - ThanatosLaser.TelegraphFadeTime) {
                yScale = MathHelper.Lerp(2f, 0f, (thanatosLaser.TelegraphDelay - (ThanatosLaser.TelegraphTotalTime - ThanatosLaser.TelegraphFadeTime)) / 15f);
            }

            Vector2 scaleInner = new(ThanatosLaser.TelegraphWidth / laserTelegraph.Width, yScale);
            Vector2 origin = laserTelegraph.Size() * new Vector2(0f, 0.5f);
            Vector2 scaleOuter = scaleInner * new Vector2(1f, 2.2f);

            Color colorOuter = Color.Lerp(Color.Red, Color.Crimson, thanatosLaser.TelegraphDelay / ThanatosLaser.TelegraphTotalTime * 2f % 1f);
            Color colorInner = Color.Lerp(colorOuter, Color.White, 0.75f);

            colorOuter *= 0.6f;
            colorInner *= 0.6f;

            Main.EntitySpriteDraw(laserTelegraph, projectile.Center - Main.screenPosition, null, colorInner, thanatosLaser.Velocity.ToRotation(), origin, scaleInner, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(laserTelegraph, projectile.Center - Main.screenPosition, null, colorOuter, thanatosLaser.Velocity.ToRotation(), origin, scaleOuter, SpriteEffects.None, 0);
            return true;
        }
    }
}
