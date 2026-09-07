using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 死灵套 · 骸骨投掷（远程，含远古死灵头盔）。单件沿用原版（三件各 +5% 远程伤害）。<br/>
    /// 原版旗标清点：套装 +10% 远程暴击 → 原样补回；无删除项。<br/>
    /// 签名：远程命中 20% 概率（冷却半秒）掷出一根骸骨；远程击杀时尸位迸出三根骸骨
    /// </summary>
    internal class GsNecroArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.NecroHelmet, ItemID.AncientNecroHelmet];
        public override int BodyID => ItemID.NecroBreastplate;
        public override int LegsID => ItemID.NecroGreaves;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "10% increased ranged critical strike chance; ranged hits have a 20% chance to hurl a bone at the target, and ranged kills burst three bones from the corpse";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetCritChance(DamageClass.Ranged) += 10f;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || !hit.DamageType.CountsAsClass(DamageClass.Ranged)
                || target.life <= 0 || Main.rand.Next(100) >= 20 || !state.TryUseCooldown(this, 30)) {
                return;
            }
            //抛物线：朝目标方向抛出并略微抬手，飞行途中受重力落回
            Vector2 toTarget = (target.Center - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            float dist = player.Center.Distance(target.Center);
            float speed = MathHelper.Clamp(dist / 28f, 8f, 14f);
            Vector2 velocity = (toTarget * speed + new Vector2(0f, -3f - dist * 0.01f)).RotatedByRandom(0.08f);
            SpawnProc(player, "GodSmithNecroEndow", player.Center, velocity,
                ModContent.ProjectileType<GsNecroBoneProj>(), ProcDamage(damageDone, 0.4f, 4, 12), 2f);
        }

        public override void OnEndowKillNPC(Player player, GodSmithArmorPlayer state, NPC target) {
            if (player.whoAmI != Main.myPlayer || target.lifeMax <= 5 || target.type == NPCID.TargetDummy) {
                return;
            }
            int damage = Math.Clamp(target.lifeMax / 10, 4, 12);
            for (int i = 0; i < 3; i++) {
                //向上扇形抛出，受重力落回
                Vector2 velocity = new Vector2(-3f + i * 3f, -7f).RotatedByRandom(0.15f);
                SpawnProc(player, "GodSmithNecroEndow", target.Center, velocity,
                    ModContent.ProjectileType<GsNecroBoneProj>(), damage, 2f);
            }
        }
    }

    /// <summary>
    /// 死灵骸骨：借骨手套骸骨贴图，受重力抛飞、随速自转，可穿透两次（最多命中三次），触地即碎；碎裂只用原版骨屑粒子
    /// </summary>
    internal class GsNecroBoneProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BoneGloveProj;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.3f, 16f);
            Projectile.rotation += Projectile.velocity.X * 0.08f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.5f, MaxInstances = 3 }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Bone,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 1f));
                dust.noGravity = false;
            }
        }
    }
}
