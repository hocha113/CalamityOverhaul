using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.NPCs.AquaticScourge;
using CalamityMod.NPCs.CalClone;
using CalamityMod.NPCs.CeaselessVoid;
using CalamityMod.NPCs.NormalNPCs;
using CalamityMod.NPCs.Polterghast;
using CalamityMod.NPCs.Ravager;
using CalamityMod.NPCs.SlimeGod;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Particles;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    internal class MuraSlashDefault : BaseHeldProj
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "MurasamaSlash";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<Murasama>();
        public ref int HitCooldown => ref Owner.Calamity().murasamaHitCooldown;
        public bool onspan;
        public bool CanAttemptDead;
        public bool Slashing = false;
        public bool Slash1 => Projectile.frame == 10;
        public bool Slash2 => Projectile.frame == 0;
        public bool Slash3 => Projectile.frame == 6;
        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 14;
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }
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

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle frame = texture.Frame(verticalFrames: Main.projFrames[Type], frameY: Projectile.frame);
            Vector2 origin = frame.Size() * 0.5f;
            SpriteEffects spriteEffects = Projectile.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition + Projectile.velocity * 0.3f + new Vector2(0, -32).RotatedBy(Projectile.rotation), frame
                , MurasamaOverride.NameIsVergil(Owner) ? Color.Blue : Color.White, Projectile.rotation, origin, Projectile.scale, spriteEffects, 0);
            return false;
        }

        public static float GetMuraSizeInMeleeSengs(Player player) {
            Item mura = player.GetItem();
            if (mura.type == ModContent.ItemType<Murasama>()) {
                return MathHelper.Clamp(player.GetAdjustedItemScale(mura), 0.5f, 1.5f);
            }
            return 1f;
        }

        public override void AI() {
            Projectile.Calamity().timesPierced = 0;
            if (!onspan) {
                Projectile.scale = MurasamaOverride.NameIsSam(Owner) ? 1.65f : MurasamaOverride.GetOnScale(Item);
                Projectile.scale *= GetMuraSizeInMeleeSengs(base.Owner);
                Projectile.frame = Main.zenithWorld ? 6 : 10;
                Projectile.alpha = 0;
                onspan = true;
            }
            if (Slash2) {
                _ = SoundEngine.PlaySound(Murasama.Swing with { Pitch = -0.1f }, Projectile.Center);
                if (HitCooldown == 0) {
                    Slashing = true;
                }
                CanAttemptDead = true;
                Projectile.numHits = 0;
            }
            else if (Slash3) {
                _ = SoundEngine.PlaySound(Murasama.BigSwing with { Pitch = 0f }, Projectile.Center);
                if (HitCooldown == 0) {
                    Slashing = true;
                }
                CanAttemptDead = true;
                Projectile.numHits = 0;
            }
            else if (Slash1) {
                _ = SoundEngine.PlaySound(Murasama.Swing with { Pitch = -0.05f }, Projectile.Center);
                if (HitCooldown == 0) {
                    Slashing = true;
                }
                CanAttemptDead = true;
                Projectile.numHits = 0;
            }
            else {
                CanAttemptDead = false;
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

            Vector2 origin = Projectile.Center + Projectile.velocity * 3f;
            Lighting.AddLight(origin, Color.Red.ToVector3() * (Slashing == true ? 3.5f : 2f));

            Vector2 playerRotatedPoint = Owner.RotatedRelativePoint(Owner.MountedCenter, true);
            if (Projectile.IsOwnedByLocalPlayer()) {
                if (Owner.channel && !Owner.noItems && !Owner.CCed
                    && Owner.ownedProjectileCounts[ModContent.ProjectileType<MuraTriggerDash>()] <= 0) {
                    HandleChannelMovement(Owner, playerRotatedPoint);
                }
                else {
                    if (CanAttemptDead || Main.zenithWorld) {
                        HitCooldown = Main.zenithWorld ? 0 : 8;
                        Projectile.Kill();
                    }
                }
            }

            if (Slashing || Slash1) {
                float velocityAngle = Projectile.velocity.ToRotation();
                Projectile.rotation = velocityAngle + (Projectile.direction == -1).ToInt() * MathHelper.Pi;
            }
            float velocityAngle2 = Projectile.velocity.ToRotation();
            Projectile.direction = (Math.Cos(velocityAngle2) > 0).ToDirectionInt();

            float offset = 80f * Projectile.scale;
            Projectile.Center = playerRotatedPoint + velocityAngle2.ToRotationVector2() * offset;

            Owner.ChangeDir(Projectile.direction);
            Owner.itemRotation = (Projectile.velocity * Projectile.direction).ToRotation();
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            // boss存活时对非Boss单位造成2倍伤害
            if (CWRWorld.HasBoss && !target.boss) {
                modifiers.FinalDamage *= 2f;
            }
            // 对红宝石，蓝宝石仅造成1.5倍
            if (target.type == ModContent.NPCType<KingSlimeJewelRuby>() || target.type == ModContent.NPCType<KingSlimeJewelSapphire>()) {
                modifiers.FinalDamage *= 0.75f;
            }
            // 对飞眼怪仅造成0.5倍伤害
            if (target.type == NPCID.Creeper) {
                modifiers.FinalDamage *= 0.25f;
            }
            // 对骷髅王之手仅造成1倍伤害
            if (target.type == NPCID.SkeletronHand) {
                modifiers.FinalDamage *= 0.5f;
            }
            // 对史神护卫仅造成1倍伤害
            if (target.type == ModContent.NPCType<EbonianPaladin>() || target.type == ModContent.NPCType<CrimulanPaladin>()) {
                modifiers.FinalDamage *= 0.5f;
            }
            // 对史神小护卫仅造成0.5倍伤害
            if (target.type == ModContent.NPCType<SplitEbonianPaladin>() || target.type == ModContent.NPCType<SplitCrimulanPaladin>()) {
                modifiers.FinalDamage *= 0.25f;
            }
            // 对肉山眼仅造成1倍伤害
            if (target.type == NPCID.WallofFleshEye) {
                modifiers.FinalDamage *= 0.5f;
            }
            // 对灾眼兄弟仅造成1.5倍伤害
            if (target.type == ModContent.NPCType<Cataclysm>() || target.type == ModContent.NPCType<Catastrophe>()) {
                modifiers.FinalDamage *= 0.75f;
            }
            // 对毁灭魔像飞出的头仅造成50%伤害
            if (target.type == ModContent.NPCType<RavagerHead2>()) {
                modifiers.FinalDamage *= 0.25f;
            }
            // 对暗能量仅造成1倍伤害
            if (target.type == ModContent.NPCType<DarkEnergy>()) {
                modifiers.FinalDamage *= 0.5f;
            }
            // 对幽花复制体仅造成1.5倍伤害
            if (target.type == ModContent.NPCType<PolterghastHook>()) {
                modifiers.FinalDamage *= 0.75f;
            }
            // 对蠕虫只造成66%伤害
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.66f;
            }
            // 对渊海灾虫仅造成20%伤害
            if (CWRLoad.targetNpcTypes11.Contains(target.type)) {
                modifiers.FinalDamage *= 0.3f;
            }
            if (target.type == ModContent.NPCType<AquaticScourgeBodyAlt>()) {
                modifiers.FinalDamage *= 0.2f;
            }
            // 对风编仅造成50%伤害
            if (CWRLoad.targetNpcTypes2.Contains(target.type)) {
                modifiers.FinalDamage *= 0.75f;
            }
            // 对塔纳托斯体节仅造成66%伤害
            if (target.type == CWRLoad.ThanatosBody1 || target.type == CWRLoad.ThanatosBody2 || target.type == CWRLoad.ThanatosTail) {
                modifiers.FinalDamage *= 1f;
            }
            // 神明吞噬者头尾，风编尾不受上述影响
            if (target.type == CWRLoad.DevourerofGodsHead || target.type == CWRLoad.DevourerofGodsTail) {
                modifiers.FinalDamage *= 1.5f;
            }
            if (target.type == CWRLoad.StormWeaverTail) {
                modifiers.FinalDamage *= 2f;
            }
            // 对塔纳托斯头造成2.85倍伤害
            if (target.type == CWRLoad.ThanatosHead) {
                modifiers.FinalDamage *= 4.28f;
            }
            // 对克苏鲁之眼造成1.5倍伤害
            if (target.type == NPCID.EyeofCthulhu) {
                modifiers.FinalDamage *= 1.5f;
            }
            // 对肉山造成1.5倍伤害
            if (target.type == NPCID.WallofFlesh) {
                modifiers.FinalDamage *= 1.5f;
            }
            // 对世纪之花造成66%伤害
            if (target.type == NPCID.Plantera) {
                modifiers.FinalDamage *= 0.66f;
            }
            // 对毁灭魔像身体部位造成50%伤害
            if (target.type == CWRLoad.RavagerClawLeft || target.type == CWRLoad.RavagerClawRight || target.type == CWRLoad.RavagerHead
                || target.type == CWRLoad.RavagerLegLeft || target.type == CWRLoad.RavagerLegRight) {
                modifiers.FinalDamage *= 0.5f;
            }
            // 对星流双子造成1.5倍伤害
            if (target.type == CWRLoad.Apollo || target.type == CWRLoad.Artemis) {
                modifiers.FinalDamage *= 1.5f;
            }
            // 对终灾造成1.63倍伤害
            if (target.type == ModContent.NPCType<SupremeCalamitas>()) {
                modifiers.FinalDamage *= 1.66f;
            }
            modifiers.DefenseEffectiveness *= 0f;
        }

        public void HandleChannelMovement(Player player, Vector2 playerRotatedPoint) {
            float speed = 1f;
            if (player.GetItem().shoot == Projectile.type) {
                speed = player.GetItem().shootSpeed * Projectile.scale;
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
                Owner.CWR().RisingDragonCharged += (int)(MurasamaOverride.GetOnRDCD(Item) / 10f);
                if (Owner.CWR().RisingDragonCharged > MurasamaOverride.GetOnRDCD(Item)) {
                    Owner.CWR().RisingDragonCharged = MurasamaOverride.GetOnRDCD(Item);
                }
                int type = ModContent.ProjectileType<MurasamaHeld>();
                foreach (var p in Main.projectile) {
                    if (p.type != type) {
                        continue;
                    }
                    MurasamaHeld murasamaHeldProj = p.ModProjectile as MurasamaHeld;
                    if (murasamaHeldProj != null) {
                        murasamaHeldProj.noAttenuationTime = 180;
                    }
                }
            }
            _ = !CWRLoad.NPCValue.ISTheofSteel(target)
                ? SoundEngine.PlaySound(Murasama.OrganicHit with { Pitch = Slash2 ? -0.1f : Slash3 ? 0.1f : Slash1 ? -0.15f : 0 }, Projectile.Center)
                : SoundEngine.PlaySound(Murasama.InorganicHit with { Pitch = Slash2 ? -0.1f : Slash3 ? 0.1f : Slash1 ? -0.15f : 0 }, Projectile.Center);

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

            float sparkCount = MathHelper.Clamp(Slash3 ? 18 - Projectile.numHits * 3 : 5 - Projectile.numHits * 2, 0, 18);
            for (int i = 0; i < sparkCount; i++) {
                Vector2 sparkVelocity2 = Projectile.velocity.RotatedBy(Slash2 ? -0.45f * Owner.direction : Slash3 ? 0 : Slash1 ? 0.45f * Owner.direction : 0).RotatedByRandom(0.35f) * Main.rand.NextFloat(0.5f, 1.8f);
                int sparkLifetime2 = Main.rand.Next(23, 35);
                float sparkScale2 = Main.rand.NextFloat(0.95f, 1.8f);
                Color sparkColor2 = Slash3 ? Main.rand.NextBool(3) ? Color.Red : Color.IndianRed : Main.rand.NextBool() ? Color.Red : Color.Firebrick;
                if (Main.rand.NextBool()) {
                    AltSparkParticle spark = new(target.Center + Main.rand.NextVector2Circular(target.width * 0.5f, target.height * 0.5f) + Projectile.velocity * 1.2f, sparkVelocity2 * (Slash3 ? 1f : 0.65f), false, (int)(sparkLifetime2 * (Slash3 ? 1.2f : 1f)), sparkScale2 * (Slash3 ? 1.4f : 1f), sparkColor2);
                    GeneralParticleHandler.SpawnParticle(spark);
                }
                else {
                    LineParticle spark = new(target.Center + Main.rand.NextVector2Circular(target.width * 0.5f, target.height * 0.5f) + Projectile.velocity * 1.2f, sparkVelocity2 * (Projectile.frame == 7 ? 1f : 0.65f), false, (int)(sparkLifetime2 * (Projectile.frame == 7 ? 1.2f : 1f)), sparkScale2 * (Projectile.frame == 7 ? 1.4f : 1f), Main.rand.NextBool() ? Color.Red : Color.Firebrick);
                    GeneralParticleHandler.SpawnParticle(spark);
                }
            }
            float dustCount = MathHelper.Clamp(Slash3 ? 25 - Projectile.numHits * 3 : 12 - Projectile.numHits * 2, 0, 25);
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
            Vector2 unitOffset = new Vector2(0, -32).RotatedBy(Projectile.rotation);
            Vector2 orig = Owner.GetPlayerStabilityCenter() + unitOffset;
            float lengMode = 220;
            float lengMode2 = 250;
            if (Slash3) {
                lengMode = 300;
                lengMode2 = 270;
            }
            Vector2 endPos = orig + ToMouse.UnitVector() * (lengMode * Projectile.scale);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , orig, endPos, lengMode2 * Projectile.scale, ref point);
        }
    }
}
