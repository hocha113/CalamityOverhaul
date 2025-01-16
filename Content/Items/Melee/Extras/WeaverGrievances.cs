using CalamityMod;
using CalamityMod.Items.Accessories;
using CalamityMod.Items.Armor.Bloodflare;
using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Items.Weapons.Rogue;
using CalamityMod.Items.Weapons.Summon;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Projectiles.Weapons;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
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

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    /// <summary>
    /// 怨念编织者
    /// </summary>
    internal class WeaverGrievances : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "WeaverGrievances";
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        public override void SetDefaults() {
            Item.height = 154;
            Item.width = 154;
            Item.damage = 855;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 22;
            Item.scale = 1;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.knockBack = 7.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(13, 53, 75, 0);
            Item.rare = ItemRarityID.Red;
            Item.crit = 8;
            Item.shoot = ModContent.ProjectileType<WeaverBeam>();
            Item.shootSpeed = 18f;
            Item.SetKnifeHeld<WeaverGrievancesHeld>();
        }
        internal static void SpwanInOwnerDust(Player player) {
            if (Main.dedServ) {
                return;
            }
            Vector2 handOffset = Main.OffsetsPlayerOnhand[player.bodyFrame.Y / 56] * 2f;
            if (player.direction != 1) {
                handOffset.X = player.bodyFrame.Width - handOffset.X;
            }
            if (player.gravDir != 1f) {
                handOffset.Y = player.bodyFrame.Height - handOffset.Y;
            }
            handOffset -= new Vector2(player.bodyFrame.Width - player.width, player.bodyFrame.Height - player.height) / 2f;
            Vector2 rotatedHandPosition = player.RotatedRelativePoint(player.position + handOffset, true);
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustDirect(player.Center, 0, 0, DustID.RedTorch, 0f, 0f, 150, default, 1.3f);
                dust.position = rotatedHandPosition;
                dust.velocity = Vector2.Zero;
                dust.noGravity = true;
                dust.fadeIn = 1f;
                dust.velocity += player.velocity;
                dust.position += Utils.RandomVector2(Main.rand, -4f, 4f);
                dust.scale += Main.rand.NextFloat();
            }
        }
        public override bool AltFunctionUse(Player player) => true;
        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 6;
        public override void ModifyTooltips(List<TooltipLine> tooltips) => CWRUtils.SetItemLegendContentTops(ref tooltips, Name);
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                if (player.CWR().CustomCooldownCounter <= 0) {
                    float _swingDir = position.X + velocity.X > player.position.X ? 1 : -1;
                    Projectile.NewProjectile(source, position, velocity
                        , ModContent.ProjectileType<WeaverGrievancesHurmp>(), damage, knockback, player.whoAmI, ai1: _swingDir);
                }

                return false;
            }
            if (Main.zenithWorld) {
                Projectile.NewProjectile(source, position, velocity
                        , ModContent.ProjectileType<WeaverBeam>(), damage, knockback, player.whoAmI);
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<TerrorBlade>()
                .AddIngredient<BansheeHook>()
                .AddIngredient<GhoulishGouger>()
                .AddIngredient<FatesReveal>()
                .AddIngredient<GhastlyVisage>()
                .AddIngredient<DaemonsFlame>()
                .AddIngredient<EtherealSubjugator>()
                .AddIngredient<Affliction>()
                .AddIngredient<Necroplasm>(5)
                .AddIngredient<RuinousSoul>(5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    internal class WeaverGrievancesHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<WeaverGrievances>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail2";
        public override string gradientTexturePath => CWRConstant.ColorBar + "WeaverGrievances_Bar";
        public override string GlowTexturePath => CWRConstant.Item_Melee + "WeaverGrievances_Glow";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = -20;
            drawTrailBtommWidth = 80;
            drawTrailTopWidth = 80;
            drawTrailCount = 6;
            Length = 200;
            unitOffsetDrawZkMode = 0;
            Projectile.width = Projectile.height = 186;
            distanceToOwner = -60;
            SwingData.starArg = 30;
            SwingData.ler1_UpLengthSengs = 0.05f;
            SwingData.minClampLength = 200;
            SwingData.maxClampLength = 210;
            SwingData.ler1_UpSizeSengs = 0.016f;
            SwingData.baseSwingSpeed = 4.2f;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            autoSetShoot = true;
        }

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(
            phase0SwingSpeed: -0.2f,
            phase1SwingSpeed: 18.2f,
            phase2SwingSpeed: 2f,
            swingSound: SoundID.Item1 with { Pitch = -0.6f });
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {
            for (int i = 0; i < 6; i++) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), ShootSpanPos
                    , ShootVelocity.RotatedByRandom(0.2f).UnitVector() * Main.rand.NextFloat(16.6f, 28f),
                ModContent.ProjectileType<WeaverBeam>(), Projectile.damage / 2, Projectile.knockBack / 2, Projectile.owner, 0, 0, i);
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(ModContent.BuffType<SoulBurning>(), 300);
        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(ModContent.BuffType<SoulBurning>(), 300);
    }

    internal class WeaverGrievancesHurmp : BaseHeldProj
    {
        public override string Texture => "CalamityMod/NPCs/Polterghast/Polterghast";
        private ref float Time => ref Projectile.ai[0];
        private ref float SwingDir => ref Projectile.ai[1];
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }
        public override void SetDefaults() {
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.height = 64;
            Projectile.width = 64;
            Projectile.friendly = true;
            Projectile.scale = 1f;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }
        public override bool? CanDamage() => null;
        public override bool ShouldUpdatePosition() => false;
        public override void AI() {
            CWRPlayer modPlayer = Owner.CWR();
            modPlayer.IsRotatingDuringDash = true;

            // 初始化冲刺效果
            if (Time == 0) {
                modPlayer.PendingDashVelocity = Projectile.velocity.UnitVector() * 23;

                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Bottom
                    , Vector2.Zero, ModContent.ProjectileType<WeaverExplode>(), 100, 2);

                // 生成烟雾和火焰粒子
                for (int k = 0; k < 7; k++) {
                    Vector2 randomVelocity = Owner.velocity.RotatedByRandom(MathHelper.ToRadians(7)) * (1f - Main.rand.NextFloat(0.3f));
                    Dust.NewDust(Owner.Bottom, 0, 0, DustID.Smoke, randomVelocity.X * 0.5f, randomVelocity.Y * 0.5f);
                    Dust.NewDust(Owner.Bottom, 0, 0, DustID.InfernoFork, randomVelocity.X * 0.5f, randomVelocity.Y * 0.5f);
                }

                CreateEllipseDust(Projectile.velocity, Projectile.Center, 13, 1.2f, 0.8f);
                SoundStyle sound = BloodflareHeadRanged.ActivationSound;
                sound.Pitch = -0.3f;
                SoundEngine.PlaySound(sound, Projectile.position);
            }

            // 周期性粒子效果
            if (Time % 12 == 0 || Time % 6 == 0) {
                Vector2 particlePosition = Time % 12 == 0
                    ? Projectile.Center
                    : Projectile.Center + Main.rand.NextVector2Circular(8, 8);
                Dust d = Dust.NewDustPerfect(particlePosition, DustID.RedTorch, Vector2.Zero, Scale: 1);
                d.noGravity = true;
            }

            if (Time < 20) {
                Owner.GivePlayerImmuneState(6);
            }

            modPlayer.RotationDirection = (int)SwingDir;

            Projectile.Center = Owner.GetPlayerStabilityCenter();
            Projectile.rotation = Owner.fullRotation;

            if (Time < 10) {
                Projectile.scale += 0.04f;
            }
            else if (Projectile.scale > 1f) {
                Projectile.scale -= 0.02f;
            }

            Owner.heldProj = Projectile.whoAmI;
            Owner.ChangeDir(Projectile.velocity.X < 0 ? -1 : 1);
            Owner.itemRotation = Projectile.rotation * Owner.direction;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;

            Lighting.AddLight(Projectile.position, Color.Red.ToVector3() * 0.78f);

            if ((Time > 8 && Owner.velocity.Length() < 5) || DownLeft) {
                Projectile.Kill();
            }

            Time++;
        }

        private void CreateEllipseDust(Vector2 velocity, Vector2 center, float scale, float ellipseFactorX, float ellipseFactorY) {
            Vector2 velocityDirection = velocity.SafeNormalize(Vector2.Zero);
            float angle = (float)Math.Atan2(velocityDirection.Y, velocityDirection.X);

            for (int i = 0; i <= 360; i += 3) {
                float radian = MathHelper.ToRadians(i);
                Vector2 dustOffset = new Vector2(
                    MathF.Cos(radian) * ellipseFactorX,
                    MathF.Sin(radian) * ellipseFactorY
                ) * scale;
                dustOffset = dustOffset.RotatedBy(angle - MathHelper.PiOver2);

                int dustIndex = Dust.NewDust(center, 0, 0, DustID.FireworkFountain_Red, dustOffset.X, dustOffset.Y, 0, Color.White, 2f);
                Main.dust[dustIndex].noGravity = true;
                Main.dust[dustIndex].position = center + dustOffset;
                Main.dust[dustIndex].velocity = dustOffset;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.Explode();
            target.AddBuff(ModContent.BuffType<SoulBurning>(), 300);
            if (Time < 4) {
                Owner.GivePlayerImmuneState(16);
                CombatText.NewText(target.Hitbox, Color.Gold, "Perfect Dodge!!!", true);
            }
        }

        public override void OnKill(int timeLeft) {
            CWRPlayer modPlayer = Owner.CWR();
            modPlayer.IsRotatingDuringDash = false;
            modPlayer.RotationResetCounter = 15;
            modPlayer.DashCooldownCounter = 35;
            modPlayer.CustomCooldownCounter = 30;
            if (Main.zenithWorld) {
                modPlayer.CustomCooldownCounter = 2;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D boss = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = CWRUtils.GetRec(boss, 0, 12);
            float drawBossRot = Owner.velocity.ToRotation() + MathHelper.PiOver2;
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 dreaBossPos = (Projectile.oldPos[k] - Main.screenPosition) + Projectile.Size / 2;
                Color color = Projectile.GetAlpha(Color.Lerp(Color.DarkRed, Color.OrangeRed, 1f / Projectile.oldPos.Length * k) * (1f - 1f / Projectile.oldPos.Length * k));
                color.A = 0;
                float slp = (0.6f + 0.4f * (Projectile.oldPos.Length - k) / Projectile.oldPos.Length);
                Main.EntitySpriteDraw(boss, dreaBossPos, rectangle, color, drawBossRot, rectangle.Size() / 2, Projectile.scale * slp, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    internal class WeaverExplode : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 82;
            Projectile.height = 82;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Vector2 pos = Projectile.Center + CWRUtils.randVr(Projectile.width / 2);
            if (Main.player[Projectile.owner].ZoneDungeon) {
                PRTLoader.NewParticle<PRT_SoulFire>(pos, new Vector2(0, -Main.rand.Next(2, 4)), default, Main.rand.NextFloat(0.3f, 1));
            }
            else {
                PRTLoader.NewParticle<PRT_HellFire>(pos, new Vector2(0, -Main.rand.Next(2, 4)), default, Main.rand.NextFloat(0.3f, 1));
            }
        }
    }

    internal class WeaverBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "BansheeHookScythe";
        public static Color sloudColor1 => new Color(100, 43, 69);
        public static Color sloudColor2 => new Color(200, 111, 145);
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 32;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 62;
            Projectile.height = 62;
            Projectile.timeLeft = 300;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 4;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Projectile.ai[0] == 0 && Projectile.ai[1] >= 0) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot, Projectile.position);
                Projectile.spriteDirection = Projectile.direction;
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
                Projectile.ai[0] = 1;
            }

            if (Projectile.ai[1] < 160) {
                if (Projectile.ai[1] >= 60) {
                    Projectile.scale -= 0.004f;
                }
                else {
                    if (Main.zenithWorld) {
                        CartePRTEffect();
                    }
                    Projectile.scale += 0.016f;
                }

                if (Projectile.alpha <= 155) {
                    Projectile.alpha += 2;
                }

                Projectile.velocity *= 0.98f;
                if (Projectile.velocity.Length() > 16) {
                    Projectile.velocity *= 0.98f;
                }

                if (Main.zenithWorld) {//在天顶世界中追踪敌人
                    CalamityUtils.HomeInOnNPC(Projectile, ignoreTiles: true, 300f, 12f, 20f);
                }
            }
            else {
                Projectile.SmoothHomingBehavior(Main.player[Projectile.owner].Center, 1, 0.01f);
            }

            Projectile.spriteDirection = Projectile.direction;
            Projectile.rotation += Projectile.velocity.X;

            if (Projectile.ai[1] == 162) {
                if (Main.player[Projectile.owner].ZoneDungeon || Main.player[Projectile.owner].ZoneHell()) {
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<WeaverExplode>(), Projectile.damage, 0, Projectile.owner);
                }
            }

            if (Projectile.ai[1] == 160) {
                if (Projectile.ai[2] == 0) {
                    SoundStyle sound = SoundID.NPCDeath39;
                    sound.MaxInstances = 6;
                    sound.Pitch = -0.6f;
                    sound.Volume = 0.6f;
                    SoundEngine.PlaySound(sound, Projectile.Center);
                }
                Projectile.velocity = Projectile.Center.To(Main.player[Projectile.owner].Center).RotatedByRandom(0.6f).UnitVector() * 16;

                Vector2 velocityDirection = Projectile.velocity.SafeNormalize(Vector2.Zero);
                float angle = (float)Math.Atan2(velocityDirection.Y, velocityDirection.X);
                float ellipseFactorX = 1.2f;  // X轴的缩放，控制椭圆的宽度
                float ellipseFactorY = 0.8f;  // Y轴的缩放，控制椭圆的高度
                for (int i = 0; i <= 360; i += 3) {
                    // 计算粒子在椭圆轨迹上的位置
                    float radian = MathHelper.ToRadians(i);
                    Vector2 vr = new Vector2(MathF.Cos(radian) * ellipseFactorX, MathF.Sin(radian) * ellipseFactorY) * 3;
                    vr = vr.RotatedBy(angle - MathHelper.PiOver2);
                    // 新建粒子，并设置其位置、速度
                    int num = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height, DustID.RedTorch, vr.X, vr.Y, 0, Color.White, 2f);
                    Main.dust[num].noGravity = true;  // 不受重力影响
                    Main.dust[num].position = Projectile.Center + vr;  // 设置粒子在轨迹上的位置
                    Main.dust[num].velocity = vr;  // 设置粒子的速度方向
                }
            }

            Lighting.AddLight(Projectile.Center, sloudColor1.ToVector3() * 1.75f * Main.essScale);
            Projectile.ai[1]++;
        }

        private void CartePRTEffect() {
            int particleCount = 6; // 粒子数量
            float arcAngle = MathHelper.Pi; // 圆弧的角度范围，MathHelper.Pi 表示半圆
            Vector2 baseDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX); // 基准方向

            for (int i = 0; i < particleCount; i++) {
                // 根据粒子索引计算角度
                float angleOffset = -arcAngle / 2 + arcAngle * (i / (float)(particleCount - 1));
                Vector2 direction = baseDirection.RotatedBy(angleOffset); // 基准方向旋转到新角度
                float distance = Main.rand.Next(20, 30) * Projectile.scale; // 粒子的随机距离
                Vector2 spawnPos = Projectile.Center;
                Vector2 ver = -direction * Main.rand.NextFloat(0.6f, 0.9f);
                float slp = Main.rand.NextFloat(1f, 1.2f);
                PRT_Spark prt = new PRT_Spark(spawnPos, ver, false, 8, slp, sloudColor1);
                PRTLoader.AddParticle(prt);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawOrigin = texture.Size() / 2;
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + Projectile.Size / 2;
                Color color = Projectile.GetAlpha(Color.Lerp(sloudColor1, sloudColor2, 1f / Projectile.oldPos.Length * k) * (1f - 1f / Projectile.oldPos.Length * k));
                if (Projectile.ai[1] > 160) {
                    color.A = 0;
                }
                float slp = (0.6f + 0.4f * (Projectile.oldPos.Length - k) / Projectile.oldPos.Length);
                Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, drawOrigin, Projectile.scale * slp, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
