using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    /// <summary>
    /// 脉冲弓（重铸 110%）：弹墙保留，但衰减反转成奖励：每次弹墙 +10% 伤（盖 5 层）。
    /// 齐射成三道平行脉冲轨；「导体」：有标敌 80px 内经过的脉冲折射咬向其（每束一次）；
    /// 处决电涌链电两名近敌（60% each）。
    /// 期望：齐射 +0.8/17.7 ≈ +4.5%，弹墙技巧 ≈ +3%，链电 ≈ +2.5%
    /// </summary>
    internal class GsPulseBow : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.PulseBow;

        protected override string GsDescFallback =>
            "Reforged: each wall bounce now grants +10% damage (up to 5); volley charge looses 3 parallel pulse tracks, one ammo per volley\nBranded foes bend passing pulses toward them; execution chains a surge to 2 nearby foes";

        protected override int VolleyCount => 3;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Line;
        protected override float SpreadPx => 24f;
        protected override float ChargePerShot => 6f;
        protected override float SideArrowMul => 0.6f;
        protected override Color TrailColor => new(90, 230, 200);

        protected override int VolleyProjType(int ammoProjType) => ProjectileID.PulseBolt;

        /// <summary>每束脉冲的本端状态：反弹计数、上帧速度、出生基伤、折射闩</summary>
        private class PulseState
        {
            public int Bounces;
            public Vector2 PrevVel;
            public int BaseDamage;
            public bool Initialized;
            public bool Refracted;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            base.GsProjPostAI(proj, router);
            if (proj.type != ProjectileID.PulseBolt) {
                return;
            }
            PulseState st = router.GetOrCreateState<PulseState>();
            if (!st.Initialized) {
                st.Initialized = true;
                st.BaseDamage = proj.damage;
                st.PrevVel = proj.velocity;
                return;
            }

            //反弹检测：任一轴瞬间反号（阈值挡过零渐变误判）。伤害只在 owner 端裁决
            bool bounced =
                (Math.Sign(proj.velocity.X) != Math.Sign(st.PrevVel.X) && MathF.Abs(st.PrevVel.X) > 2f)
                || (Math.Sign(proj.velocity.Y) != Math.Sign(st.PrevVel.Y) && MathF.Abs(st.PrevVel.Y) > 2f);
            if (bounced && st.Bounces < 5) {
                st.Bounces++;
                if (proj.IsOwnedByLocalPlayer()) {
                    proj.damage = (int)(st.BaseDamage * (1f + 0.10f * st.Bounces));
                }
                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_Light>(proj.Center, Vector2.Zero, TrailColor, 0.13f)?.Configure(8, 0.85f);
                }
            }

            //导体折射：owner 端一次性转向 + netUpdate 矫正远端
            if (!st.Refracted && proj.IsOwnedByLocalPlayer()) {
                NPC marked = GsHuntMarkNPC.FindNearestMarked(proj.Center, 80f);
                if (marked != null) {
                    st.Refracted = true;
                    proj.velocity = (marked.Center - proj.Center).SafeNormalize(Vector2.UnitX)
                        * proj.velocity.Length();
                    proj.netUpdate = true;
                }
            }
            st.PrevVel = proj.velocity;
        }

        /// <summary>电涌链电：目标周围 300px 内至多两名其他敌各吃一记 60% 电爆，电弧连线（攻击方端演出）</summary>
        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            int chained = 0;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (chained >= 2) {
                    break;
                }
                if (npc.whoAmI == target.whoAmI || !GsHuntMarkNPC.CanMark(npc)
                    || npc.Center.Distance(target.Center) > 300f) {
                    continue;
                }
                chained++;
                SpawnBurst(player, npc.Center, (int)(proj.damage * 0.6f), 56f, GsVolleyBurstProj.ThemeVolt);
                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_SkyBolt>(target.Center, Vector2.Zero, TrailColor, 1f)
                        ?.Configure(target.Center, npc.Center, 20);
                }
            }
        }
    }
}
