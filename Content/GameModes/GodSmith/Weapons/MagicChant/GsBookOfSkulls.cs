using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
            "Reforged: hits brand foes into the registry, on-beat skulls hunt branded prey;" +
            "\nat full resonance the next cast looses three skulls, three brands detonate into a bone burst";

        protected override float BaseDamageMult => 1.10f;

        protected override Color ChantColor => new(168, 196, 214);

        /// <summary>形态：骨爆</summary>
        private const float FormBurst = 10f;

        /// <summary>名录印持续 3s</summary>
        private const uint MarkDuration = 180;

        private static readonly Color SoulPale = new(190, 226, 232);
        private static readonly Color BoneGray = new(160, 158, 148);

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
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, ChantColor.ToVector3() * 0.2f);
            //飞行相：幽魂身份是魂焰曳尾与淡烟
            if (proj.timeLeft % 5 == 0) {
                PRTLoader.NewParticle<PRT_SoulFire>(proj.Center + Main.rand.NextVector2Circular(4f, 4f),
                    -proj.velocity * 0.15f, SoulPale, Main.rand.NextFloat(0.3f, 0.5f));
            }
            if (proj.timeLeft % 9 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(proj.Center, -proj.velocity * 0.05f - Vector2.UnitY * 0.3f,
                    new Color(70, 76, 88) * 0.5f, Main.rand.NextFloat(0.4f, 0.6f));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (!VaultUtils.isServer) {
                //命中相：魂焰散
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_SoulFire>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                        Main.rand.NextVector2Circular(2f, 2f) - Vector2.UnitY * 0.8f,
                        SoulPale, Main.rand.NextFloat(0.35f, 0.55f));
                }
            }
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
            //骨爆：定身一跳
            if (router.MarkData == FormBurst) {
                proj.velocity = Vector2.Zero;
                proj.alpha = 255;
                return false;
            }
            return true;
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            //骨爆自绘：灰白骨环炸开
            if (router.MarkData != FormBurst) {
                return null;
            }
            float t = 1f - proj.timeLeft / 8f;
            ShockRingDraw.Draw(Main.spriteBatch, proj.Center, 14f + 44f * t, 8f,
                Color.White, BoneGray, new Color(60, 58, 54), 0.85f * (1f - t * t),
                innerGlow: 0.3f, timeSeed: proj.identity * 0.41f);
            return false;
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            if (router.MarkData == FormBurst) {
                //骨爆余韵：骨屑魂火四散
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_SoulFire>(proj.Center + Main.rand.NextVector2Circular(10f, 10f),
                        Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY,
                        i % 2 == 0 ? SoulPale : BoneGray, Main.rand.NextFloat(0.4f, 0.6f));
                }
                return;
            }
            //余痕相：魂火余烬上飘，比骷髅活得久
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_SoulFire>(proj.Center + Main.rand.NextVector2Circular(4f, 4f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.2f),
                    SoulPale, Main.rand.NextFloat(0.3f, 0.45f));
            }
        }
    }
}
