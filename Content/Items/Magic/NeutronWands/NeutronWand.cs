using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.NeutronWands
{
    /// <summary>
    /// 星墓之光：左键在战场上播下脉冲星，右键磁制动把它们压到崩裂。
    /// 星本身是持续输出，星震是收割，两键互为资源。
    /// </summary>
    internal class NeutronWand : ModItem
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
            Item.useTime = Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(15, 3, 5, 0);
            Item.rare = ItemRarityID.Red;
            Item.shoot = ModContent.ProjectileType<NeutronPulsar>();
            Item.shootSpeed = 15;
            Item.mana = 16;
            Item.crit = 6;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<NeutronStarIngot>(11)
                .AddEndgameStation()
                .DisableDecraft()
                .Register();
        }

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

        /// <summary>同时在场的脉冲星上限，再播就把最老的一颗震掉</summary>
        internal const int MaxPulsars = 3;
        private const int QuakeChargeFrames = 55;
        /// <summary>播星伤害系数，三颗齐扫已是可观持续输出</summary>
        private const float PulsarDamageScale = 0.62f;

        /// <summary>磁制动蓄力 0~1</summary>
        private float quakeCharge;
        private bool rightHeldLast;
        private int muzzleFlash;
        private float muzzleAngle;

        //蓄力未散尽或枪口余光未灭时保持存活
        public override bool StayAlive() => quakeCharge > 0f || muzzleFlash > 0;

        public override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            HandIdleDistanceX = 52;
            HandIdleDistanceY = -20;
            HandFireDistanceX = 52;
            GunPressure = 0.32f;
            ControlForce = 0.08f;
            RecoilRetroForceMagnitude = 9f;
            RecoilOffsetRecoverValue = 0.62f;
            AlwaysAimPose = true;
            Onehanded = true;
            ArmRotSengsBackNoFireOffset = -20;
            MuzzleForwardOffset = 20;
        }

        public override void NetHeldSend(BinaryWriter writer) => writer.Write(quakeCharge);

        public override void NetHeldReceive(BinaryReader reader) => quakeCharge = reader.ReadSingle();

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 11);
            UpdateHeldPose(CanFire);

            if (CanFire) {
                HoldManaRegenDelay();
            }
            if (muzzleFlash > 0) {
                muzzleFlash--;
            }

            bool rightHeld = WantsFireRight;
            if (rightHeld) {
                ChargeBrake();
            }
            else {
                if (rightHeldLast && quakeCharge > 0.06f) {
                    ReleaseQuake();
                }
                quakeCharge = 0f;

                if (WantsFireLeft && FireCooldown <= 0 && PayMana()) {
                    SeedPulsar();
                    SetFireCooldown();
                }
            }
            rightHeldLast = rightHeld;

            Time++;
        }

        /// <summary>磁制动：把在场脉冲星的自旋拖慢，动能转成壳层应力</summary>
        private void ChargeBrake() {
            //场上无星可制动就攒不起来，让两键的依赖关系自解释
            if (!HasBrakeableStar()) {
                quakeCharge = 0f;
                return;
            }

            if (quakeCharge <= 0f) {
                SoundEngine.PlaySound(SoundID.Item77 with { Pitch = -0.35f }, Projectile.Center);
            }
            quakeCharge = MathHelper.Min(quakeCharge + 1f / QuakeChargeFrames, 1f);

            foreach (NeutronPulsar pulsar in OwnedPulsars()) {
                pulsar.DriveQuake(quakeCharge);
            }

            if (!VaultUtils.isServer) {
                DrawBrakeTether();
            }
        }

        private void ReleaseQuake() {
            SnapToAimPose();
            SoundEngine.PlaySound(SoundID.NPCDeath56 with { Pitch = -0.25f + quakeCharge * 0.4f }, Projectile.Center);

            foreach (NeutronPulsar pulsar in OwnedPulsars()) {
                pulsar.TriggerQuake(quakeCharge);
            }
        }

        private bool HasBrakeableStar() {
            foreach (NeutronPulsar pulsar in OwnedPulsars()) {
                if (pulsar.CanBrake) {
                    return true;
                }
            }
            return false;
        }

        private void SeedPulsar() {
            SnapToAimPose();
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.55f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item88 with { Pitch = -0.35f }, Projectile.Center);
            CreateFireLight();

            muzzleFlash = 6;
            muzzleAngle = UnitToMouseV.ToRotation();
            SpawnMuzzleParticles();

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Owner.CWR().GetScreenShake(2.2f);

            //超编时把最老的一颗直接震掉，不让播星变成空放
            EnforcePulsarCap();

            //制动是几何衰减，解出初速让它正好停在光标上
            float dist = Vector2.Distance(InMousePos, ShootPos);
            float speed = MathHelper.Clamp(dist / NeutronPulsar.TravelFactor, 8f, 64f);

            Projectile.NewProjectile(Source, ShootPos, UnitToMouseV * speed
                , ModContent.ProjectileType<NeutronPulsar>()
                , (int)(WeaponDamage * PulsarDamageScale), WeaponKnockback, Owner.whoAmI
                , Main.rand.NextFloat(MathHelper.TwoPi));
        }

        /// <summary>超编时让最老的一颗以星震谢幕，播星不会白放</summary>
        private void EnforcePulsarCap() {
            NeutronPulsar oldest = null;
            int alive = 0;
            foreach (NeutronPulsar pulsar in OwnedPulsars()) {
                if (!pulsar.CanBrake) {
                    continue;//已在超频或已判出局的不占编制
                }
                alive++;
                if (oldest == null || pulsar.Projectile.timeLeft < oldest.Projectile.timeLeft) {
                    oldest = pulsar;
                }
            }
            if (alive >= MaxPulsars && oldest != null) {
                oldest.TriggerQuake(0.35f, forced: true);
            }
        }

        private System.Collections.Generic.IEnumerable<NeutronPulsar> OwnedPulsars() {
            int type = ModContent.ProjectileType<NeutronPulsar>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && proj.owner == Projectile.owner
                    && proj.ModProjectile is NeutronPulsar pulsar) {
                    yield return pulsar;
                }
            }
        }

        private void SpawnMuzzleParticles() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dir = UnitToMouseV;
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_Spark>(ShootPos
                    , dir.RotatedByRandom(0.35f) * Main.rand.NextFloat(3f, 11f)
                    , Color.Lerp(NeutronPulsar.ParticleViolet, NeutronPulsar.ParticleHot, Main.rand.NextFloat(0.6f))
                    , Main.rand.NextFloat(0.5f, 1f))
                    ?.Configure(false, Main.rand.Next(8, 15));
            }
        }

        /// <summary>制动期杖口聚拢的物质流，蓄力读数长在结构上而非光球</summary>
        private void DrawBrakeTether() {
            if (Projectile.timeLeft % 2 != 0) {
                return;
            }
            PRTLoader.NewParticle<PRT_GravityVortex>(ShootPos, Vector2.Zero
                , Color.Lerp(NeutronPulsar.ParticleBlue, NeutronPulsar.ParticleHot, quakeCharge)
                , 0.25f + quakeCharge * 0.35f)
                ?.Configure(Main.rand.NextFloat(MathHelper.TwoPi), 34f + quakeCharge * 46f, Main.rand.Next(16, 26));
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, drawPos, TextureValue.GetRectangle(Projectile.frame, 12), lightColor
                , Projectile.rotation + MathHelper.PiOver4 * DirSign, VaultUtils.GetOrig(TextureValue, 12), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);

            DrawMuzzleFlash(drawPos);
        }

        /// <summary>枪口闪沿射向拉长，黑底 Masking 只能加色</summary>
        private void DrawMuzzleFlash(Vector2 drawPos) {
            if (muzzleFlash <= 0) {
                return;
            }
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star == null) {
                return;
            }

            float life = muzzleFlash / 6f;
            Vector2 pos = drawPos + muzzleAngle.ToRotationVector2() * MuzzleForwardOffset;
            Vector2 scale = new(0.55f * life, 0.13f * life);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            sb.Draw(star, pos, null, NeutronPulsar.ParticleBlue * life, muzzleAngle, star.Size() * 0.5f
                , scale, SpriteEffects.None, 0f);
            sb.Draw(star, pos, null, NeutronPulsar.ParticleHot * life, muzzleAngle, star.Size() * 0.5f
                , scale * 0.45f, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
