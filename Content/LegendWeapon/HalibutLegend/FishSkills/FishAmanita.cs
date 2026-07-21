using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.PRTTypes;
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
    /// <summary>真菌鱼技能，周期切换孢子形态并生成对应攻击</summary>
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

        /// <summary>形态切换拍</summary>
        private void SpawnPhaseTransitionEffect(Player player) {
            FishAmanitaVFX.SporePuffSound(player.Center, 0.3f, 0.42f);

            if (Main.dedServ) {
                return;
            }
            Color phaseColor = FishAmanitaVFX.PhaseColor(sporePhase);

            //暗紫压底环 + 新形态色内环
            PRTLoader.NewParticle<PRT_DWave>(player.Center, Vector2.Zero, FishAmanitaVFX.SporeDusk, 0.15f)
                ?.Configure(Vector2.One, 0f, 0.85f, 14);
            PRTLoader.NewParticle<PRT_DWave>(player.Center, Vector2.Zero, phaseColor, 0.1f)
                ?.Configure(Vector2.One, 0f, 0.55f, 10);

            //孢子小环，撒出后转入布朗漂移
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(0.3f);
                FishAmanitaVFX.SporeDrift(player.Center + angle.ToRotationVector2() * 14f
                    , angle.ToRotationVector2() * Main.rand.NextFloat(2.2f, 3.4f), phaseColor);
            }

            //闪电形态切入
            if (sporePhase == 3) {
                for (int i = 0; i < 2; i++) {
                    Vector2 dir = Main.rand.NextVector2Unit();
                    FishAmanitaVFX.MyceliumArc(player.Center + dir * 12f
                        , player.Center + dir * Main.rand.NextFloat(52f, 78f)
                        , FishAmanitaVFX.ArcVolt, 7f, 8, 1);
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
                    (int)(damage * 2.2f + HalibutData.GetDomainLayer() * 0.55f),
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
                    (int)(damage * 1.2f + HalibutData.GetDomainLayer() * 0.3f),
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
                    (int)(damage * 1.2f * +HalibutData.GetDomainLayer() * 0.3f),
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
                    (int)(damage * 1.6f + HalibutData.GetDomainLayer() * 0.4f),
                    2f,
                    player.whoAmI
                );
            }
        }

        private void SpawnAmbientSpores(Player player) {
            if (Main.dedServ) {
                return;
            }
            Color sporeColor = FishAmanitaVFX.PhaseColor(sporePhase);

            //环绕玩家的低频环境孢子
            if (Main.rand.NextBool(3)) {
                Vector2 spawnPos = player.Center + Main.rand.NextVector2Circular(60f, 60f);
                FishAmanitaVFX.SporeDrift(spawnPos, Main.rand.NextVector2Circular(0.8f, 0.8f)
                    , sporeColor, 0.9f, Main.rand.Next(36, 56));
            }
            if (Main.rand.NextBool(4)) {
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(player.Center + Main.rand.NextVector2Circular(70f, 70f)
                    , Main.rand.NextVector2Circular(1f, 1f));
                if (prt != null) {
                    prt.Color = sporeColor;
                    prt.Scale = Main.rand.NextFloat(0.7f, 1.1f);
                }
            }
        }
    }

    #region 爆炸蘑菇弹幕
    /// <summary>爆炸蘑菇，触敌或触地时菌盖炸裂成孢子环</summary>
    internal class AmanitaExplosiveMushroom : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "Glomushroom";

        private bool exploded = false;
        private float spin;

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
            Projectile.velocity.Y += 0.15f; //重力

            //抛掷翻滚
            float speed = Projectile.velocity.Length();
            spin = MathHelper.Lerp(spin, 0.09f + speed * 0.014f, 0.2f);
            Projectile.rotation += spin * (Projectile.velocity.X >= 0f ? 1f : -1f);

            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 0.5f * pulse, 0.16f * pulse, 0.2f * pulse);

            //孢子粉尾，甩出率∝速度
            if (!Main.dedServ && Projectile.timeLeft % (speed > 9f ? 3 : 5) == 0) {
                FishAmanitaVFX.SporeDrift(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f)
                    , -Projectile.velocity * 0.12f, FishAmanitaVFX.CapCrimson, 0.7f, Main.rand.Next(20, 32));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Explode();
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Explode();
        }

        /// <summary>菌盖炸裂</summary>
        private void Explode() {
            if (exploded) return;
            exploded = true;

            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.45f,
                Pitch = 0.35f
            }, Projectile.Center);
            FishAmanitaVFX.SporePuffSound(Projectile.Center, 0.1f, 0.6f);

            if (!Main.dedServ) {
                FishAmanitaVFX.SporeRing(Projectile.Center, FishAmanitaVFX.CapCrimson, 14, 5.2f, 1.05f);
                for (int i = 0; i < 5; i++) {
                    Vector2 vel = (-Vector2.UnitY).RotatedByRandom(0.9f) * Main.rand.NextFloat(2.5f, 6f);
                    PRTLoader.NewParticle<PRT_FishAmanitaCapShard>(Projectile.Center, vel
                        , FishAmanitaVFX.CapCrimson, Main.rand.NextFloat(1.3f, 2f));
                }
                //孢光热芯，小面积速灭的过冲爆点
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero
                    , FishAmanitaVFX.SporeGlow, 0.3f)?.Configure(8, 0.9f, 0.6f, 1.4f);
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
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float dir = Projectile.velocity.X >= 0f ? 1f : -1f;

            //底层孢光晕，画在蘑菇本体之下
            float pulse = 0.72f + 0.28f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.whoAmI);
            Main.EntitySpriteDraw(soft, drawPos, null, FishAmanitaVFX.CapCrimson with { A = 0 } * (0.4f * pulse)
                , 0f, soft.Size() / 2f, 0.5f, SpriteEffects.None);

            //位移残影，旧位置暗红渐隐
            Color ghost = FishAmanitaVFX.CapDeep with { A = 0 };
            for (int i = 2; i < Projectile.oldPos.Length; i += 2) {
                float fade = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Vector2 gp = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float gr = i < Projectile.oldRot.Length ? Projectile.oldRot[i] : Projectile.rotation;
                Main.EntitySpriteDraw(texture, gp, null, ghost * (fade * 0.4f), gr, origin
                    , Projectile.scale * 0.92f, SpriteEffects.None);
            }

            //旋转拖影，自旋的可视化
            for (int i = 1; i <= 3; i++) {
                Main.EntitySpriteDraw(texture, drawPos, null, ghost * (0.3f - i * 0.08f)
                    , Projectile.rotation - spin * i * 2.6f * dir, origin, Projectile.scale, SpriteEffects.None);
            }

            //本体
            Main.EntitySpriteDraw(texture, drawPos, null, Color.Lerp(lightColor, FishAmanitaVFX.CapCrimson, 0.22f)
                , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);
            return false;
        }
    }
    #endregion

    #region 追踪孢子弹幕
    /// <summary>追踪孢子，寻最近敌人，咬合强度随时间收紧</summary>
    internal class AmanitaHomingSpore : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float homingDelay = 15f;
        private float age;

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
            age++;
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 0.25f * pulse, 0.6f * pulse, 0.62f * pulse);

            //追踪逻辑，延迟后咬合随时间收紧
            if (homingDelay > 0) {
                homingDelay--;
            }
            else {
                NPC target = Projectile.Center.FindClosestNPC(400f, true, chasedByNPC: npc => npc.CanBeChasedBy(Projectile));
                if (target != null) {
                    float bite = MathHelper.Lerp(0.05f, 0.14f, MathHelper.Clamp(age / 90f, 0f, 1f));
                    Projectile.SmoothHomingBehavior(target.Center, 1f, bite);
                }
            }

            if (!Main.dedServ) {
                //孢子粉尾，追孢青发光孢子
                if (Projectile.timeLeft % 6 == 0) {
                    FishAmanitaVFX.SporeDrift(Projectile.Center
                        , -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.4f, 0.4f)
                        , FishAmanitaVFX.HomingCyan, 0.62f, Main.rand.Next(18, 30));
                }
                //实体孢子颗粒补底
                if (Main.rand.NextBool(24)) {
                    var prt = PRTLoader.NewParticle<PRT_SporeBobo>(Projectile.Center, -Projectile.velocity * 0.1f);
                    if (prt != null) {
                        prt.Color = FishAmanitaVFX.HomingCyan;
                        prt.Scale = 0.55f;
                    }
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            FishAmanitaVFX.SporePuffSound(Projectile.Center, 0.5f, 0.3f);
            if (Main.dedServ) {
                return;
            }
            //咬中，小孢子环贴着命中点炸开
            FishAmanitaVFX.SporeRing(Projectile.Center, FishAmanitaVFX.HomingCyan, 7, 2.8f, 0.7f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //消亡后孢子继续漂移
            for (int i = 0; i < 5; i++) {
                FishAmanitaVFX.SporeDrift(Projectile.Center, Main.rand.NextVector2Circular(1.6f, 1.6f)
                    , FishAmanitaVFX.HomingCyan, 0.7f, Main.rand.Next(24, 40));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Vector2 origin = soft.Size() / 2f;
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(0.55f + speed * 0.075f, 0.6f, 1.7f);
            float rot = Projectile.rotation + MathHelper.PiOver2;

            //残影链
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i -= 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 p = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float gr = (i < Projectile.oldRot.Length ? Projectile.oldRot[i] : Projectile.rotation) + MathHelper.PiOver2;
                Main.EntitySpriteDraw(soft, p, null, FishAmanitaVFX.SporeDusk with { A = 0 } * (0.3f * fade)
                    , gr, origin, new Vector2(0.3f, 0.62f) * fade * stretch, SpriteEffects.None);
                Main.EntitySpriteDraw(soft, p, null, FishAmanitaVFX.HomingCyan with { A = 0 } * (0.4f * fade)
                    , gr, origin, new Vector2(0.16f, 0.5f) * fade * stretch, SpriteEffects.None);
            }

            //本体
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(soft, drawPos, null, FishAmanitaVFX.SporeDusk with { A = 0 } * 0.55f
                , rot, origin, new Vector2(0.34f, 0.5f * stretch + 0.2f), SpriteEffects.None);
            Main.EntitySpriteDraw(soft, drawPos, null, FishAmanitaVFX.HomingCyan with { A = 0 } * 0.85f
                , rot, origin, new Vector2(0.2f, 0.34f * stretch + 0.12f), SpriteEffects.None);
            Texture2D sheet = FishAmanitaAssets.SporeSheet?.Value;
            if (sheet != null) {
                Rectangle frame = sheet.GetRectangle(Projectile.whoAmI % 4, 4);
                Main.EntitySpriteDraw(sheet, drawPos, frame, FishAmanitaVFX.HomingCyan with { A = 0 } * 0.95f
                    , Projectile.rotation + age * 0.04f, frame.Size() / 2f, 0.6f, SpriteEffects.None);
            }
            Main.EntitySpriteDraw(soft, drawPos, null, FishAmanitaVFX.SporeGlow with { A = 0 } * 0.9f
                , rot, origin, new Vector2(0.08f, 0.14f * stretch), SpriteEffects.None);
            return false;
        }
    }
    #endregion

    #region 毒雾蘑菇弹幕
    /// <summary>毒雾蘑菇，落地伞盖破裂展开浓稠孢子云持续伤害</summary>
    internal class AmanitaToxicMushroom : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "Glomushroom";

        private bool deployed = false;
        private float spin;
        private float cloudSeed;

        //部署时间轴
        private const int DeployLife = 180;

        private float DeployAge => DeployLife - Projectile.timeLeft;

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
                modifiers.FinalDamage *= 0.5f;
            }
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail) {
                modifiers.FinalDamage *= 2f;
            }
        }

        public override void AI() {
            if (!deployed) {
                //飞行阶段
                Projectile.velocity.Y += 0.2f;
                spin = MathHelper.Lerp(spin, 0.05f + Projectile.velocity.Length() * 0.009f, 0.2f);
                Projectile.rotation += spin * (Projectile.velocity.X >= 0f ? 1f : -1f);

                Lighting.AddLight(Projectile.Center, 0.32f, 0.2f, 0.5f);

                if (!Main.dedServ && Projectile.timeLeft % 5 == 0) {
                    //浓孢子比弹体沉，往下滴
                    FishAmanitaVFX.SporeDrift(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f)
                        , new Vector2(-Projectile.velocity.X * 0.05f, 0.4f)
                        , FishAmanitaVFX.MistOrchid, 0.72f, Main.rand.Next(22, 34));
                }
            }
            else {
                //部署阶段
                Projectile.velocity = Vector2.Zero;
                Projectile.alpha += 3;

                if (Projectile.alpha >= 255) {
                    Projectile.Kill();
                    return;
                }

                //周期性生成云内悬浮孢子
                if (Projectile.timeLeft % 5 == 0) {
                    SpawnToxicCloud();
                }

                float env = MathHelper.Clamp(DeployAge / 14f, 0f, 1f) * (1f - Projectile.alpha / 255f * 0.6f);
                Lighting.AddLight(Projectile.Center, 0.3f * env, 0.16f * env, 0.5f * env);

                //持续伤害
                if (Projectile.timeLeft % 10 == 0 && Projectile.IsOwnedByLocalPlayer()) {
                    DamageNearbyEnemies();
                }
            }
        }

        /// <summary>云内点缀</summary>
        private void SpawnToxicCloud() {
            if (Main.dedServ) {
                return;
            }
            Vector2 offset = Main.rand.NextVector2Circular(62f, 44f);
            FishAmanitaVFX.SporeDrift(Projectile.Center + offset
                , new Vector2(0f, -0.25f) + Main.rand.NextVector2Circular(0.3f, 0.2f)
                , Main.rand.NextBool(3) ? FishAmanitaVFX.SporeGlow : FishAmanitaVFX.MistOrchid
                , 0.66f, Main.rand.Next(30, 46));

            if (Main.rand.NextBool(2)) {
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(Projectile.Center + Main.rand.NextVector2Circular(52f, 40f)
                    , Main.rand.NextVector2Circular(0.6f, 0.4f));
                if (prt != null) {
                    prt.Color = FishAmanitaVFX.MistOrchid;
                    prt.Scale = Main.rand.NextFloat(0.7f, 1.2f);
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

        /// <summary>伞盖破裂</summary>
        private void Deploy() {
            if (deployed) return;
            deployed = true;

            Projectile.timeLeft = DeployLife;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            cloudSeed = (Projectile.identity % 97) * 0.117f;

            FishAmanitaVFX.SporePuffSound(Projectile.Center, -0.2f, 0.5f);

            if (!Main.dedServ) {
                FishAmanitaVFX.SporeRing(Projectile.Center, FishAmanitaVFX.MistOrchid, 12, 3.6f, 1f);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_FishAmanitaCapShard>(Projectile.Center
                        , (-Vector2.UnitY).RotatedByRandom(1.1f) * Main.rand.NextFloat(1.8f, 4.2f)
                        , FishAmanitaVFX.MistOrchid, Main.rand.NextFloat(1.2f, 1.8f));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !deployed) {
                return;
            }
            //云散残留
            for (int i = 0; i < 8; i++) {
                FishAmanitaVFX.SporeDrift(Projectile.Center + Main.rand.NextVector2Circular(56f, 40f)
                    , new Vector2(0f, -0.15f) + Main.rand.NextVector2Circular(0.35f, 0.2f)
                    , Main.rand.NextBool(4) ? FishAmanitaVFX.SporeGlow : FishAmanitaVFX.MistOrchid
                    , 0.7f, Main.rand.Next(50, 80));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (deployed) {
                DrawMistCloud();
                return false;
            }

            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float dir = Projectile.velocity.X >= 0f ? 1f : -1f;

            //底层瘴紫光晕
            Main.EntitySpriteDraw(soft, drawPos, null, FishAmanitaVFX.MistOrchid with { A = 0 } * 0.38f
                , 0f, soft.Size() / 2f, 0.55f, SpriteEffects.None);

            //旋转拖影
            Color ghost = FishAmanitaVFX.MistDeep with { A = 0 };
            for (int i = 1; i <= 3; i++) {
                Main.EntitySpriteDraw(texture, drawPos, null, ghost * (0.28f - i * 0.07f)
                    , Projectile.rotation - spin * i * 2.4f * dir, origin, Projectile.scale, SpriteEffects.None);
            }

            //本体
            Main.EntitySpriteDraw(texture, drawPos, null, Color.Lerp(lightColor, FishAmanitaVFX.MistOrchid, 0.3f)
                , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);
            return false;
        }

        /// <summary>浓稠孢子云</summary>
        private void DrawMistCloud() {
            float reveal = MathHelper.Clamp(DeployAge / 16f, 0f, 1f);
            //前 45% 生命云体保持完整，之后进入噪声侵蚀消散
            float erode = MathHelper.Clamp((Projectile.alpha / 255f - 0.45f) / 0.55f, 0f, 1f);
            Effect fx = FishAmanitaAssets.FishAmanitaMist;
            Vector2 center = Projectile.Center;
            const float hx = 150f;
            const float hy = 105f;

            if (fx == null || CWRAsset.PerlinNoise?.Value == null) {
                //降级，雾团贴图三层，暗紫压底
                Texture2D fog = CWRAsset.Fog?.Value;
                if (fog == null) {
                    return;
                }
                float alpha = reveal * (1f - erode);
                for (int i = 0; i < 3; i++) {
                    float t = Main.GlobalTimeWrappedHourly * (0.2f + i * 0.13f) + i * 2.1f + cloudSeed * 10f;
                    Vector2 off = new Vector2(MathF.Cos(t), MathF.Sin(t * 1.3f)) * (10f + i * 8f);
                    Main.EntitySpriteDraw(fog, center + off - Main.screenPosition, null
                        , (i == 0 ? FishAmanitaVFX.MistDeep : FishAmanitaVFX.MistOrchid) with { A = 0 } * (alpha * (0.36f - i * 0.08f))
                        , t * 0.3f, fog.Size() / 2f, 0.9f + i * 0.22f, SpriteEffects.None);
                }
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(cloudSeed);
            fx.Parameters["uReveal"]?.SetValue(reveal);
            fx.Parameters["uErode"]?.SetValue(erode);
            fx.Parameters["uSizePx"]?.SetValue(new Vector2(hx * 2f, hy * 2f));
            fx.Parameters["uNoiseTex"]?.SetValue(CWRAsset.PerlinNoise.Value);
            fx.Parameters["uColDense"]?.SetValue(FishAmanitaVFX.MistDeep.ToVector3());
            fx.Parameters["uColMist"]?.SetValue(FishAmanitaVFX.MistOrchid.ToVector3());
            fx.Parameters["uColGlow"]?.SetValue(FishAmanitaVFX.SporeGlow.ToVector3());

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(center.X - hx, center.Y - hy, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(center.X + hx, center.Y - hy, 0f), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector3(center.X - hx, center.Y + hy, 0f), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(new Vector3(center.X + hx, center.Y + hy, 0f), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }
    }
    #endregion

    #region 闪电孢子弹幕
    /// <summary>闪电孢子，命中后经菌丝电弧折射至下一目标</summary>
    internal class AmanitaLightningSpore : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

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
                modifiers.FinalDamage *= 0.75f;
            }
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail) {
                modifiers.FinalDamage *= 1.33f;
            }
        }

        public override void AI() {
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 15f) * 0.4f + 0.6f;
            Lighting.AddLight(Projectile.Center, 0.55f * pulse, 0.45f * pulse, 0.85f * pulse);

            if (!Main.dedServ) {
                //体表生物电
                if (Main.rand.NextBool(20)) {
                    Vector2 back = Projectile.Center - Projectile.velocity * Main.rand.NextFloat(2f, 5f);
                    FishAmanitaVFX.MyceliumArc(back, Projectile.Center + Projectile.velocity * 2f
                        , FishAmanitaVFX.ArcVolt, 5f, 6, 0, 1.3f);
                }
                //孢子粉尾
                if (Main.rand.NextBool(9)) {
                    FishAmanitaVFX.SporeDrift(Projectile.Center, -Projectile.velocity * 0.06f
                        , FishAmanitaVFX.ArcVolt, 0.55f, Main.rand.Next(16, 26));
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

        /// <summary>命中节点</summary>
        private void SpawnLightningEffect(Vector2 position) {
            SoundEngine.PlaySound(SoundID.Item93 with {
                Volume = 0.4f,
                Pitch = 0.5f
            }, position);
            SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.2f, Pitch = 0.6f, MaxInstances = 4 }, position);

            if (Main.dedServ) {
                return;
            }
            FishAmanitaVFX.SporeRing(position, FishAmanitaVFX.ArcVolt, 8, 3.4f, 0.8f);
            for (int i = 0; i < 2; i++) {
                Vector2 dir = Main.rand.NextVector2Unit();
                FishAmanitaVFX.MyceliumArc(position, position + dir * Main.rand.NextFloat(34f, 60f)
                    , FishAmanitaVFX.ArcVolt, 6f, 8, 1, 1.2f);
            }
        }

        /// <summary>折射链</summary>
        private void CreateLightningChain(Vector2 start, Vector2 end) {
            if (Main.dedServ) {
                return;
            }
            FishAmanitaVFX.MyceliumArc(start, end, FishAmanitaVFX.ArcVolt, 11f, 13, 2);
            //链身孢子迸出
            for (int i = 0; i < 4; i++) {
                Vector2 p = Vector2.Lerp(start, end, Main.rand.NextFloat());
                FishAmanitaVFX.SporeDrift(p, Main.rand.NextVector2Circular(1.2f, 1.2f)
                    , FishAmanitaVFX.ArcVolt, 0.6f, Main.rand.Next(18, 30));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //终末星散
            for (int i = 0; i < 3; i++) {
                float ang = MathHelper.TwoPi * i / 3f + Main.rand.NextFloat(0.7f);
                FishAmanitaVFX.MyceliumArc(Projectile.Center
                    , Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(30f, 52f)
                    , FishAmanitaVFX.ArcVolt, 6f, 9, 1, 1.25f);
            }
            for (int i = 0; i < 6; i++) {
                FishAmanitaVFX.SporeDrift(Projectile.Center, Main.rand.NextVector2Circular(1.6f, 1.6f)
                    , FishAmanitaVFX.ArcVolt, 0.65f, Main.rand.Next(24, 40));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Vector2 origin = soft.Size() / 2f;
            float rot = Projectile.rotation + MathHelper.PiOver2;

            //残影链
            Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i -= 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                float wob = MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + i * 1.7f + Projectile.whoAmI) * 2.4f * (1f - fade);
                Vector2 p = Projectile.oldPos[i] + Projectile.Size / 2f + perp * wob - Main.screenPosition;
                Main.EntitySpriteDraw(soft, p, null, FishAmanitaVFX.SporeDusk with { A = 0 } * (0.3f * fade)
                    , rot, origin, new Vector2(0.24f, 0.5f) * fade, SpriteEffects.None);
                Main.EntitySpriteDraw(soft, p, null, FishAmanitaVFX.ArcVolt with { A = 0 } * (0.42f * fade)
                    , rot, origin, new Vector2(0.13f, 0.4f) * fade, SpriteEffects.None);
            }

            //本体
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(0.5f + speed * 0.06f, 0.55f, 1.4f);
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 15f) * 0.25f + 0.75f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(soft, drawPos, null, FishAmanitaVFX.SporeDusk with { A = 0 } * (0.6f * pulse)
                , rot, origin, new Vector2(0.36f, 0.44f * stretch + 0.16f), SpriteEffects.None);
            Main.EntitySpriteDraw(soft, drawPos, null, FishAmanitaVFX.ArcVolt with { A = 0 } * (0.85f * pulse)
                , rot, origin, new Vector2(0.2f, 0.3f * stretch + 0.1f), SpriteEffects.None);
            Texture2D sheet = FishAmanitaAssets.SporeSheet?.Value;
            if (sheet != null) {
                Rectangle frame = sheet.GetRectangle(Projectile.whoAmI % 4, 4);
                Main.EntitySpriteDraw(sheet, drawPos, frame, FishAmanitaVFX.ArcVolt with { A = 0 } * (0.9f * pulse)
                    , Projectile.rotation, frame.Size() / 2f, 0.52f, SpriteEffects.None);
            }
            Main.EntitySpriteDraw(soft, drawPos, null, FishAmanitaVFX.SporeGlow with { A = 0 } * (0.95f * pulse)
                , rot, origin, new Vector2(0.08f, 0.13f * stretch), SpriteEffects.None);
            return false;
        }
    }
    #endregion
}
