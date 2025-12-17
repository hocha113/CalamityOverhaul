using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.MeleeModify;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBansheeHook : CWRItemOverride
    {
        internal static int index;
        public override void SetStaticDefaults() {
            ItemID.Sets.Spears[TargetID] = true;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        }

        public override void SetDefaults(Item item) {
            item.damage = 580;
            item.SetKnifeHeld<BansheeHookHeld>();
        }

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<BansheeHookHeldAlt>()] == 0;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(item, player, source, position, velocity, type, damage, knockback);
        }

        public static void PlaySouldSound(Player player) {
            SoundStyle sound1 = "CalamityMod/Sounds/Custom/MeatySlash".GetSound();
            SoundStyle sound2 = "CalamityMod/Sounds/Custom/AbilitySounds/BloodflareRangerActivation".GetSound();
            sound1.Volume = 0.6f;
            sound2.Volume = 0.6f;
            SoundEngine.PlaySound(sound1, player.Center);
            SoundEngine.PlaySound(sound2, player.Center);
        }

        public static bool ShootFunc(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                type = ModContent.ProjectileType<BansheeHookHeldAlt>();
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
                PlaySouldSound(player);
                item.CWR().MeleeCharge = 0;
                return false;
            }
            if (++index > 3) {
                index = 0;
            }

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, index);
            return false;
        }
    }

    internal class BansheeHookHeld : BaseKnife
    {
        public override int TargetID => CWRID.Item_BansheeHook;
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "WeaverGrievances_Bar";
        public override string GlowTexturePath => CWRConstant.Cay_Proj_Melee + "Spears/BansheeHookAltGlow";
        public override Texture2D TextureValue => CWRUtils.GetT2DValue(CWRConstant.Cay_Proj_Melee + "Spears/BansheeHookAlt");
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 64;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 20;
            drawTrailCount = 16;
            Length = 52;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            SwingDrawRotingOffset = MathHelper.PiOver2;
            autoSetShoot = true;
        }

        public override bool PreSwingAI() {
            if (Projectile.ai[0] == 3) {
                if (Time == 0) {
                    OtherMeleeSize = 1.4f;
                }

                SwingData.baseSwingSpeed = 10;
                SwingAIType = SwingAITypeEnum.Down;

                if (Time < maxSwingTime / 3) {
                    OtherMeleeSize += 0.025f / SwingMultiplication;
                }
                else {
                    OtherMeleeSize -= 0.005f / SwingMultiplication;
                }
                return true;
            }

            if (Projectile.ai[0] == 0) {
                StabBehavior(initialLength: 60, scaleFactorDenominator: 400f, minLength: 20, maxLength: 120, canDrawSlashTrail: true);
                return false;
            }

            return true;
        }

        public override void Shoot() {
            if (Projectile.numHits > 0) {
                return;
            }
            if (Projectile.ai[0] == 3) {
                return;
            }
            if (Projectile.ai[0] == 1 || Projectile.ai[0] == 2) {
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity.RotatedBy((-1 + i) * 0.06f)
                        , CWRID.Proj_BansheeHookScythe, Projectile.damage
                        , Projectile.knockBack * 0.85f, Projectile.owner);
                }
                return;
            }
            Projectile.NewProjectile(Source, ShootSpanPos, AbsolutelyShootVelocity
               , ModContent.ProjectileType<GiantBansheeScythe>(), (int)(Projectile.damage * 2.25f)
               , Projectile.knockBack * 0.85f, Projectile.owner);
        }

        public override void MeleeEffect() {
            Player player = Main.player[Projectile.owner];
            Vector2 vector = player.RotatedRelativePoint(player.MountedCenter);
            float num = player.itemAnimation / (float)player.itemAnimationMax;
            float num2 = (1f - num) * (MathF.PI * 2f);
            float num3 = Projectile.velocity.ToRotation();
            float num4 = Projectile.velocity.Length();
            Vector2 spinningPoint = Vector2.UnitX.RotatedBy(MathF.PI + num2) * new Vector2(num4, Projectile.ai[0]);
            Vector2 destination = vector + spinningPoint.RotatedBy(num3) + new Vector2(num4 + ShootSpeed + 40f, 0f).RotatedBy(num3);
            Vector2 directionToDestination = player.To(destination).UnitVector();
            Vector2 velocityNormalized = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            float num5 = 2f;
            float randomDustScaleMin = 0.6f;
            for (int i = 0; i < num5; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.Center, 14, 14, DustID.RedTorch, 0f, 0f, 110);
                dust.velocity = player.To(dust.position).UnitVector() * 2f;
                dust.position = Projectile.Center + velocityNormalized.RotatedBy(num2 * 2f + i / num5 * (MathF.PI * 2f)) * 10f;
                dust.scale = 1f + Main.rand.NextFloat(randomDustScaleMin);
                dust.velocity += velocityNormalized * 3f;
                dust.noGravity = true;
            }
            if (Main.rand.NextBool(3)) {
                Dust dust2 = Dust.NewDustDirect(Projectile.Center, 20, 20, DustID.RedTorch, 0f, 0f, 110);
                dust2.velocity = player.To(dust2.position).UnitVector() * 2f;
                dust2.position = Projectile.Center + directionToDestination * -110f;
                dust2.scale = 0.45f + Main.rand.NextFloat(0.4f);
                dust2.fadeIn = 0.7f + Main.rand.NextFloat(0.4f);
                dust2.noGravity = true;
                dust2.noLight = true;
            }
        }

        private void SpanSoulSearchScythe(NPC target) {
            Vector2 randOffsetTargetInPos = VaultUtils.RandVr(target.width * 2);
            Vector2 randPos = target.Center + randOffsetTargetInPos + VaultUtils.RandVr(1162, 1282);
            Vector2 shootVer = randPos.To(target.Center + randOffsetTargetInPos).UnitVector() * 16;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), randPos, shootVer
                    , ModContent.ProjectileType<SoulSearchScythe>(), Projectile.damage, 10f
                    , Projectile.owner, 0f, 0.85f + Main.rand.NextFloat() * 1.15f);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SpanSoulSearchScythe(target);
        }
    }

    internal class SoulSearchScythe : ModProjectile
    {
        public override bool IsLoadingEnabled(Mod mod) => CWRRef.Has;
        public override string Texture => CWRConstant.Cay_Proj_Melee + "BansheeHookScythe";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 28;
        }

        public override void SetDefaults() {
            Projectile.width = 38;
            Projectile.height = 38;
            Projectile.alpha = 100;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 290;
            Projectile.extraUpdates = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, (255 - Projectile.alpha) * 0.6f / 255f, 0f, 0f);
            Projectile.rotation += MathHelper.ToRadians(35);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.velocity *= 0.95f;
            Projectile.damage -= 25;
        }

        public override Color? GetAlpha(Color lightColor) {
            byte b = (byte)(Projectile.timeLeft * 3);
            byte alpha = (byte)(100f * (b / 255f));
            return new Color(lightColor.R, lightColor.G, lightColor.B, alpha);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawOrigin = texture.Size() / 2;
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = Projectile.GetAlpha(Color.Lerp(WeaverBeam.sloudColor1, WeaverBeam.sloudColor2, 1f / Projectile.oldPos.Length * k) * (1f - 1f / Projectile.oldPos.Length * k));
                if (Projectile.ai[1] > 160) {
                    color.A = 0;
                }
                float slp = 0.8f + 0.2f * (Projectile.oldPos.Length - k) / Projectile.oldPos.Length;
                Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation + k * 0.21f, drawOrigin, Projectile.scale * slp, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    //yes yes yes yes 我知道你们在想什么，这是个失败的设计，是一个耻辱
    internal class BansheeHookHeldAlt : BaseHeldProj
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "BansheeHook";
        public override bool IsLoadingEnabled(Mod mod) => CWRRef.Has;
        private int drawUIalp = 0;
        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 90;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.hide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
            Projectile.alpha = 255;
            Projectile.hide = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (Owner == null || Item == null || Item.type != CWRID.Item_BansheeHook) {
                Projectile.Kill();
                return;
            }

            Projectile.localAI[1]++;
            Owner.heldProj = Projectile.whoAmI;

            if (Projectile.IsOwnedByLocalPlayer()) {
                int SafeGravDir = Math.Sign(Owner.gravDir);
                float rot = (MathHelper.PiOver2 * SafeGravDir - Owner.Center.To(Projectile.Center).ToRotation()) * Owner.direction * SafeGravDir * SafeGravDir;
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot * -Owner.direction * SafeGravDir);
                Owner.direction = Owner.Center.To(Projectile.Center).X > 0 ? 1 : -1;
                Projectile.spriteDirection = Owner.direction;
                if (PlayerInput.Triggers.Current.MouseRight) Projectile.timeLeft = 2;
            }

            if (Projectile.ai[2] == 0) {
                Projectile.Center = Owner.GetPlayerStabilityCenter();
                Projectile.rotation += MathHelper.ToRadians(25);

                drawUIalp = Math.Min(drawUIalp + 5, 255);

                if (Projectile.IsOwnedByLocalPlayer()) {
                    Item.CWR().MeleeCharge += 8.333f;
                    if (Projectile.localAI[1] % 10 == 0) {
                        for (int i = 0; i < 7; i++) {
                            Vector2 vr = (MathHelper.TwoPi / 7 * i).ToRotationVector2() * 10;
                            Projectile.NewProjectile(Owner.FromObjectGetParent(), Owner.Center, vr, ModContent.ProjectileType<SpiritFlame>(), Projectile.damage / 3, 0, Owner.whoAmI, 1);
                        }
                    }
                }
                if (Projectile.localAI[1] > 60) {
                    Item.CWR().MeleeCharge = 500;
                    Projectile.ai[2] = 1;
                    Projectile.localAI[1] = 0;
                }
            }

            if (Projectile.ai[2] == 1 && Projectile.IsOwnedByLocalPlayer()) {
                Vector2 toMous = Owner.GetPlayerStabilityCenter().To(Main.MouseWorld).UnitVector();
                Vector2 topos = toMous * 56 + Owner.GetPlayerStabilityCenter();
                Projectile.Center = Vector2.Lerp(topos, Projectile.Center, 0.01f);
                Projectile.rotation = toMous.ToRotation();
                Projectile.localAI[2]++;

                Item.CWR().MeleeCharge--;

                if (Projectile.localAI[1] > 10) {
                    if (Projectile.localAI[1] % 24 == 0) {
                        SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Pitch = 0.35f, Volume = 0.7f }, Projectile.Center);
                        int damages = (int)(base.Owner.GetWeaponDamage(base.Item) * 0.25f);
                        for (int i = 0; i < 3; i++) {
                            Vector2 spanPos = InMousePos + (MathHelper.TwoPi / 3f * i).ToRotationVector2() * 160;
                            Projectile.NewProjectile(Owner.FromObjectGetParent(), spanPos, spanPos.To(Main.MouseWorld).UnitVector() * 15f
                                , ModContent.ProjectileType<AbominateHookScythe>(), damages, 0, Owner.whoAmI);
                        }
                    }
                    if (Projectile.localAI[1] % 15 == 0) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 pos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 45 * Projectile.scale + VaultUtils.RandVrInAngleRange(0, 360, Main.rand.Next(2, 16));
                            Projectile.NewProjectile(Owner.FromObjectGetParent(), pos, Vector2.Zero, ModContent.ProjectileType<SpiritFlame>(), Projectile.damage / 2, 0, Owner.whoAmI);
                        }
                    }
                }

                if (Item.CWR().MeleeCharge <= 0) {
                    Projectile.ai[2] = 0;
                    Projectile.localAI[1] = 0;
                    Projectile.netUpdate = true;
                    Item.CWR().MeleeCharge = 0;
                    RBansheeHook.PlaySouldSound(Owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture2D = TextureAssets.Projectile[Type].Value;
            Texture2D glow = SwingSystem.glowTextures[ModContent.ProjectileType<BansheeHookHeld>()].Value;

            SpriteEffects spriteEffects = SpriteEffects.None;
            float drawRot = Projectile.rotation + MathHelper.PiOver4;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            drawPos += Owner.CWR().SpecialDrawPositionOffset;

            if (Projectile.spriteDirection == -1) {
                spriteEffects = SpriteEffects.FlipVertically;
                drawRot = Projectile.rotation - MathHelper.PiOver4;
            }

            Main.EntitySpriteDraw(texture2D, drawPos, null, lightColor,
                    drawRot, texture2D.GetOrig(), Projectile.scale, spriteEffects);

            Main.EntitySpriteDraw(glow, drawPos, null, lightColor,
                    drawRot + (Projectile.spriteDirection > 0 ? MathHelper.PiOver2 : -MathHelper.PiOver2)
                    , glow.GetOrig(), Projectile.scale, spriteEffects);

            if (Projectile.ai[2] == 0) {
                Texture2D value = CWRAsset.SemiCircularSmear.Value;
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                Main.EntitySpriteDraw(color: WeaverBeam.sloudColor2 * 0.9f
                    , origin: value.Size() * 0.5f, texture: value, position: drawPos
                    , sourceRectangle: null, rotation: Projectile.rotation + MathHelper.PiOver2
                    , scale: Projectile.scale, effects: SpriteEffects.None);
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            RTerrorBlade.DrawFrightEnergyChargeBar(
                Main.player[Projectile.owner], drawUIalp / 255f,
                Item.CWR().MeleeCharge / 500f);

            if (Projectile.localAI[2] != 0) {
                Texture2D mainValue = CWRAsset.StarTexture_White.Value;
                Vector2 pos = drawPos + UnitToMouseV * 40;
                int Time = (int)Projectile.localAI[2];
                int slp = Time * 5;
                if (slp > 255) { slp = 255; }

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp
                    , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                for (int i = 0; i < 5; i++) {
                    Main.spriteBatch.Draw(mainValue, pos, null, Color.Red, MathHelper.ToRadians(Time * 5 + i * 17), mainValue.GetOrig(), slp / 1755f, SpriteEffects.None, 0);
                }
                for (int i = 0; i < 5; i++) {
                    Main.spriteBatch.Draw(mainValue, pos, null, Color.White, MathHelper.ToRadians(Time * 6 + i * 17), mainValue.GetOrig(), slp / 2055f, SpriteEffects.None, 0);
                }
                for (int i = 0; i < 5; i++) {
                    Main.spriteBatch.Draw(mainValue, pos, null, Color.Gold, MathHelper.ToRadians(Time * 9 + i * 17), mainValue.GetOrig(), slp / 2355f, SpriteEffects.None, 0);
                }
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
            return false;
        }

        public override void PostDraw(Color lightColor) { }
    }
}
