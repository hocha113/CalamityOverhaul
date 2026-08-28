using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class FocusingGrimoire : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Magic + "FocusingGrimoire";
        [VaultLoaden(CWRConstant.Item_Magic + "FocusingGrimoireGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 40;
            Item.height = 44;
            Item.damage = 52;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.mana = 8;
            Item.shoot = ModContent.ProjectileType<PowerCoil>();
            Item.shootSpeed = 8;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(0, 1, 60, 10);
            Item.CWR().BrutalWorldItem = true;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<FocusingGrimoireHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<FocusingGrimoireHeld>(player, source);

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    internal class FocusingGrimoireHeld : BaseHeldGun, IOverlayDrawable
    {
        public override string Texture => CWRConstant.Item_Magic + "FocusingGrimoire";
        public override Asset<Texture2D> GlowAsset => FocusingGrimoire.Glow;
        public override int TargetID => ModContent.ItemType<FocusingGrimoire>();
        public override bool CanRightClick => true;
        //左右键各自节奏与蓝耗
        private const int CoilUseTime = 18;
        private const int CoilMana = 8;
        private const int LaserUseTime = 5;
        private const int LaserMana = 2;
        public override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            AlwaysAimPose = true;
        }

        public override void AI() {
            UpdateHeldPose(CanFire);

            if (CanFire) {
                HoldManaRegenDelay();
            }

            if (FireCooldown <= 0) {
                if (WantsFireLeft && PayMana(CoilMana)) {
                    FireCoil();
                    FireCooldown += MathF.Max(CoilUseTime / AttackSpeed, 1f);
                }
                else if (WantsFireRight && PayMana(LaserMana)) {
                    FireLaser();
                    FireCooldown += MathF.Max(LaserUseTime / AttackSpeed, 1f);
                }
            }
            Time++;
        }

        private void FireCoil() {
            SnapToAimPose();
            SoundEngine.PlaySound(SoundID.Item84, Projectile.Center);
            CreateFireLight();
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , ModContent.ProjectileType<PowerCoil>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        private void FireLaser() {
            SnapToAimPose();
            SoundEngine.PlaySound(SoundID.Item12, Projectile.Center);
            CreateFireLight();
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , ModContent.ProjectileType<FocusingDeathRay>(), WeaponDamage / 2, WeaponKnockback, Owner.whoAmI);
            }
        }

        //书本走 IOverlayDrawable 遮挡层，盖住自身特效
        public override bool PreDraw(ref Color lightColor) => false;

        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (!OnHandheldDisplayBool) {
                return;
            }
            Color lightColor = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            GunDraw(Projectile.Center - Main.screenPosition + SpecialDrawPositionOffset, ref lightColor);
        }
    }

    /// <summary>左键热能环，命中挂 <see cref="FocusMark"/> 供右键连携</summary>
    internal class PowerCoil : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "PowerCoil";

        private static readonly Color ThemeColor = new(255, 110, 35);
        private static readonly Color ThemeGlow = new(255, 220, 120);

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 6;
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.timeLeft = 460;
            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, ThemeColor.ToVector3() * 0.7f * Projectile.scale);

            //短直飞后再追踪脉动
            if (Projectile.timeLeft > 400) {
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_TwinsSpark>(Projectile.Center, -Projectile.velocity * 0.05f, Color.White, 0.6f)?.Configure(10, 1);
                }
                return;
            }

            Projectile.rotation += Math.Sign(Projectile.velocity.X) * 0.22f;
            NPC target = Projectile.Center.FindClosestNPC(600);
            if (target != null) {
                Projectile.SmoothHomingBehavior(target.Center, 1, 0.1f);
            }
            Projectile.scale = 1 + Math.Abs(MathF.Sin(Projectile.ai[0] * 0.04f)) * 0.2f;
            Projectile.ai[0]++;

            if (VaultUtils.isServer) {
                return;
            }

            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_LavaFire>(Projectile.Center + VaultUtils.RandVr(6f), Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(1f, 1f), Color.White, Main.rand.NextFloat(0.6f, 1f) * Projectile.scale)
                    ?.SetLifetime(10, 20);
            }
            if (Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(Projectile.Center, Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(0.6f, 0.6f), Color.White, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(16, 1);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.9f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.9f;
            }
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, VaultUtils.RandVr(2, 5), ThemeColor, Main.rand.NextFloat(0.6f, 1f))
                        ?.Configure(false, Main.rand.Next(8, 14));
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<FocusMark>(), FocusMark.Duration);

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item82 with { Pitch = 0.5f, Volume = 0.6f }, target.Center);
            PRTLoader.NewParticle<PRT_DWave>(target.Center, Vector2.Zero, new Color(255, 225, 150), 0.2f)
                ?.Configure(Vector2.One, 0f, 1.4f, 18);
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f;
                PRTLoader.NewParticle<PRT_TwinsSpark>(target.Center, angle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f), Color.White, Main.rand.NextFloat(0.8f, 1.2f))
                    ?.Configure(16, 1);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f), ThemeGlow, Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(false, Main.rand.Next(12, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = texture.GetRectangle();
            Vector2 drawOrigin = rectangle.Size() / 2;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glow, drawPos, null, (ThemeColor with { A = 0 }) * 0.55f, 0f, glow.Size() / 2f, Projectile.scale * 1.5f, SpriteEffects.None, 0);

            Color tint = Color.Lerp(Color.White, ThemeColor, 0.35f);
            Main.EntitySpriteDraw(texture, drawPos, rectangle, tint, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>右键死亡射线，复用 <see cref="EffectLoader.TwinsDeathRayBeam"/>；命中 <see cref="FocusMark"/> 加伤续标</summary>
    internal class FocusingDeathRay : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private static readonly Color ThemeColor = new(120, 200, 255);
        private static readonly Color ThemeGlow = new(150, 110, 255);
        private const float BoltLength = 84f;
        private const float BoltWidth = 13f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 160;
            Projectile.alpha = 0;
            Projectile.extraUpdates = 3;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, ThemeColor.ToVector3() * 0.55f);

            if (VaultUtils.isServer) {
                return;
            }
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(Projectile.Center, -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.6f, 0.6f), Color.White, Main.rand.NextFloat(0.7f, 1.05f))
                    ?.Configure(12, 0);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.HasBuff<FocusMark>()) {
                modifiers.FinalDamage *= FocusMark.RayDamageMul;
                modifiers.SetCrit();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool marked = target.HasBuff<FocusMark>();
            if (marked) {
                ExtendMark(target);
            }
            if (!VaultUtils.isServer) {
                SpawnImpactBurst(target, marked);
            }
        }

        //AddBuff 取较大值不累加，续标须手改剩余时间
        private static void ExtendMark(NPC target) {
            int buffIndex = target.FindBuffIndex(ModContent.BuffType<FocusMark>());
            if (buffIndex < 0) {
                return;
            }
            target.buffTime[buffIndex] = Math.Min(target.buffTime[buffIndex] + FocusMark.RayExtend, FocusMark.Duration);
        }

        private static void SpawnImpactBurst(NPC target, bool marked) {
            int sparkCount = marked ? 14 : 8;
            for (int i = 0; i < sparkCount; i++) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(target.Center, VaultUtils.RandVr(6, marked ? 18 : 13), Color.White, Main.rand.NextFloat(1f, marked ? 2.2f : 1.7f))
                    ?.Configure(marked ? 24 : 18, 0);
            }
            Color ringColor = marked ? Color.Lerp(ThemeGlow, Color.White, 0.4f) : ThemeGlow;
            PRTLoader.NewParticle<PRT_DWave>(target.Center, Vector2.Zero, ringColor, marked ? 0.18f : 0.1f)
                ?.Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), marked ? 0.75f : 0.45f, marked ? 18 : 12);
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawBolt();
            return false;
        }

        private void DrawBolt() {
            if (VaultUtils.isServer) {
                return;
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition + Projectile.velocity.UnitVector() * 22;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float opacity = 1f - Projectile.alpha / 255f;

            if (EffectLoader.TwinsDeathRayBeam?.Value != null) {
                DrawShaderBolt(drawPos, dir, opacity);
            }
            else {
                DrawFallbackBolt(drawPos, dir, opacity);
            }

            DrawHeadGlow(drawPos, opacity);
        }

        /// <summary>主表现，TwinsDeathRay 模式0，弹头 uv0 向后拖尾</summary>
        private void DrawShaderBolt(Vector2 drawPos, Vector2 dir, float opacity) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Effect shader = EffectLoader.TwinsDeathRayBeam.Value;
            shader.Parameters["uColor"]?.SetValue(ThemeColor.ToVector3());
            shader.Parameters["uSecondaryColor"]?.SetValue(ThemeGlow.ToVector3());
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 5f);
            shader.Parameters["uOpacity"]?.SetValue(opacity);
            shader.Parameters["uIntensity"]?.SetValue(1.1f);
            shader.Parameters["uPulseSpeed"]?.SetValue(22f);
            shader.Parameters["uFlameMode"]?.SetValue(0f);
            shader.Parameters["uExpandProgress"]?.SetValue(1f);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = VaultAsset.placeholder2.Value;
            sb.Draw(quad, drawPos, null, Color.White, (-dir).ToRotation(),
                new Vector2(0, quad.Height / 2f),
                new Vector2(BoltLength / quad.Width, BoltWidth / quad.Height),
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>着色器缺失时的分层光线兜底</summary>
        private void DrawFallbackBolt(Vector2 drawPos, Vector2 dir, float opacity) {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Vector2 lineOrigin = new(0, line.Height / 2f);
            float rot = (-dir).ToRotation();
            float lenScale = BoltLength / line.Width;

            Main.EntitySpriteDraw(line, drawPos, null, (ThemeColor with { A = 0 }) * 0.5f * opacity, rot, lineOrigin,
                new Vector2(lenScale, BoltWidth / line.Height * 3f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, (ThemeGlow with { A = 0 }) * 0.8f * opacity, rot, lineOrigin,
                new Vector2(lenScale, BoltWidth / line.Height * 1.6f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, (Color.White with { A = 0 }) * opacity, rot, lineOrigin,
                new Vector2(lenScale, BoltWidth / line.Height * 0.7f), SpriteEffects.None, 0);
        }

        /// <summary>弹头辉光</summary>
        private void DrawHeadGlow(Vector2 drawPos, float opacity) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 origin = glow.Size() / 2f;
            Main.EntitySpriteDraw(glow, drawPos, null, (ThemeGlow with { A = 0 }) * 0.8f * opacity, 0f, origin, 0.34f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, (Color.White with { A = 0 }) * opacity, 0f, origin, 0.18f, SpriteEffects.None, 0);
        }
    }
}
