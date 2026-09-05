using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing
{
    /// <summary>
    /// 雷火组中间类:连投窗内引信 -20%。
    /// 引信帧数经 MarkData2 随生成包过线,各端首帧统一重设 timeLeft,预测一致;
    /// 爆炸伤害与判定全部保留原版(增强只做路由层)。AoE 返还:单次爆炸命中 ≥3 敌返还一件
    /// </summary>
    internal abstract class GsGrenadeLikeScheme : GsThrowScheme
    {
        /// <summary>本武器的主雷弹幕类型(承签的火花/蜂群不吃雷逻辑)</summary>
        protected abstract int GrenadeProjType { get; }
        /// <summary>原版引信帧数</summary>
        protected virtual int BaseFuse => 180;

        protected override bool AoERefund => true;

        protected override void GsThrowOnSpawn(Projectile proj, GodSmithProjRouter router, GsThrowProjState st) {
            int fuse = BaseFuse;
            if (Main.player[proj.owner].GetModPlayer<GsThrowPlayer>().ComboFor(TargetItemID) > 0) {
                //连投窗内:引信 -20%
                fuse = (int)(fuse * 0.8f);
            }
            router.MarkData2 = fuse == BaseFuse ? 0f : fuse;
            GsGrenadeOnSpawn(proj, router, st);
        }

        /// <summary>雷子类的出生扩展</summary>
        protected virtual void GsGrenadeOnSpawn(Projectile proj, GodSmithProjRouter router, GsThrowProjState st) { }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != GrenadeProjType || !router.IsMarked) {
                return true;
            }
            GsThrowProjState st = router.GetOrCreateState<GsThrowProjState>();
            if (!st.FuseSet) {
                //各端首帧统一重设引信(MarkData2 已随生成包过线)
                st.FuseSet = true;
                if (router.MarkData2 > 0f) {
                    proj.timeLeft = (int)router.MarkData2;
                }
            }
            return true;
        }
    }

    /// <summary>手榴弹:连投窗内引信更短;一炸三敌返还一枚</summary>
    internal class GsGrenade : GsGrenadeLikeScheme
    {
        public override int TargetItemID => ItemID.Grenade;
        protected override int GrenadeProjType => ProjectileID.Grenade;
        protected override string GsDescFallback =>
            "Reforged: hitting 3+ foes in one blast refunds a grenade; fuses burn 20% faster inside your combo window";
        protected override float NoConsumeChance => 0.10f;
        protected override float DamageMul => 1.05f;
    }

    /// <summary>粘性手榴弹:爆炸给敌挂蚀灼标记,已标记的敌再吃你的爆炸 +30% 且必暴</summary>
    internal class GsStickyGrenade : GsGrenadeLikeScheme
    {
        public override int TargetItemID => ItemID.StickyGrenade;
        protected override int GrenadeProjType => ProjectileID.StickyGrenade;
        protected override string GsDescFallback =>
            "Reforged: blasts brand foes for 3s; branded foes take 30% more from your next blast and it always crits\nHitting 3+ foes refunds one";
        protected override float NoConsumeChance => 0.10f;
        protected override float DamageMul => 1.06f;

        protected override void GsThrowModifyHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (proj.type != GrenadeProjType) {
                return;
            }
            GsThrowGlobalNPC gn = target.GetGlobalNPC<GsThrowGlobalNPC>();
            if (Main.GameUpdateCount <= gn.StickyMarkUntil) {
                modifiers.FinalDamage *= 1.3f;
                modifiers.SetCrit();
            }
        }

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.owner != Main.myPlayer || proj.type != GrenadeProjType || target.friendly) {
                return;
            }
            //蚀灼标记:攻击方本地量,先结算后刷新(本次爆炸吃的是上一次的标)
            target.GetGlobalNPC<GsThrowGlobalNPC>().StickyMarkUntil = Main.GameUpdateCount + 180;
        }
    }

    /// <summary>弹力手榴弹:每次落地弹跳爆伤 +12%(叠 3);跳满 3 次后炸中敌人必返还</summary>
    internal class GsBouncyGrenade : GsGrenadeLikeScheme
    {
        public override int TargetItemID => ItemID.BouncyGrenade;
        protected override int GrenadeProjType => ProjectileID.BouncyGrenade;
        protected override string GsDescFallback =>
            "Reforged: each bounce charges the blast +12%, up to 3 stacks\nA grenade that bounced 3 times refunds itself when its blast finds a foe";
        protected override float NoConsumeChance => 0.10f;
        protected override float DamageMul => 1.05f;

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != GrenadeProjType || !router.IsMarked) {
                return;
            }
            //弹跳检测:下坠转上升即一跳(速度各端同步,计数近似一致;结算读 owner 本地值)
            GsThrowProjState st = router.GetOrCreateState<GsThrowProjState>();
            if (st.CustomF > 1f && proj.velocity.Y < -0.5f) {
                st.Bounces++;
            }
            st.CustomF = proj.velocity.Y;
        }

        protected override void GsThrowModifyHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (proj.type != GrenadeProjType || router.LocalState is not GsThrowProjState st || st.Bounces <= 0) {
                return;
            }
            modifiers.FinalDamage *= 1f + 0.12f * System.Math.Min(3, st.Bounces);
        }

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            //技巧奖励:跳满 3 次的雷炸中敌人,整颗返还(每雷一次,免耗投掷不参与)
            if (proj.owner != Main.myPlayer || proj.type != GrenadeProjType || !st.IsPrimary
                || st.FreeThrow || st.Latch || st.Bounces < 3 || target.type == NPCID.TargetDummy) {
                return;
            }
            st.Latch = true;
            RefundOne(Main.player[proj.owner], target.Center);
        }
    }

    /// <summary>蜂弹:爆炸多放两只蜂(全场蜂 ≤12 共池);被蜂蜇过的敌短时受后续蜂群 +25%</summary>
    internal class GsBeenade : GsGrenadeLikeScheme
    {
        public override int TargetItemID => ItemID.Beenade;
        protected override int GrenadeProjType => ProjectileID.Beenade;
        protected override string GsDescFallback =>
            "Reforged: bursts release 2 extra bees; stung foes take 25% more from your bees for 4s\nHitting 3+ foes in one burst refunds one";
        protected override float NoConsumeChance => 0.10f;

        private static bool IsBee(int type) => type == ProjectileID.Bee || type == ProjectileID.GiantBee;

        protected override void GsThrowModifyHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //蜂怒:先结算后刷新,首只蜂点火后续蜂吃增伤
            if (IsBee(proj.type)
                && Main.GameUpdateCount <= target.GetGlobalNPC<GsThrowGlobalNPC>().BeeRageUntil) {
                modifiers.FinalDamage *= 1.25f;
            }
        }

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.owner != Main.myPlayer || !IsBee(proj.type) || target.friendly) {
                return;
            }
            target.GetGlobalNPC<GsThrowGlobalNPC>().BeeRageUntil = Main.GameUpdateCount + 240;
        }

        protected override void GsThrowOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //爆裂放蜂 +2:owner 权威,与蜂膝弓同查一个计数天然共池
            if (proj.owner != Main.myPlayer || proj.type != GrenadeProjType
                || router.LocalState is not GsThrowProjState { IsPrimary: true }) {
                return;
            }
            Player owner = Main.player[proj.owner];
            for (int i = 0; i < 2; i++) {
                if (owner.ownedProjectileCounts[ProjectileID.Bee]
                    + owner.ownedProjectileCounts[ProjectileID.GiantBee] >= 12) {
                    break;
                }
                Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center,
                    Main.rand.NextVector2Circular(4f, 4f), owner.beeType(),
                    owner.beeDamage(proj.damage), owner.beeKB(0f), proj.owner);
            }
        }
    }

    /// <summary>快乐手榴弹:彩带爆让非 Boss 敌短暂迷向乱走;暴击返还,纯趣味强化</summary>
    internal class GsHappyGrenade : GsGrenadeLikeScheme
    {
        public override int TargetItemID => ItemID.PartyGirlGrenade;
        protected override int GrenadeProjType => ProjectileID.PartyGirlGrenade;
        protected override string GsDescFallback =>
            "Reforged: the confetti blast leaves non-boss foes wandering confused for 1s\nCrits refund one grenade; the party never has to end";
        protected override float NoConsumeChance => 0.15f;
        protected override bool AoERefund => false;
        protected override bool CritRefund => true;
        protected override float DamageMul => 1.30f;

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.owner != Main.myPlayer || proj.type != GrenadeProjType || target.boss || target.friendly) {
                return;
            }
            //迷向:原版混乱对 NPC 有实装,AddBuff 自动同步
            target.AddBuff(BuffID.Confused, 60);
        }
    }
}
