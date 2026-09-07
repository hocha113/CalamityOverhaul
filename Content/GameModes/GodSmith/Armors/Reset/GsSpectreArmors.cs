using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>幽魂套两顶头盔的公共层：同一身袍与裤，按头盔分治愈与伤害两路；单件沿用原版</summary>
    internal abstract class GsSpectreArmorScheme : GsResetArmorScheme
    {
        public override int BodyID => ItemID.SpectreRobe;
        public override int LegsID => ItemID.SpectrePants;
        public sealed override bool OverridesPieceStats => false;
    }

    /// <summary>
    /// 幽魂兜帽（治疗）：魔法命中的治疗灵球照旧，魔法伤害 −40%（原版取舍原样），最大魔力 +100、魔力再生提升；
    /// 受到伤害时 40% 概率立即回复 10% 最大生命，每 8 秒一次。<br/>
    /// 原版旗标清点：ghostHeal / −40% 魔伤 → 原样补回；无删除项
    /// </summary>
    internal class GsSpectreHoodArmor : GsSpectreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.SpectreHood];

        protected override string SetBonusLineFallback =>
            "Magic hits release healing orbs, but magic damage is reduced by 40%; 100 more maximum mana and faster mana regeneration; taking damage has a 40% chance to instantly heal 10% of your maximum life, once every 8 seconds";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.ghostHeal = true;
            player.GetDamage(DamageClass.Magic) -= 0.40f;
            player.statManaMax2 += 100;
            player.manaRegenBonus += 30;
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            if (player.whoAmI != Main.myPlayer || Main.rand.Next(100) >= 40 || state.IsOnCooldown(this)) {
                return;
            }
            state.TryUseCooldown(this, 480);
            int heal = Math.Max(1, player.statLifeMax2 / 10);
            player.Heal(heal);
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f }, player.Center);
        }
    }

    /// <summary>
    /// 幽魂面具（输出）：魔法命中的迷失之魂照旧，魔法伤害 +10%、魔法暴击 +10%、魔耗 −15%；
    /// 魔法暴击时 30% 概率再射出一枚幽魂之火追向目标（魔法伤害，= 本次命中 60%）。<br/>
    /// 原版旗标清点：ghostHurt → 原样补回；无删除项
    /// </summary>
    internal class GsSpectreMaskArmor : GsSpectreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.SpectreMask];

        protected override string SetBonusLineFallback =>
            "Magic hits release lost souls; 10% increased magic damage and critical strike chance, 15% reduced mana usage; magic critical strikes have a 30% chance to loose an extra spectral flame that homes in for 60% of the hit";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.ghostHurt = true;
            player.GetDamage(DamageClass.Magic) += 0.10f;
            player.GetCritChance(DamageClass.Magic) += 10f;
            player.manaCost -= 0.15f;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || !hit.Crit || !hit.DamageType.CountsAsClass(DamageClass.Magic)
                || target.life <= 0 || Main.rand.Next(100) >= 30) {
                return;
            }
            Vector2 velocity = Main.rand.NextVector2Unit() * 6f;
            SpawnProc(player, "GodSmithSpectreEndow", player.Center, velocity,
                ModContent.ProjectileType<GsSpectreSoulProj>(), ProcDamage(damageDone, 0.6f, 20, 120), 1f, target.whoAmI);
        }
    }

    /// <summary>幽魂之火：借原版迷失之魂贴图，散开后追向锁定目标（ai[0]）；轨迹撒幽魂法杖粒子</summary>
    internal class GsSpectreSoulProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SpectreWrath;

        private ref float TargetIndex => ref Projectile.ai[0];

        private ref float Life => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Math.Max(1, Main.projFrames[ProjectileID.SpectreWrath]);
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
            if (target == null || !target.active || target.friendly) {
                target = NearestTarget();
            }
            if (Life > 10f && target != null) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 12f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.08f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
            Lighting.AddLight(Projectile.Center, 0.2f, 0.5f, 0.6f);
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.SpectreStaff,
                    0f, 0f, 100, default, 1f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        private NPC NearestTarget() {
            NPC best = null;
            float bestDist = 700f;
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
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.35f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.SpectreStaff,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 1.2f);
                dust.noGravity = true;
            }
        }
    }
}
