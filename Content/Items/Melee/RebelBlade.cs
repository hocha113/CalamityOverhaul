using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 叛逆之刃
    /// </summary>
    internal class RebelBlade : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "RebelBlade";
        public override void SetDefaults() {
            Item.width = Item.height = 54;
            Item.shootSpeed = 9;
            Item.crit = 8;
            Item.damage = 286;
            Item.useTime = 30;
            Item.useAnimation = 15;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(0, 83, 55, 0);
            Item.rare = ItemRarityID.Lime;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = null;
            Item.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Item.shoot = ModContent.ProjectileType<RebelBladeFlyAttcke>();
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.CWR().isHeldItem = true;
            Item.SetKnifeHeld<RebelBladeHeld>(true);
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame) => player.itemLocation = player.GetPlayerStabilityCenter();

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<RebelBladeFlyAttcke>()] == 0;

        public override void HoldItem(Player player) {
            if (Main.myPlayer != player.whoAmI || player.PressKey()) {
                return;
            }

            bool spwan = true;

            int rebelBladeBack = ModContent.ProjectileType<RebelBladeBack>();
            int rebelBladeFlyAttcke = ModContent.ProjectileType<RebelBladeFlyAttcke>();
            int rebelBladeHeld = ModContent.ProjectileType<RebelBladeHeld>();

            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI) {
                    continue;
                }
                if (proj.type == rebelBladeBack || proj.type == rebelBladeFlyAttcke || proj.type == rebelBladeHeld) {
                    spwan = false;
                    break;
                }
            }

            if (spwan) {
                Projectile.NewProjectileDirect(player.GetSource_FromThis(), player.Center, Vector2.Zero, rebelBladeBack, 0, 0, player.whoAmI);
            }
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                SoundEngine.PlaySound(SoundID.Item1, position);
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<RebelBladeFlyAttcke>(), (int)(damage * 0.6f), knockback, player.whoAmI);
                return false;
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.LunarBar, 10)
                .AddIngredient(ItemID.SoulofMight, 15)
                .AddIngredient(ItemID.SoulofLight, 15)
                .AddIngredient(ItemID.SoulofNight, 15)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    internal class RebelBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<RebelBlade>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail2";
        public override string gradientTexturePath => CWRConstant.ColorBar + "RebelBlade_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            distanceToOwner = -20;
            drawTrailBtommWidth = 110;
            drawTrailTopWidth = 130;
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
            ShootSpeed = 12;
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(
            phase0SwingSpeed: -0.4f,
            phase1SwingSpeed: 3.4f,
            phase2SwingSpeed: 7f,
            swingSound: SoundID.Item71 with { Pitch = -0.6f });
            return base.PreInOwner();
        }

        public override void MeleeEffect() {
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height
                , DustID.FireworkFountain_Blue, 0, 0, 55);
            dust.noGravity = true;
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.FromWormBodysRandomSet(5)) {
                return;
            }

            int type = ModContent.ProjectileType<RebelBladeOrb>();
            if (Owner.ownedProjectileCounts[type] > 33) {
                return;
            }

            for (int i = 0; i < 3; i++) {
                Vector2 spwanPos = target.position + new Vector2(target.width * Main.rand.NextFloat(), target.height * Main.rand.NextFloat());
                Projectile.NewProjectile(Source, spwanPos, Vector2.Zero
                    , ModContent.ProjectileType<RebelBladeOrb>(), Item.damage / 5, 0, Owner.whoAmI);
                Owner.ownedProjectileCounts[type]++;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            for (int i = 0; i < 3; i++) {
                Vector2 spwanPos = target.position + new Vector2(target.width * Main.rand.NextFloat(), target.height * Main.rand.NextFloat());
                Projectile.NewProjectile(Source, spwanPos, Vector2.Zero
                    , ModContent.ProjectileType<RebelBladeOrb>(), Item.damage / 5, 0, Owner.whoAmI);
            }
        }
    }

    internal class RebelBladeBack : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "RebelBlade";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 45;
            Projectile.timeLeft = 200;
            Projectile.knockBack = 2;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            if (Owner.GetItem().type != ModContent.ItemType<RebelBlade>()
                || Owner.ownedProjectileCounts[ModContent.ProjectileType<RebelBladeFlyAttcke>()] > 0
                || DownLeft || DownRight
                ) {
                Projectile.Kill();
            }
            Projectile.timeLeft = 2;
            Projectile.Center = Owner.GetPlayerStabilityCenter();
            float rot = 120;
            Projectile.rotation = Owner.direction > 0 ? MathHelper.ToRadians(rot) : MathHelper.ToRadians(180 - rot);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition + Owner.CWR().SpecialDrawPositionOffset;
            Main.EntitySpriteDraw(value, drawPos, null, lightColor, Projectile.rotation + MathHelper.PiOver4, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs
            , List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }
    }

    internal class RebelBladeFlyAttcke : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "RebelBlade";

        private Color tillColor = Color.White;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 3;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 45;
            Projectile.timeLeft = 200;
            Projectile.knockBack = 2;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.SetProjtimesPierced(0);
            if (Projectile.localAI[1] <= 0) {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            if (Projectile.localAI[0] > 0 || Projectile.localAI[1] > 0) {
                tillColor = Color.Red;
            }

            if (!DownRight) {
                Projectile.tileCollide = false;
                tillColor = Color.CadetBlue;
                Projectile.ChasingBehavior(Owner.Center, 23);
                if (Projectile.Distance(Owner.Center) < 80) {
                    Projectile.Kill();
                }
            }
            else if (Projectile.localAI[1] <= 0) {
                tillColor = Color.Yellow;
                Projectile.tileCollide = true;
                Projectile.timeLeft = 200;
                Owner.itemRotation = (Projectile.velocity * Projectile.direction).ToRotation();
                Vector2 mousePos = ToMouse + Owner.GetPlayerStabilityCenter();
                Vector2 ver = Projectile.Center.To(mousePos);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.ai[0] += Main.rand.Next(1, 3);
                    Projectile.netUpdate = true;//肮脏的手段——HoCha113, 2024-06-02 02:37
                }
                if (Projectile.ai[0] > 30) {
                    SoundEngine.PlaySound(SoundID.Item7, Projectile.Center);
                    Projectile.velocity = ver.UnitVector() * 45;
                    Projectile.ai[0] = 0;
                }
                Projectile.velocity *= 0.98f;
                if (ver.Length() < 16) {
                    Projectile.velocity = Projectile.velocity.RotatedByRandom(0.9f);
                }
            }

            if (Projectile.localAI[0] > 0) {
                Projectile.localAI[0]--;
            }
            if (Projectile.localAI[1] > 0) {
                Projectile.localAI[1]--;
            }

            float rot = (MathHelper.PiOver2 * SafeGravDir - Owner.Center.To(Projectile.Center).ToRotation()) * DirSign * SafeGravDir;
            float rot2 = (MathHelper.PiOver2 * SafeGravDir - MathHelper.ToRadians(DirSign > 0 ? -20 : 200)) * DirSign * SafeGravDir;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rot2 * -DirSign);
            Owner.direction = Owner.Center.To(Projectile.Center).X > 0 ? 1 : -1;

            Lighting.AddLight(Projectile.Center, tillColor.ToVector3() * 2.2f);
        }

        private void HitEffet(Vector2 returnVer) {
            if (Projectile.localAI[0] <= 0) {
                Projectile.localAI[0] = 12;
                Projectile.localAI[1] = 12;
                Projectile.rotation = (-Projectile.velocity).ToRotation();
                Vector2 splatterDirection = returnVer.SafeNormalize(Vector2.UnitY);
                for (int j = 0; j < 3; j++) {
                    float sparkScale = Main.rand.NextFloat(1.2f, 2.33f);
                    int sparkLifetime = Main.rand.Next(22, 36);
                    Color sparkColor = Color.Lerp(Color.Silver, Color.Gold, Main.rand.NextFloat(0.7f));
                    Vector2 sparkVelocity = splatterDirection.RotatedByRandom(0.9f) * Main.rand.NextFloat(19f, 34.5f);
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, sparkVelocity, sparkColor, sparkScale).Configure(true, sparkLifetime);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            HitEffet(Projectile.velocity);
            if (Projectile.damage < Projectile.originalDamage * 5) {
                Projectile.damage += 15;
            }
            Projectile.velocity = Projectile.velocity.RotatedByRandom(0.6f);
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.timeLeft = 30;
            Projectile.velocity = -oldVelocity;
            Projectile.DigByTile(CWRSound.HitTheSteel with { MaxInstances = 3, Volume = 0.5f });
            HitEffet(Projectile.velocity);
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = texture.GetRectangle();
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = lightColor * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.oldRot[k] + MathHelper.PiOver4, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + MathHelper.PiOver4, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    public class RebelBladeOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.penetrate = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width
                , Projectile.height, DustID.FireworkFountain_Blue, 0, 0, 55, Main.DiscoColor);
            dust.noGravity = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 30);
            target.AddBuff(BuffID.OnFire3, 30);

            if (target.IsWormBody()) {
                Projectile.timeLeft = 1;
            }
            else {
                target.AddBuff(ModContent.BuffType<HellburnBuff>(), 30);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage /= 10;
            }
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(66, SoundID.Item60 with { Pitch = 0.6f });
        }
    }
}
