using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 神圣套（四种头盔与远古件可混搭）：单件沿用原版；套装奖励为伤害 +10%、暴击 +10%，
    /// 击中敌人后获得圣光护体（每 20 秒一次）完全闪避下一次伤害，闪避时向周围射出 6 颗圣光星，各 150 伤害
    /// </summary>
    internal class GsHallowedArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [
            ItemID.HallowedHelmet, ItemID.HallowedMask, ItemID.HallowedHeadgear, ItemID.HallowedHood,
            ItemID.AncientHallowedHelmet, ItemID.AncientHallowedMask, ItemID.AncientHallowedHeadgear, ItemID.AncientHallowedHood,
        ];
        public override int BodyID => ItemID.HallowedPlateMail;
        public override int LegsID => ItemID.HallowedGreaves;
        public override int[] BodyIDs => [ItemID.HallowedPlateMail, ItemID.AncientHallowedPlateMail];
        public override int[] LegsIDs => [ItemID.HallowedGreaves, ItemID.AncientHallowedGreaves];
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "10% increased damage and critical strike chance; hitting an enemy grants Holy Ward once every 20 seconds, which fully dodges the next hit and releases 6 holy stars dealing 150 damage each";

        /// <summary>护体冷却帧数</summary>
        private const int WardCooldown = 1200;

        /// <summary>圣光星伤害</summary>
        private const int StarDamage = 150;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetDamage(DamageClass.Generic) += 0.10f;
            player.GetCritChance(DamageClass.Generic) += 10f;
        }

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsHallowedStarProj>();

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            //护体就绪：金色微光绕身（原版附魔金粒子）
            if (!state.EndowFlag || VaultUtils.isServer || !Main.rand.NextBool(6)) {
                return;
            }
            Dust dust = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2CircularEdge(22f, 30f), DustID.Enchanted_Gold,
                new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.9f)), 120, default, Main.rand.NextFloat(0.8f, 1.2f));
            dust.noGravity = true;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (state.EndowFlag || !state.TryUseCooldown(this, WardCooldown)) {
                return;
            }
            state.EndowFlag = true;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f }, player.Center);
            }
        }

        public override bool EndowConsumableDodge(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            if (!state.EndowFlag) {
                return false;
            }
            state.EndowFlag = false;
            player.SetImmuneTimeForAllTypes(40);
            SoundEngine.PlaySound(SoundID.Item4, player.Center);
            for (int i = 0; i < 6; i++) {
                Vector2 velocity = (MathHelper.TwoPi * i / 6f).ToRotationVector2() * 10f;
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithHallowedEndow"), player.Center, velocity,
                    ModContent.ProjectileType<GsHallowedStarProj>(), StarDamage, 4f, player.whoAmI);
            }
            for (int i = 0; i < 20; i++) {
                Dust dust = Dust.NewDustPerfect(player.Center, DustID.Enchanted_Gold,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f), 100, default, Main.rand.NextFloat(1f, 1.5f));
                dust.noGravity = true;
            }
            return true;
        }

        public override void OnEndowLost(Player player, GodSmithArmorPlayer state) => state.ClearScratch();
    }

    /// <summary>圣光星：借星怒之星贴图，径向飞出后轻微追向最近敌人，可穿透一次；轨迹撒附魔金粒子</summary>
    internal class GsHallowedStarProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Starfury;

        private ref float Life => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life > 10f) {
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 12f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.07f);
                }
            }
            Projectile.rotation += 0.3f;
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Enchanted_Gold,
                    0f, 0f, 100, default, 1f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
            Lighting.AddLight(Projectile.Center, 0.6f, 0.5f, 0.2f);
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 500f;
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

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Enchanted_Gold,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 1.2f);
                dust.noGravity = true;
            }
        }
    }
}
