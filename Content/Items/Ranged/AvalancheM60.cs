using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class AvalancheM60 : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "AvalancheM60";
        public override void SetDefaults() {
            Item.CloneDefaults(CWRID.Item_Onyxia);
            Item.damage = 62;
            Item.useAmmo = AmmoID.Snowball;
            Item.shoot = ProjectileID.Bullet;
            Item.UseSound = null;//开火音效由手持弹幕负责
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(0, 4, 75, 0);
        }

        //物品使用本身不消耗雪球，由手持弹幕在实际开火时自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseHeldGun.AmmoConsumeContext;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<AvalancheM60Held>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<AvalancheM60Held>(player, source);

        public override void AddRecipes() {
            if (CWRID.Item_CryonicBar > 0 && CWRID.Item_EssenceofEleum > 0) {
                _ = CreateRecipe().
                AddIngredient<SnowQuayMK2>().
                AddIngredient(ItemID.ShroomiteBar, 3).
                AddIngredient(CWRID.Item_CryonicBar, 3).
                AddIngredient(CWRID.Item_EssenceofEleum, 5).
                AddIngredient(ItemID.BeetleHusk, 5).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient<SnowQuayMK2>().
                AddIngredient(ItemID.ShroomiteBar, 3).
                AddIngredient(ItemID.BeetleHusk, 5).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
        }
    }

    internal class AvalancheM60Held : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "AvalancheM60Held";
        public override int TargetID => ModContent.ItemType<AvalancheM60>();
        public override SoundStyle? ShootSound => SoundID.Item36 with { Pitch = 0.2f };
        /// <summary>过热爆发的剩余帧数</summary>
        private int onFireTime;
        /// <summary>爆发期间的帧动画维持计时</summary>
        private int onFireTime2;
        /// <summary>持续射击的热度（数值越低射得越久），用于解锁强化弹与过热爆发</summary>
        private int fireRateValue = 20;
        //过热爆发未结束时枪体不要消失
        public override bool StayAlive() => onFireTime > 0;
        public override void SetGunProperty() {
            GunPressure = 0;
            HandIdleDistanceX = 54;
            HandIdleDistanceY = -4;
            HandFireDistanceX = 55;
            HandFireDistanceY = -8;
            AngleFirearmRest = -6;
            MuzzleForwardOffset = 18;
            MuzzleNormalOffset = 0;
            RecoilRetroForceMagnitude = 5;
        }

        public override void AI() {
            bool firing = WantsFireLeft && HasAmmo;
            UpdateHeldPose(firing);

            //帧动画
            if (firing) {
                VaultUtils.ClockFrame(ref Projectile.frame, 2, 3);
            }
            else {
                Projectile.frame = 4;
            }

            if (onFireTime2 > 0) {
                onFireTime2--;
            }

            //过热爆发期间的演出：呼啸声、喷雪、枪体狂震
            if (onFireTime > 0) {
                SoundEngine.PlaySound(SoundID.Item23 with { Pitch = (60 - onFireTime) * 0.15f, MaxInstances = 13, Volume = 0.2f + (60 - onFireTime) * 0.006f }, Projectile.Center);
                if (onFireTime % 15 == 0) {
                    SpawnGunFireDust(ShootPos, ShootVelocity, splNum: 3, dustID1: 76, dustID2: 149, dustID3: 76);
                    onFireTime2 = 8;
                }
                if (onFireTime2 > 0) {
                    VaultUtils.ClockFrame(ref Projectile.frame, 2, 3);
                }
                else {
                    Projectile.frame = 4;
                }

                RecoilOffset += VaultUtils.RandVr(8f);
                onFireTime--;
            }
            else if (fireRateValue > 30) {
                fireRateValue = 15;
            }

            if (firing && FireCooldown <= 0) {
                Fire();
            }
            Time++;
        }

        private void Fire() {
            SnapToAimPose();
            CreateFireLight();
            ConsumeAmmo();

            //过热爆发期间的暴风雪扫射
            if (onFireTime > 0) {
                GunPressure = 0.6f;
                ControlForce = 0.1f;
                RecoilRetroForceMagnitude = 15;
                RecoilOffsetRecoverValue = 0.85f;

                SoundEngine.PlaySound(CWRSound.Gun_50CAL_Shoot with { Pitch = -0.5f, Volume = 0.3f });
                CreateRecoil();

                if (Projectile.IsOwnedByLocalPlayer()) {
                    for (int i = 0; i < 18; i++) {
                        Projectile proj = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.75f, 1.12f)
                        , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                        proj.scale += Main.rand.NextFloat(0.3f);
                        proj.netUpdate = true;
                        if (Main.rand.NextBool(2)) {
                            Projectile proj2 = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.75f, 1.12f)
                        , CWRID.Proj_FlurrystormIceChunk, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                            proj2.extraUpdates += 2;
                            proj2.netUpdate = true;
                        }
                    }
                    for (int i = 0; i < 33; i++) {
                        Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.15f, 1.12f)
                        , ModContent.ProjectileType<IceExplosionFriend>(), WeaponDamage / 6, WeaponKnockback, Owner.whoAmI, 0);
                    }
                }

                FireCooldown = 15;
                fireRateValue = 8;
                return;
            }

            //常规射击
            GunPressure = 0;
            RecoilRetroForceMagnitude = 5;
            RecoilOffsetRecoverValue = 0.5f;

            if (fireRateValue > 7) {
                fireRateValue--;
            }

            PlayShootSound();
            CreateRecoil();
            SpawnGunFireDust(ShootPos, ShootVelocity, splNum: 3, dustID1: 76, dustID2: 149, dustID3: 76);

            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 3; i++) {
                    Projectile proj = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity.RotatedByRandom(0.12f) * Main.rand.NextFloat(0.7f, 1.1f)
                        , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                    proj.extraUpdates += 1;
                    if (Main.rand.NextBool(2)) {
                        proj.damage /= 3;
                    }
                    if (Main.rand.NextBool(4) && fireRateValue <= 15) {
                        proj.scale += Main.rand.NextFloat(0.35f);
                    }
                    if (Main.rand.NextBool(3) && fireRateValue <= 10) {
                        proj.extraUpdates += 1;
                        proj.penetrate += 5;
                        proj.usesLocalNPCImmunity = true;
                        proj.localNPCHitCooldown = -1;
                    }
                    proj.netUpdate = true;
                }

                Projectile proj3 = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity, ModContent.ProjectileType<ExtremeColdHail>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, ShootVelocity.Y);
                proj3.rotation = proj3.velocity.ToRotation() + MathHelper.PiOver2;
                proj3.netUpdate = true;
            }

            SetFireCooldown();

            //持续压制足够久后触发过热爆发
            if (fireRateValue <= 8) {
                fireIndex++;
                if (fireIndex > 20) {
                    fireRateValue = 50;
                    onFireTime += 60;
                    fireIndex = 0;
                }
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, drawPos
                , TextureValue.GetRectangle(Projectile.frame, 5), lightColor
                , Projectile.rotation, VaultUtils.GetOrig(TextureValue, 5), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }

    internal class IceExplosionFriend : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public float randomX;
        public float randomY;
        public override void AutoStaticDefaults() => AutoProj.AutoStaticDefaults(this);
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 184;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.hide = true;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 20;
            Projectile.localAI[1] = Projectile.timeLeft;
            Projectile.ArmorPenetration = 10;
            randomX = Main.rand.NextFloat(0f, MathHelper.TwoPi);
            randomY = Main.rand.NextFloat(0f, MathHelper.TwoPi);
        }

        public override void AI() {
            Projectile.scale = 0.5f + (Projectile.ai[1] * 0.01f);
            if (Projectile.timeLeft < 30) {
                Projectile.ai[0] = 1;
            }
            Projectile.ai[1]++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn2, 180);
        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(BuffID.Frostburn2, 180);
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => VaultUtils.CircleIntersectsRectangle(Projectile.Center, Projectile.scale * 92f, targetHitbox);
        public override bool? CanDamage() => Projectile.ai[0] == 0 ? null : false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (Projectile.ai[1] < 3) {
                return;
            }
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Color drawColor = new Color(118, 217, 222) * (Projectile.timeLeft / Projectile.localAI[1]) * 0.9f;
            Vector2 scale = Projectile.Size / texture.Size() * Projectile.scale * 1.35f;
            spriteBatch.Draw(texture, drawPosition, null, drawColor, randomX, origin, scale, 0, 0f);
            spriteBatch.Draw(texture, drawPosition, null, drawColor, randomY, origin, scale, 0, 0f);
        }
    }

    internal class ExtremeColdHail : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "FlurrystormIceChunk";
        public override void AutoStaticDefaults() => AutoProj.AutoStaticDefaults(this);
        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 24;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 10;
        }

        public override bool PreAI() {
            if (Projectile.knockBack == 0f)
                Projectile.hostile = true;
            else Projectile.friendly = true;
            return true;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                SoundEngine.PlaySound(SoundID.Item89 with { Volume = 0.4f }, Projectile.position);
            }
            Projectile.localAI[0]++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(6)) {
                Projectile.NewProjectileDirect(Projectile.FromObjectGetParent(), Projectile.Center, Projectile.velocity * 0.1f, ModContent.ProjectileType<IceExplosionFriend>(), 13, 0, Projectile.owner, 0);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(CWRID.Buff_GlacialState, 30);
            target.AddBuff(BuffID.Frostburn2, 180);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(CWRID.Buff_GlacialState, 30);
            target.AddBuff(BuffID.Frostburn2, 180);
            if (Projectile.hostile && VaultUtils.isClient)
                return;
            Projectile.Kill();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.oldVelocity.Y > 0f && Projectile.velocity.X != 0f) {
                Projectile.velocity.Y = -0.6f * Projectile.oldVelocity.Y;
                Projectile.velocity.X *= 0.975f;
            }
            else if (Projectile.velocity.X == 0f) {
                Projectile.velocity.X = -0.6f * Projectile.oldVelocity.X;
            }
            return false;
        }

        public override Color? GetAlpha(Color drawColor) {
            return Projectile.timeLeft < 30 && Projectile.timeLeft % 10 < 5 ? Color.Orange : Color.White;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/NPCHit/CryogenHit", 3) with { Volume = 0.55f }, Projectile.Center);
        }
    }

    internal class IceParclose : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "IceParclose";
        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 38;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            NPC npc = Main.npc[(int)Projectile.ai[0]];
            if (!npc.Alives()) {
                Projectile.Kill();
                return;
            }
            if (npc.type != (int)Projectile.ai[1]) {
                Projectile.Kill();
                return;
            }

            if (!Main.dedServ) {
                Projectile.scale = npc.scale * (npc.height / (float)TextureAssets.Projectile[Type].Value.Height) * 2;
            }

            npc.Center = Projectile.Center;
            npc.rotation = Projectile.ai[2];
            npc.CWR().IceParclose = true;
            npc.CWR().TimeFrozenTick = 2;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound("CalamityMod/Sounds/NPCHit/CryogenHit3".GetSound(), Projectile.Center);
            for (int i = 0; i < 10 * Projectile.scale; i++) {
                int index2 = Dust.NewDust(Projectile.Center + VaultUtils.RandVr(Projectile.width * Projectile.scale), 1, 1, DustID.BlueCrystalShard, Projectile.velocity.X, Projectile.velocity.Y, 0, default, 1.1f);
                Main.dust[index2].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
