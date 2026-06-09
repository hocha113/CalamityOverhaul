using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Marbles
{
    /// <summary>
    /// 大理石猎刀：快速的交替连斩，每第三击为更宽的终结斩，向前迸射大理石碎片
    /// </summary>
    internal class MarbleHuntingKnife : ModItem
    {
        private static int swingCounter;
        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarbleHuntingKnife";

        public override void SetDefaults() {
            Item.width = Item.height = 40;
            Item.damage = 13;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTurn = false;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 3f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MarbleHuntingKnifeHeld>();
            Item.shootSpeed = 8f;
            Item.value = Item.sellPrice(0, 0, 50, 0);
            Item.rare = ItemRarityID.Green;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            MarbleSwingPlayer mp = player.GetModPlayer<MarbleSwingPlayer>();
            bool finisher = mp.ComboStep >= 2;
            mp.ComboStep = finisher ? 0 : mp.ComboStep + 1;
            mp.ComboTimer = 45;

            swingCounter++;
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback
                , player.whoAmI, swingCounter % 2 == 0 ? 1f : -1f, finisher ? 1f : 0f);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Marble, 14)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 6)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    /// <summary>
    /// 猎刀手持弹幕：交替上/下斩的快速挥砍，终结斩弧度更大、收尾迸射碎片（结构参照断罪师 Arbiter）
    /// </summary>
    internal class MarbleHuntingKnifeHeld : BaseHeldProj
    {
        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarbleHuntingKnife";

        private const int WindupTime = 2;
        private const int SlashTime = 9;
        private const int RecoverTime = 5;
        private const int TotalTime = WindupTime + SlashTime + RecoverTime;
        //刀刃在无旋转时指向约 -57°（右上，依据贴图像素主轴实测）
        private const float TextureBladeAngle = -0.996f;

        private int elapsed;
        private int lockedDirection = 1;
        private int swingSign = 1;
        private bool isFinisher;
        private bool shardsSpawned;
        private float currentRotation;
        private float lastRotation;
        private float startAngle;
        private float endAngle;
        private Vector2 pivot;

        public float CurrentAngle => currentRotation;

        private float HoldDistance => 24f;
        private float SwingDistance => 30f;
        private float BladeLength => isFinisher ? 66f : 52f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = TotalTime + 30;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => elapsed >= WindupTime && elapsed <= WindupTime + SlashTime + 1;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            Vector2 tip = pivot + currentRotation.ToRotationVector2() * BladeLength;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , pivot, tip, 30f, ref collisionPoint);
        }

        public override void Initialize() {
            isFinisher = Projectile.ai[1] >= 0.5f;
            swingSign = Math.Sign(Projectile.ai[0]);
            if (swingSign == 0) {
                swingSign = 1;
            }

            lockedDirection = Math.Sign(ToMouse.X);
            if (lockedDirection == 0) {
                lockedDirection = Owner.direction;
            }
            Owner.direction = lockedDirection;

            //上斩与下斩的玩家相对角，swingSign 决定本次是自上而下还是自下而上
            float up = isFinisher ? -MathHelper.Pi * 0.62f : -MathHelper.Pi * 0.46f;
            float down = isFinisher ? MathHelper.Pi * 0.42f : MathHelper.Pi * 0.28f;
            if (swingSign > 0) {
                startAngle = MirrorAngle(up);
                endAngle = MirrorAngle(down);
            }
            else {
                startAngle = MirrorAngle(down);
                endAngle = MirrorAngle(up);
            }
            currentRotation = startAngle;
            lastRotation = startAngle;

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(isFinisher
                    ? SoundID.Item71 with { Pitch = 0.15f }
                    : SoundID.Item1 with { Pitch = 0.35f, Volume = 0.7f }, Owner.Center);
            }
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<MarbleHuntingKnife>()) {
                Projectile.Kill();
                return;
            }
            if (elapsed >= TotalTime) {
                Projectile.Kill();
                return;
            }

            lastRotation = currentRotation;
            float distance;
            if (elapsed < WindupTime) {
                currentRotation = startAngle;
                distance = HoldDistance;
            }
            else if (elapsed < WindupTime + SlashTime) {
                //利落的 ease-out 快斩
                float s = (elapsed - WindupTime) / (float)SlashTime;
                float eased = s * (2f - s);
                currentRotation = MathHelper.Lerp(startAngle, endAngle, eased);
                distance = MathHelper.Lerp(HoldDistance, SwingDistance, eased);

                if (isFinisher && !VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 along = GetHandPos() + currentRotation.ToRotationVector2()
                        * Main.rand.NextFloat(BladeLength * 0.5f, BladeLength);
                    PRTLoader.NewParticle<PRT_Sparkle>(along, Vector2.Zero, GraniteMarbleVFX.MarbleGold, 0.5f)
                        .Configure(GraniteMarbleVFX.MarbleGold, 12, 0.2f, 0.5f);
                }
            }
            else {
                currentRotation = endAngle;
                distance = SwingDistance;
            }

            pivot = GetHandPos() + currentRotation.ToRotationVector2() * distance;

            //终结斩收尾迸射碎片（不依赖是否命中，保证效果可见）
            if (isFinisher && !shardsSpawned && elapsed >= WindupTime + SlashTime - 1) {
                shardsSpawned = true;
                SpawnFinisherShards();
            }

            UpdatePlayerPose();
            Lighting.AddLight(pivot, GraniteMarbleVFX.MarbleCore.ToVector3() * 0.35f);
            elapsed++;
        }

        private void SpawnFinisherShards() {
            Vector2 tip = GetHandPos() + currentRotation.ToRotationVector2() * BladeLength;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.2f }, tip);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(tip, Main.rand.NextVector2Circular(3f, 3f)
                        , GraniteMarbleVFX.MarbleGold, 0.6f).Configure(GraniteMarbleVFX.MarbleGold, 16, 0.2f, Main.rand.NextFloat(0.5f, 0.8f));
                }
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 baseDir = currentRotation.ToRotationVector2();
                for (int i = 0; i < 3; i++) {
                    Vector2 v = baseDir.RotatedBy(MathHelper.Lerp(-0.5f, 0.5f, i / 2f)) * Main.rand.NextFloat(7f, 10f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), tip, v
                        , ModContent.ProjectileType<MarbleShard>(), (int)(Projectile.damage * 0.5f)
                        , Projectile.knockBack * 0.5f, Projectile.owner);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center, Main.rand.NextVector2Circular(2f, 2f)
                    , GraniteMarbleVFX.MarbleCore, 0.5f).Configure(GraniteMarbleVFX.MarbleCore, 12, 0.2f, 0.5f);
            }
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;

            float armAngle = currentRotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle);

            Projectile.Center = pivot;
            Projectile.timeLeft = TotalTime + 30;
        }

        private Vector2 GetHandPos() {
            Vector2 p = Owner.GetPlayerStabilityCenter();
            p.Y -= 6f * Owner.gravDir;
            return p;
        }

        private float MirrorAngle(float rightFacingAngle)
            => lockedDirection > 0 ? rightFacingAngle : MathHelper.Pi - rightFacingAngle;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            SpriteEffects effect = lockedDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float drawRot = lockedDirection == -1 ? currentRotation + TextureBladeAngle : currentRotation - TextureBladeAngle;

            if (elapsed >= WindupTime && elapsed <= WindupTime + SlashTime + 1) {
                int trail = 5;
                Color tint = isFinisher ? GraniteMarbleVFX.MarbleGold : GraniteMarbleVFX.MarbleCore;
                for (int i = 0; i < trail; i++) {
                    float t = (i + 1) / (float)(trail + 1);
                    float rot = MathHelper.Lerp(currentRotation, lastRotation, t);
                    Vector2 pos = GetHandPos() + rot.ToRotationVector2() * SwingDistance - Main.screenPosition;
                    float trailRot = lockedDirection == -1 ? rot + TextureBladeAngle : rot - TextureBladeAngle;
                    Color trailColor = tint * (0.4f * (1f - i / (float)trail));
                    trailColor.A = 0;
                    Main.EntitySpriteDraw(tex, pos, null, trailColor, trailRot, origin, Projectile.scale, effect, 0);
                }
            }

            Main.EntitySpriteDraw(tex, pivot - Main.screenPosition, null, lightColor, drawRot, origin
                , Projectile.scale, effect, 0);
            return false;
        }
    }

    /// <summary>
    /// 大理石近战连击状态：用于猎刀的三连终结判定
    /// </summary>
    internal class MarbleSwingPlayer : ModPlayer
    {
        public int ComboStep;
        public int ComboTimer;

        public override void ResetEffects() {
            if (ComboTimer > 0) {
                ComboTimer--;
            }
            else {
                ComboStep = 0;
            }
        }
    }
}
