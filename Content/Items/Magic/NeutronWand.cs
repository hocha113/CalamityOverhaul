using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class NeutronWand : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Magic + "NeutronWand";
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 12));
        }
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 355;
            Item.DamageType = DamageClass.Magic;
            Item.useTime = Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(15, 3, 5, 0);
            Item.rare = ItemRarityID.Red;
            Item.shoot = ModContent.ProjectileType<NeutronMagchStar>();
            Item.shootSpeed = 15;
            Item.mana = 15;
            Item.crit = 6;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_NeutronWand;
        }

        //右键：蓄力中子湮灭阵列
        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<NeutronWandHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<NeutronWandHeld>(player, source);
    }

    internal class NeutronWandHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Magic + "NeutronWand";
        public override int TargetID => ModContent.ItemType<NeutronWand>();
        public override bool CanRightClick => true;
        /// <summary>右键蓄力 0~1，满后倾泻湮灭柱</summary>
        private float colers;
        /// <summary>右键按下边沿</summary>
        private bool colers2;
        /// <summary>湮灭柱落点锚，缓跟光标</summary>
        private Vector2 firePos;
        private bool rightHolding;
        //蓄力未散尽时保持存活，能量环衰减
        public override bool StayAlive() => colers > 0;
        public override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            HandIdleDistanceX = 52;
            HandIdleDistanceY = -20;
            HandFireDistanceX = 52;
            GunPressure = 0;
            ControlForce = 0;
            AlwaysAimPose = true;
            Onehanded = true;
            ArmRotSengsBackNoFireOffset = -20;
            MuzzleForwardOffset = 20;
        }

        public override void NetHeldSend(BinaryWriter writer) {
            writer.Write(colers);
            writer.WriteVector2(firePos);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            colers = reader.ReadSingle();
            firePos = reader.ReadVector2();
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 11);
            rightHolding = WantsFireRight;
            UpdateHeldPose(CanFire);

            if (CanFire) {
                HoldManaRegenDelay();
            }

            if (rightHolding) {
                //右键按下瞬间锚定落点并蓄力
                if (!colers2) {
                    colers2 = true;
                    if (colers <= 0) {
                        firePos = InMousePos;
                    }
                    SoundEngine.PlaySound(SoundID.Item77, Projectile.Center);
                }
                if (colers < 1f) {
                    colers += 0.01f;
                }
                else if (FireCooldown <= 0 && PayMana()) {
                    FireRight();
                    SetFireCooldown();
                }
            }
            else {
                if (colers > 0) {
                    colers -= 0.015f;
                }
                fireIndex = 0;
                colers2 = false;

                if (WantsFireLeft && FireCooldown <= 0 && PayMana()) {
                    FireLeft();
                    SetFireCooldown();
                }
            }

            firePos = Vector2.Lerp(firePos, InMousePos, 0.1f);
            Time++;
        }

        private void FireLeft() {
            SnapToAimPose();
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.6f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item88 with { Pitch = -0.6f }, Projectile.Center);
            CreateFireLight();

            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 4; i++) {
                    Projectile.NewProjectile(Source, ShootPos, ShootVelocity * (0.6f + i * 0.1f)
                    , ModContent.ProjectileType<NeutronMagchStar>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                }
            }
        }

        private void FireRight() {
            SnapToAimPose();
            SoundEngine.PlaySound(SoundID.NPCDeath56 with { Pitch = -0.1f + fireIndex * 0.15f }, Projectile.Center);

            if (Projectile.IsOwnedByLocalPlayer()) {
                int newdamage = (int)(WeaponDamage * (1 + fireIndex * 0.15f));
                for (int i = 0; i < 3; i++) {
                    Vector2 shootPos = firePos;
                    shootPos.X += (i - 1) * fireIndex * 30;
                    shootPos.Y += Main.rand.Next(-113, 33);
                    Projectile.NewProjectile(Source, shootPos, new Vector2(0, 1)
                    , ModContent.ProjectileType<NeutronWandExplode>(), newdamage, WeaponKnockback, Owner.whoAmI, 0);
                }
            }

            if (++fireIndex > 3) {
                fireIndex = 0;
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, drawPos, TextureValue.GetRectangle(Projectile.frame, 12), lightColor
                , Projectile.rotation + MathHelper.PiOver4 * DirSign, VaultUtils.GetOrig(TextureValue, 12), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);

            if (colers > 0) {
                Vector2 origPos = firePos - Main.screenPosition;
                Vector2 drawVr = new Vector2(0, 1);
                drawMatric("NormalMatrix", origPos, new Vector2(0.25f, 0.8f) * colers, Main.GameUpdateCount / 20f, 1f, fireIndex == 3);
                drawMatric("NormalMatrix", origPos + drawVr * 133 * colers * colers, new Vector2(0.15f, 0.6f) * colers, Main.GameUpdateCount / 15f, 0.9f, fireIndex == 1);
                drawMatric("NormalMatrix", origPos + drawVr * 266 * colers * colers, new Vector2(0.15f, 0.5f) * colers, Main.GameUpdateCount / 5f, 0.8f, fireIndex == 2);
                drawMatric("NormalMatrix", origPos + drawVr * -133 * colers * colers, new Vector2(0.15f, 0.6f) * colers, Main.GameUpdateCount / 15f, 0.9f, fireIndex == 1);
                drawMatric("NormalMatrix", origPos + drawVr * -266 * colers * colers, new Vector2(0.15f, 0.5f) * colers, Main.GameUpdateCount / 5f, 0.8f, fireIndex == 2);
            }
        }

        private void drawMatric(string texkey, Vector2 drawpos, Vector2 size, float rotation, float uOpacity, bool set) {
            Texture2D texRing = CWRAsset.NormalMatrix.Value;
            Effect effect = EffectLoader.NeutronRing.Value;
            effect.Parameters["uTime"].SetValue(rotation);
            effect.Parameters["cosine"].SetValue((float)Math.Cos(rotation));
            effect.Parameters["uColor"].SetValue(Color.White.ToVector3());
            effect.Parameters["uOpacity"].SetValue(uOpacity);
            effect.Parameters["set"].SetValue(set && rightHolding);
            effect.CurrentTechnique.Passes[0].Apply();
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(default, BlendState.Additive, Main.DefaultSamplerState, default
                , RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            var rec = new Rectangle(-texRing.Width / 2, -texRing.Height / 2, texRing.Width * 2, texRing.Height * 2);
            Main.spriteBatch.Draw(texRing, drawpos, rec, Color.White, MathHelper.PiOver2, rec.Size() / 2, size, SpriteEffects.None, 0);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }

    internal class NeutronWandExplode : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = 100;
            Projectile.height = 2000;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 3;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.ArmorPenetration = 80;
        }

        public bool CanDrawCustom() => false;

        private void SpanStar(Vector2 offset) {
            for (int i = 0; i < 4; i++) {
                float rot1 = MathHelper.PiOver2 * i;
                Vector2 vr = rot1.ToRotationVector2();
                for (int j = 0; j < 13; j++) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + offset, vr * (0.1f + j * 0.1f), Color.CadetBlue, 0.8f).Configure(false, 20);
                }
            }
        }

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                for (int j = 0; j < 122; j++) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + new Vector2(Main.rand.Next(-16, 16), -700), Projectile.velocity.UnitVector() * Main.rand.Next(66, 166), Color.BlueViolet, Main.rand.NextFloat(1.2f, 1.3f)).Configure(false, 17);
                }
            }
            if (Projectile.ai[2] % 5 == 0) {
                SpanStar(new Vector2(0, Projectile.ai[2] * 80 - 500));
            }
            Projectile.ai[0] += 0.25f;
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.25f;
                Projectile.ai[1] += 0.2f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);
            Projectile.ai[2]++;
            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(ModContent.BuffType<VoidErosion>(), 1200);

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 60f,
                screenHeight: System.Math.Min(5000f * Projectile.ai[1], Main.screenHeight * 2f),
                intensity: Projectile.ai[1] * 0.8f,
                progress: Projectile.ai[1],
                rotation: Projectile.velocity.ToRotation() + MathHelper.PiOver2,
                technique: "RelativisticJet"
            );
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }

    internal class NeutronMagchStar : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "MagicStar2";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.MaxUpdates = 4;
            Projectile.penetrate = 13 * Projectile.MaxUpdates;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 3;
            Projectile.tileCollide = false;
            Projectile.alpha = 0;
            Projectile.timeLeft = 300;
            Projectile.ArmorPenetration = 80;
        }

        public override void AI() {
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 25;
            }
            Lighting.AddLight(Projectile.Center, Color.Blue.ToVector3());
            Projectile.rotation += Projectile.velocity.X * 0.1f;
            PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + VaultUtils.RandVr(8), Projectile.velocity.UnitVector() * Main.rand.Next(6, 16), Color.BlueViolet, Main.rand.NextFloat(0.2f, 0.3f)).Configure(false, 7);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<VoidErosion>(), 1200);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 6; i++) {
                Vector2 vr = VaultUtils.RandVr(6);
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueTorch, vr.X, vr.Y);
                Main.dust[Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.FireworkFountain_Blue, vr.X, vr.Y)].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float fade = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                float oldRotation = Projectile.oldRot[i];
                SpriteEffects effects = Projectile.oldSpriteDirection[i] == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Color color = Color.Lerp(Color.BlueViolet, Color.White, fade * 0.5f) * fade * 0.8f;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(texture, drawPos, null, color, oldRotation, origin, Projectile.scale, effects);
            }
            return false;
        }
    }


}
