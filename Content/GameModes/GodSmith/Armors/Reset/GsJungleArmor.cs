using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 丛林套（含远古钴，三件可混搭）：头伤害 +5 固定，衣伤害 +10%，裤移速 +10%；
    /// 套装奖励为召唤栏 +2，伤害敌人时立即向其射出一根 4 点伤害的毒刺
    /// </summary>
    internal class GsJungleArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.JungleHat, ItemID.AncientCobaltHelmet];
        public override int BodyID => ItemID.JungleShirt;
        public override int LegsID => ItemID.JunglePants;
        public override int[] BodyIDs => [ItemID.JungleShirt, ItemID.AncientCobaltBreastplate];
        public override int[] LegsIDs => [ItemID.JunglePants, ItemID.AncientCobaltLeggings];

        protected override string HeadLineFallback => "Attacks deal 5 more damage";
        protected override string BodyLineFallback => "10% increased damage";
        protected override string LegsLineFallback => "10% increased movement speed";
        protected override string SetBonusLineFallback =>
            "+2 minion slots; damaging an enemy instantly fires a 4-damage poison stinger at it";

        /// <summary>毒刺伤害</summary>
        private const int StingerDamage = 4;

        public override void UpdateHead(Player player, Item item) => player.GetDamage(DamageClass.Generic).Flat += 5f;

        public override void UpdateBody(Player player, Item item) => player.GetDamage(DamageClass.Generic) += 0.10f;

        public override void UpdateLegs(Player player, Item item) => player.moveSpeed += 0.10f;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) => player.maxMinions += 2;

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsJungleStingerProj>();

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            Vector2 velocity = (target.Center - player.Center).SafeNormalize(Vector2.UnitX * player.direction) * 14f;
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithJungleEndow"), player.Center, velocity,
                ModContent.ProjectileType<GsJungleStingerProj>(), StingerDamage, 1f, player.whoAmI, target.whoAmI);
        }
    }

    /// <summary>丛林毒刺：借黄蜂仆从毒刺贴图，直飞并轻微追向锁定目标（ai[0]），命中中毒两秒</summary>
    internal class GsJungleStingerProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.HornetStinger;

        private ref float TargetIndex => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            int index = (int)TargetIndex;
            if (index >= 0 && index < Main.maxNPCs) {
                NPC target = Main.npc[index];
                if (target.active && !target.friendly) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 14f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.08f);
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 120);
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.JungleGrass,
                    Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f));
                dust.noGravity = true;
            }
        }
    }
}
