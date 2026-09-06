using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows.Projectiles;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows
{
    //连弩组共享节奏（全组）：每发 2px 后坐回拉帧；每第 6 发三连点射（0/+4f/+8f 同弹道 ±2°，
    //补射两发免弹药、各 0.3 倍，期望 +10%）；齐射充能满自动成编队（整次只耗 1 发弹药，副箭免费）；
    //齐射命中叠猎标，满 3 层再中触发处决（默认补射双追击箭）；有标时每第 4 发分裂追击箭（15f 节流）。
    //期望算式记法：cycle = 100/CPS + 1（每 cycle 发里 1 发齐射），齐射增益 = 副箭数×副伤 / cycle。
    //P13 返工（2026-08-27）：六把连弩逐把补签名 rider，禁纯参数+色。

    /// <summary>
    /// 钴钢连弩：疾风淬钢的轻弩。①齐射改「风压三连」：三矢错帧鱼贯离弦、飞行中愈飞愈疾
    /// ②齐射命中卷起疾风，令射手短暂快步。
    /// 齐射 +2×0.55/15.3≈7%，迅捷是机动收益不计伤害，合计约 118%
    /// </summary>
    internal class GsCobaltRepeater : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.CobaltRepeater;
        protected override string GsDescFallback =>
            "Reforged: every 6th bolt triple-taps; volley charge looses 3 bolts one after another that accelerate in flight, one ammo per volley\nVolley hits brand prey and grant you a short gust of Swiftness";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 3;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Line;
        protected override float SpreadPx => 14f;
        protected override float ChargePerShot => 7f;
        protected override float VolleyVelMul => 1.15f;

        /// <summary>风压三连：齐射改为错帧鱼贯（1/4/7 帧），读作「咻、咻、咻」而非齐墙</summary>
        protected override void FireVolley(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int count) {
            GsVolleyPlayer vp = player.GetModPlayer<GsVolleyPlayer>();
            int mainIndex = FormationLib.MainIndex(count);
            for (int i = 0; i < count; i++) {
                bool isMain = i == mainIndex;
                vp.Enqueue(new GsPendingShot {
                    Delay = 1 + i * 3,
                    WeaponType = item.type,
                    ProjType = VolleyProjType(type),
                    Velocity = velocity * VolleyVelMul,
                    Damage = isMain ? damage : (int)(damage * SideArrowMul),
                    Knockback = isMain ? knockback : knockback * 0.7f,
                    Role = isMain ? GsVolleyRole.VolleyMain : GsVolleyRole.VolleySide,
                    Param = i,
                });
            }
        }

        private class CobaltRushState
        {
            public float TopSpeed;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role != GsVolleyRole.VolleyMain && role != GsVolleyRole.VolleySide) {
                return;
            }
            //愈飞愈疾：初速逐帧提到 1.3 倍封顶（确定性，各端同式）
            CobaltRushState st = router.GetOrCreateState<CobaltRushState>();
            if (st.TopSpeed <= 0f) {
                st.TopSpeed = proj.velocity.Length() * 1.3f;
            }
            if (proj.velocity.Length() < st.TopSpeed) {
                proj.velocity *= 1.025f;
            }
        }

        protected override void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role != GsVolleyRole.VolleyMain && role != GsVolleyRole.VolleySide) {
                return;
            }
            //疾风回身：齐射命中给射手 0.75 秒迅捷（机动收益，不进伤害预算）
            Player owner = Main.player[proj.owner];
            if (owner.whoAmI == Main.myPlayer) {
                owner.AddBuff(BuffID.Swiftness, 45);
            }
        }
    }

    /// <summary>
    /// 钯金连弩：温血活金的续命弩。①标记之敌倒下时渡回 2 生命
    /// ②处决额外渡 5 生命。合计约 118%
    /// </summary>
    internal class GsPalladiumRepeater : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.PalladiumRepeater;
        protected override string GsDescFallback =>
            "Reforged: every 6th bolt triple-taps; volley charge looses a 3-bolt line, one ammo per volley\nBranded foes slain restore 2 life; executions restore 5 more";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 3;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Line;
        protected override float SpreadPx => 14f;
        protected override float ChargePerShot => 7f;

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

        /// <summary>处决渡血：额外 5 生命</summary>
        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            base.OnExecute(player, target, proj, damageDone);
            if (player.whoAmI == Main.myPlayer && player.statLife < player.statLifeMax2) {
                player.Heal(5);
            }
        }
    }

    /// <summary>
    /// 秘银连弩：翠冷精工的狙癖弩。①「弱点透镜」：齐射主箭命中带标之敌必定暴击
    /// ②齐射副箭 +8% 暴击③楔形齐射。
    /// 主箭必暴对已标敌 ≈ +4%/cycle，合计约 118%
    /// </summary>
    internal class GsMythrilRepeater : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.MythrilRepeater;
        protected override string GsDescFallback =>
            "Reforged: every 6th bolt triple-taps; volley charge looses a 4-bolt wedge, one ammo per volley\nThe lead volley bolt always crits against branded foes; side bolts gain +8% crit";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 4;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Wedge;
        protected override float SpreadPx => 16f;
        protected override float ChargePerShot => 5f;
        protected override float SideArrowMul => 0.5f;

        protected override void OnSpawnMarkedHook(Projectile proj, GodSmithProjRouter router) {
            if ((int)router.MarkData == GsVolleyRole.VolleySide) {
                proj.CritChance += 8;
            }
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //弱点透镜：主箭对已标之敌必暴（先叠标、再送主箭，玩家能主动组出这一发）
            if ((int)router.MarkData == GsVolleyRole.VolleyMain
                && GsHuntMarkNPC.CanMark(target) && target.GetGlobalNPC<GsHuntMarkNPC>().Stacks > 0) {
                modifiers.SetCrit();
            }
        }

        protected override void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if ((int)router.MarkData != GsVolleyRole.VolleyMain || !hit.Crit || VaultUtils.isServer) {
                return;
            }
            //透镜聚焦：暴击帧一记清响
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.35f, Pitch = 0.6f }, target.Center);
        }
    }

    /// <summary>山铜连弩：四发楔形，齐射箭命中迸花瓣爆（15% 小域）。合计约 117%</summary>
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

        protected override void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role != GsVolleyRole.VolleyMain && role != GsVolleyRole.VolleySide) {
                return;
            }
            Player owner = Main.player[proj.owner];
            SpawnBurst(owner, target.Center, (int)(proj.damage * 0.15f), 40f, GsVolleyBurstProj.ThemeHoly);
        }
    }

    /// <summary>
    /// 精金连弩：赤红重锻的破阵弩。①「破阵震爆」：齐射主箭命中砸出赤红震纹，
    /// 25% 伤害的冲击波把打击传给周身之敌②齐射击退 ×1.5，读作重锤推阵③雁行五发。
    /// 震爆 +0.25/26≈1%，合计约 117%
    /// </summary>
    internal class GsAdamantiteRepeater : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.AdamantiteRepeater;
        protected override string GsDescFallback =>
            "Reforged: every 6th bolt triple-taps; volley charge looses a 5-bolt echelon at x1.5 knockback, one ammo per volley\nThe lead volley bolt lands with a crimson shockwave that splashes nearby foes";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 5;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Echelon;
        protected override float SpreadPx => 15f;
        protected override float ChargePerShot => 4f;
        protected override float SideArrowMul => 0.4f;

        protected override void FireVolley(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int count)
            => base.FireVolley(item, player, source, position, velocity, type, damage, knockback * 1.5f, count);

        protected override void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if ((int)router.MarkData != GsVolleyRole.VolleyMain) {
                return;
            }
            //破阵震爆：主箭落点赤红冲击环（真弹幕跨端可见）
            SpawnBurst(Main.player[proj.owner], target.Center, (int)(proj.damage * 0.25f), 70f, GsVolleyBurstProj.ThemeEmber);
        }
    }

    /// <summary>
    /// 钛金连弩：冷白秘金的蚀甲弩。①「蚀甲」：齐射箭每次命中同一敌叠 1 层蚀刻
    /// （每层 +2 穿甲，至多 5 层，3 秒衰减）②齐射箭基础 +4 穿甲。
    /// 穿甲是对甲收益，合计约 116%
    /// </summary>
    internal class GsTitaniumRepeater : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.TitaniumRepeater;
        protected override string GsDescFallback =>
            "Reforged: every 6th bolt triple-taps; volley charge looses a 5-bolt echelon, one ammo per volley\nVolley bolts etch armor: each hit on the same foe adds +2 armor penetration, up to 5 stacks";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 5;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Echelon;
        protected override float SpreadPx => 15f;
        protected override float ChargePerShot => 4f;
        protected override float SideArrowMul => 0.4f;

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role != GsVolleyRole.VolleyMain && role != GsVolleyRole.VolleySide) {
                return;
            }
            //基础 +4 穿甲，蚀甲每层再 +2（对甲收益，攻击方端结算）
            float pen = 4f;
            if (GsHuntMarkNPC.CanMark(target)) {
                pen += 2f * target.GetGlobalNPC<GsHuntMarkNPC>().ErodeStacks;
            }
            modifiers.ArmorPenetration += pen;
        }

        protected override void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role != GsVolleyRole.VolleyMain && role != GsVolleyRole.VolleySide) {
                return;
            }
            if (!GsHuntMarkNPC.CanMark(target)) {
                return;
            }
            GsHuntMarkNPC mark = target.GetGlobalNPC<GsHuntMarkNPC>();
            bool wasMax = mark.ErodeStacks >= 5;
            mark.ErodeStacks = Math.Min(5, mark.ErodeStacks + 1);
            mark.ErodeTimer = 180;
            if (VaultUtils.isServer) {
                return;
            }
            //首次满层一记清响
            if (!wasMax && mark.ErodeStacks >= 5) {
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.4f, Pitch = 0.6f }, target.Center);
            }
        }
    }

    /// <summary>
    /// 神圣连弩：鎏金圣辉的落星弩。①处决召一支 120% 圣星坠向目标
    /// ②四发十字齐射③点射节拍照常。齐射 +3×0.5/29.6≈5%，合计约 116%
    /// </summary>
    internal class GsHallowedRepeater : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.HallowedRepeater;
        protected override string GsDescFallback =>
            "Reforged: every 6th bolt triple-taps; volley charge looses a 4-bolt cross, one ammo per volley\nExecuting a fully branded foe calls down a hallowed star trailing gold";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 4;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Cross;
        protected override float SpreadPx => 15f;
        protected override float ChargePerShot => 3.5f;
        protected override float SideArrowMul => 0.5f;

        /// <summary>处决圣星角色（仍持本弩时走 ItemUse 源打标）</summary>
        internal const int RoleHolyStar = GsVolleyRole.CustomBase;

        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            //圣星坠：目标上方偏位落星，原版星弹自带斜落轨迹
            Vector2 from = target.Center + new Vector2(Main.rand.Next(-60, 61), -340f);
            Vector2 vel = (target.Center - from).SafeNormalize(Vector2.UnitY) * 15f;
            int dmg = (int)(proj.damage * 1.2f);
            if (player.HeldItem.type == TargetItemID) {
                //仍持本弩：走打标生成
                SpawnTagged(player, player.GetSource_ItemUse(player.HeldItem), from, vel,
                    ProjectileID.HallowStar, dmg, 4f, RoleHolyStar);
            }
            else {
                Projectile.NewProjectile(player.GetSource_Misc("GsVolleyExecute"), from, vel,
                    ProjectileID.HallowStar, dmg, 4f, player.whoAmI);
            }
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
    /// 标桩发射器（B→A 返工）：白蜡木桩、淬银桩头的猎手弩。
    /// 身份宣言：①任意桩命中钉停并叠标②满标钉刑：该发 ×2.3，亡灵再 ×1.25
    /// ③钉刑得手回填 30 充能，猎局连环；重弩后坐 5px，满充弩身细颤。
    /// 对吸血鬼即死的原版特性不动；回填是节奏收益，合计约 116%
    /// </summary>
    internal class GsStakeLauncher : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.StakeLauncher;
        protected override string GsDescFallback =>
            "Reforged: every 6th stake triple-taps; volley charge looses a 3-stake wedge, one ammo per volley\nStakes pin and brand prey; the executing stake crucifies at x2.3, undead take a further +25%\nEach crucifixion reloads 30 volley charge; silver light gathers at the muzzle when fully charged";
        protected override bool UsePointBlast => true;
        protected override int VolleyCount => 3;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Wedge;
        protected override float SpreadPx => 18f;
        protected override float ChargePerShot => 6f;
        protected override float SideArrowMul => 0.6f;

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

        //==================== 动画法：重弩后坐 ====================

        /// <summary>重弩后坐：出手瞬间弩身反坐 5px 加桩口上踢，指数回坐；满充弩身细颤读作桩已上膛。
        /// 上踢走 GsMagicKickMath 差分——useStyle-5 的 itemRotation 无人每帧重算，
        /// 直接 -= 会逐帧累减漂移（S10 同型定罪，剖面 0.09·exp(-5·elapsed) 原值保留）</summary>
        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float elapsed = 1f - player.itemAnimation / (float)player.itemAnimationMax;
            float kick = MathF.Exp(-5f * elapsed);
            Vector2 aimDir = player.itemRotation.ToRotationVector2() * player.direction;
            player.itemLocation -= aimDir * (5f * kick);
            float kickPrev = MathF.Exp(-5f * (1f - (player.itemAnimation + 1) / (float)player.itemAnimationMax));
            GsMagicKickMath.ApplyKickDiff(player, 0.09f * kick, 0.09f * kickPrev);
            if (player.whoAmI == Main.myPlayer
                && player.GetModPlayer<GsVolleyPlayer>().Charge >= 100f) {
                player.itemLocation.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 40f) * 0.6f * player.gravDir;
            }
        }

        //==================== 命中相：钉刑与钉停 ====================

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

        /// <summary>钉刑伤害已在 ModifyHit 结算；这里做处决音效与猎局回填</summary>
        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            //猎局连环：钉刑得手回填 30 充能（本机节奏收益）
            if (player.whoAmI == Main.myPlayer) {
                GsVolleyPlayer vp = player.GetModPlayer<GsVolleyPlayer>();
                float before = vp.Charge;
                vp.Charge = MathF.Min(100f, vp.Charge + 30f);
                if (!VaultUtils.isServer && vp.Charge > before) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = 0.6f }, player.Center);
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.6f, Pitch = -0.15f }, target.Center);
            //净化时刻：亡灵在钉刑下倒下
            if (IsUndead(target) && target.life <= 0) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = 0.4f }, target.Center);
            }
        }
    }
}
