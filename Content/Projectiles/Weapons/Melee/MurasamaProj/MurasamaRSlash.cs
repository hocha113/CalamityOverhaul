using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.NPCs.Crabulon;
using CalamityMod.NPCs.OldDuke;
using CalamityMod.NPCs.ProfanedGuardians;
using CalamityMod.NPCs.Providence;
using CalamityMod.NPCs.SlimeGod;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Particles;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class MurasamaRSlash : BaseHeldProj
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "MurasamaSlash";
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<MurasamaEcType>();
        public ref int hitCooldown => ref Main.player[Projectile.owner].Calamity().murasamaHitCooldown;
        public int time = 0;
        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 14;
        }

        public bool Slashing = false;
        public bool Slash1 => Projectile.frame == 10;
        public bool Slash2 => Projectile.frame == 0;
        public bool Slash3 => Projectile.frame == 6;

        public override void SetDefaults() {
            Projectile.width = 216;
            Projectile.height = 216;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 6;
            Projectile.frameCounter = 0;
            Projectile.alpha = 255;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.frameCounter <= 1) {
                return false;
            }

            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Rectangle frame = texture.Frame(verticalFrames: Main.projFrames[Type], frameY: Projectile.frame);
            Vector2 origin = frame.Size() * 0.5f;
            SpriteEffects spriteEffects = Projectile.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition + (Projectile.velocity * 0.3f) + new Vector2(0, -32).RotatedBy(Projectile.rotation), frame
                , MurasamaEcType.NameIsVergil(Owner) ? Color.Blue : Color.White, Projectile.rotation, origin, Projectile.scale, spriteEffects, 0);
            return false;
        }

        public static float GetMuraSizeInMeleeSengs(Player player) {
            Item mura = player.ActiveItem();
            if ((mura.type == ModContent.ItemType<MurasamaEcType>()
                || mura.type == ModContent.ItemType<Murasama>())
                && mura.type != ItemID.None) {
                float meleeSizeSengs = player.GetAdjustedItemScale(mura);
                if (meleeSizeSengs > 1.5f) {
                    meleeSizeSengs = 1.5f;
                }
                if (meleeSizeSengs < 0.5f) {
                    meleeSizeSengs = 0.5f;
                }
                return meleeSizeSengs;
            }
            return 1f;
        }

        public override void AI() {
            Projectile.Calamity().timesPierced = 0;
            Player player = Main.player[Projectile.owner];
            if (time == 0) {
                Projectile.scale = (MurasamaEcType.NameIsSam(player) ? 1.65f : MurasamaEcType.GetOnScale) * MurasamaEcType.ScaleOffset_PercentageValue;
                Projectile.scale *= GetMuraSizeInMeleeSengs(Owner);
                Projectile.frame = Main.zenithWorld ? 6 : 10;
                Projectile.alpha = 0;
                time++;
            }
            if (Slash2) {
                _ = SoundEngine.PlaySound(MurasamaEcType.Swing with { Pitch = -0.1f }, Projectile.Center);
                if (hitCooldown == 0) {
                    Slashing = true;
                }

                Projectile.numHits = 0;
            }
            else if (Slash3) {
                _ = SoundEngine.PlaySound(MurasamaEcType.BigSwing with { Pitch = 0f }, Projectile.Center);
                if (hitCooldown == 0) {
                    Slashing = true;
                }

                Projectile.numHits = 0;
            }
            else if (Slash1) {
                _ = SoundEngine.PlaySound(MurasamaEcType.Swing with { Pitch = -0.05f }, Projectile.Center);
                if (hitCooldown == 0) {
                    Slashing = true;
                }

                Projectile.numHits = 0;
            }
            else {
                Slashing = false;
            }

            if (Projectile.frame == 5 && Projectile.frameCounter % 3 == 0) {
                Projectile.damage = Projectile.damage * 2;
            }
            if (Projectile.frame == 7 && Projectile.frameCounter % 3 == 0) {
                Projectile.damage = (int)(Projectile.damage * 0.5f);
            }

            Projectile.frameCounter++;
            if (Projectile.frameCounter % 3 == 0) {
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }

            Vector2 origin = Projectile.Center + (Projectile.velocity * 3f);
            Lighting.AddLight(origin, Color.Red.ToVector3() * (Slashing == true ? 3.5f : 2f));

            Vector2 playerRotatedPoint = player.RotatedRelativePoint(player.MountedCenter, true);
            if (Main.myPlayer == Projectile.owner) {
                if (player.channel && !player.noItems && !player.CCed
                    && player.ownedProjectileCounts[ModContent.ProjectileType<MurasamaBreakOut>()] <= 0) {
                    HandleChannelMovement(player, playerRotatedPoint);
                }
                else {
                    hitCooldown = Main.zenithWorld ? 0 : 8;
                    Projectile.Kill();
                }
            }

            if (Slashing || Slash1) {
                float velocityAngle = Projectile.velocity.ToRotation();
                Projectile.rotation = velocityAngle + ((Projectile.direction == -1).ToInt() * MathHelper.Pi);
            }
            float velocityAngle2 = Projectile.velocity.ToRotation();
            Projectile.direction = (Math.Cos(velocityAngle2) > 0).ToDirectionInt();

            float offset = 80f * Projectile.scale;
            Projectile.Center = playerRotatedPoint + (velocityAngle2.ToRotationVector2() * offset);

            player.ChangeDir(Projectile.direction);

            Projectile.timeLeft = 2;

            player.itemRotation = (Projectile.velocity * Projectile.direction).ToRotation();
            player.heldProj = Projectile.whoAmI;
            player.itemTime = 2;
            player.itemAnimation = 2;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.type == ModContent.NPCType<Crabulon>()) {
                modifiers.FinalDamage *= 3f;
            }
            if (target.type == ModContent.NPCType<CrabShroom>()) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == NPCID.Creeper) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (target.type == NPCID.KingSlime) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == NPCID.QueenBee) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == NPCID.SkeletronHand) {
                modifiers.FinalDamage *= 0.6f;
            }
            if (target.type == NPCID.WallofFlesh) {
                modifiers.FinalDamage *= 0.45f;
            }
            if (target.type == NPCID.WallofFleshEye) {
                modifiers.FinalDamage *= 0.35f;
            }
            if (target.type == NPCID.QueenSlimeBoss) {
                modifiers.FinalDamage *= 0.9f;
            }
            if (target.type == CWRLoad.AquaticScourgeBody) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (target.type == NPCID.PrimeCannon || target.type == NPCID.PrimeSaw || target.type == NPCID.PrimeVice || target.type == NPCID.PrimeLaser) {
                modifiers.FinalDamage *= 0.75f;
            }
            if (target.type == NPCID.Retinazer || target.type == NPCID.Spazmatism) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == CWRLoad.PerforatorBodyLarge || target.type == CWRLoad.DevourerofGodsBody || target.type == CWRLoad.CosmicGuardianBody
                || target.type == CWRLoad.PerforatorBodyMedium || target.type == NPCID.EaterofWorldsBody || target.type == CWRLoad.PerforatorBodySmall) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (target.type == CWRLoad.CosmicGuardianTail || target.type == CWRLoad.CosmicGuardianHead
                || target.type == CWRLoad.DevourerofGodsHead || target.type == CWRLoad.DevourerofGodsTail) {
                modifiers.FinalDamage *= 1.33f;
            }
            if (target.type == CWRLoad.AstrumDeusHead || target.type == CWRLoad.AstrumDeusTail) {
                modifiers.FinalDamage *= 3.5f;
            }
            if (target.type == NPCID.TheDestroyerBody || target.type == CWRLoad.StormWeaverBody) {
                modifiers.FinalDamage *= 0.55f;
            }
            if (target.type == NPCID.TheDestroyer || target.type == NPCID.TheDestroyerTail) {
                modifiers.FinalDamage *= 2.5f;
            }
            if (target.type == NPCID.Probe) {
                modifiers.FinalDamage *= 1.75f;
            }
            if (target.type == ModContent.NPCType<SplitEbonianPaladin>() || target.type == ModContent.NPCType<SplitCrimulanPaladin>()) {
                modifiers.FinalDamage *= 0.3f;
            }
            if (target.type == CWRLoad.PlaguebringerGoliath) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == CWRLoad.RavagerBody) {
                modifiers.FinalDamage *= 2f;
            }
            if (target.type == CWRLoad.RavagerClawLeft || target.type == CWRLoad.RavagerClawRight || target.type == CWRLoad.RavagerHead
                || target.type == CWRLoad.RavagerLegLeft || target.type == CWRLoad.RavagerLegRight) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == NPCID.MoonLordFreeEye || target.type == NPCID.MoonLordHand || target.type == NPCID.MoonLordHead || target.type == NPCID.MoonLordCore) {
                modifiers.FinalDamage *= 1.1f;
            }
            if (target.type == ModContent.NPCType<ProfanedGuardianHealer>() || target.type == ModContent.NPCType<ProfanedGuardianDefender>()) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == ModContent.NPCType<ProfanedGuardianCommander>()) {
                modifiers.FinalDamage *= 1.75f;
            }
            if (target.type == ModContent.NPCType<Providence>()) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == CWRLoad.StormWeaverTail || target.type == CWRLoad.StormWeaverHead) {
                modifiers.FinalDamage *= 2f;
            }
            if (target.type == CWRLoad.Polterghast) {
                modifiers.FinalDamage *= 0.8f;
            }
            if (target.type == ModContent.NPCType<OldDukeToothBall>() || target.type == ModContent.NPCType<SulphurousSharkron>()) {
                modifiers.FinalDamage *= 3f;
            }
            if (target.type == CWRLoad.Yharon) {
                modifiers.FinalDamage *= 0.9f;
            }
            if (target.type == CWRLoad.ThanatosBody1 || target.type == CWRLoad.ThanatosBody2) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == CWRLoad.ThanatosHead) {
                modifiers.FinalDamage *= 2.86f;
            }
            if (target.type == CWRLoad.Apollo || target.type == CWRLoad.Artemis) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == CWRLoad.AresBody || target.type == CWRLoad.AresGaussNuke || target.type == CWRLoad.AresLaserCannon
                || target.type == CWRLoad.AresPlasmaFlamethrower || target.type == CWRLoad.AresTeslaCannon) {
                modifiers.FinalDamage *= 0.85f;
            }
            if (target.type == ModContent.NPCType<SupremeCataclysm>() || target.type == ModContent.NPCType<SupremeCatastrophe>()) {
                modifiers.FinalDamage *= 1.666666f;
            }
            if (target.type == ModContent.NPCType<SupremeCalamitas>()) {
                modifiers.FinalDamage *= 2f;
            }
            if (target.type == ModContent.NPCType<BrimstoneHeart>()) {
                modifiers.FinalDamage *= 1.5f;
            }

            //饿鬼(被触手连接在肉山身上的状态)
            if (target.type == NPCID.TheHungry) {
                modifiers.FinalDamage /= 2f;
            }

            if (!target.IsWormBody()) {
                modifiers.DefenseEffectiveness *= 0.75f;
            }

            if (target.boss) {
                float sengsValue = 0.5f + InWorldBossPhase.Instance.Mura_Level() * 0.03f;
                modifiers.FinalDamage *= sengsValue;
            }
        }

        public void HandleChannelMovement(Player player, Vector2 playerRotatedPoint) {
            float speed = 1f;
            if (player.ActiveItem().shoot == Projectile.type) {
                speed = player.ActiveItem().shootSpeed * Projectile.scale;
            }
            Vector2 newVelocity = ToMouse.SafeNormalize(Vector2.UnitX * player.direction) * speed;

            if (Slashing) {
                if (Projectile.velocity.X != newVelocity.X || Projectile.velocity.Y != newVelocity.Y) {
                    Projectile.netUpdate = true;
                }
                Projectile.velocity = newVelocity;
            }
        }
        public override void ModifyDamageHitbox(ref Rectangle hitbox) {
            int size = 60;
            if (Slash3) {
                hitbox.Inflate(size, size);
            }
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                Owner.CWR().RisingDragonCharged += (int)(MurasamaEcType.GetOnRDCD / 10f);
                if (Owner.CWR().RisingDragonCharged > MurasamaEcType.GetOnRDCD) {
                    Owner.CWR().RisingDragonCharged = MurasamaEcType.GetOnRDCD;
                }
                int type = ModContent.ProjectileType<MurasamaHeldProj>();
                foreach (var p in Main.projectile) {
                    if (p.type != type) {
                        continue;
                    }
                    MurasamaHeldProj murasamaHeldProj = p.ModProjectile as MurasamaHeldProj;
                    if (murasamaHeldProj != null) {
                        murasamaHeldProj.noAttenuationTime = 180;
                    }
                }
            }
            _ = !CWRLoad.NPCValue.ISTheofSteel(target.type)
                ? SoundEngine.PlaySound(MurasamaEcType.OrganicHit with { Pitch = Slash2 ? -0.1f : Slash3 ? 0.1f : Slash1 ? -0.15f : 0 }, Projectile.Center)
                : SoundEngine.PlaySound(MurasamaEcType.InorganicHit with { Pitch = Slash2 ? -0.1f : Slash3 ? 0.1f : Slash1 ? -0.15f : 0 }, Projectile.Center);

            for (int i = 0; i < 3; i++) {
                Color impactColor = Slash3 ? Main.rand.NextBool(3) ? Color.LightCoral : Color.White : Main.rand.NextBool(4) ? Color.LightCoral : Color.Crimson;
                float impactParticleScale = Main.rand.NextFloat(1f, 1.75f);

                if (Slash3) {
                    SparkleParticle impactParticle2 = new(target.Center + Main.rand.NextVector2Circular(target.width * 0.75f, target.height * 0.75f), Vector2.Zero, Color.White, Color.Red, impactParticleScale * 1.2f, 8, 0, 4.5f);
                    GeneralParticleHandler.SpawnParticle(impactParticle2);
                }
                SparkleParticle impactParticle = new(target.Center + Main.rand.NextVector2Circular(target.width * 0.75f, target.height * 0.75f), Vector2.Zero, impactColor, Color.Red, impactParticleScale, 8, 0, 2.5f);
                GeneralParticleHandler.SpawnParticle(impactParticle);
            }

            float sparkCount = MathHelper.Clamp(Slash3 ? 18 - (Projectile.numHits * 3) : 5 - (Projectile.numHits * 2), 0, 18);
            for (int i = 0; i < sparkCount; i++) {
                Vector2 sparkVelocity2 = Projectile.velocity.RotatedBy(Slash2 ? -0.45f * Owner.direction : Slash3 ? 0 : Slash1 ? 0.45f * Owner.direction : 0).RotatedByRandom(0.35f) * Main.rand.NextFloat(0.5f, 1.8f);
                int sparkLifetime2 = Main.rand.Next(23, 35);
                float sparkScale2 = Main.rand.NextFloat(0.95f, 1.8f);
                Color sparkColor2 = Slash3 ? Main.rand.NextBool(3) ? Color.Red : Color.IndianRed : Main.rand.NextBool() ? Color.Red : Color.Firebrick;
                if (Main.rand.NextBool()) {
                    AltSparkParticle spark = new(target.Center + Main.rand.NextVector2Circular(target.width * 0.5f, target.height * 0.5f) + (Projectile.velocity * 1.2f), sparkVelocity2 * (Slash3 ? 1f : 0.65f), false, (int)(sparkLifetime2 * (Slash3 ? 1.2f : 1f)), sparkScale2 * (Slash3 ? 1.4f : 1f), sparkColor2);
                    GeneralParticleHandler.SpawnParticle(spark);
                }
                else {
                    LineParticle spark = new(target.Center + Main.rand.NextVector2Circular(target.width * 0.5f, target.height * 0.5f) + (Projectile.velocity * 1.2f), sparkVelocity2 * (Projectile.frame == 7 ? 1f : 0.65f), false, (int)(sparkLifetime2 * (Projectile.frame == 7 ? 1.2f : 1f)), sparkScale2 * (Projectile.frame == 7 ? 1.4f : 1f), Main.rand.NextBool() ? Color.Red : Color.Firebrick);
                    GeneralParticleHandler.SpawnParticle(spark);
                }
            }
            float dustCount = MathHelper.Clamp(Slash3 ? 25 - (Projectile.numHits * 3) : 12 - (Projectile.numHits * 2), 0, 25);
            for (int i = 0; i <= dustCount; i++) {
                int dustID = Main.rand.NextBool(3) ? 182 : Main.rand.NextBool() ? Slash3 ? 309 : 296 : 90;
                Dust dust2 = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(target.width * 0.5f, target.height * 0.5f), dustID, Projectile.velocity.RotatedBy(Slash2 ? -0.45f * Owner.direction : Slash3 ? 0 : Slash1 ? 0.45f * Owner.direction : 0).RotatedByRandom(0.55f) * Main.rand.NextFloat(0.3f, 1.1f));
                dust2.scale = Main.rand.NextFloat(0.9f, 2.4f);
                dust2.noGravity = true;
            }
        }
        public override Color? GetAlpha(Color lightColor) {
            return new Color(100, 0, 0, 0);
        }

        public override bool? CanDamage() {
            return Slashing == false ? false : null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0;
            Vector2 toMou = ToMouse;
            Vector2 orig = Owner.Center + toMou.GetNormalVector() * 30;
            float lengMode = 220;
            float lengMode2 = 250;
            if (Slash3) {
                lengMode = 300;
                lengMode2 = 270;
            }
            Vector2 endPos = orig + toMou.UnitVector() * (lengMode * Projectile.scale);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , orig, endPos, (lengMode2 * Projectile.scale), ref point);
        }
    }
}
