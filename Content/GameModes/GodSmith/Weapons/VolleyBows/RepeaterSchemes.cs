using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    //连弩组共享节奏（全组）：每发 2px 后坐回拉帧；每第 6 发三连点射（0/+4f/+8f 同弹道 ±2°，
    //补射两发免弹药、各 0.3 倍，期望 +10%）；齐射充能满自动成编队（整次只耗 1 发弹药，副箭免费）；
    //齐射命中叠猎标，满 3 层再中触发处决（默认补射双追击箭）；有标时每第 4 发分裂追击箭（15f 节流）。
    //期望算式记法：cycle = 100/CPS + 1（每 cycle 发里 1 发齐射），齐射增益 = 副箭数×副伤 / cycle。
    //P13 返工（2026-08-27）：六把连弩逐把补签名 rider，禁纯参数+色。

    /// <summary>
    /// 钴钢连弩：疾风淬钢的轻弩。①齐射改「风压三连」：三矢错帧鱼贯离弦、飞行中愈飞愈疾
    /// ②齐射命中卷起疾风，令射手短暂快步③点射与齐射箭皆曳钴蓝流光。
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
        protected override Color TrailColor => new(80, 140, 235);

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
            base.GsProjPostAI(proj, router);
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

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role == GsVolleyRole.VolleyMain || role == GsVolleyRole.VolleySide) {
                //疾风矢：更长的钴蓝流光
                DrawSpeedGhost(proj, TrailColor, 0.52f);
                return null;
            }
            return base.GsProjPreDraw(proj, ref lightColor, router);
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
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Light>(target.Center - proj.velocity * (0.3f + i * 0.3f),
                        -proj.velocity * 0.12f, TrailColor, 0.1f)?.Configure(8 + i * 3, 0.8f);
                }
            }
        }
    }

    /// <summary>
    /// 钯金连弩：温血活金的续命弩。①标记之敌倒下时渡回 2 生命，暖橙生命尘自尸身漂回射手
    /// ②处决额外渡 5 生命③齐射箭曳暖橙脉光。合计约 118%
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
                LifeMotesFX(target.Center, Main.player[proj.owner], 3);
            }
        }

        /// <summary>处决渡血：额外 5 生命</summary>
        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            base.OnExecute(player, target, proj, damageDone);
            if (player.whoAmI == Main.myPlayer && player.statLife < player.statLifeMax2) {
                player.Heal(5);
            }
            LifeMotesFX(target.Center, player, 5);
        }

        /// <summary>渡血演出：暖橙生命尘自目标漂向射手（攻击方端个人反馈）</summary>
        private void LifeMotesFX(Vector2 from, Player toward, int count) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dir = (toward.Center - from).SafeNormalize(-Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Light>(from + Main.rand.NextVector2Circular(10f, 10f),
                    dir * Main.rand.NextFloat(1.2f, 2.4f) - new Vector2(0f, 0.5f),
                    TrailColor, Main.rand.NextFloat(0.08f, 0.13f))?.Configure(Main.rand.Next(16, 26), 0.8f);
            }
        }
    }

    /// <summary>
    /// 秘银连弩：翠冷精工的狙癖弩。①「弱点透镜」：齐射主箭命中带标之敌必定暴击，
    /// 暴击帧绽翠绿棱光十字②齐射副箭 +8% 暴击③楔形齐射。
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
        protected override Color TrailColor => new(120, 235, 190);

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
            //透镜聚焦：暴击帧翠绿棱光十字一闪
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.35f, Pitch = 0.6f }, target.Center);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center, Vector2.Zero,
                    TrailColor, 0.5f - i * 0.15f)?.Configure(TrailColor, 14 + i * 4, 0.08f, 1.2f);
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
        protected override Color TrailColor => new(235, 90, 100);

        protected override void FireVolley(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int count)
            => base.FireVolley(item, player, source, position, velocity, type, damage, knockback * 1.5f, count);

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            base.GsProjPostAI(proj, router);
            int role = (int)router.MarkData;
            bool volley = role == GsVolleyRole.VolleyMain || role == GsVolleyRole.VolleySide;
            if (volley && !VaultUtils.isServer && proj.timeLeft % 5 == 0) {
                //赤红重矢：坠散的精金火屑
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.04f + new Vector2(0f, 0.3f),
                    new Color(235, 120, 60), Main.rand.NextFloat(0.2f, 0.3f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

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
    /// （每层 +2 穿甲，至多 5 层，3 秒衰减），满层白光剪影一闪②齐射箭基础 +4 穿甲
    /// ③钛影三相环绕重影。穿甲是对甲收益，合计约 116%
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
        protected override Color TrailColor => new(200, 210, 225);

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
            //蚀刻反馈：银屑剥落；首次满层白光剪影一闪
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f),
                    TrailColor, Main.rand.NextFloat(0.22f, 0.36f))?.Configure(true, Main.rand.Next(10, 18));
            }
            if (!wasMax && mark.ErodeStacks >= 5) {
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.4f, Pitch = 0.6f }, target.Center);
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, Color.White, 0.22f)?.Configure(10, 0.9f);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role != GsVolleyRole.VolleyMain && role != GsVolleyRole.VolleySide) {
                return base.GsProjPreDraw(proj, ref lightColor, router);
            }
            //钛影三相：三道冷白残影绕矢体缓旋（identity 定相，零随机）
            Main.instance.LoadProjectile(proj.type);
            Texture2D tex = TextureAssets.Projectile[proj.type].Value;
            Color ghost = TrailColor with { A = 0 };
            float basePhase = Main.GlobalTimeWrappedHourly * 6f + proj.identity * 0.77f;
            for (int i = 0; i < 3; i++) {
                Vector2 off = (basePhase + MathHelper.TwoPi / 3f * i).ToRotationVector2() * 5f;
                Main.EntitySpriteDraw(tex, proj.Center + off - proj.velocity * 0.4f - Main.screenPosition, null,
                    ghost * 0.3f, proj.rotation, tex.Size() * 0.5f, 1f, SpriteEffects.None, 0);
            }
            DrawSpeedGhost(proj, TrailColor, 0.22f);
            return null;
        }
    }

    /// <summary>
    /// 神圣连弩：鎏金圣辉的落星弩。①处决召一支 120% 圣星坠向目标，圣星曳鎏金星尘
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
        protected override Color TrailColor => new(255, 220, 120);

        /// <summary>处决圣星角色（打标走 ItemUse 源，星体获得鎏金增强层）</summary>
        internal const int RoleHolyStar = GsVolleyRole.CustomBase;

        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            //圣星坠：目标上方偏位落星，原版星弹自带斜落轨迹
            Vector2 from = target.Center + new Vector2(Main.rand.Next(-60, 61), -340f);
            Vector2 vel = (target.Center - from).SafeNormalize(Vector2.UnitY) * 15f;
            int dmg = (int)(proj.damage * 1.2f);
            if (player.HeldItem.type == TargetItemID) {
                //仍持本弩：走打标生成，圣星吃鎏金增强层
                SpawnTagged(player, player.GetSource_ItemUse(player.HeldItem), from, vel,
                    ProjectileID.HallowStar, dmg, 4f, RoleHolyStar);
            }
            else {
                Projectile.NewProjectile(player.GetSource_Misc("GsVolleyExecute"), from, vel,
                    ProjectileID.HallowStar, dmg, 4f, player.whoAmI);
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            base.GsProjPostAI(proj, router);
            if ((int)router.MarkData != RoleHolyStar || VaultUtils.isServer) {
                return;
            }
            //圣星尾：鎏金星尘剥落
            Lighting.AddLight(proj.Center, TrailColor.ToVector3() * 0.3f);
            if (proj.timeLeft % 5 == 0) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.08f, TrailColor, 0.32f)?.Configure(false, 14);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if ((int)router.MarkData == RoleHolyStar) {
                DrawSpeedGhost(proj, TrailColor, 0.5f);
                return null;
            }
            return base.GsProjPreDraw(proj, ref lightColor, router);
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if ((int)router.MarkData != RoleHolyStar || VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f),
                    TrailColor, Main.rand.NextFloat(0.26f, 0.42f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }
    }

    /// <summary>
    /// 叶绿散弹弩：原版 2~3 箭霰射保留；齐射换 6 箭孢子锥（±12°），齐射箭洒孢尘。
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

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            base.GsProjPostAI(proj, router);
            int role = (int)router.MarkData;
            bool volley = role == GsVolleyRole.VolleyMain || role == GsVolleyRole.VolleySide;
            if (volley && !VaultUtils.isServer && proj.timeLeft % 6 == 0) {
                //孢尘：翠绿微尘缓浮
                PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.02f + new Vector2(0f, -0.25f),
                    new Color(170, 240, 110), 0.07f)?.Configure(14, 0.7f);
            }
        }

        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone)
            => SpawnBurst(player, target.Center, (int)(proj.damage * 0.75f), 80f, GsVolleyBurstProj.ThemeSpore);
    }

    /// <summary>
    /// 标桩发射器（B→A 返工）：白蜡木桩、淬银桩头的猎手弩。
    /// 身份宣言：①桩矢坠木屑、亮银冕头光，任意桩命中钉停并叠标②满标钉刑：该发 ×2.3、
    /// 四向银芒向钉心收束、桩身炸裂木屑，亡灵再 ×1.25，倒下时绽净化白焰
    /// ③钉刑得手回填 30 充能，猎局连环；重弩后坐 5px，满充弩身细颤、桩口银冕待发。
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
        protected override Color TrailColor => new(220, 190, 140);

        /// <summary>银冕桩头色</summary>
        private static readonly Color SilverCrown = new(222, 230, 240);

        /// <summary>白蜡木屑色</summary>
        private static readonly Color WoodChip = new(150, 112, 70);

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

        //==================== 动画法：重弩后坐 + 上膛读数 ====================

        /// <summary>重弩后坐：出手瞬间弩身反坐 5px 加桩口上踢，指数回坐；满充弩身细颤读作桩已上膛</summary>
        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float elapsed = 1f - player.itemAnimation / (float)player.itemAnimationMax;
            float kick = MathF.Exp(-5f * elapsed);
            Vector2 aimDir = player.itemRotation.ToRotationVector2() * player.direction;
            player.itemLocation -= aimDir * (5f * kick);
            player.itemRotation -= player.direction * 0.09f * kick;
            if (player.whoAmI == Main.myPlayer
                && player.GetModPlayer<GsVolleyPlayer>().Charge >= 100f) {
                player.itemLocation.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 40f) * 0.6f * player.gravDir;
            }
        }

        /// <summary>满充上膛读数：桩口银冕微光待发（本机个人读数）</summary>
        public override void GsHoldItem(Item item, Player player) {
            if (VaultUtils.isServer || player.whoAmI != Main.myPlayer) {
                return;
            }
            if (player.GetModPlayer<GsVolleyPlayer>().Charge < 100f || !Main.rand.NextBool(5)) {
                return;
            }
            Vector2 muzzle = player.MountedCenter
                + new Vector2(player.direction * Main.rand.NextFloat(14f, 24f), -Main.rand.NextFloat(0f, 6f));
            PRTLoader.NewParticle<PRT_Light>(muzzle, new Vector2(player.direction * 0.3f, -0.4f),
                SilverCrown, 0.08f)?.Configure(12, 0.75f);
        }

        //==================== 飞行相：木屑与银冕 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            base.GsProjPostAI(proj, router);
            if (VaultUtils.isServer || !router.IsMarked) {
                return;
            }
            //白蜡木屑坠散（含普通射击的 None 角色）
            if (proj.timeLeft % 6 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.05f + new Vector2(0f, 0.4f), WoodChip,
                    Main.rand.NextFloat(0.18f, 0.3f))?.Configure(true, Main.rand.Next(10, 16));
            }
            //银冕头光
            if (proj.timeLeft % 5 == 0) {
                PRTLoader.NewParticle<PRT_Light>(
                    proj.Center + proj.velocity.SafeNormalize(Vector2.UnitX) * 8f,
                    Vector2.Zero, SilverCrown, 0.06f)?.Configure(6, 0.8f);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (!router.IsMarked) {
                return null;
            }
            //一切桩矢挂银冕重影，齐射桩更亮
            int role = (int)router.MarkData;
            float strength = role == GsVolleyRole.None ? 0.2f
                : role == GsVolleyRole.PointBlast ? 0.26f : 0.38f;
            DrawSpeedGhost(proj, SilverCrown, strength);
            return null;
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
            //钉入反馈：命中点崩两粒木屑（攻击方端）
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                        (-proj.velocity.SafeNormalize(Vector2.UnitY)).RotatedByRandom(0.7) * Main.rand.NextFloat(1.5f, 3f),
                        WoodChip, Main.rand.NextFloat(0.2f, 0.32f))?.Configure(true, Main.rand.Next(10, 16));
                }
            }
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

        /// <summary>钉刑伤害已在 ModifyHit 结算；这里做十字银芒收束、木屑迸发与猎局回填</summary>
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
            //四向银芒向钉心收束（章节感的处决收笔）
            for (int i = 0; i < 4; i++) {
                Vector2 from = target.Center + (MathHelper.PiOver2 * i).ToRotationVector2() * 52f;
                PRTLoader.NewParticle<PRT_SkyBolt>(from, Vector2.Zero, SilverCrown, 0.65f)
                    ?.Configure(from, target.Center, 14);
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, SilverCrown, 0.07f)
                ?.Configure(0.07f, 0.4f, 12);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? WoodChip : SilverCrown,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(true, Main.rand.Next(14, 24));
            }
            //净化时刻：亡灵在钉刑下倒下，绽白焰一闪
            if (IsUndead(target) && target.life <= 0) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = 0.4f }, target.Center);
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, Color.White, 0.3f)?.Configure(14, 0.9f);
            }
        }
    }
}
