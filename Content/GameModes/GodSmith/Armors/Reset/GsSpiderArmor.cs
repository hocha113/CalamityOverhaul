using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 蜘蛛套 · 蛛丝缠缚（召唤）。单件沿用原版（三件各 +1 栏，+5/5/6% 召唤伤害）。<br/>
    /// 原版旗标清点：+12% 召唤伤害 → 原样补回；无删除项。<br/>
    /// 签名：仆从命中 20% 概率喷出蛛网使敌人缠缚 2 秒（移速 −30%）；受击时朝攻击者喷三道蛛网（冷却 4 秒）
    /// </summary>
    internal class GsSpiderArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.SpiderMask];
        public override int BodyID => ItemID.SpiderBreastplate;
        public override int LegsID => ItemID.SpiderGreaves;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "12% increased summon damage; minion hits have a 20% chance to spit a web that binds the enemy for 2 seconds, and taking damage spits three webs at your attacker, once every 4 seconds";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetDamage(DamageClass.Summon) += 0.12f;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || sourceProj == null || !hit.DamageType.CountsAsClass(DamageClass.Summon)
                || hit.DamageType.CountsAsClass(DamageClass.SummonMeleeSpeed) || target.life <= 0 || Main.rand.Next(100) >= 20) {
                return;
            }
            Vector2 velocity = (target.Center - sourceProj.Center).SafeNormalize(Vector2.UnitX).RotatedByRandom(0.2f) * 10f;
            SpawnProc(player, "GodSmithSpiderEndow", sourceProj.Center, velocity,
                ModContent.ProjectileType<GsSpiderArmorWebProj>(), ProcDamage(damageDone, 0.3f, 4, 20), 1f, target.whoAmI);
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            if (player.whoAmI != Main.myPlayer || !state.TryUseCooldown(this, 240)) {
                return;
            }
            NPC attacker = HurtSourceNPC(info);
            Vector2 dir = attacker != null
                ? (attacker.Center - player.Center).SafeNormalize(Vector2.UnitX * player.direction)
                : Vector2.UnitX * -player.direction;
            for (int i = -1; i <= 1; i++) {
                Vector2 velocity = dir.RotatedBy(i * 0.28f) * 11f;
                SpawnProc(player, "GodSmithSpiderEndow", player.Center, velocity,
                    ModContent.ProjectileType<GsSpiderArmorWebProj>(), 10, 2f, attacker?.whoAmI ?? -1);
            }
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = 0.3f }, player.Center);
        }
    }

    /// <summary>蛛网：借黑寡妇吐网贴图，直飞并轻微追向锁定目标（ai[0]），命中缠缚；拖蛛丝尘</summary>
    internal class GsSpiderArmorWebProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WebSpit;

        private ref float TargetIndex => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            int index = (int)TargetIndex;
            if (index >= 0 && index < Main.maxNPCs) {
                NPC target = Main.npc[index];
                if (target.active && !target.friendly) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 11f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.07f);
                }
            }
            Projectile.rotation += 0.2f;
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Web, 0f, 0f, 120, default, 0.9f);
                dust.noGravity = true;
                dust.velocity *= 0.2f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<GsWebSlowBuff>(), 120);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Web,
                    Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f), 100, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
