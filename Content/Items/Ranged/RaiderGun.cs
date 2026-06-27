using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class RaiderGun : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "RaiderGun";
        public const int DashCooling = 120;
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 32;
            Item.height = 32;
            Item.damage = 52;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useAmmo = AmmoID.Bullet;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 12;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(0, 1, 60, 10);
            Item.CWR().DeathModeItem = true;
        }

        //右键：冲刺
        public override bool AltFunctionUse(Player player) => true;

        //物品使用本身不消耗子弹，由手持弹幕在实际开火时自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseHeldGun.AmmoConsumeContext;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<RaiderGunHeld>()] == 0
            && player.ownedProjectileCounts[ModContent.ProjectileType<RaiderGunDash>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<RaiderGunHeld>(player, source);

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            DrawDashCooldownBar(spriteBatch, position, frame, scale);
        }

        /// <summary>
        /// 通用的冲刺冷却进度条绘制
        /// </summary>
        internal static void DrawDashCooldownBar(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, float scale) {
            int cooldown = Main.LocalPlayer.CWR().RaiderGunDashCooldown;
            if (cooldown <= 0) {
                return;
            }
            Texture2D barBG = CWRAsset.GenericBarBack.Value;
            Texture2D barFG = CWRAsset.GenericBarFront.Value;
            float barScale = 2f;
            Vector2 barOrigin = barBG.Size() * 0.5f;
            float yOffset = 60f;
            Vector2 drawPos = position + Vector2.UnitY * scale * (frame.Height - yOffset);
            Rectangle frameCrop = new Rectangle(0, 0, (int)(cooldown / (float)DashCooling * barFG.Width), barFG.Height);
            Color color = Main.hslToRgb(Main.GlobalTimeWrappedHourly * 0.6f % 1f, 1f, 0.75f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.1f);
            spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, scale * barScale, 0, 0f);
            spriteBatch.Draw(barFG, drawPos, frameCrop, color * 0.8f, 0f, barOrigin, scale * barScale, 0, 0f);
        }
    }

    internal class RaiderGunEX : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "RaiderGunEX";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 32;
            Item.height = 32;
            Item.damage = 122;
            Item.useTime = 4;
            Item.useAnimation = 4;
            Item.useAmmo = AmmoID.Bullet;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 15;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 12, 60, 10);
            Item.CWR().DeathModeItem = true;
        }

        //右键：冲刺
        public override bool AltFunctionUse(Player player) => true;

        //物品使用本身不消耗子弹，由手持弹幕在实际开火时自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseHeldGun.AmmoConsumeContext;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<RaiderGunEXHeld>()] == 0
            && player.ownedProjectileCounts[ModContent.ProjectileType<RaiderGunDash>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<RaiderGunEXHeld>(player, source);

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            //与基础版本共享冲刺冷却进度条
            RaiderGun.DrawDashCooldownBar(spriteBatch, position, frame, scale);
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<RaiderGun>().
                AddIngredient<SoulofFrightEX>().
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    internal class RaiderGunHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "RaiderGun";
        public override int TargetID => ModContent.ItemType<RaiderGun>();
        public override bool CanRightClick => true;
        public override SoundStyle? ShootSound => SoundID.Item36 with { Volume = 0.7f };
        private bool DashAlive => Owner.ownedProjectileCounts[ModContent.ProjectileType<RaiderGunDash>()] != 0;
        public override void SetGunProperty() {
            GunPressure = 0.1f;
            ControlForce = 0.02f;
            HandIdleDistanceX = 26;
            HandIdleDistanceY = 2;
            HandFireDistanceX = 26;
            HandFireDistanceY = -2;
            MuzzleForwardOffset = 8;
            MuzzleNormalOffset = -4;
            RecoilRetroForceMagnitude = 8;
            RecoilOffsetRecoverValue = 0.8f;
        }

        public override void AI() {
            UpdateHeldPose(CanFire);

            if (FireCooldown <= 0 && !DashAlive) {
                if (WantsFireLeft && HasAmmo && CanFireNormally()) {
                    FireLeft();
                    SetFireCooldown();
                }
                else if (WantsFireRight && Owner.CWR().RaiderGunDashCooldown <= 0) {
                    FireRight();
                    SetFireCooldown();
                }
            }
            Time++;
        }

        /// <summary>
        /// 附近存在尚未引爆的毁灭者榴弹时暂缓普通射击
        /// </summary>
        private bool CanFireNormally() {
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != ModContent.ProjectileType<DestroyerGrenade>() || proj.owner != Owner.whoAmI) {
                    continue;
                }
                if (proj.DistanceSQ(Owner.Center) < 1200 * 1200) {
                    return false;
                }
            }
            return true;
        }

        private void FireLeft() {
            SnapToAimPose();
            CreateFireLight();
            CWRPlayer modPlayer = Owner.CWR();

            //冲刺结束后的强化射击：发射一枚追踪榴弹
            if (modPlayer.RaiderGunDashReady) {
                SoundEngine.PlaySound("CalamityMod/Sounds/Item/ScorchedEarthShot3".GetSound(), Projectile.Center);
                RecoilPitch += 0.2f;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Source, Owner.Center, ShootVelocity
                        , ModContent.ProjectileType<DestroyerGrenade>()
                        , WeaponDamage * 2, WeaponKnockback, Owner.whoAmI);
                }
                modPlayer.RaiderGunDashReady = false;
                return;
            }

            PlayShootSound();
            CreateRecoil();
            SpawnGunFireDust();
            if (Projectile.IsOwnedByLocalPlayer()) {
                int shootType = AmmoTypes == ProjectileID.Bullet ? ProjectileID.BulletHighVelocity : AmmoTypes;
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , shootType, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            ConsumeAmmo();
        }

        private void FireRight() {
            SoundEngine.PlaySound(CWRSound.Dash, Owner.Center);
            if (Projectile.IsOwnedByLocalPlayer()) {
                float _swingDir = Owner.Center.X + ShootVelocity.X > Owner.position.X ? 1 : -1;
                Projectile.NewProjectile(Source, Owner.Center, ShootVelocity, ModContent.ProjectileType<RaiderGunDash>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, ai1: _swingDir);
            }
        }
    }

    internal class RaiderGunEXHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "RaiderGunEX";
        public override int TargetID => ModContent.ItemType<RaiderGunEX>();
        public override bool CanRightClick => true;
        public override SoundStyle? ShootSound => SoundID.Item36 with { Volume = 0.7f };
        private bool DashAlive => Owner.ownedProjectileCounts[ModContent.ProjectileType<RaiderGunDash>()] != 0;
        public override void SetGunProperty() {
            GunPressure = 0.06f;
            ControlForce = 0.02f;
            HandIdleDistanceX = 26;
            HandIdleDistanceY = 2;
            HandFireDistanceX = 26;
            HandFireDistanceY = -2;
            MuzzleForwardOffset = 8;
            MuzzleNormalOffset = -4;
            RecoilRetroForceMagnitude = 8;
            RecoilOffsetRecoverValue = 0.8f;
        }

        public override void AI() {
            UpdateHeldPose(CanFire);

            if (FireCooldown <= 0 && !DashAlive) {
                if (WantsFireLeft && HasAmmo && CanFireNormally()) {
                    FireLeft();
                    SetFireCooldown();
                }
                else if (WantsFireRight && Owner.CWR().RaiderGunDashCooldown <= 0) {
                    FireRight();
                    SetFireCooldown();
                }
            }
            Time++;
        }

        /// <summary>
        /// 常规射击时限制毁灭者榴弹的数量，防止过多堆积
        /// </summary>
        private bool CanFireNormally() {
            int grenadeCount = 0;
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == ModContent.ProjectileType<DestroyerGrenade>() && proj.owner == Owner.whoAmI) {
                    grenadeCount++;
                }
            }
            return grenadeCount <= 20;
        }

        private void FireLeft() {
            SnapToAimPose();
            CreateFireLight();
            CWRPlayer modPlayer = Owner.CWR();

            //冲刺后的强化射击：一次性发射5枚毁灭者榴弹
            if (modPlayer.RaiderGunDashReady) {
                SoundEngine.PlaySound("CalamityMod/Sounds/Item/ScorchedEarthShot3".GetSound(), Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.8f }, Owner.Center);
                RecoilPitch += 0.3f;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    for (int i = 0; i < 5; i++) {
                        Projectile.NewProjectile(Source, Owner.Center, ShootVelocity.RotatedByRandom(0.25f)
                            , ModContent.ProjectileType<DestroyerGrenade>(), WeaponDamage * 2, WeaponKnockback, Owner.whoAmI, 1);
                    }
                }
                modPlayer.RaiderGunDashReady = false;
                return;
            }

            //常规射击
            PlayShootSound();
            CreateRecoil();
            if (Projectile.IsOwnedByLocalPlayer()) {
                int shootType = AmmoTypes == ProjectileID.Bullet ? ProjectileID.BulletHighVelocity : AmmoTypes;
                Projectile.NewProjectile(Source, Owner.Center, ShootVelocity.RotatedByRandom(0.1f)
                        , shootType, WeaponDamage, WeaponKnockback, Owner.whoAmI);

                if (fireIndex + 1 > 3) {
                    Projectile.NewProjectile(Source, Owner.Center, ShootVelocity.RotatedBy(-0.2f)
                        , ModContent.ProjectileType<DestroyerGrenade>(), WeaponDamage * 2, WeaponKnockback, Owner.whoAmI, 1);
                    Projectile.NewProjectile(Source, Owner.Center, ShootVelocity.RotatedBy(0.2f)
                        , ModContent.ProjectileType<DestroyerGrenade>(), WeaponDamage * 2, WeaponKnockback, Owner.whoAmI, 1);
                }

                if (Main.rand.NextBool(9)) {
                    Projectile.NewProjectile(Source, Owner.Center, ShootVelocity
                        , ModContent.ProjectileType<DestroyerGrenade>(), WeaponDamage * 3, WeaponKnockback, Owner.whoAmI);
                }
            }

            if (++fireIndex > 3) {
                fireIndex = 0;
            }
            ConsumeAmmo();
        }

        private void FireRight() {
            //右键冲刺逻辑
            SoundEngine.PlaySound(CWRSound.Dash, Owner.Center);
            if (Projectile.IsOwnedByLocalPlayer()) {
                float _swingDir = Owner.Center.X + ShootVelocity.X > Owner.position.X ? 1 : -1;
                Projectile.NewProjectile(Source, Owner.Center, ShootVelocity, ModContent.ProjectileType<RaiderGunDash>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, ai1: _swingDir);
            }
        }
    }

    internal class PunisherGrenade : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "PunisherGrenade";
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.velocity *= 0.95f;
            if (Projectile.velocity.Length() < 0.5f && Projectile.timeLeft > 10) {
                Projectile.timeLeft = 10;
            }
        }

        public override void OnKill(int timeLeft) {
            //强化爆炸效果
            SoundEngine.PlaySound(SoundID.Item14, Projectile.position);
            if (!VaultUtils.isServer) {
                CWRUtils.BlastingSputteringDust(Projectile, DustID.Smoke, DustID.GoldCoin, DustID.GoldCoin, DustID.GoldCoin, DustID.GoldCoin);

                Vector2 goreSource = Projectile.Center;
                int goreAmt = 5;
                Vector2 source = new Vector2(goreSource.X - 12f, goreSource.Y - 12f);

                for (int goreIndex = 0; goreIndex < goreAmt; goreIndex++) {
                    float velocityMult = Main.rand.NextFloat(0.3f, 0.8f);
                    float angle = Main.rand.NextFloat(0, MathHelper.TwoPi);
                    Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1.5f, 4f) * velocityMult;

                    int type = Main.rand.Next(61, 64);
                    int smoke = Gore.NewGore(Projectile.GetSource_Death(), source, default, type, Main.rand.NextFloat(0.5f, 1.0f));
                    Gore gore = Main.gore[smoke];
                    gore.velocity = velocity;
                    gore.rotation = Main.rand.NextFloat(0, MathHelper.TwoPi);
                    gore.scale = Main.rand.NextFloat(0.5f, 0.9f);
                    gore.timeLeft = Main.rand.Next(30, 60);
                    gore.alpha = 100;
                }
            }
        }
    }

    internal class DestroyerGrenade : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "DestroyerGrenade";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }
        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 2;
            Projectile.ArmorPenetration = 10;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 1f, 0.79f, 0.3f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Projectile.ai[0] != 0) {
                NPC target = Projectile.Center.FindClosestNPC(350f, true, chasedByNPC: npc => npc.CanBeChasedBy(Projectile));
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1.05f, 0.18f);
                }
            }
            float xVel = Projectile.velocity.X * 0.5f;
            float yVel = Projectile.velocity.Y * 0.5f;
            //使用更亮的粒子效果
            int d = Dust.NewDust(new Vector2(Projectile.position.X + 3f + xVel, Projectile.position.Y + 3f + yVel) - Projectile.velocity * 0.5f, Projectile.width - 8, Projectile.height - 8, DustID.GoldCoin, 0f, 0f, 100, default, 1f);
            Main.dust[d].scale *= 2f + Main.rand.Next(10) * 0.1f;
            Main.dust[d].velocity *= 0.2f;
            Main.dust[d].noGravity = true;
            d = Dust.NewDust(new Vector2(Projectile.position.X + 3f + xVel, Projectile.position.Y + 3f + yVel) - Projectile.velocity * 0.5f, Projectile.width - 8, Projectile.height - 8, DustID.GoldCoin, 0f, 0f, 100, default, 0.5f);
            Main.dust[d].fadeIn = 1f + Main.rand.Next(5) * 0.1f;
            Main.dust[d].velocity *= 0.05f;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14, Projectile.position);

            if (!VaultUtils.isServer) {
                if (Projectile.ai[0] == 0) {
                    CWRUtils.BlastingSputteringDust(Projectile, DustID.Smoke, DustID.AmberBolt, DustID.AmberBolt, DustID.AmberBolt, DustID.AmberBolt);
                }

                Vector2 goreSource = Projectile.Center;
                int goreAmt = 10;
                Vector2 source = new Vector2(goreSource.X - 24f, goreSource.Y - 24f);

                for (int goreIndex = 0; goreIndex < goreAmt; goreIndex++) {
                    float velocityMult = Main.rand.NextFloat(0.5f, 1.2f);//速度范围更广
                    float angle = Main.rand.NextFloat(0, MathHelper.TwoPi);//随机角度
                    Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 6f) * velocityMult;//随机方向的速度

                    int type = Main.rand.Next(61, 64);
                    int smoke = Gore.NewGore(Projectile.GetSource_Death(), source, default, type, Main.rand.NextFloat(0.8f, 1.3f));//让烟雾大小不同
                    Gore gore = Main.gore[smoke];
                    gore.velocity = velocity;
                    gore.rotation = Main.rand.NextFloat(0, MathHelper.TwoPi);//初始旋转
                    gore.scale = Main.rand.NextFloat(0.7f, 1.4f);//缩放不同
                    gore.timeLeft = Main.rand.Next(40, 80);//让烟雾存活时间不同
                    gore.alpha = 100;//初始半透明
                }
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int j = 0; j < 5; j++) {
                    Vector2 velocity = VaultUtils.RandVr(6, 9);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity
                        , ModContent.ProjectileType<PunisherGrenade>(), (int)(Projectile.damage * 0.25), Projectile.knockBack * 0.25f, Projectile.owner);
                }

                Projectile.Explode(400);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = texture.GetRectangle();
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = Color.White * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class RaiderGunDash : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        private ref float Time => ref Projectile.ai[0];
        private ref float SwingDir => ref Projectile.ai[1];
        public override void SetDefaults() {
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.height = 64;
            Projectile.width = 64;
            Projectile.friendly = true;
            Projectile.scale = 1f;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }
        public override bool? CanDamage() {
            return null;
        }
        public override bool ShouldUpdatePosition() {
            return false;
        }
        public override void AI() {
            CWRPlayer modPlayer = Owner.CWR();
            modPlayer.IsRotatingDuringDash = true;

            //初始化冲刺效果
            if (Time == 0) {
                modPlayer.PendingDashVelocity = Projectile.velocity.UnitVector() * 23;
            }

            //冲刺时给予短暂无敌
            if (Time < 20) {
                Owner.GivePlayerImmuneState(6);
            }

            //添加粒子拖尾效果
            for (int i = 0; i < 3; i++) {
                int dustType = DustID.SolarFlare;
                Vector2 dustPos = Owner.Center + Main.rand.NextVector2Circular(Owner.width / 2f, Owner.height / 2f);
                Dust dust = Dust.NewDustDirect(dustPos, 0, 0, dustType, -Owner.velocity.X * 0.3f, -Owner.velocity.Y * 0.3f, 100, default, 1.5f);
                dust.noGravity = true;
                dust.velocity *= 0.2f;
            }

            modPlayer.RotationDirection = (int)SwingDir;

            Projectile.Center = Owner.GetPlayerStabilityCenter();
            Projectile.rotation = Owner.fullRotation;

            if (Time < 10) {
                Projectile.scale += 0.04f;
            }
            else {
                if (Projectile.scale > 1f) {
                    Projectile.scale -= 0.02f;
                }
            }

            Owner.ChangeDir(Projectile.velocity.X < 0 ? -1 : 1);
            Owner.itemRotation = Projectile.rotation * Owner.direction;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;

            Lighting.AddLight(Projectile.position, Color.Red.ToVector3() * 0.78f);

            if (Time > 12 && Owner.velocity.Length() < 10 || DownLeft) {
                Projectile.Kill();
            }

            Time++;
        }

        public override void OnKill(int timeLeft) {
            //冲刺结束，强化射击就绪，进入冲刺冷却
            SoundEngine.PlaySound(CWRSound.Gun_Clipout, Owner.Center);
            CWRPlayer modPlayer = Owner.CWR();
            modPlayer.RaiderGunDashReady = true;
            modPlayer.RaiderGunDashCooldown += RaiderGun.DashCooling;
            modPlayer.IsRotatingDuringDash = false;
            modPlayer.RotationResetCounter = 15;
            modPlayer.DashCooldownCounter = 35;
            modPlayer.CustomCooldownCounter = 30;
            if (Main.zenithWorld) {
                modPlayer.CustomCooldownCounter = 2;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }
}
