using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 海盗法杖「分赃令」：海盗团成船员纵队跟行；每次劫掠命中积攒赃金，
    /// 签名 = 集结令下赃金满 6 份即在旗点开箱分赃，五枚鎏金臼弹抛物洒向近敌
    /// （<see cref="GsPiratePlunderProj"/>，各 0.7×，点金指附体，间隔 90 帧）；
    /// 增强层 = 劫掠命中溅掠金火花。公认弱势武器，底伤按公约放宽（注释见下）
    /// </summary>
    internal class GsPirateStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.PirateStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Plunder Writ: every pirate strike stashes loot; under the rally order, six shares crack the chest open at the flag and five gilded mortar coins rain toward nearby foes, marking them with the Midas touch";

        private static readonly Color CoinGold = new(255, 210, 96);
        private static readonly Color CoinDeep = new(178, 122, 32);

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Column,
            Spacing = 34f,
            Grounded = true,
            DriftMul = 0.8f,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes
            => [ProjectileID.OneEyedPirate, ProjectileID.SoulscourgePirate, ProjectileID.PirateCaptain];

        //==================== 赃金记账（owner 命中路径独占消费） ====================

        /// <summary>已积攒赃金（命中 +1，封顶 10）</summary>
        private int lootStacks;
        private uint plunderReadyTick;

        /// <summary>海盗杖为公认弱势召唤，底伤按公约弱势条款放宽到 1.10</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        public override void GsUseAnimation(Item item, Player player)
            => GsMinionCastMotion.CastBurst(player, CoinGold, CoinDeep);

        //==================== 签名：赃金与分赃 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //臼弹自身命中不再积赃（防自喂）
            if (proj.type == ModContent.ProjectileType<GsPiratePlunderProj>()) {
                return;
            }
            lootStacks = Math.Min(lootStacks + 1, 10);
            //掠金火花：劫掠得手的即时反馈
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    -Vector2.UnitY.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f))
                        * Main.rand.NextFloat(1.2f, 2.6f),
                    CoinGold, Main.rand.NextFloat(0.18f, 0.28f))?.Configure(true, Main.rand.Next(10, 16));
            }

            //分赃：集结令 + 旗点在场 + 赃金满 6 份
            if (lootStacks < 6 || Main.GameUpdateCount < plunderReadyTick
                || MinionDoctrine.GetCommand(proj.owner) != MinionDoctrine.CommandRally
                || !MinionDoctrine.TryGetRallyPoint(proj.owner, out Vector2 flagPoint)) {
                return;
            }
            plunderReadyTick = Main.GameUpdateCount + 90;
            lootStacks -= 6;

            //臼弹朝旗点近敌一侧偏斜，无敌则纯上抛
            float bias = 0f;
            NPC near = FindNearHostile(flagPoint, 500f);
            if (near != null) {
                bias = MathHelper.Clamp((near.Center.X - flagPoint.X) / 500f, -1f, 1f) * 2.6f;
            }
            for (int i = 0; i < 5; i++) {
                Vector2 vel = new(bias + (i - 2) * 1.5f, -Main.rand.NextFloat(8.5f, 11f));
                Projectile.NewProjectile(proj.GetSource_FromAI(),
                    flagPoint - new Vector2(0f, 18f), vel,
                    ModContent.ProjectileType<GsPiratePlunderProj>(),
                    (int)(proj.damage * 0.7f), 4f, proj.owner);
            }
        }

        /// <summary>旗点范围内最近的可追猎敌人（owner 路径调用）</summary>
        private static NPC FindNearHostile(Vector2 point, float radius) {
            NPC best = null;
            float bestDist = radius;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = npc.Center.Distance(point);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }
    }
}
