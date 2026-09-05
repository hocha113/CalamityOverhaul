using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows
{
    /// <summary>
    /// 矿弓组共享：三级蓄力 + 金属淬火质变。T2 起箭头淬火（命中向 40px 内小溅射 15% 伤），
    /// T3 按矿对追加个性 rider（伴射/冲压/弹射/王权）
    /// </summary>
    internal abstract class GsOreBowScheme : GsChargeBowScheme
    {
        internal override float DpsTarget => 1.05f;

        internal sealed override void OnQualityHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) {
            //金属淬火：小范围溅射（攻击方端）
            SplashDamage(Main.player[proj.owner], proj, target, 40f, Math.Max(1, (int)(damageDone * 0.15f)));
            if (tier >= 3) {
                OreRiderHit(proj, target, hit, damageDone, router, tier);
            }
        }

        /// <summary>逐矿个性 rider：T3 主箭命中时（攻击方端）</summary>
        internal virtual void OreRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) { }

        //==================== 组内公用件 ====================

        /// <summary>弹射状态包（owner 端本地，剩余次数）</summary>
        protected class BounceState
        {
            public int Left = -1;
        }

        /// <summary>
        /// 圣辉弹射：命中后把箭转向 200px 内最近的其他敌人，每跳 -20% 伤（owner 端权威，velocity 过 netUpdate）。
        /// 返回是否为最后一跳的落点（钨弓末跳小爆用）
        /// </summary>
        protected static bool TryRicochet(Projectile proj, NPC target, GodSmithProjRouter router, int maxBounces) {
            BounceState state = router.GetOrCreateState<BounceState>();
            if (state.Left < 0) {
                state.Left = maxBounces;
            }
            if (state.Left <= 0) {
                return true;
            }
            NPC next = FindNearestEnemy(target.Center, 200f, proj, target.whoAmI);
            if (next == null) {
                return true;
            }
            state.Left--;
            proj.damage = Math.Max(1, (int)(proj.damage * 0.8f));
            proj.velocity = (next.Center - proj.Center).SafeNormalize(Vector2.UnitX) * proj.velocity.Length();
            proj.ai[0] = 0f;//重置原版箭的下坠计时，弹射段直飞
            proj.netUpdate = true;
            return state.Left <= 0;
        }

        /// <summary>
        /// 王权掉币（owner 端命中钩子）：假人/雕像怪不结算，币值护栏远低于 1 银。<br/>
        /// 联机下客户端 Item.NewItem 只落本地 400 槽且不广播（TML 源 Item.cs：非服务器不挑真槽/仅服务器发包），
        /// 需补发 SyncItem 走原版客户端掷物通道：服务器收到 400 槽包后分配真槽并全网转播
        /// </summary>
        protected static void DropCoins(Projectile proj, NPC target, int min, int max) {
            if (!ValidRiderTarget(target)) {
                return;
            }
            int stack = Main.rand.Next(min, max + 1);
            int idx = Item.NewItem(proj.GetSource_FromThis(), target.Hitbox, ItemID.CopperCoin, stack, noGrabDelay: true);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendData(MessageID.SyncItem, number: idx, number2: 1f);
            }
        }
    }

    /// <summary>铜弓：T3 出膛伴射 1 支 ±8° 副箭（60% 伤，免费不耗弹）</summary>
    internal class GsCopperBow : GsOreBowScheme
    {
        public override int TargetItemID => ItemID.CopperBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw with quenched arrowheads. An overdrawn shot looses a free side arrow";
        internal override void OnLoose(Player player, Item item, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int tier) {
            if (tier < 3) {
                return;
            }
            float rot = MathHelper.ToRadians(8f) * (Main.rand.NextBool() ? 1f : -1f);
            StampNext(tier, KindSideArrow);
            Projectile.NewProjectile(source, position, velocity.RotatedBy(rot), type,
                Math.Max(1, (int)(damage * 0.6f)), knockback * 0.6f, player.whoAmI);
        }
    }

    /// <summary>锡弓：同铜弓</summary>
    internal class GsTinBow : GsOreBowScheme
    {
        public override int TargetItemID => ItemID.TinBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw with quenched arrowheads. An overdrawn shot looses a free side arrow in a denser shower of sparks";
        internal override void OnLoose(Player player, Item item, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int tier) {
            if (tier < 3) {
                return;
            }
            float rot = MathHelper.ToRadians(8f) * (Main.rand.NextBool() ? 1f : -1f);
            StampNext(tier, KindSideArrow);
            Projectile.NewProjectile(source, position, velocity.RotatedBy(rot), type,
                Math.Max(1, (int)(damage * 0.6f)), knockback * 0.6f, player.whoAmI);
        }
    }

    /// <summary>铁弓：T3 冲压箭，击退 ×1.8，非 boss 命中顿挫定住一瞬</summary>
    internal class GsIronBow : GsOreBowScheme
    {
        public override int TargetItemID => ItemID.IronBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw with quenched arrowheads. Overdrawn shots slam with crushing knockback, briefly staggering lesser foes";
        internal override void ModifyArrowHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, int tier, int kind) {
            if (tier >= 3 && kind == KindMain) {
                modifiers.Knockback *= 1.8f;
            }
        }

        internal override void OreRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) {
            if (!target.boss && ValidRiderTarget(target)) {
                target.velocity = Vector2.Zero;
            }
        }
    }

    /// <summary>铅弓：铁弓冲压 + 铅毒（中毒 2s）</summary>
    internal class GsLeadBow : GsOreBowScheme
    {
        public override int TargetItemID => ItemID.LeadBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw with quenched arrowheads. Overdrawn shots slam and leave lead poisoning in the wound";
        internal override void ModifyArrowHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, int tier, int kind) {
            if (tier >= 3 && kind == KindMain) {
                modifiers.Knockback *= 1.8f;
            }
        }

        internal override void OreRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) {
            if (!ValidRiderTarget(target)) {
                return;
            }
            target.AddBuff(BuffID.Poisoned, 120);
            if (!target.boss) {
                target.velocity = Vector2.Zero;
            }
        }
    }

    /// <summary>银弓：T3 圣辉弹射，命中后弹向 200px 内最近敌（每跳 -20%）</summary>
    internal class GsSilverBow : GsOreBowScheme
    {
        public override int TargetItemID => ItemID.SilverBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw with quenched arrowheads. Overdrawn arrows ricochet toward a nearby foe on impact";
        internal override void OreRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) {
            TryRicochet(proj, target, router, 1);
        }
    }

    /// <summary>钨弓：T3 弹射 2 次，末跳带 40px 小爆</summary>
    internal class GsTungstenBow : GsOreBowScheme
    {
        public override int TargetItemID => ItemID.TungstenBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw with quenched arrowheads. Overdrawn arrows ricochet twice; the final hit bursts";
        internal override void OnArrowSpawned(Projectile proj, GodSmithProjRouter router, int tier, int kind) {
            base.OnArrowSpawned(proj, router, tier, kind);
            //弹射两跳需要多一段穿透（带守卫）
            if (kind == KindMain && tier >= 3 && proj.penetrate > 0) {
                proj.penetrate++;
            }
        }

        internal override void OreRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) {
            bool lastHop = TryRicochet(proj, target, router, 2);
            if (!lastHop) {
                return;
            }
            //末跳小爆：40px 溅射 25% 伤
            SplashDamage(Main.player[proj.owner], proj, target, 40f, Math.Max(1, (int)(damageDone * 0.25f)));
        }
    }

    /// <summary>金弓：T3 王权箭，+8% 暴击补正，暴击命中迸出铜币</summary>
    internal class GsGoldBow : GsOreBowScheme
    {
        public override int TargetItemID => ItemID.GoldBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw with quenched arrowheads. Overdrawn royal arrows crit more often; critical hits burst into a few copper coins";
        internal override void ModifyArrowHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, int tier, int kind) {
            if (tier >= 3 && kind == KindMain && Main.rand.NextFloat() < 0.08f) {
                modifiers.SetCrit();
            }
        }

        internal override void OreRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) {
            if (hit.Crit) {
                DropCoins(proj, target, 1, 3);
            }
        }
    }

    /// <summary>铂金弓：T3 金冠箭必定暴击，暴击迸出更多铜币</summary>
    internal class GsPlatinumBow : GsOreBowScheme
    {
        public override int TargetItemID => ItemID.PlatinumBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw with quenched arrowheads. Overdrawn crown arrows always crit and burst into copper coins";
        internal override void ModifyArrowHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, int tier, int kind) {
            if (tier >= 3 && kind == KindMain) {
                modifiers.SetCrit();
            }
        }

        internal override void OreRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) {
            if (hit.Crit) {
                DropCoins(proj, target, 5, 10);
            }
        }
    }
}
