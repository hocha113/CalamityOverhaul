using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 提基套：单件沿用原版；套装奖励为召唤栏 +3、召唤伤害 +15%、鞭子范围 +25%，
    /// 仆从与鞭子命中时 20% 概率召来图腾魂火追向目标，造成 60 点伤害并使其中毒 3 秒
    /// </summary>
    internal class GsTikiArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.TikiMask];
        public override int BodyID => ItemID.TikiShirt;
        public override int LegsID => ItemID.TikiPants;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "+3 minion slots, 15% increased summon damage and 25% increased whip range; minion and whip hits have a 20% chance to call a totem spirit flame that homes in for 60 damage and poisons for 3 seconds";

        /// <summary>魂火伤害</summary>
        private const int FlameDamage = 60;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.maxMinions += 3;
            player.GetDamage(DamageClass.Summon) += 0.15f;
            player.whipRangeMultiplier += 0.25f;
        }

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsTikiSpiritFlameProj>();

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || !hit.DamageType.CountsAsClass(DamageClass.Summon) || Main.rand.Next(100) >= 20) {
                return;
            }
            //自玩家头顶飘出，先散开再追踪
            Vector2 velocity = new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(4f, 7f));
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithTikiEndow"), player.Top, velocity,
                ModContent.ProjectileType<GsTikiSpiritFlameProj>(), FlameDamage, 1f, player.whoAmI, target.whoAmI);
        }
    }

    /// <summary>图腾魂火：借原版魂火贴图，飘出后追向锁定目标（ai[0]），命中中毒；轨迹撒丛林草粒子</summary>
    internal class GsTikiSpiritFlameProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SpiritFlame;

        private ref float TargetIndex => ref Projectile.ai[0];

        private ref float Life => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
            if (target == null || !target.active || target.friendly) {
                target = NearestTarget();
            }
            if (Life > 8f && target != null) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 13f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.09f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.2f, 0.6f, 0.3f);
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.JungleGrass,
                    0f, 0f, 100, default, 1f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        private NPC NearestTarget() {
            NPC best = null;
            float bestDist = 600f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Center.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 180);
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.4f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.JungleGrass,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 1.2f);
                dust.noGravity = true;
            }
        }
    }
}
