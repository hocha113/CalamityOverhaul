using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    //连弩组共享节奏（全组）：每发 2px 后坐回拉帧；每第 6 发三连点射（0/+4f/+8f 同弹道 ±2°，
    //补射两发免弹药、各 0.3 倍，期望 +10%）；齐射充能满自动成编队（整次只耗 1 发弹药，副箭免费）；
    //齐射命中叠猎标，满 3 层再中触发处决（默认补射双追击箭）；有标时每第 4 发分裂追击箭（15f 节流）。
    //期望算式记法：cycle = 100/CPS + 1（每 cycle 发里 1 发齐射），齐射增益 = 副箭数×副伤 / cycle

    /// <summary>钴钢连弩：三发横列，齐射箭初速 +15%。齐射 +2×0.55/15.3≈7%，合计约 118%</summary>
    internal class GsCobaltRepeater : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.CobaltRepeater;
        protected override string GsDescFallback =>
            "Reforged: every 6th bolt triple-taps; volley charge looses a 3-bolt line at +15% speed, one ammo per volley\nVolley hits brand prey; branded foes draw pursuit bolts";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 3;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Line;
        protected override float SpreadPx => 14f;
        protected override float ChargePerShot => 7f;
        protected override float VolleyVelMul => 1.15f;
        protected override Color TrailColor => new(80, 140, 235);
    }

    /// <summary>钯金连弩：三发横列，标记敌死亡回 2 生命（30 帧限 1 次）。合计约 118%</summary>
    internal class GsPalladiumRepeater : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.PalladiumRepeater;
        protected override string GsDescFallback =>
            "Reforged: every 6th bolt triple-taps; volley charge looses a 3-bolt line, one ammo per volley\nBranded foes slain restore 2 life";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 3;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Line;
        protected override float SpreadPx => 14f;
        protected override float ChargePerShot => 7f;
        protected override Color TrailColor => new(240, 130, 90);

        /// <summary>上次吸血的世界帧（owner 端命中钩子消费，本机契约）</summary>
        private uint lastLeechTick;

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            bool hadMark = GsHuntMarkNPC.CanMark(target) && target.GetGlobalNPC<GsHuntMarkNPC>().Stacks > 0;
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            //标敌死亡：owner 本地结算自愈，30 帧冷却防挂机吸血
            if (hadMark && target.life <= 0 && Main.GameUpdateCount - lastLeechTick >= 30) {
                lastLeechTick = Main.GameUpdateCount;
                Player owner = Main.player[proj.owner];
                if (owner.whoAmI == Main.myPlayer && owner.statLife < owner.statLifeMax2) {
                    owner.Heal(2);
                }
            }
        }
    }

    /// <summary>秘银连弩：四发楔形，齐射箭暴击率 +8%。齐射 +3×0.5/21≈7%，合计约 117%</summary>
    internal class GsMythrilRepeater : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.MythrilRepeater;
        protected override string GsDescFallback =>
            "Reforged: every 6th bolt triple-taps; volley charge looses a 4-bolt wedge with +8% crit, one ammo per volley";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 4;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Wedge;
        protected override float SpreadPx => 16f;
        protected override float ChargePerShot => 5f;
        protected override float SideArrowMul => 0.5f;
        protected override Color TrailColor => new(120, 235, 190);

        protected override void OnSpawnMarkedHook(Projectile proj, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role == GsVolleyRole.VolleyMain || role == GsVolleyRole.VolleySide) {
                proj.CritChance += 8;
            }
        }
    }

    /// <summary>山铜连弩：四发楔形带花瓣尾，齐射箭命中迸花瓣爆（15% 小域）。合计约 117%</summary>
    internal class GsOrichalcumRepeater : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.OrichalcumRepeater;
        protected override string GsDescFallback =>
            "Reforged: every 6th bolt triple-taps; volley charge looses a 4-bolt wedge trailing petals, one ammo per volley\nVolley hits burst into a small petal blast";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 4;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Wedge;
        protected override float SpreadPx => 16f;
        protected override float ChargePerShot => 5f;
        protected override float SideArrowMul => 0.5f;
        protected override Color TrailColor => new(245, 130, 180);

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            base.GsProjPostAI(proj, router);
            int role = (int)router.MarkData;
            bool volley = role == GsVolleyRole.VolleyMain || role == GsVolleyRole.VolleySide;
            if (volley && !VaultUtils.isServer && proj.timeLeft % 5 == 0) {
                PRTLoader.NewParticle<PRT_BrideDryPetal>(
                    proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.03f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    TrailColor, Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(18, 28), 0.3f);
            }
        }

        protected override void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role != GsVolleyRole.VolleyMain && role != GsVolleyRole.VolleySide) {
                return;
            }
            Player owner = Main.player[proj.owner];
            SpawnBurst(owner, target.Center, (int)(proj.damage * 0.15f), 40f, GsVolleyBurstProj.ThemeHoly);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_BrideDryPetal>(target.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                        TrailColor, Main.rand.NextFloat(0.45f, 0.7f))?.Configure(Main.rand.Next(20, 32), 0.35f);
                }
            }
        }
    }

    /// <summary>精金连弩：五发雁行，齐射击退 ×1.5。齐射 +4×0.4/26≈6%，合计约 116%</summary>
    internal class GsAdamantiteRepeater : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.AdamantiteRepeater;
        protected override string GsDescFallback =>
            "Reforged: every 6th bolt triple-taps; volley charge looses a 5-bolt echelon at x1.5 knockback, one ammo per volley";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 5;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Echelon;
        protected override float SpreadPx => 15f;
        protected override float ChargePerShot => 4f;
        protected override float SideArrowMul => 0.4f;
        protected override Color TrailColor => new(235, 90, 100);

        protected override void FireVolley(Item item, Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int count)
            => base.FireVolley(item, player, source, position, velocity, type, damage, knockback * 1.5f, count);
    }

    /// <summary>钛金连弩：五发雁行，齐射箭 +4 穿甲。合计约 116%</summary>
    internal class GsTitaniumRepeater : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.TitaniumRepeater;
        protected override string GsDescFallback =>
            "Reforged: every 6th bolt triple-taps; volley charge looses a 5-bolt echelon with +4 armor penetration, one ammo per volley";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 5;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Echelon;
        protected override float SpreadPx => 15f;
        protected override float ChargePerShot => 4f;
        protected override float SideArrowMul => 0.4f;
        protected override Color TrailColor => new(200, 210, 225);

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role == GsVolleyRole.VolleyMain || role == GsVolleyRole.VolleySide) {
                modifiers.ArmorPenetration += 4f;
            }
        }
    }

    /// <summary>神圣连弩：四发十字，处决召一支 120% 圣星坠向目标。齐射 +3×0.5/29.6≈5%，合计约 116%</summary>
    internal class GsHallowedRepeater : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.HallowedRepeater;
        protected override string GsDescFallback =>
            "Reforged: every 6th bolt triple-taps; volley charge looses a 4-bolt cross, one ammo per volley\nExecuting a fully branded foe calls down a hallowed star";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 4;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Cross;
        protected override float SpreadPx => 15f;
        protected override float ChargePerShot => 3.5f;
        protected override float SideArrowMul => 0.5f;
        protected override Color TrailColor => new(255, 220, 120);

        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            //圣星坠：目标上方偏位落星，原版星弹自带斜落轨迹
            Vector2 from = target.Center + new Vector2(Main.rand.Next(-60, 61), -340f);
            Vector2 vel = (target.Center - from).SafeNormalize(Vector2.UnitY) * 15f;
            Projectile.NewProjectile(player.GetSource_Misc("GsVolleyExecute"), from, vel,
                ProjectileID.HallowStar, (int)(proj.damage * 1.2f), 4f, player.whoAmI);
        }
    }

    /// <summary>
    /// 叶绿散弹弩：原版 2~3 箭霰射保留；齐射换 6 箭孢子锥（±12°）。
    /// 孢标敌受本弩弹幕伤害 +10%；处决孢爆 75%/80px。多箭武器分母大，合计约 116%
    /// </summary>
    internal class GsChlorophyteShotbow : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.ChlorophyteShotbow;
        protected override string GsDescFallback =>
            "Reforged: every 6th shot triple-taps; volley charge looses a 6-arrow spore cone, one ammo per volley\nBranded foes take +10% from this bow; execution bursts a spore cloud";
        protected override bool UsePointBlast => true;
        protected override float PointBlastMul => 0.5f;
        protected override int VolleyCount => 6;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Cone;
        protected override float SpreadPx => 24f;
        protected override float ChargePerShot => 8f;
        protected override Color TrailColor => new(140, 220, 80);

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //孢标：有标敌受本弩一切打标弹幕 +10%
            if (GsHuntMarkNPC.CanMark(target) && target.GetGlobalNPC<GsHuntMarkNPC>().Stacks > 0) {
                modifiers.FinalDamage *= 1.10f;
            }
        }

        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone)
            => SpawnBurst(player, target.Center, (int)(proj.damage * 0.75f), 80f, GsVolleyBurstProj.ThemeSpore);
    }

    /// <summary>
    /// 标桩发射器：对吸血鬼即死的原版特性不动。任意桩命中都叠钉标并短暂钉停非 Boss（90 帧/目标节流）；
    /// 处决帧钉刑：该发 ×2.3，对骷髅/僵尸/血裔类再 ×1.25
    /// </summary>
    internal class GsStakeLauncher : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.StakeLauncher;
        protected override string GsDescFallback =>
            "Reforged: every 6th stake triple-taps; volley charge looses a 3-stake wedge, one ammo per volley\nStakes pin and brand prey; the executing stake crucifies at x2.3, undead take a further +25%";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 3;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Wedge;
        protected override float SpreadPx => 18f;
        protected override float ChargePerShot => 6f;
        protected override float SideArrowMul => 0.6f;
        protected override Color TrailColor => new(220, 190, 140);

        /// <summary>任意角色的桩命中都参与钉标（含普通射击）</summary>
        protected override bool IsMarkingHit(Projectile proj, int role) => true;

        /// <summary>亡灵与血裔：骷髅系、僵尸系、吸血鬼、幽魂</summary>
        private static bool IsUndead(NPC npc) {
            if (npc.type >= 0 && npc.type < NPCID.Sets.Skeletons.Length && NPCID.Sets.Skeletons[npc.type]) {
                return true;
            }
            if (npc.type >= 0 && npc.type < NPCID.Sets.Zombies.Length && NPCID.Sets.Zombies[npc.type]) {
                return true;
            }
            return npc.type is NPCID.Vampire or NPCID.VampireBat or NPCID.Wraith or NPCID.Ghost;
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //钉刑：满标敌吃的这发即处决发，先于 OnHit 的消耗结算增伤
            if (GsHuntMarkNPC.CanMark(target) && target.GetGlobalNPC<GsHuntMarkNPC>().Stacks >= MarkCap) {
                modifiers.FinalDamage *= 2.3f;
                if (IsUndead(target)) {
                    modifiers.FinalDamage *= 1.25f;
                }
            }
        }

        protected override void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //钉停：非 Boss 短暂定身，走真弹幕（服务器同压才是权威定身），90 帧/目标节流
            if (!GsHuntMarkNPC.CanMark(target) || target.boss) {
                return;
            }
            if (target.realLife >= 0 && Main.npc[target.realLife].boss) {
                return;
            }
            GsHuntMarkNPC mark = target.GetGlobalNPC<GsHuntMarkNPC>();
            if (mark.PinCooldown > 0) {
                return;
            }
            mark.PinCooldown = 90;
            Player owner = Main.player[proj.owner];
            Projectile.NewProjectile(owner.GetSource_Misc("GsStakePin"), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsStakePinProj>(), 0, 0f, owner.whoAmI, target.whoAmI);
        }

        /// <summary>钉刑伤害已在 ModifyHit 结算，处决只留演出</summary>
        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) { }
    }
}
