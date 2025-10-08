using CalamityMod;
using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ID.ContentSamples.CreativeHelper;

namespace CalamityOverhaul.Content.Items.Rogue
{
    internal class GeminisTribute : ModItem
    {
        public override string Texture => CWRConstant.Item_Rogue + "GeminisTribute";
        public override void SetDefaults() {
            Item.width = 52;
            Item.height = 52;
            Item.damage = 40;
            Item.DamageType = CWRLoad.RogueDamageClass;
            Item.useAnimation = Item.useTime = 20;
            Item.shootSpeed = 22f;
            Item.knockBack = 4f;
            Item.shoot = ModContent.ProjectileType<GeminisTributeHeld>();
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = null;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(0, 1, 60, 5);
            Item.crit = 4;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.CWR().DeathModeItem = true;
            ItemOverride.ItemMeleePrefixDic[Type] = true;
            ItemOverride.ItemRangedPrefixDic[Type] = false;
        }
        public override void ModifyResearchSorting(ref ItemGroup itemGroup) => itemGroup = (ItemGroup)CalamityResearchSorting.RogueWeapon;
    }

    internal class GeminisTributeEX : ModItem
    {
        public override string Texture => CWRConstant.Item_Rogue + "GeminisTribute";
        public override void SetDefaults() {
            Item.width = 52;
            Item.height = 52;
            Item.damage = 282;
            Item.DamageType = CWRLoad.RogueDamageClass;
            Item.useAnimation = Item.useTime = 18;
            Item.shootSpeed = 22f;
            Item.knockBack = 4f;
            Item.shoot = ModContent.ProjectileType<GeminisTributeEXHeld>();
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = null;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 8, 60, 5);
            Item.crit = 6;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            ItemOverride.ItemMeleePrefixDic[Type] = true;
            ItemOverride.ItemRangedPrefixDic[Type] = false;
        }
        public override void ModifyResearchSorting(ref ItemGroup itemGroup) => itemGroup = (ItemGroup)CalamityResearchSorting.RogueWeapon;
        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<GeminisTribute>().
                AddIngredient<SoulofSightEX>().
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    internal class GeminisTributeHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Item_Rogue + "GeminisTribute";
        private bool outFire;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetThrowable() {
            HandOnTwringMode = -50;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, 75, targetHitbox);
        }

        public override bool PreThrowOut() {
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, Owner.Center);
            Projectile.extraUpdates = 2;
            Projectile.penetrate = -1;
            outFire = true;
            return true;
        }

        public override void FlyToMovementAI() {
            if (!outFire) {
                base.FlyToMovementAI();
                return;
            }

            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3());
            Projectile.rotation += 0.6f * Math.Sign(Projectile.velocity.X);

            if (Projectile.soundDelay == 0) {
                Projectile.soundDelay = 8;
                SoundEngine.PlaySound(SoundID.Item7, Projectile.position);
            }

            switch (Projectile.ai[0]) {
                case 0f:
                    if (++Projectile.ai[1] >= 40f) {
                        Projectile.ai[0] = 1f;
                        Projectile.ai[1] = 0f;
                        Projectile.netUpdate = true;
                    }
                    break;

                case 1f:
                    const float returnSpeed = 25f;
                    const float acceleration = 5f;

                    Vector2 playerVec = Owner.Center - Projectile.Center;
                    if (playerVec.LengthSquared() > 16000000f) { //4000^2, 避免调用 Length()
                        Projectile.Kill();
                        break;
                    }

                    playerVec.Normalize();
                    playerVec *= returnSpeed;

                    Projectile.velocity.X = AdjustVelocity(Projectile.velocity.X, playerVec.X, acceleration);
                    Projectile.velocity.Y = AdjustVelocity(Projectile.velocity.Y, playerVec.Y, acceleration);

                    if (Projectile.IsOwnedByLocalPlayer() && Projectile.Hitbox.Intersects(Owner.Hitbox)) {
                        Projectile.Kill();
                    }
                    break;
            }
        }

        private static float AdjustVelocity(float current, float target, float accel) {
            if (current < target) {
                current += accel;
                if (current < 0f && target > 0f) current += accel;
            }
            else if (current > target) {
                current -= accel;
                if (current > 0f && target < 0f) current -= accel;
            }
            return current;
        }

        public override void DrawThrowable(Color lightColor) {
            if (!outFire) {
                base.DrawThrowable(lightColor);
                return;
            }

            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = texture.GetRectangle();
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = lightColor * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle
                , lightColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
        }

        private void HitEffect(Entity target) {
            if (Projectile.numHits != 0 || !stealthStrike) {
                return;
            }
            Projectile.numHits++;
            int starPoints = 8;
            for (int i = 0; i < starPoints; i++) {
                float angle = MathHelper.TwoPi * i / starPoints;
                for (int j = 0; j < 12; j++) {
                    float starSpeed = MathHelper.Lerp(2f, 10f, j / 12f);
                    Color dustColor = Color.Lerp(Color.Red, Color.DarkRed, j / 12f);
                    float dustScale = MathHelper.Lerp(1.6f, 0.85f, j / 12f);

                    Dust fire = Dust.NewDustPerfect(target.Center, DustID.RedTorch);
                    fire.velocity = angle.ToRotationVector2() * starSpeed;
                    fire.color = dustColor;
                    fire.scale = dustScale;
                    fire.noGravity = true;
                }
            }
            for (int i = 0; i < 8; i++) {
                Vector2 ver = Projectile.velocity.RotatedBy(MathHelper.TwoPi / 8f * i);
                Projectile.NewProjectile(Projectile.FromObjectGetParent(), target.Center, ver, ModContent.ProjectileType<PunisherGrenadeRogue>()
                , Projectile.damage, Projectile.knockBack, Projectile.owner, Main.rand.NextBool() ? 0 : 1);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            HitEffect(target);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            HitEffect(target);
        }
    }

    internal class GeminisTributeEXHeld : GeminisTributeHeld
    {
        public override string Texture => CWRConstant.Item_Rogue + "GeminisTribute";
        public override void SetThrowable() {
            base.SetThrowable();
        }
        private void HitEffect(Entity target) {
            if (Projectile.numHits != 0) {
                return;
            }
            Projectile.numHits++;
            Vector2 spanPos = Projectile.Center + Projectile.velocity.UnitVector().RotatedByRandom(0.6f) * Main.rand.Next(760, 920);
            Vector2 ver = spanPos.To(target.Center).UnitVector() * 13;
            Projectile.NewProjectile(Projectile.FromObjectGetParent(), spanPos, ver, ModContent.ProjectileType<GeminisTributeAlt>()
                , Projectile.damage / 2, Projectile.knockBack / 2, Projectile.owner, Main.rand.NextBool() ? 0 : 1);

            if (stealthStrike) {
                for (int i = 0; i < 16; i++) {
                    ver = Projectile.velocity.RotatedBy(MathHelper.TwoPi / 16f * i);
                    Projectile.NewProjectile(Projectile.FromObjectGetParent()
                        , target.Center, ver, ModContent.ProjectileType<PunisherGrenadeRogue>()
                    , Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            HitEffect(target);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            HitEffect(target);
        }
    }

    internal class GeminisTributeAlt : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Rogue + "GeminisTributeAlt";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.localNPCHitCooldown = 30;
            Projectile.extraUpdates = 3;
            Projectile.penetrate = -1;
            Projectile.width = Projectile.height = 132;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.DamageType = CWRLoad.RogueDamageClass;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Main.DiscoR * 0.5f / 255f, Main.DiscoG * 0.5f / 255f, Main.DiscoB * 0.5f / 255f);
            Projectile.rotation += 1f;

            if (Projectile.soundDelay == 0) {
                Projectile.soundDelay = 8;
                SoundEngine.PlaySound(SoundID.Item7, Projectile.position);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            HitEffect();
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            HitEffect();
        }

        private void HitEffect() {
            int shootID = Projectile.ai[0] == 0 ? ProjectileID.MiniRetinaLaser : ProjectileID.BallofFire;
            for (int i = 0; i < 4; i++) {
                Vector2 ver = (MathHelper.TwoPi / 4 * i).ToRotationVector2() * 23;
                int proj;
                proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, ver
                    , shootID, (int)(Projectile.damage * 0.7), Projectile.knockBack, Projectile.owner);
                Main.projectile[proj].DamageType = CWRLoad.RogueDamageClass;
                ver = (MathHelper.TwoPi / 4 * i).ToRotationVector2() * 33;
                proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, ver
                    , shootID, (int)(Projectile.damage * 0.7), Projectile.knockBack, Projectile.owner);
                Main.projectile[proj].DamageType = CWRLoad.RogueDamageClass;
            }
            SoundEngine.PlaySound(SoundID.Item122, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = texture.GetRectangle();
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = lightColor * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle
                , lightColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class PunisherGrenadeRogue : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "PunisherGrenade";
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = CWRLoad.RogueDamageClass;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Projectile.timeLeft > 90) {
                Projectile.velocity *= 0.95f;
            }
            else {
                Projectile.penetrate = 1;
                CalamityUtils.HomeInOnNPC(Projectile, true, 350f, 15f, 10f);
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14, Projectile.position);
            for (int i = 0; i < 10; i++) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.CopperCoin, 0f, 0f, 0, default, 1f);
            }
        }
    }
}
