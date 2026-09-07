using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 神圣套 · 审判之星（四种头盔与远古件可混搭）。单件沿用原版。<br/>
    /// 原版旗标清点：圣光闪避（onHitDodge，30 秒冷却）与兜帽变体 +2 栏 → 放行原版套装奖励（KeepsVanillaSetBonus）；无删除项。<br/>
    /// 签名：原版闪避触发的瞬间向四周放出 6 颗圣光星，闪避后 5 秒内伤害与暴击 +10%
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
        public override bool KeepsVanillaSetBonus => true;

        protected override string SetBonusLineFallback =>
            "Holy Protection works as before (the hood still grants 2 minion slots); when it dodges a hit, 6 holy stars burst out around you and for the next 5 seconds your damage and critical strike chance are increased by 10%";

        /// <summary>闪避后增益窗口帧数</summary>
        private const int WindowFrames = 300;

        /// <summary>圣光星伤害</summary>
        private const int StarDamage = 90;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            GsHallowedArmorPlayer hallowed = player.GetModPlayer<GsHallowedArmorPlayer>();
            if (Main.GameUpdateCount >= hallowed.WindowEnd) {
                return;
            }
            player.GetDamage(DamageClass.Generic) += 0.10f;
            player.GetCritChance(DamageClass.Generic) += 10f;
            if (!Main.dedServ && Main.rand.NextBool(5)) {
                Dust dust = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2CircularEdge(22f, 30f), DustID.Enchanted_Gold,
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.9f)), 120, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            GsHallowedArmorPlayer hallowed = player.GetModPlayer<GsHallowedArmorPlayer>();
            bool dodge = player.shadowDodge;
            bool consumed = hallowed.PrevDodge && !dodge && !player.dead;
            hallowed.PrevDodge = dodge;
            if (!consumed) {
                return;
            }
            //闪避被消耗的下降沿：各端都能看到旗标变化，星只由 owner 生成
            hallowed.WindowEnd = Main.GameUpdateCount + WindowFrames;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item4, player.Center);
                for (int i = 0; i < 20; i++) {
                    Dust dust = Dust.NewDustPerfect(player.Center, DustID.Enchanted_Gold,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f), 100, default, Main.rand.NextFloat(1f, 1.5f));
                    dust.noGravity = true;
                }
            }
            for (int i = 0; i < 6; i++) {
                Vector2 velocity = (MathHelper.TwoPi * i / 6f).ToRotationVector2() * 10f;
                SpawnProc(player, "GodSmithHallowedEndow", player.Center, velocity,
                    ModContent.ProjectileType<GsHallowedStarProj>(), StarDamage, 4f);
            }
        }
    }

    /// <summary>神圣套的私有状态：上一帧的闪避旗标（找下降沿）与增益窗口结束帧；不存档不联网</summary>
    internal class GsHallowedArmorPlayer : ModPlayer
    {
        internal bool PrevDodge;
        internal uint WindowEnd;
    }

    /// <summary>圣光星：借星怒之星贴图，径向飞出后轻微追向最近敌人，可穿透一次；轨迹撒附魔金粒子</summary>
    internal class GsHallowedStarProj : ModProjectile, IGsArmorProc
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
