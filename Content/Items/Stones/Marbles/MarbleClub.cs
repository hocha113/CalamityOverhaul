using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>
    /// 大理石巨棍：缓慢沉重的过顶猛砸，命中有几率石化减速，砸落瞬间在落点迸发冲击波 + 尘土 + 屏震
    /// </summary>
    internal class MarbleClub : ModItem
    {
        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarbleClub";

        public override void SetDefaults() {
            Item.width = Item.height = 56;
            Item.damage = 22;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 33;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTurn = false;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 7.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MarbleClubHeld>();
            Item.shootSpeed = 6f;
            Item.value = Item.sellPrice(0, 0, 80, 0);
            Item.rare = ItemRarityID.Orange;
        }

        //场上只允许一柄巨棍，避免连点重复进入挥击
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Marble, 25)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 12)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    /// <summary>
    /// 巨棍手持弹幕：完整接管玩家手臂动作，自抬手 → 过顶猛砸 → 收力的全过程（结构参照断罪师 Arbiter）
    /// </summary>
    internal class MarbleClubHeld : BaseHeldProj
    {
        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarbleClub";

        //三段式节奏：抬手蓄势 → 猛砸 → 收力，总时长与 useAnimation 对齐
        private const int WindupTime = 9;
        private const int SlamTime = 13;
        private const int RecoverTime = 11;
        private const int TotalTime = WindupTime + SlamTime + RecoverTime;

        //以"朝右"为基准的玩家相对角：自然预备 → 高举(上后方) → 砸落(下前方)，左右由 MirrorAngle 处理镜像
        private const float ReadyRel = -MathHelper.Pi * 0.26f;
        private const float LiftRel = -MathHelper.Pi * 0.72f;
        private const float EndRel = MathHelper.Pi * 0.5f;
        //纹理在无旋转时棍头指向约 -63.5°（右上偏陡，依据贴图像素主轴实测），绘制时补偿到实际指向
        private const float TextureBladeAngle = -1.108f;

        private static float HoldDistance => 40f;
        private static float SwingDistance => 40f;
        private static float BladeLength => 96f;

        private int elapsed;
        private int lockedDirection = 1;
        private float currentRotation;
        private float lastRotation;
        private Vector2 pivot;
        private bool impactDone;

        public float CurrentAngle => currentRotation;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 130;
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

        //仅在砸落阶段（含一点收尾余量）参与伤害
        public override bool? CanDamage() => elapsed >= WindupTime && elapsed <= WindupTime + SlamTime + 2;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            Vector2 tip = pivot + currentRotation.ToRotationVector2() * BladeLength;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , pivot, tip, 42f, ref collisionPoint);
        }

        public override void Initialize() {
            //朝向锁定到光标所在的左右侧，整个挥击过程不再随玩家转身而抖动
            lockedDirection = Math.Sign(ToMouse.X);
            if (lockedDirection == 0) {
                lockedDirection = Owner.direction;
            }
            Owner.direction = lockedDirection;
            currentRotation = MirrorAngle(ReadyRel);
            lastRotation = currentRotation;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.55f, Volume = 0.8f }, Owner.Center);
            }
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<MarbleClub>()) {
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
                //抬手：缓出地把棍举到上后方
                float w = elapsed / (float)WindupTime;
                w = 1f - (1f - w) * (1f - w);
                currentRotation = MathHelper.Lerp(MirrorAngle(ReadyRel), MirrorAngle(LiftRel), w);
                distance = HoldDistance;
            }
            else if (elapsed < WindupTime + SlamTime) {
                //猛砸：EaseInCubic，前慢后快地砸下
                float s = (elapsed - WindupTime) / (float)SlamTime;
                float eased = s * s * s;
                currentRotation = MathHelper.Lerp(MirrorAngle(LiftRel), MirrorAngle(EndRel), eased);
                distance = MathHelper.Lerp(HoldDistance, SwingDistance, eased);
                SpawnSwingParticles();
            }
            else {
                //收力：从砸落点略微回抬
                float r = (elapsed - WindupTime - SlamTime) / (float)RecoverTime;
                currentRotation = MathHelper.Lerp(MirrorAngle(EndRel), MirrorAngle(EndRel) - 0.32f * lockedDirection, r);
                distance = SwingDistance;
            }

            pivot = GetHandPos() + currentRotation.ToRotationVector2() * distance;

            //砸落末端触发一次性落点冲击
            if (!impactDone && elapsed >= WindupTime + SlamTime - 2) {
                impactDone = true;
                OnSlamImpact();
            }

            UpdatePlayerPose();
            Lighting.AddLight(pivot, GraniteMarbleVFX.MarbleGold.ToVector3() * 0.5f);
            elapsed++;
        }

        private void SpawnSwingParticles() {
            if (VaultUtils.isServer || !Main.rand.NextBool(2)) {
                return;
            }
            Vector2 along = GetHandPos() + currentRotation.ToRotationVector2()
                * Main.rand.NextFloat(BladeLength * 0.4f, BladeLength);
            PRTLoader.NewParticle<PRT_Sparkle>(along, Vector2.Zero, GraniteMarbleVFX.MarbleGold, 0.6f)
                .Configure(GraniteMarbleVFX.MarbleGold, 12, 0.2f, Main.rand.NextFloat(0.4f, 0.7f));
        }

        private void OnSlamImpact() {
            Vector2 impact = GetHandPos() + currentRotation.ToRotationVector2() * BladeLength;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.35f }, impact);
                for (int i = 0; i < 14; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(impact, Main.rand.NextVector2Circular(5f, 5f)
                        , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.4f, 0.7f)).Configure(26, 0.7f, 0.05f);
                }
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(impact, Main.rand.NextVector2Circular(3f, 3f)
                        , GraniteMarbleVFX.MarbleGold, 0.7f).Configure(GraniteMarbleVFX.MarbleGold, 18, 0.2f, Main.rand.NextFloat(0.5f, 0.9f));
                }
            }

            if (CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(impact, Main.rand.NextVector2Unit()
                    , 6.5f, 6f, 14, 800f, FullName));
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), impact, Vector2.Zero
                    , ModContent.ProjectileType<MarbleShockwave>(), (int)(Projectile.damage * 0.55f)
                    , Projectile.knockBack * 0.5f, Projectile.owner, 0f, 150f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //石化：减速并短暂迟滞
            if (Main.rand.NextBool(2)) {
                target.AddBuff(BuffID.Slow, 120);
                if (!target.boss) {
                    target.velocity *= 0.45f;
                }
            }
        }

        /// <summary>
        /// 让玩家双臂跟随棍体朝向，避免"凭空持棍"，这是挥击观感的关键
        /// </summary>
        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;

            float armAngle = currentRotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armAngle + 0.1f * lockedDirection);

            Projectile.Center = pivot;
            Projectile.timeLeft = TotalTime + 30;
        }

        private Vector2 GetHandPos() {
            Vector2 p = Owner.GetPlayerStabilityCenter();
            p.Y -= 6f * Owner.gravDir;
            return p;
        }

        //朝右直接返回，朝左绕 Y 轴镜像（π - θ），保证斜向姿势在两个朝向都正确
        private float MirrorAngle(float rightFacingAngle)
            => lockedDirection > 0 ? rightFacingAngle : MathHelper.Pi - rightFacingAngle;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            //朝左时竖直镜像，并按 +TextureBladeAngle 补偿（FlipVertically 下的通用解，使棍头始终指向 currentRotation）
            SpriteEffects effect = lockedDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float drawRot = lockedDirection == -1 ? currentRotation + TextureBladeAngle : currentRotation - TextureBladeAngle;

            //砸落阶段的弧线残影（在上一帧与本帧角度之间采样若干中间姿态）
            if (elapsed >= WindupTime && elapsed <= WindupTime + SlamTime + 2) {
                int trail = 6;
                for (int i = 0; i < trail; i++) {
                    float t = (i + 1) / (float)(trail + 1);
                    float rot = MathHelper.Lerp(currentRotation, lastRotation, t);
                    Vector2 pos = GetHandPos() + rot.ToRotationVector2() * SwingDistance - Main.screenPosition;
                    float trailRot = lockedDirection == -1 ? rot + TextureBladeAngle : rot - TextureBladeAngle;
                    Color trailColor = GraniteMarbleVFX.MarbleGold * (0.42f * (1f - i / (float)trail));
                    trailColor.A = 0;
                    Main.EntitySpriteDraw(tex, pos, null, trailColor, trailRot, origin, Projectile.scale * 1.02f, effect, 0);
                }
            }

            Main.EntitySpriteDraw(tex, pivot - Main.screenPosition, null, lightColor, drawRot, origin
                , Projectile.scale * 1.05f, effect, 0);
            return false;
        }
    }
}
