using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class CommandersStaff : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "CommandersStaff";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 32;
            Item.height = 32;
            Item.damage = 72;
            Item.useTime = 62;
            Item.useAnimation = 62;
            Item.mana = 20;
            Item.shoot = ModContent.ProjectileType<CommandersRay>();
            Item.shootSpeed = 10;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(0, 1, 60, 10);
            Item.CWR().DeathModeItem = true;
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<CommandersStaffHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<CommandersStaffHeld>(player, source);
    }

    internal class CommandersStaffEX : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "CommandersStaffEX";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 32;
            Item.height = 32;
            Item.damage = 202;
            Item.useTime = 62;
            Item.useAnimation = 62;
            Item.mana = 20;
            Item.shoot = ModContent.ProjectileType<CommandersRay>();
            Item.shootSpeed = 10;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 8, 60, 10);
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<CommandersStaffEXHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<CommandersStaffEXHeld>(player, source);

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<CommandersStaff>().
                AddIngredient<SoulofFrightEX>().
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    /// <summary>指挥官持握，45°持杖从杖尖放射线</summary>
    internal abstract class BaseCommandersStaffHeld : BaseHeldGun, IOverlayDrawable
    {
        public override SoundStyle? ShootSound => SoundID.Item68;
        public sealed override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            HandFireDistanceX = 0;
            HandFireDistanceY = 0;
            MuzzleForwardOffset = 90;
            GunPressure = 0;
            ControlForce = 0;
            Onehanded = true;
            AlwaysAimPose = true;
        }

        public override void AI() {
            UpdateHeldPose(WantsFireLeft);

            if (CanFire) {
                HoldManaRegenDelay();
            }

            if (WantsFireLeft && FireCooldown <= 0 && PayMana()) {
                SnapToAimPose();
                PlayShootSound();
                CreateFireLight();
                if (Projectile.IsOwnedByLocalPlayer()) {
                    FireRay();
                }
                SetFireCooldown();
            }
            Time++;
        }

        /// <summary>放射线，仅主人端</summary>
        public abstract void FireRay();

        //法杖走 IOverlayDrawable 遮挡层，盖住杖尖射线几何
        public sealed override bool PreDraw(ref Color lightColor) => false;

        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (!OnHandheldDisplayBool) {
                return;
            }
            Color lightColor = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            GunDraw(Projectile.Center - Main.screenPosition + SpecialDrawPositionOffset, ref lightColor);
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float rot = DirSign > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4;
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
            Vector2 orig = DirSign > 0 ? new Vector2(0, TextureValue.Height) : new Vector2(0, 0);
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation + offsetRot + rot, orig, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }

    internal class CommandersStaffHeld : BaseCommandersStaffHeld
    {
        public override string Texture => CWRConstant.Item_Magic + "CommandersStaffHeld";
        public override int TargetID => ModContent.ItemType<CommandersStaff>();
        public override void FireRay() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , ModContent.ProjectileType<CommandersRay>()
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, Projectile.identity);
        }
    }

    internal class CommandersStaffEXHeld : BaseCommandersStaffHeld
    {
        public override string Texture => CWRConstant.Item_Magic + "CommandersStaffEXHeld";
        public override int TargetID => ModContent.ItemType<CommandersStaffEX>();
        public override void FireRay() {
            //五束环口，ai1=端口号给 EX 自旋
            const int beamCount = 5;
            for (int i = 0; i < beamCount; i++) {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , ModContent.ProjectileType<CommandersRayEX>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI
                    , ai0: Projectile.identity, ai1: i);
            }
        }
    }

    /// <summary>射线骨架，跟瞄准 raycast 长度，quad+<see cref="EffectLoader.CommandersBeam"/></summary>
    internal abstract class BaseCommandersRay : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //展开/维持/收束
        private const int ExpandTime = 6;
        private const int SustainTime = 38;
        private const int CollapseTime = 10;
        private const int TotalLife = ExpandTime + SustainTime + CollapseTime;
        private const float MaxRayLength = 2000f;

        protected int Age;
        private float widthMul;
        private float toTileLeng;

        /// <summary>视觉/碰撞基准宽</summary>
        protected abstract float BeamWidth { get; }
        /// <summary>quad 相对 <see cref="BeamWidth"/> 倍率，多束时宜小</summary>
        protected virtual float VisualWidthMul => 3.2f;
        /// <summary>0=热能 1=EX，喂 CommandersBeam.exMode</summary>
        protected abstract float BeamMode { get; }
        protected abstract Color CoreThemeColor { get; }
        protected abstract Color GlowThemeColor { get; }

        /// <summary>子类给本帧起点与方向</summary>
        protected abstract void GetMuzzle(Projectile gunProj, out Vector2 origin, out Vector2 direction);

        /// <summary>子类 SetDefaults 收尾，默认可空</summary>
        protected virtual void SetExtraDefaults() { }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Magic;
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.alpha = 0;
            Projectile.timeLeft = TotalLife + 10;
            SetExtraDefaults();
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile homeProj = Main.projectile.FindByIdentity((int)Projectile.ai[0]);
            if (!homeProj.Alives() || homeProj.type <= ProjectileID.None) {
                Projectile.Kill();
                return;
            }

            Age++;
            if (Age >= TotalLife) {
                Projectile.Kill();
                return;
            }

            GetMuzzle(homeProj, out Vector2 origin, out Vector2 direction);
            Projectile.Center = origin;
            Projectile.rotation = direction.ToRotation();

            widthMul = Age < ExpandTime
                ? VaultUtils.EaseOutCubic(Age / (float)ExpandTime)
                : Age > TotalLife - CollapseTime
                    ? 1f - VaultUtils.EaseInQuad((Age - (TotalLife - CollapseTime)) / (float)CollapseTime)
                    : 1f;

            toTileLeng = MeasureRayLength(direction);

            if (!VaultUtils.isServer) {
                UpdateVisuals(direction);
            }
        }

        //raycast 终点，碰撞/绘制共用，服务端也算
        private float MeasureRayLength(Vector2 direction) {
            float length = 0f;
            while (length < MaxRayLength) {
                if (Framing.GetTileSafely(Projectile.Center + direction * length).HasSolidTile()) {
                    break;
                }
                length += 8f;
            }
            return length;
        }

        private void UpdateVisuals(Vector2 direction) {
            Lighting.AddLight(Projectile.Center, CoreThemeColor.ToVector3() * (0.5f * widthMul));
            int lightSteps = (int)(toTileLeng / 48f);
            for (int i = 1; i <= lightSteps; i++) {
                Lighting.AddLight(Projectile.Center + direction * (i * 48f), CoreThemeColor.ToVector3() * (0.6f * widthMul));
            }

            Vector2 perp = direction.RotatedBy(MathHelper.PiOver2);
            if (Main.rand.NextBool(3)) {
                float along = Main.rand.NextFloat(0.1f, 0.95f);
                Vector2 pos = Projectile.Center + direction * (toTileLeng * along) + perp * Main.rand.NextFloat(-BeamWidth * 0.35f, BeamWidth * 0.35f);
                Vector2 vel = direction.RotatedBy(Main.rand.NextFloat(-0.25f, 0.25f)) * Main.rand.NextFloat(2f, 5f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, Color.Lerp(CoreThemeColor, Color.White, Main.rand.NextFloat(0.2f, 0.6f)), Main.rand.NextFloat(0.5f, 0.95f))
                    ?.Configure(false, Main.rand.Next(10, 16));
            }
            if (toTileLeng > 48f) {
                Vector2 hitPos = Projectile.Center + direction * toTileLeng;
                PRTLoader.NewParticle<PRT_HeavenfallStar>(hitPos, VaultUtils.RandVr(5), GlowThemeColor, Main.rand.NextFloat(0.55f, 1.1f) * widthMul)
                    ?.Configure(false, 3);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (toTileLeng < 4f || widthMul < 0.05f) {
                return false;
            }
            float point = 0f;
            Vector2 direction = Projectile.rotation.ToRotationVector2();
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.Center, Projectile.Center + direction * toTileLeng, BeamWidth * 0.7f * widthMul, ref point);
        }

        public override void CutTiles() {
            DelegateMethods.tilecut_0 = TileCuttingContext.AttackProjectile;
            Vector2 direction = Projectile.rotation.ToRotationVector2();
            Utils.PlotTileLine(Projectile.Center, Projectile.Center + direction * toTileLeng, Projectile.width, DelegateMethods.CutTiles);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);
                Color color = Color.Lerp(CoreThemeColor, Color.White, Main.rand.NextFloat(0.15f, 0.5f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(target.Center, vel, color, Main.rand.NextFloat(0.7f, 1.3f))
                    ?.Configure(false, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, VaultUtils.RandVr(2, 5), CoreThemeColor, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (toTileLeng < 4f || widthMul < 0.02f) {
                return;
            }

            Effect effect = EffectLoader.CommandersBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            Vector2 direction = Projectile.rotation.ToRotationVector2();
            Vector2 perp = direction.RotatedBy(MathHelper.PiOver2);
            //起点回缩进杖体
            Vector2 muzzle = Projectile.Center - direction * (BeamWidth * 0.6f + 16f);
            Vector2 tip = Projectile.Center + direction * toTileLeng;
            float halfWidth = BeamWidth * VisualWidthMul * widthMul;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((muzzle + perp * halfWidth).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[1] = new VertexPositionColorTexture((muzzle - perp * halfWidth).ToVector3(), Color.White, new Vector2(1f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfWidth).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfWidth).ToVector3(), Color.White, new Vector2(0f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(widthMul);
            effect.Parameters["exMode"]?.SetValue(BeamMode);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.173f % 1f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (toTileLeng < 4f || widthMul < 0.02f) {
                return;
            }

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return;
            }

            Vector2 direction = Projectile.rotation.ToRotationVector2();
            Vector2 muzzleScreen = Projectile.Center - Main.screenPosition;
            Vector2 tipScreen = muzzleScreen + direction * toTileLeng;
            float flicker = 1f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 30f);
            float widthScale = BeamWidth / glow.Width;

            spriteBatch.Draw(glow, muzzleScreen, null, CoreThemeColor * (0.85f * widthMul), 0f
                , glow.Size() / 2f, widthScale * 2.4f * flicker, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, muzzleScreen, null, Color.White * (0.6f * widthMul), 0f
                , glow.Size() / 2f, widthScale * 1.1f, SpriteEffects.None, 0f);

            spriteBatch.Draw(glow, tipScreen, null, GlowThemeColor * (0.8f * widthMul), 0f
                , glow.Size() / 2f, widthScale * 1.7f * flicker, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, tipScreen, null, CoreThemeColor * (0.7f * widthMul), Main.GlobalTimeWrappedHourly * 2.4f
                , star.Size() / 2f, (BeamWidth / star.Width) * 1.3f, SpriteEffects.None, 0f);
        }
    }

    /// <summary>基础射线，单束热能柱</summary>
    internal class CommandersRay : BaseCommandersRay
    {
        protected override float BeamWidth => 24f;
        protected override float BeamMode => 0f;
        protected override Color CoreThemeColor => new(255, 96, 48);
        protected override Color GlowThemeColor => new(255, 196, 120);

        protected override void GetMuzzle(Projectile gunProj, out Vector2 origin, out Vector2 direction) {
            direction = gunProj.rotation.ToRotationVector2();
            origin = gunProj.ModProjectile is BaseHeldGun gun ? gun.ShootPos : gunProj.Center;
        }
    }

    /// <summary>EX 切割射线，五束环绕自旋并收束成锥</summary>
    internal class CommandersRayEX : BaseCommandersRay
    {
        private const int PortCount = 5;
        private const float PortRadius = 22f;
        private const float FocalDistance = 320f;
        private const float SpinSpeed = 0.06f;//弧度/帧

        protected override float BeamWidth => 21f;
        //五束环绕，光晕略小于基础版
        protected override float VisualWidthMul => 2.6f;
        protected override float BeamMode => 1f;
        protected override Color CoreThemeColor => new(255, 32, 18);
        protected override Color GlowThemeColor => new(255, 120, 60);

        protected override void SetExtraDefaults() {
            //五束各吃独立免疫帧
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        protected override void GetMuzzle(Projectile gunProj, out Vector2 origin, out Vector2 direction) {
            Vector2 muzzle = gunProj.ModProjectile is BaseHeldGun gun ? gun.ShootPos : gunProj.Center;
            float aimRot = gunProj.rotation;
            int portIndex = (int)Projectile.ai[1];
            float ringAngle = aimRot + Age * SpinSpeed + portIndex * (MathHelper.TwoPi / PortCount);

            origin = muzzle + ringAngle.ToRotationVector2() * PortRadius;
            Vector2 focal = muzzle + aimRot.ToRotationVector2() * FocalDistance;
            direction = (focal - origin).SafeNormalize(aimRot.ToRotationVector2());
        }
    }
}
