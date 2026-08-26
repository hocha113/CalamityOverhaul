using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 涡流射手重铸：经典不毁，速射加周期火箭的招牌原样可辨。<br/>
    /// [涡流扫射]：原版节奏，周期火箭获得微追踪。<br/>
    /// [涡旋重炮]：射速放慢 2.2 倍换每发 2.6 倍的充能涡旋重弹（慢重弹流），
    /// 重弹命中周期性拉开涡旋小场把附近敌人拽向弹着点。<br/>
    /// 拉扯走命中击退向心（不写 NPC 速度）
    /// </summary>
    internal class GsVortexBeater : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.VortexBeater;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch flow\n" +
            "Vortex Sweep keeps the classic storm, rockets gently home in\n" +
            "Vortex Cannon fires slow charged rounds hitting 2.6 times harder; heavy impacts tear open a pulling vortex";

        /// <summary>重炮弹私有 flag</summary>
        private const int FlagHeavy = 1;

        /// <summary>涡旋青紫</summary>
        private static readonly Color VortexTeal = new(96, 218, 190);

        /// <summary>涡旋场生成冷却（世界帧）；命中回调只在 owner 端跑，天然本地</summary>
        private uint zoneCdUntil;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeSweep", EnName = "Vortex Sweep",
            },
            new GsFireMode {
                Key = "ModeCannon", EnName = "Vortex Cannon",
                UseSpeed = 1f / 2.2f, DamageMul = 2.60f, Converge = 0.5f,
            },
        ];

        protected override void GsGunModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            //重炮档：弹速压到 0.8 倍读出「重」，击退加成
            if (mp.ModeIndex == 1) {
                velocity *= 0.8f;
                knockback *= 1.5f;
            }
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            GsGunsHardPlayer mp = Main.player[proj.owner].GetModPlayer<GsGunsHardPlayer>();
            //重炮标只给子弹类主弹；火箭在两档都保持原版身份（扫射档吃微追踪）
            bool heavy = mp.ModeIndex == 1 && proj.type != ProjectileID.VortexBeaterRocket;
            router.MarkData = PackMark(mp.ModeIndex, heavy ? FlagHeavy : 0);
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //周期火箭：微追踪（各端同源找最近目标，漂移由弹幕同步纠正）
            if (proj.type == ProjectileID.VortexBeaterRocket) {
                NPC target = FindRocketTarget(proj);
                if (target != null) {
                    float speed = Math.Max(6f, proj.velocity.Length());
                    Vector2 want = (target.Center - proj.Center).SafeNormalize(Vector2.UnitX) * speed;
                    proj.velocity = Vector2.Lerp(proj.velocity, want, 0.045f);
                }
                return;
            }
            //重炮弹飞行相：涡旋绕轨粒 + 青紫光（预算每 4 tick 一粒）
            if (MarkFlagOf(router.MarkData) != FlagHeavy || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, VortexTeal.ToVector3() * 0.35f);
            if (proj.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_GravityVortex>(proj.Center, Vector2.Zero, VortexTeal,
                    Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.NextFloat(MathHelper.TwoPi), 14f, 16);
            }
        }

        private static NPC FindRocketTarget(Projectile proj) {
            NPC best = null;
            float bestDist = 340f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(proj)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, proj.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //只在攻击方端执行：重炮命中周期性开涡旋场（45 tick 冷却防高频弹幕刷场）
            if (MarkFlagOf(router.MarkData) != FlagHeavy) {
                return;
            }
            uint now = Main.GameUpdateCount;
            if (now < zoneCdUntil) {
                return;
            }
            zoneCdUntil = now + 45;
            Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsGunsHardZoneProj>(),
                Math.Max(1, proj.damage / 8), 1f, proj.owner, 110f, 2f);
        }

        internal override void GsGunHeldReset(Player player) => zoneCdUntil = 0;
    }
}
