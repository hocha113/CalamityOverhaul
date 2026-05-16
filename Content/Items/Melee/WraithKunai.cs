using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 鬼魅飞刀 —— 战士的鬼魂消耗投掷品
    /// 一次掷出三把猩红飞刀，飞刀会先疾飞短停、再从远处加速冲撞最近敌人，最后炸裂为灼魂火浪
    /// </summary>
    internal class WraithKunai : ModItem
    {
        public override string Texture => CWRConstant.Item_Rogue + "WraithKunai";

        public override void SetDefaults() {
            Item.width = 38;
            Item.height = 38;
            Item.damage = 160;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 10;
            Item.knockBack = 4f;
            Item.UseSound = SoundID.Item39 with { Pitch = -0.1f, Volume = 0.7f };
            Item.autoReuse = true;
            Item.value = Item.sellPrice(copper: 24);
            Item.rare = ItemRarityID.Purple;
            Item.DamageType = DamageClass.Melee;
            Item.shoot = ModContent.ProjectileType<WraithKunaiProj>();
            Item.shootSpeed = 18f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //战士齐射 3 把扇形飞刀，每把都进入"短停 + 二次冲撞"AI
            for (int i = 0; i < 3; i++) {
                Vector2 vel = velocity.RotatedBy(MathHelper.ToRadians(-12 + 12 * i));
                Projectile.NewProjectile(source, position, vel, type, damage, knockback, player.whoAmI, 0f, 0f, 1f);
            }
            return false;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe(333)
                .AddIngredient(ItemID.LunarBar, 30)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
                return;
            }
            CreateRecipe(333).
                AddIngredient(CWRID.Item_RuinousSoul).
                AddIngredient(CWRID.Item_Necroplasm).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    /// <summary>
    /// 鬼魅飞刀实体
    /// 阶段0(0~Inder1): 直线投掷
    /// 阶段1(Inder1): 短停定点
    /// 阶段2(Inder1~Inder2): 重定位锁定目标
    /// 阶段3(Inder2+): 加速二次冲撞，命中或寿命终结时炸裂
    /// </summary>
    internal class WraithKunaiProj : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Rogue + "WraithKunai";

        private const int Inder1 = 45;
        private const int Inder2 = 80;

        private Vector2 origPos;
        private Vector2 origVer;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.timeLeft = 300;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.extraUpdates = 4;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            //ai[2] = 1 表示进入战士冲锋 AI（默认所有由本武器投出的飞刀都启用此 AI）
            if (Projectile.ai[2] > 0) {
                Projectile.extraUpdates = 0;

                //首次进入冲锋 AI 时记录回旋点
                if (Projectile.ai[0] == 0 && Projectile.IsOwnedByLocalPlayer()) {
                    origPos = Projectile.Center + VaultUtils.RandVr(132, 660);
                    Projectile.netUpdate = true;
                }

                Projectile.ai[0]++;

                if (Projectile.ai[0] == Inder2) {
                    Projectile.extraUpdates = 2;
                }

                if (Projectile.ai[0] >= Inder2) {
                    if (Projectile.Center.FindClosestNPC(1300f) != null) {
                        CWRRef.HomeInOnNPC(Projectile, !Projectile.tileCollide, 1300f, 12f, 20f);
                        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                    }
                }
                else if (Projectile.ai[0] > Inder1) {
                    origVer = Projectile.Center.DirectionTo(Main.player[Projectile.owner].Center) * 45;
                    NPC target = Projectile.Center.FindClosestNPC(1600);
                    if (target != null) {
                        origVer = Projectile.Center.DirectionTo(target.Center) * 45;
                    }
                    Projectile.rotation = origVer.ToRotation() + MathHelper.PiOver2;
                }
                else if (Projectile.ai[0] == Inder1) {
                    Projectile.velocity = Vector2.Zero;
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        origVer = Projectile.Center.DirectionTo(Main.player[Projectile.owner].Center) * 45;
                        Projectile.netUpdate = true;
                    }
                }
                else if (Projectile.ai[0] < Inder1) {
                    AdjustPosition(origPos, 15, 1);
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                }
            }
            else {
                CWRRef.HomeInOnNPC(Projectile, !Projectile.tileCollide, 300f, 12f, 20f);
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            //飞行残烬粒子
            if (Main.rand.NextBool(3)) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(8f, 8f);
                Dust dust = Dust.NewDustPerfect(dustPos, DustID.RedTorch, -Projectile.velocity * 0.04f,
                    150, default, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3());
        }

        private void AdjustPosition(Vector2 destination, float maxSpeed, float increment = 1f) {
            float deltaX = destination.X - Projectile.Center.X;
            float deltaY = destination.Y - Projectile.Center.Y;

            if (Projectile.Center.X < destination.X && Projectile.velocity.X < maxSpeed) {
                Projectile.velocity.X = Math.Min(Projectile.velocity.X + increment, deltaX);
            }
            else if (Projectile.Center.X > destination.X && Projectile.velocity.X > -maxSpeed) {
                Projectile.velocity.X = Math.Max(Projectile.velocity.X - increment, deltaX);
            }

            if (Projectile.Center.Y < destination.Y && Projectile.velocity.Y < maxSpeed) {
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + increment, deltaY);
            }
            else if (Projectile.Center.Y > destination.Y && Projectile.velocity.Y > -maxSpeed) {
                Projectile.velocity.Y = Math.Max(Projectile.velocity.Y - increment, deltaY);
            }
        }

        public override bool? CanHitNPC(NPC target) {
            //冲锋阶段 (Inder2+) 之前的"短停 / 重定位"环节不造成伤害
            if (Projectile.ai[2] > 0 && Projectile.ai[0] < Inder2) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //战士附加效果: 日间灼烧、夜间灵魂燃烧
            if (Main.dayTime) {
                target.AddBuff(BuffID.OnFire, 300);
            }
            else {
                target.AddBuff(ModContent.BuffType<SoulBurning>(), 300);
            }

            //命中迸发铁屑火星
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                Dust spark = Dust.NewDustPerfect(target.Center, DustID.Iron, vel,
                    100, default, Main.rand.NextFloat(1.0f, 1.5f));
                spark.noGravity = true;
                spark.fadeIn = 1.05f;
            }

            //冲锋命中: 屏震 + Spark
            if (Projectile.ai[2] > 0 && Projectile.ai[0] >= Inder2 && Projectile.numHits <= 1) {
                if (CWRServerConfig.Instance.ScreenVibration) {
                    Vector2 hitDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        target.Center, hitDir, 2.5f, 4f, 5, 400f, FullName));
                }

                for (int i = 0; i < 6; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(7f, 7f);
                    PRT_Spark prt = new PRT_Spark(target.Center, vel, false, 14,
                        Main.rand.NextFloat(1.1f, 1.7f), Color.Crimson);
                    PRTLoader.AddParticle(prt);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.ai[2] <= 0) {
                return;
            }

            //冲锋飞刀寿命终结时炸裂
            Projectile.damage /= 2;
            Projectile.Explode(300);

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);

            for (int i = 0; i < 6; i++) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.RedTorch, 0f, 0f, 100, default, 1.5f);
            }

            for (int i = 0; i < 66; i++) {
                Vector2 pos = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2()
                    * Main.rand.Next(-200, 200) + Projectile.Center;
                int idx = Dust.NewDust(pos, 1, 1, DustID.RedTorch, 0f, 0f, 0, default, 2.5f);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].velocity *= 3f;
                idx = Dust.NewDust(pos, 2, 2, DustID.RedTorch, 0f, 0f, 100, default, 1.5f);
                Main.dust[idx].velocity *= 2f;
                Main.dust[idx].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            CWRRef.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }
    }
}
