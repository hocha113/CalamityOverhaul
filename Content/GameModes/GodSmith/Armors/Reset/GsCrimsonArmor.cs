using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 血腥套 · 血蛭（通用·续航）。单件沿用原版（三件各 +3% 伤害）。<br/>
    /// 原版旗标清点：血肉再生（crimsonRegen）→ 原样补回；无删除项。<br/>
    /// 签名：命中生命低于一半的敌人时 20% 概率（冷却 1.5 秒）放出一只血蛭咬向目标，咬中后飞回为你治疗 3 点
    /// </summary>
    internal class GsCrimsonArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.CrimsonHelmet];
        public override int BodyID => ItemID.CrimsonScalemail;
        public override int LegsID => ItemID.CrimsonGreaves;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "Greatly increased life regeneration; hitting an enemy below half life has a 20% chance to release a blood leech that bites it and flies back to heal you for 3";

        /// <summary>血蛭回程治疗量</summary>
        internal const int LeechHeal = 3;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.crimsonRegen = true;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || target.life <= 0 || target.life > target.lifeMax / 2
                || target.type == NPCID.TargetDummy || Main.rand.Next(100) >= 20 || !state.TryUseCooldown(this, 90)) {
                return;
            }
            Vector2 velocity = (target.Center - player.Center).SafeNormalize(Vector2.UnitX * player.direction).RotatedByRandom(0.4f) * 7f;
            SpawnProc(player, "GodSmithCrimsonEndow", player.Center, velocity,
                ModContent.ProjectileType<GsCrimsonArmorLeechProj>(), ProcDamage(damageDone, 0.3f, 3, 10), 1f, target.whoAmI);
        }
    }

    /// <summary>
    /// 血蛭：借吸血刀回血光点贴图，去程追向锁定目标（ai[0]），咬中后转回程（ai[1] = 1）追主人，
    /// 到达时在 owner 端治疗；一路滴血尘
    /// </summary>
    internal class GsCrimsonArmorLeechProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.VampireHeal;

        private ref float TargetIndex => ref Projectile.ai[0];

        private ref float Returning => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool? CanDamage() => Returning == 0f;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (Returning == 0f) {
                NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
                if (target == null || !target.active || target.friendly) {
                    //目标没了就直接回航
                    Returning = 1f;
                    Projectile.netUpdate = true;
                }
                else {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 12f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.12f);
                }
            }
            if (Returning == 1f) {
                if (!owner.active || owner.dead) {
                    Projectile.Kill();
                    return;
                }
                Vector2 toOwner = owner.Center - Projectile.Center;
                if (toOwner.Length() < 24f) {
                    if (Projectile.owner == Main.myPlayer && owner.statLife < owner.statLifeMax2) {
                        owner.Heal(GsCrimsonArmor.LeechHeal);
                    }
                    Projectile.Kill();
                    return;
                }
                Vector2 want = toOwner.SafeNormalize(Vector2.UnitX) * 14f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.15f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood,
                    0f, 0f, 60, default, 1f);
                dust.velocity *= 0.2f;
                dust.noGravity = false;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Returning = 1f;
            Projectile.netUpdate = true;
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.3f, Pitch = 0.3f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Blood,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 1f), 0, default, 1.3f);
                dust.noGravity = false;
            }
        }
    }
}
