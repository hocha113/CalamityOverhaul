using CalamityMod;
using CalamityOverhaul.Content.Items.Ranged;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    ///<summary>
    ///真菌鱼技能 - 荧光孢子生态系统
    ///</summary>
    internal class FishAmanita : FishSkill
    {
        public override int UnlockFishID => ItemID.AmanitaFungifin;
        public override int DefaultCooldown => 90 - HalibutData.GetDomainLayer() * 6;
        public override int ResearchDuration => 60 * 20;
        private int sporePhase = 0;
        private int shootCounter = 0;
        private static int PhaseChangeInterval = 1; //每1次射击切换一次孢子形态

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                shootCounter++;

                //周期性切换孢子形态
                if (shootCounter >= PhaseChangeInterval) {
                    shootCounter = 0;
                    sporePhase = (sporePhase + 1) % 4;

                    //形态切换特效
                    SpawnPhaseTransitionEffect(player);
                }

                //根据不同形态生成不同的蘑菇攻击
                SpawnMushroomAttack(player, position, velocity, damage, sporePhase);

                //生成环绕玩家的孢子云
                SpawnAmbientSpores(player);

                SetCooldown();
            }

            return null;
        }

        private void SpawnPhaseTransitionEffect(Player player) {
            SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                Volume = 0.5f,
                Pitch = 0.3f
            }, player.Center);

            Color phaseColor = GetPhaseColor(sporePhase);

            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f);

                Dust dust = Dust.NewDustPerfect(
                    player.Center,
                    DustID.BlueFairy,
                    velocity,
                    100,
                    phaseColor,
                    Main.rand.NextFloat(1.5f, 2.2f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1.3f;
            }

            //生成孢子粒子环
            for (int i = 0; i < 20; i++) {
                Vector2 spawnPos = player.Center + Main.rand.NextVector2Circular(80f, 80f);
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(spawnPos, Main.rand.NextVector2Circular(2f, 2f));
                if (prt != null) {
                    prt.Color = phaseColor;
                    prt.Scale = Main.rand.NextFloat(0.8f, 1.4f);
                }
            }
        }

        private void SpawnMushroomAttack(Player player, Vector2 position, Vector2 velocity, int damage, int phase) {
            var source = player.GetSource_FromThis();

            switch (phase) {
                case 0: //爆炸蘑菇形态
                    SpawnExplodingMushrooms(source, player, position, velocity, damage);
                    break;

                case 1: //追踪孢子形态
                    SpawnHomingSpores(source, player, position, velocity, damage);
                    break;

                case 2: //毒雾蘑菇形态
                    SpawnToxicMushrooms(source, player, position, velocity, damage);
                    break;

                case 3: //闪光孢子形态
                    SpawnLightningSpores(source, player, position, velocity, damage);
                    break;
            }
        }

        private void SpawnExplodingMushrooms(IEntitySource source, Player player, Vector2 position, Vector2 velocity, int damage) {
            //生成3个小型爆炸蘑菇
            for (int i = 0; i < 3; i++) {
                float angleOffset = MathHelper.Lerp(-0.3f, 0.3f, i / 2f);
                Vector2 spawnVel = velocity.RotatedBy(angleOffset) * Main.rand.NextFloat(0.7f, 1.2f);

                Projectile.NewProjectile(
                    source,
                    position,
                    spawnVel,
                    ModContent.ProjectileType<AmanitaExplosiveMushroom>(),
                    (int)(damage * 2.5 + HalibutData.GetDomainLayer() * 0.6),
                    2f,
                    player.whoAmI
                );
            }
        }

        private void SpawnHomingSpores(IEntitySource source, Player player, Vector2 position, Vector2 velocity, int damage) {
            //生成5个追踪孢子
            for (int i = 0; i < 5; i++) {
                Vector2 randomVel = velocity.RotatedByRandom(0.5f) * Main.rand.NextFloat(0.8f, 1.3f);

                Projectile.NewProjectile(
                    source,
                    position,
                    randomVel,
                    ModContent.ProjectileType<AmanitaHomingSpore>(),
                    (int)(damage * 1.25 + HalibutData.GetDomainLayer() * 0.35),
                    1.5f,
                    player.whoAmI
                );
            }
        }

        private void SpawnToxicMushrooms(IEntitySource source, Player player, Vector2 position, Vector2 velocity, int damage) {
            //生成2个毒雾蘑菇
            for (int i = 0; i < 2; i++) {
                Vector2 spawnVel = velocity.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f));

                Projectile.NewProjectile(
                    source,
                    position,
                    spawnVel,
                    ModContent.ProjectileType<AmanitaToxicMushroom>(),
                    (int)(damage * 1.75 * +HalibutData.GetDomainLayer() * 0.55),
                    3f,
                    player.whoAmI
                );
            }
        }

        private void SpawnLightningSpores(IEntitySource source, Player player, Vector2 position, Vector2 velocity, int damage) {
            //生成5个闪电孢子
            for (int i = -2; i < 3; i++) {
                float angleOffset = MathHelper.TwoPi * i / 4f;
                Vector2 spawnVel = velocity.RotatedBy(angleOffset * 0.3f) * Main.rand.NextFloat(0.9f, 1.1f);

                Projectile.NewProjectile(
                    source,
                    position,
                    spawnVel,
                    ModContent.ProjectileType<AmanitaLightningSpore>(),
                    (int)(damage * 1.75f + HalibutData.GetDomainLayer() * 0.55),
                    2f,
                    player.whoAmI
                );
            }
        }

        private void SpawnAmbientSpores(Player player) {
            //周期性在玩家周围生成环境孢子效果
            if (Main.rand.NextBool(3)) {
                Vector2 spawnPos = player.Center + Main.rand.NextVector2Circular(60f, 60f);
                Color sporeColor = GetPhaseColor(sporePhase);

                Dust dust = Dust.NewDustPerfect(
                    spawnPos,
                    DustID.BlueFairy,
                    Main.rand.NextVector2Circular(1f, 1f),
                    100,
                    sporeColor,
                    Main.rand.NextFloat(1f, 1.5f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1f;
            }
        }

        private Color GetPhaseColor(int phase) {
            return phase switch {
                0 => new Color(255, 100, 100), //红色 - 爆炸
                1 => new Color(100, 255, 255), //青色 - 追踪
                2 => new Color(150, 255, 100), //绿色 - 毒雾
                3 => new Color(255, 255, 150), //黄色 - 闪电
                _ => Color.White
            };
        }
    }

    #region 爆炸蘑菇弹幕
    ///<summary>
    ///爆炸蘑菇 - 接触敌人或地面时爆炸
    ///</summary>
    internal class AmanitaExplosiveMushroom : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "Glomushroom";

        private bool exploded = false;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.velocity.Y += 0.15f; //重力

            //发光效果
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 1f * pulse, 0.3f * pulse, 0.3f * pulse);

            //轨迹粒子
            if (Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.BlueFairy,
                    0, 0, 100,
                    new Color(255, 100, 100),
                    1.2f
                );
                dust.velocity = -Projectile.velocity * 0.3f;
                dust.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Explode();
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Explode();
        }

        private void Explode() {
            if (exploded) return;
            exploded = true;

            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.6f,
                Pitch = 0.3f
            }, Projectile.Center);

            //爆炸粒子
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.BlueFairy,
                    velocity,
                    100,
                    new Color(255, 100, 100),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1.3f;
            }

            //生成孢子粒子
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(Projectile.Center, velocity);
                if (prt != null) {
                    prt.Color = new Color(255, 100, 100);
                    prt.Scale = Main.rand.NextFloat(0.8f, 1.2f);
                }
            }

            //爆炸伤害范围
            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.active && !npc.friendly && npc.CanBeChasedBy() &&
                        Vector2.Distance(npc.Center, Projectile.Center) < 100f) {

                        Player owner = Main.player[Projectile.owner];
                        owner.ApplyDamageToNPC(npc, Projectile.damage, 0, 0, false);
                    }
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (!exploded) {
                Explode();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;

            //绘制拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float fade = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Color color = new Color(255, 100, 100) * fade * 0.5f;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, origin, Projectile.scale * 0.9f, SpriteEffects.None);
            }

            //绘制主体（发光）
            float glowIntensity = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.3f + 0.7f;
            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                Color.White * glowIntensity,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None
            );

            return false;
        }
    }
    #endregion

    #region 追踪孢子弹幕
    ///<summary>
    ///追踪孢子 - 会追踪最近的敌人
    ///</summary>
    internal class AmanitaHomingSpore : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private float homingDelay = 15f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            //发光效果
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 0.3f * pulse, 1f * pulse, 1f * pulse);

            //追踪逻辑
            if (homingDelay > 0) {
                homingDelay--;
            }
            else {
                CalamityUtils.HomeInOnNPC(Projectile, true, 400f, 8f, 20f);
            }

            //轨迹粒子
            if (Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.BlueFairy,
                    -Projectile.velocity * 0.2f,
                    100,
                    new Color(100, 255, 255),
                    Main.rand.NextFloat(0.8f, 1.3f)
                );
                dust.noGravity = true;
            }

            //孢子粒子
            if (Main.rand.NextBool(5)) {
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(
                    Projectile.Center,
                    -Projectile.velocity * 0.1f
                );
                if (prt != null) {
                    prt.Color = new Color(100, 255, 255);
                    prt.Scale *= 0.6f;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中特效
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.BlueFairy,
                    velocity,
                    100,
                    new Color(100, 255, 255),
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.BlueFairy,
                    Scale: Main.rand.NextFloat(1f, 1.5f)
                );
                dust.velocity = Main.rand.NextVector2Circular(3f, 3f);
                dust.noGravity = true;
                dust.color = new Color(100, 255, 255);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glowTex = TextureAssets.Extra[ExtrasID.SharpTears].Value; //发光材质

            //绘制发光拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float fade = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Color color = new Color(100, 255, 255) * fade * 0.6f;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float scale = Projectile.scale * fade * 0.5f;

                Main.EntitySpriteDraw(
                    glowTex,
                    drawPos,
                    null,
                    color,
                    0,
                    glowTex.Size() / 2f,
                    scale,
                    SpriteEffects.None
                );
            }

            //绘制主体发光
            Main.EntitySpriteDraw(
                glowTex,
                Projectile.Center - Main.screenPosition,
                null,
                new Color(100, 255, 255) * 0.8f,
                0,
                glowTex.Size() / 2f,
                Projectile.scale * 0.8f,
                SpriteEffects.None
            );

            return false;
        }
    }
    #endregion

    #region 毒雾蘑菇弹幕
    ///<summary>
    ///毒雾蘑菇 - 生成持续性毒雾区域
    ///</summary>
    internal class AmanitaToxicMushroom : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "Glomushroom";

        private bool deployed = false;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                Projectile.damage *= (int)0.5f;
            }
            if (target.type == CWRLoad.DevourerofGodsHead || target.type == CWRLoad.DevourerofGodsTail) {
                modifiers.FinalDamage *= 2f;
            }
        }

        public override void AI() {
            if (!deployed) {
                //飞行阶段
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                Projectile.velocity.Y += 0.2f;

                //发光
                Lighting.AddLight(Projectile.Center, 0.5f, 1f, 0.4f);

                //轨迹粒子
                if (Main.rand.NextBool(2)) {
                    Dust dust = Dust.NewDustDirect(
                        Projectile.position,
                        Projectile.width,
                        Projectile.height,
                        DustID.BlueFairy,
                        0, 0, 100,
                        new Color(150, 255, 100),
                        1.2f
                    );
                    dust.velocity = -Projectile.velocity * 0.3f;
                    dust.noGravity = true;
                }
            }
            else {
                //部署阶段 - 生成毒雾
                Projectile.velocity = Vector2.Zero;
                Projectile.alpha += 3;

                if (Projectile.alpha >= 255) {
                    Projectile.Kill();
                    return;
                }

                //周期性生成毒雾粒子
                if (Projectile.timeLeft % 5 == 0) {
                    SpawnToxicCloud();
                }

                //持续伤害
                if (Projectile.timeLeft % 10 == 0 && Projectile.IsOwnedByLocalPlayer()) {
                    DamageNearbyEnemies();
                }
            }
        }

        private void SpawnToxicCloud() {
            for (int i = 0; i < 3; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(60f, 60f);
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + offset,
                    DustID.BlueFairy,
                    Vector2.Zero,
                    100,
                    new Color(150, 255, 100, 100),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
                dust.velocity = Main.rand.NextVector2Circular(0.5f, 0.5f);
            }

            //孢子粒子
            if (Main.rand.NextBool(2)) {
                Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(50f, 50f);
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(
                    spawnPos,
                    Main.rand.NextVector2Circular(1f, 1f)
                );
                if (prt != null) {
                    prt.Color = new Color(150, 255, 100);
                    prt.Scale = Main.rand.NextFloat(0.8f, 1.4f);
                }
            }
        }

        private void DamageNearbyEnemies() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && npc.CanBeChasedBy() &&
                    Vector2.Distance(npc.Center, Projectile.Center) < 80f) {

                    Player owner = Main.player[Projectile.owner];
                    owner.ApplyDamageToNPC(npc, Projectile.damage / 3, 0, 0, false);

                    //中毒效果
                    npc.AddBuff(BuffID.Poisoned, 120);
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Deploy();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Deploy();
            target.AddBuff(BuffID.Poisoned, 180);
        }

        private void Deploy() {
            if (deployed) return;
            deployed = true;

            Projectile.timeLeft = 180;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;

            SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                Volume = 0.4f,
                Pitch = -0.2f
            }, Projectile.Center);

            //部署特效
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.BlueFairy,
                    velocity,
                    100,
                    new Color(150, 255, 100),
                    Main.rand.NextFloat(1.5f, 2f)
                );
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (deployed) {
                //毒雾状态不绘制蘑菇本体
                return false;
            }

            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;
            float alpha = (255f - Projectile.alpha) / 255f;

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                Color.Lerp(lightColor, new Color(150, 255, 100), 0.5f) * alpha,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None
            );

            return false;
        }
    }
    #endregion

    #region 闪电孢子弹幕
    ///<summary>
    ///闪电孢子 - 会在敌人之间跳跃放电
    ///</summary>
    internal class AmanitaLightningSpore : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private int bounceCount = 0;
        private const int MaxBounces = 3;
        private List<int> hitNPCs = new();

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                Projectile.damage *= (int)0.75f;
            }
            if (target.type == CWRLoad.DevourerofGodsHead || target.type == CWRLoad.DevourerofGodsTail) {
                modifiers.FinalDamage *= 1.33f;
            }
        }

        public override void AI() {
            //闪电发光
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 15f) * 0.4f + 0.6f;
            Lighting.AddLight(Projectile.Center, 1f * pulse, 1f * pulse, 0.6f * pulse);

            //电弧粒子
            if (Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    DustID.BlueFairy,
                    Vector2.Zero,
                    100,
                    new Color(255, 255, 150),
                    Main.rand.NextFloat(1f, 1.5f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1f;
            }

            //轨迹效果
            if (Main.rand.NextBool(4)) {
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(
                    Projectile.Center,
                    -Projectile.velocity * 0.1f
                );
                if (prt != null) {
                    prt.Color = new Color(255, 255, 150);
                    prt.Scale *= 0.5f;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            hitNPCs.Add(target.whoAmI);
            bounceCount++;

            //闪电特效
            SpawnLightningEffect(target.Center);

            if (bounceCount < MaxBounces) {
                //寻找下一个目标
                NPC nextTarget = Projectile.Center.FindClosestNPC(300f);
                if (nextTarget != null) {
                    Vector2 direction = (nextTarget.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = direction * Projectile.velocity.Length();

                    //闪电链连接特效
                    CreateLightningChain(Projectile.Center, nextTarget.Center);
                }
                else {
                    Projectile.Kill();
                }
            }
            else {
                Projectile.Kill();
            }
        }

        private void SpawnLightningEffect(Vector2 position) {
            SoundEngine.PlaySound(SoundID.Item93 with {
                Volume = 0.4f,
                Pitch = 0.5f
            }, position);

            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.BlueFairy,
                    velocity,
                    100,
                    new Color(255, 255, 150),
                    Main.rand.NextFloat(1.3f, 2f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }
        }

        private void CreateLightningChain(Vector2 start, Vector2 end) {
            int segments = 10;
            for (int i = 0; i < segments; i++) {
                float progress = i / (float)segments;
                Vector2 pos = Vector2.Lerp(start, end, progress);
                Vector2 offset = Main.rand.NextVector2Circular(8f, 8f) * (1f - Math.Abs(progress - 0.5f) * 2f);

                Dust dust = Dust.NewDustPerfect(
                    pos + offset,
                    DustID.BlueFairy,
                    Vector2.Zero,
                    100,
                    new Color(255, 255, 150, 200),
                    Main.rand.NextFloat(1f, 1.8f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1f;
            }
        }

        public override void OnKill(int timeLeft) {
            //最终爆炸
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.BlueFairy,
                    velocity,
                    100,
                    new Color(255, 255, 150),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glowTex = TextureAssets.Extra[ExtrasID.SharpTears].Value;

            //绘制闪电拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float fade = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Color color = new Color(255, 255, 150) * fade * 0.7f;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                //添加随机偏移模拟电弧
                Vector2 offset = Main.rand.NextVector2Circular(2f, 2f) * (1f - fade);

                Main.EntitySpriteDraw(
                    glowTex,
                    drawPos + offset,
                    null,
                    color,
                    Projectile.rotation,
                    glowTex.Size() / 2f,
                    Projectile.scale * fade * 0.6f,
                    SpriteEffects.None
                );
            }

            //绘制主体
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 15f) * 0.3f + 0.7f;
            Main.EntitySpriteDraw(
                glowTex,
                Projectile.Center - Main.screenPosition,
                null,
                new Color(255, 255, 150) * pulse,
                Projectile.rotation + MathHelper.PiOver2,
                glowTex.Size() / 2f,
                Projectile.scale,
                SpriteEffects.None
            );

            return false;
        }
    }
    #endregion
}
