using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 寒霜套：单件沿用原版；套装奖励为所有攻击附带霜冻，免疫霜冻、寒冷与冰冻；
    /// 伤害敌人时 25% 概率在目标处炸开冰晶（武器面板 1.2 倍）并冻结周围敌人 1 秒；
    /// 受到伤害时释放寒潮冻结周围敌人 2 秒，每 10 秒一次
    /// </summary>
    internal class GsFrostArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.FrostHelmet];
        public override int BodyID => ItemID.FrostBreastplate;
        public override int LegsID => ItemID.FrostLeggings;
        public override bool OverridesPieceStats => false;

        protected override string SetBonusLineFallback =>
            "All attacks inflict Frostburn; immune to Frostburn, Chilled and Frozen; damaging an enemy has a 25% chance to burst ice crystals for 1.2x your weapon's damage and freeze nearby enemies for 1 second; taking damage unleashes a cold snap that freezes nearby enemies for 2 seconds, once every 10 seconds";

        /// <summary>冰晶爆裂的冻结半径</summary>
        private const float BurstFreezeRange = 110f;

        /// <summary>寒潮半径</summary>
        private const float ColdSnapRange = 300f;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.buffImmune[BuffID.Frostburn] = true;
            player.buffImmune[BuffID.Frostburn2] = true;
            player.buffImmune[BuffID.Chilled] = true;
            player.buffImmune[BuffID.Frozen] = true;
        }

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsArmorBlastProj>();

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            target.AddBuff(BuffID.Frostburn, 240);
            if (player.whoAmI != Main.myPlayer || Main.rand.Next(100) >= 25) {
                return;
            }
            SpawnBlast(player, target.Center, (int)(WeaponPanelDamage(player, hit) * 1.2f), 120f,
                "GodSmithFrostEndow", GsArmorBlastProj.Style.Ice);
            FreezeAround(target.Center, BurstFreezeRange, 60);
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            if (player.whoAmI != Main.myPlayer || !state.TryUseCooldown(this, 600)) {
                return;
            }
            FreezeAround(player.Center, ColdSnapRange, 120);
            SoundEngine.PlaySound(SoundID.Item30, player.Center);
            for (int i = 0; i < 30; i++) {
                Dust dust = Dust.NewDustPerfect(player.Center, DustID.Snow,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 9f), 80, default, Main.rand.NextFloat(1f, 1.6f));
                dust.noGravity = true;
            }
        }

        /// <summary>冻结范围内的敌人（攻击方端调用，NPC.AddBuff 自带同步）</summary>
        private static void FreezeAround(Vector2 center, float range, int frames) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || npc.dontTakeDamage || npc.immortal || npc.Center.Distance(center) > range) {
                    continue;
                }
                npc.AddBuff(BuffID.Frozen, frames);
            }
        }
    }
}
