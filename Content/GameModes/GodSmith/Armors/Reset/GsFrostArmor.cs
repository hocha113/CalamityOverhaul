using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 寒霜套 · 冰锥坠落（近战/远程混合）。单件沿用原版。<br/>
    /// 原版旗标清点：近战与远程攻击附带霜冻（frostBurn）/ +10% 近战与远程伤害 → 原样补回；无删除项。<br/>
    /// 签名：近战/远程命中 20% 概率（冷却半秒）从目标上空落下一根冰锥；受击时释放寒潮冻结周围敌人 1 秒（10 秒一次）。
    /// 另免疫霜冻、寒冷与冰冻
    /// </summary>
    internal class GsFrostArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.FrostHelmet];
        public override int BodyID => ItemID.FrostBreastplate;
        public override int LegsID => ItemID.FrostLeggings;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "Melee and ranged attacks inflict Frostbite and deal 10% more damage; you are immune to Frostburn, Chilled and Frozen; melee and ranged hits have a 20% chance to drop an icicle onto the target for 50% of the hit, and taking damage unleashes a cold snap that freezes nearby enemies for 1 second, once every 10 seconds";

        /// <summary>寒潮半径</summary>
        private const float ColdSnapRange = 300f;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.frostBurn = true;
            player.GetDamage(DamageClass.Melee) += 0.10f;
            player.GetDamage(DamageClass.Ranged) += 0.10f;
            player.buffImmune[BuffID.Frostburn] = true;
            player.buffImmune[BuffID.Frostburn2] = true;
            player.buffImmune[BuffID.Chilled] = true;
            player.buffImmune[BuffID.Frozen] = true;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            bool eligible = hit.DamageType.CountsAsClass(DamageClass.Melee) || hit.DamageType.CountsAsClass(DamageClass.Ranged);
            if (!eligible || player.whoAmI != Main.myPlayer || target.life <= 0 || target.type == NPCID.TargetDummy
                || Main.rand.Next(100) >= 20 || !state.TryUseCooldown(this, 30)) {
                return;
            }
            Vector2 spawn = GsArmorTerrainProbe.SkySpawnAbove(target.Center, Main.rand.NextFloat(-24f, 24f), 240f);
            Vector2 velocity = (target.Center - spawn).SafeNormalize(Vector2.UnitY) * 13f;
            Projectile icicle = SpawnProc(player, "GodSmithFrostEndow", spawn, velocity,
                ModContent.ProjectileType<GsFrostArmorIcicleProj>(), ProcDamage(damageDone, 0.5f, 8, 60), 3f, target.Center.Y);
            if (icicle != null) {
                icicle.DamageType = hit.DamageType;
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            if (player.whoAmI != Main.myPlayer || !state.TryUseCooldown(this, 600)) {
                return;
            }
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || npc.dontTakeDamage || npc.immortal || npc.boss || npc.Center.Distance(player.Center) > ColdSnapRange) {
                    continue;
                }
                npc.AddBuff(BuffID.Frozen, 60);
            }
            SoundEngine.PlaySound(SoundID.Item30, player.Center);
            for (int i = 0; i < 30; i++) {
                Dust dust = Dust.NewDustPerfect(player.Center, DustID.Snow,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 9f), 80, default, Main.rand.NextFloat(1f, 1.6f));
                dust.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 寒霜冰锥：借冰刺贴图，出生免地形碰撞、越过标的线（ai[0]）后恢复；坠落加速，命中霜冻，碎裂只用冰尘
    /// </summary>
    internal class GsFrostArmorIcicleProj : ModProjectile, IGsArmorProc
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceSpike;

        private ref float TargetLineY => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            GsArmorTerrainProbe.UpdateFallGate(Projectile, TargetLineY);
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.2f, 18f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.2f, 0.4f, 0.7f);
            if (Main.dedServ) {
                return;
            }
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Frost,
                0f, -Projectile.velocity.Y * 0.1f, 100, default, 1f);
            dust.noGravity = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 180);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Ice,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 1f), 80, default, 1.2f);
                dust.noGravity = false;
            }
        }
    }
}
