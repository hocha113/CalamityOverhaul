using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    /// <summary>
    /// 暗影焰弓（重铸 112%）：原版任意箭转暗影焰箭保留。齐射成「品字交叉」：
    /// 三缕焰箭自品字起点射向准星交汇再散开（0.75 each）。
    /// 蚀影：有标敌被任意打标箭命中，追加一缕 25% 追踪影火（15 帧节流）；处决影爆 90%。
    /// 期望：齐射 +1.25/15.3 ≈ +8%，蚀影 ≈ +4%，处决 ≈ +2%
    /// </summary>
    internal class GsShadowflameBow : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.ShadowFlameBow;

        protected override string GsDescFallback =>
            "Reforged: volley charge looses 3 shadowflame arrows crossing at the cursor, one ammo per volley\nBranded foes struck by any arrow draw a homing shadowflame wisp; execution erupts in shadow";

        protected override int VolleyCount => 3;
        protected override float ChargePerShot => 7f;
        protected override float SideArrowMul => 0.75f;
        protected override Color TrailColor => new(170, 90, 220);

        /// <summary>追踪影火角色</summary>
        private const int RoleShadowWisp = GsVolleyRole.CustomBase;

        /// <summary>上次放出影火的世界帧（owner 命中钩子消费）</summary>
        private uint lastWispTick;

        /// <summary>品字交叉：上、左下、右下三个起点，全部射向准星点交汇后自然散开</summary>
        protected override void FireVolley(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int count) {
            Vector2 aim = Main.MouseWorld;
            float speed = velocity.Length();
            int dmg = (int)(damage * SideArrowMul);
            Vector2[] offsets = [new(0f, -26f), new(-24f, 18f), new(24f, 18f)];
            for (int i = 0; i < count && i < offsets.Length; i++) {
                Vector2 pos = position + offsets[i];
                Vector2 vel = (aim - pos).SafeNormalize(Vector2.UnitX) * speed;
                SpawnTagged(player, source, pos, vel, ProjectileID.ShadowFlameArrow, dmg,
                    knockback * 0.7f, GsVolleyRole.VolleySide, i);
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            //蚀影：命中后目标仍带标（未被本次处决清空）才追影火
            int role = (int)router.MarkData;
            if (role == RoleShadowWisp || !GsHuntMarkNPC.CanMark(target)) {
                return;
            }
            if (target.GetGlobalNPC<GsHuntMarkNPC>().Stacks <= 0 || Main.GameUpdateCount - lastWispTick < 15) {
                return;
            }
            lastWispTick = Main.GameUpdateCount;
            Player owner = Main.player[proj.owner];
            Vector2 from = target.Center + new Vector2(Main.rand.NextFloat(-70f, 70f), -80f);
            Vector2 vel = (target.Center - from).SafeNormalize(Vector2.UnitY) * 9f;
            SpawnTagged(owner, owner.GetSource_ItemUse(owner.HeldItem), from, vel,
                ProjectileID.ShadowFlameArrow, (int)(proj.damage * 0.25f), 0.5f,
                RoleShadowWisp, target.whoAmI);
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            base.GsProjPostAI(proj, router);
            //影火追踪：目标索引随 MarkData2 过线，各端转向确定一致
            if ((int)router.MarkData != RoleShadowWisp) {
                return;
            }
            int idx = (int)router.MarkData2;
            NPC target = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
            if (target == null || !target.active) {
                return;
            }
            float current = proj.velocity.ToRotation();
            float desired = (target.Center - proj.Center).ToRotation();
            proj.velocity = current.AngleTowards(desired, MathHelper.ToRadians(6f)).ToRotationVector2()
                * proj.velocity.Length();
        }

        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone)
            => SpawnBurst(player, target.Center, (int)(proj.damage * 0.9f), 90f, GsVolleyBurstProj.ThemeShadow);
    }
}
