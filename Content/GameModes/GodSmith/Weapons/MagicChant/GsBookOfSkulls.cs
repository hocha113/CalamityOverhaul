using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 骷髅头魔书重铸：亡者点名。命中挂 3s「名录」印，正拍骷髅自动追押
    /// 名录目标（每帧最多转 8 度）；满层强化「三头连环」：品字三骷髅（各 0.85 倍），
    /// 同一目标集齐 3 印即起骨爆（1.5 倍）。材质身份：幽魂。<br/>
    /// 名录印是攻击方本地量（命中钩子只在攻击方端执行），追踪目标经 MarkData2
    /// 随生成包过线，各端一致转向；骨爆是真弹幕，全端可见
    /// </summary>
    internal class GsBookOfSkulls : GsChantScheme
    {
        public override int TargetItemID => ItemID.BookofSkulls;

        protected override string GsDescFallback =>
            "Reforged: hits brand foes into the registry, on-beat skulls hunt branded prey;\nat full resonance the next cast looses three skulls, three brands detonate into a bone burst";
        protected override float BaseDamageMult => 1.10f;

        /// <summary>形态：骨爆</summary>
        private const float FormBurst = 10f;

        /// <summary>名录印持续 3s</summary>
        private const uint MarkDuration = 180;

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //三头连环：品字三骷髅，全部携带强化标与各自的追押目标
            int skullDamage = Math.Max(1, (int)(damage * 0.85f));
            for (int i = 0; i < 3; i++) {
                float off = i switch { 0 => 0f, 1 => MathHelper.ToRadians(10f), _ => MathHelper.ToRadians(-10f) };
                Projectile.NewProjectile(source, position, velocity.RotatedBy(off),
                    type, skullDamage, knockback, player.whoAmI);
            }
            return false;
        }

        protected override void ChantProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router, GsChantPlayer chant) {
            //正拍骷髅锁定名录目标：owner 端从带印敌怪里选最近者，whoAmI 随生成包过线
            if (router.MarkData is not (FormOnBeat or FormEmpower)) {
                return;
            }
            int marked = -1;
            float bestDist = 900f * 900f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || !npc.GetGlobalNPC<GsChantGlobalNPC>().SkullMarkActive) {
                    continue;
                }
                float d = Vector2.DistanceSquared(npc.Center, proj.Center);
                if (d < bestDist) {
                    bestDist = d;
                    marked = npc.whoAmI;
                }
            }
            router.MarkData2 = marked;
            if (marked >= 0) {
                proj.netUpdate = true;
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //正拍骷髅追押：目标位置各端同步，转向确定一致
            if (router.MarkData is FormOnBeat or FormEmpower && router.MarkData2 >= 0f) {
                int who = (int)router.MarkData2;
                if (who < Main.maxNPCs) {
                    NPC target = Main.npc[who];
                    if (target.active && target.CanBeChasedBy()) {
                        SteerTowards(proj, target.Center, MathHelper.ToRadians(8f));
                    }
                }
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (!proj.IsOwnedByLocalPlayer() || router.MarkData == FormBurst) {
                return;
            }
            //亡者点名：叠印，三印起骨爆
            GsChantGlobalNPC mark = target.GetGlobalNPC<GsChantGlobalNPC>();
            mark.AddSkullMark(MarkDuration);
            if (mark.SkullMarkStacks < 3) {
                return;
            }
            mark.ClearSkullMark();
            QueueForm(Main.player[proj.owner], FormBurst);
            int idx = Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, Vector2.Zero,
                proj.type, Math.Max(1, (int)(proj.damage * 1.5f)), proj.knockBack, proj.owner);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Projectile burst = Main.projectile[idx];
                burst.timeLeft = 8;
                burst.Resize(110, 110);
                burst.netUpdate = true;
            }
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            //骨爆：定身一跳（本体沿用原版骷髅贴图默认绘制）
            if (router.MarkData == FormBurst) {
                proj.velocity = Vector2.Zero;
                return false;
            }
            return true;
        }
    }
}
