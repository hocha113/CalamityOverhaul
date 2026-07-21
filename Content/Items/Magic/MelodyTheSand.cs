using CalamityOverhaul.Content.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    /// <summary>沙中曲，小沙龙卷落地巡游</summary>
    internal class MelodyTheSand : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "MelodyTheSand";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 32;
            Item.height = 32;
            Item.damage = 32;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.mana = 7;
            Item.knockBack = 3.5f;
            Item.shoot = ModContent.ProjectileType<SandSmallTornado>();
            Item.shootSpeed = 9;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(0, 0, 80, 15);
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<MelodyTheSandHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<MelodyTheSandHeld>(player, source);
    }

    internal class MelodyTheSandHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Magic + "MelodyTheSand";
        public override int TargetID => ModContent.ItemType<MelodyTheSand>();
        /// <summary>开火余韵 0~1</summary>
        private int glowPulse;
        public override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            HandFireDistanceX = 18;
            HandFireDistanceY = 0;
            MuzzleForwardOffset = 10;
            MuzzleNormalOffset = -8;
            GunPressure = 0;
            ControlForce = 0.05f;
            RecoilOffsetRecoverValue = 0.6f;
            AlwaysAimPose = true;
        }

        public override void AI() {
            UpdateHeldPose(WantsFireLeft);

            if (glowPulse > 0) {
                glowPulse--;
            }
            if (CanFire) {
                HoldManaRegenDelay();
            }

            if (WantsFireLeft && FireCooldown <= 0 && PayMana()) {
                Fire();
                SetFireCooldown();
            }
            Time++;
        }

        private void Fire() {
            glowPulse = 24;
            SnapToAimPose();
            SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.2f, Volume = 0.85f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.35f, Volume = 0.35f }, Projectile.Center);
            CreateFireLight();

            Vector2 vel = ShootVelocity;
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Source, ShootPos, vel
                    , ModContent.ProjectileType<SandSmallTornado>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }

            for (int i = 0; i < 10; i++) {
                Vector2 dustVel = vel.UnitVector().RotatedByRandom(0.55f) * Main.rand.NextFloat(2f, 6f);
                int dustId = Main.rand.NextBool(3) ? DustID.Gold : DustID.Sand;
                int d = Dust.NewDust(ShootPos, 1, 1, dustId, dustVel.X, dustVel.Y, 100, default, 1.15f);
                Main.dust[d].noGravity = true;
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
            Color color = Color.Gold;
            color.A = 0;
            float slp = 1 + 0.012f * glowPulse;
            Main.EntitySpriteDraw(TextureValue, drawPos, null, color * 0.45f
                , Projectile.rotation + offsetRot, TextureValue.Size() / 2, Projectile.scale * slp
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            base.GunDraw(drawPos, ref lightColor);
        }
    }

    /// <summary>小沙龙卷，落地巡游遇墙/断崖掉头</summary>
    internal class SandSmallTornado : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 360;
        private const float MaxGroundSpeed = 5f;
        private const float Gravity = 0.34f;
        private const float MaxFallSpeed = 13f;

        //0 = 飞行阶段，1 = 触地巡游阶段
        private ref float Phase => ref Projectile.ai[0];
        //触地后水平巡游方向（±1）
        private ref float GroundDir => ref Projectile.ai[1];
        private ref float SwirlTime => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Magic;
            Projectile.width = 36;
            Projectile.height = 72;
            Projectile.timeLeft = Lifetime;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
        }

        public override void AI() {
            SwirlTime += 0.2f;

            if (Phase == 0f) {
                //飞行抛物
                Projectile.velocity.Y += Gravity;
                if (Projectile.velocity.Y > MaxFallSpeed) {
                    Projectile.velocity.Y = MaxFallSpeed;
                }
                Projectile.rotation = Projectile.velocity.X * 0.04f;
            }
            else {
                //贴地微下压保 tile collide
                if (GroundDir == 0f) {
                    int sign = Math.Sign(Projectile.velocity.X);
                    GroundDir = sign != 0 ? sign : 1;
                }

                Projectile.velocity.X = GroundDir * MaxGroundSpeed;
                Projectile.velocity.Y = 4f;

                Vector2 ledgeProbe = Projectile.Bottom + new Vector2(GroundDir * (Projectile.width / 2f + 4f), 6f);
                Point tilePos = ledgeProbe.ToTileCoordinates();
                if (WorldGen.InWorld(tilePos.X, tilePos.Y)) {
                    Tile tile = Main.tile[tilePos.X, tilePos.Y];
                    //断崖掉头
                    bool solidBelow = tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
                    if (!solidBelow) {
                        GroundDir = -GroundDir;
                    }
                }
                Projectile.rotation = 0f;
            }

            if (!Main.dedServ) {
                SpawnVisualDust();
                Lighting.AddLight(Projectile.Center, new Color(220, 180, 80).ToVector3() * 0.6f);
            }
        }

        private void SpawnVisualDust() {
            int spawnCount = Phase == 1f ? 13 : 12;
            for (int i = 0; i < spawnCount; i++) {
                if (!Main.rand.NextBool(2)) {
                    continue;
                }
                float yT = Main.rand.NextFloat(-0.5f, 0.5f);
                float xWobble = MathF.Cos(SwirlTime * 1.4f + yT * 6f) * (Projectile.width * 0.35f);
                Vector2 spawnPos = Projectile.Center + new Vector2(xWobble, yT * Projectile.height);
                Vector2 spinTangent = new Vector2(-MathF.Sin(SwirlTime + yT * 3f), 0f);
                Vector2 dustVel = spinTangent * 3f + new Vector2(0f, -1.6f);
                int dustId = Main.rand.NextBool(4) ? DustID.Gold : DustID.Sand;
                int d = Dust.NewDust(spawnPos, 1, 1, dustId, dustVel.X, dustVel.Y, 80, default, 1.15f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.7f;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Phase == 0f) {
                //首次触地→巡游
                if (oldVelocity.Y > 0f) {
                    Phase = 1f;
                    GroundDir = oldVelocity.X >= 0f ? 1f : -1f;
                    if (oldVelocity.X == 0f) {
                        GroundDir = Main.rand.NextBool() ? 1f : -1f;
                    }
                    Projectile.velocity.Y = 0f;
                    Projectile.velocity.X = GroundDir * MaxGroundSpeed;
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.45f }, Projectile.Center);
                    for (int i = 0; i < 14; i++) {
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-3.5f, -0.5f));
                        int d = Dust.NewDust(Projectile.Bottom, 1, 1, DustID.Sand, vel.X, vel.Y, 100, default, 1.2f);
                        Main.dust[d].noGravity = true;
                    }
                    return false;
                }
                //空中蹭墙水平反弹
                if (oldVelocity.X != Projectile.velocity.X) {
                    Projectile.velocity.X = -oldVelocity.X * 0.55f;
                }
                return false;
            }

            //巡游撞墙反向
            if (oldVelocity.X != Projectile.velocity.X) {
                GroundDir = -GroundDir;
                Projectile.velocity.X = GroundDir * MaxGroundSpeed;
            }
            if (oldVelocity.Y != Projectile.velocity.Y) {
                Projectile.velocity.Y = 0f;
            }
            return false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //略推敌人便于卷入
            if (Phase == 1f) {
                modifiers.HitDirectionOverride = (int)GroundDir;
            }
            modifiers.Knockback *= 0.6f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.knockBackResist > 0f) {
                target.velocity.Y -= 2.2f * target.knockBackResist;
                target.velocity.X += (Phase == 1f ? GroundDir : Math.Sign(Projectile.velocity.X)) * 0.5f * target.knockBackResist;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 baseCenter = Projectile.Center - Main.screenPosition;
            float t = SwirlTime;
            float lifeAlpha = MathHelper.Clamp(Projectile.timeLeft / 50f, 0f, 1f);
            float fadeIn = MathHelper.Clamp((Lifetime - Projectile.timeLeft) / 10f, 0f, 1f);
            float globalAlpha = lifeAlpha * fadeIn;

            Color sandWarm = new Color(225, 185, 95);
            Color sandLight = new Color(255, 235, 170);

            //4 层旋涡切片
            Texture2D cyclone = CWRAsset.Cyclone?.Value;
            if (cyclone != null) {
                Vector2 origin = cyclone.Size() / 2f;
                const int slices = 24;
                for (int i = 0; i < slices; i++) {
                    float t01 = i / (float)(slices - 1);                  //0 顶端，1 底端
                    float yOff = MathHelper.Lerp(-Projectile.height * 0.55f, Projectile.height * 0.35f, t01);
                    //顶宽底收
                    float scaleX = MathHelper.Lerp(0.55f, 0.32f, t01);
                    float scaleY = MathHelper.Lerp(0.42f, 0.26f, t01) / 2;
                    float rot = t * (1.5f - i * 0.15f);
                    Color c = Color.Lerp(sandLight, sandWarm, t01) * globalAlpha * 0.5f;
                    c.A = 0;
                    Main.spriteBatch.Draw(cyclone, baseCenter + new Vector2(MathF.Sin(t + i) * 1.5f, yOff)
                        , null, c, rot, origin, new Vector2(scaleX, scaleY), SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(cyclone, baseCenter + new Vector2(0, yOff)
                        , null, c * 0.6f, -rot * 0.65f, origin, new Vector2(scaleX * 0.82f, scaleY * 0.9f), SpriteEffects.None, 0f);
                }
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.2f }, Projectile.Center);
            for (int i = 0; i < 26; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                int dustType = Main.rand.NextBool(3) ? DustID.Gold : DustID.Sand;
                int d = Dust.NewDust(Projectile.Center, 1, 1, dustType, vel.X, vel.Y, 100, default, 1.3f);
                Main.dust[d].noGravity = true;
            }
        }
    }
}
