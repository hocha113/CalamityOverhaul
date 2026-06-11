using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.GameContent.BaseEntity;
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

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class NeutronGlaive : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 16));
        }

        public override void SetDefaults() {
            Item.height = 154;
            Item.width = 154;
            Item.damage = 855;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 13;
            Item.scale = 1;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.knockBack = 7.5f;
            Item.UseSound = SoundID.Item60;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(13, 53, 75, 0);
            Item.rare = ItemRarityID.Red;
            Item.crit = 8;
            Item.shoot = ModContent.ProjectileType<NeutronGlaiveBeam>();
            Item.shootSpeed = 18f;
            Item.SetKnifeHeld<NeutronGlaiveHeld>(true);
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_NeutronGlaive;
        }

        public override bool CanUseItem(Player player) {
            Item.UseSound = SoundID.Item60;
            if (player.altFunctionUse == 2) {
                Item.UseSound = SoundID.AbigailAttack;
            }
            return player.ownedProjectileCounts[ModContent.ProjectileType<NeutronGlaiveHeldAlt>()] == 0;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<NeutronGlaiveHeldAlt>(), damage, knockback, player.whoAmI);
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }
    }

    internal class NeutronGlaiveHeld : BaseKnife, IWarpDrawable
    {
        public override int TargetID => ModContent.ItemType<NeutronGlaive>();
        public override void SetKnifeProperty() {
            ShootSpeed = 18;
            AnimationMaxFrme = 16;
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 20;
            drawTrailCount = 16;
            Length = 120;
            Projectile.scale = 1.25f;
            SwingData.starArg = 80;
            SwingData.baseSwingSpeed = 5.4f;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 120;
            SwingData.maxClampLength = 130;
            SwingData.ler1_UpSizeSengs = 0.056f;
        }

        public override void Shoot() {
            int type = ModContent.ProjectileType<NeutronGlaiveBeam>();
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , type, Projectile.damage, Projectile.knockBack, Projectile.owner);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                Projectile.NewProjectile(Source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage / 2, 0);
            }

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Projectile.numHits == 0) {
                Projectile.NewProjectile(Source, target.Center, Vector2.Zero
                , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage / 2, 0);
            }

        }

        bool IWarpDrawable.CanDrawCustom() => true;

        bool IWarpDrawable.DontUseBlueshiftEffect() => true;

        void IWarpDrawable.Warp() => WarpDraw();

        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) {
            Texture2D texture = TextureValue;
            Rectangle rect = texture.GetRectangle(Projectile.frame, AnimationMaxFrme);
            Vector2 drawOrigin = rect.Size() / 2;
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Vector2 offsetOwnerPos = safeInSwingUnit.GetNormalVector() * unitOffsetDrawZkMode * Projectile.spriteDirection;
            float drawRoting = Projectile.rotation;
            if (Projectile.spriteDirection == -1) {
                drawRoting += MathHelper.Pi;
            }

            Vector2 drawPosValue = Projectile.Center - RodingToVer(toProjCoreMode, (Projectile.Center - Owner.Center).ToRotation()) + offsetOwnerPos;
            Color color = Color.White;

            Vector2 trueDrawPos = drawPosValue - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY;

            Main.EntitySpriteDraw(texture, trueDrawPos, new Rectangle?(rect)
                , color, drawRoting, drawOrigin, Projectile.scale * MeleeSize, effects, 0);
        }

        public override void DrawSwing(SpriteBatch spriteBatch, Color lightColor) { }
    }

    internal class NeutronGlaiveBeam : ModProjectile, IWarpDrawable, ICWRLoader
    {
        public override string Texture => CWRConstant.Projectile_Melee + "NeutronGlaiveBeam";

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 120;
            Projectile.MaxUpdates = 3;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Color.White.ToVector3() * 0.3f);

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            Projectile.ai[0] += 0.05f;
            if (Projectile.ai[0] > 0.3f) {
                Projectile.ai[0] = 0.3f;
            }
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.15f;
                if (Projectile.localAI[0] > 0.3f) {
                    Projectile.localAI[0] = 0.3f;
                }
                Projectile.ai[1] += 0.2f;
                if (Projectile.ai[1] > 0.3f) {
                    Projectile.ai[1] = 0.3f;
                }
            }
            else {
                Projectile.localAI[0] -= 0.03f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;

            float rot = Main.rand.NextFloat(6.282f);
            for (int i = 0; i < 2; i++) {
                Vector2 dir = rot.ToRotationVector2();
                Vector2 vel = dir.RotatedBy(1.57f) * Main.rand.NextFloat(1.3f, 2.5f) + Projectile.velocity;
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.Next(3, 10)
                    , DustID.Granite, vel, Scale: Main.rand.NextFloat(1.4f, 1.6f));
                dust.noGravity = true;

                rot = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            if (++Projectile.localAI[2] > 2) {
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 3; j++) {
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (0.1f + i * 0.14f), Color.BlueViolet, Main.rand.NextFloat(0.2f, 0.3f)).Configure(false, 17);
                    }
                }
                Projectile.localAI[2] = 0;
            }
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(300, SoundID.Item14 with { Pitch = 0.45f });
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 randpos = VaultUtils.RandVr(64);
            Projectile.Center += randpos;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero
                , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage, 0);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity * -0.6f;
            for (int j = 0; j < 73; j++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + oldVelocity, oldVelocity.RotatedByRandom(0.3f) * -Main.rand.NextFloat(0.3f, 1.1f), Color.LightBlue, Main.rand.NextFloat(0.5f, 0.7f)).Configure(false, 7);
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) => false;
        bool IWarpDrawable.CanDrawCustom() => true;
        void IWarpDrawable.Warp() {
            float scale = System.Math.Max(Projectile.localAI[0], 0.01f);
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 200f * scale,
                screenHeight: 200f * scale,
                intensity: Projectile.ai[1] * 0.65f,
                progress: Projectile.ai[1],
                rotation: Projectile.ai[0],
                technique: "GravitationalLens",
                radius: 0.4f
            );
        }

        public void DrawCustom(SpriteBatch spriteBatch) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle rectangle = mainValue.GetRectangle();
            Vector2 orig = rectangle.Size() / 2;
            float rot = Projectile.rotation;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 offsetPos = Projectile.oldPos[k].To(Projectile.position);
                Vector2 drawPos2 = drawPos - offsetPos;
                Color color = Projectile.GetAlpha(Color.Pink) * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(mainValue, drawPos2, rectangle, color, rot, orig, Projectile.scale, SpriteEffects.None, 0);
            }

            VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, mainValue, Projectile.timeLeft, drawPos, rectangle, Color.Blue, rot, orig, Projectile.scale, 0);
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, rectangle
                , Color.White, Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
        }
    }

    internal class NeutronGlaiveHeldAlt : BaseHeldProj, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";
        private static Asset<Texture2D> bar1;
        private static Asset<Texture2D> bar2;
        private static Asset<Texture2D> bar3;
        private static Asset<Texture2D> bar4;
        private bool canatcck;
        private bool canatcck2 = true;
        private bool canatcck3 = true;
        private int uiframe;
        private const int maxatcck = 80;
        void ICWRLoader.SetupData() {
            if (Main.dedServ) {
                return;
            }
            bar1 = CWRUtils.GetT2DAsset(CWRConstant.UI + "NeutronsBar");
            bar2 = CWRUtils.GetT2DAsset(CWRConstant.UI + "NeutronsBar2");
            bar3 = CWRUtils.GetT2DAsset(CWRConstant.UI + "NeutronsBarTop");
            bar4 = CWRUtils.GetT2DAsset(CWRConstant.UI + "NeutronsBarTop2");
        }
        void ICWRLoader.UnLoadData() {
            bar1 = null;
            bar2 = null;
            bar3 = null;
            bar4 = null;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 112;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 4;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.hide = true;
        }

        public override void AI() {
            if (Owner.dead || !Owner.active || canatcck || !DownRight) {
                canatcck = true;
                if (Projectile.ai[0] >= maxatcck) {
                    Projectile.Kill();
                }
                else {
                    canatcck2 = false;
                    Projectile.scale = 1.25f;

                    if (++Projectile.ai[1] > 5) {
                        SoundEngine.PlaySound(SoundID.Item4, Projectile.Center);
                        Vector2 pos = Projectile.Center + Projectile.velocity.UnitVector() * Main.rand.Next(-52, 112);
                        int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), pos
                        , Projectile.velocity.RotatedByRandom(0.2f), ModContent.ProjectileType<NeutronsOrb>(), Projectile.damage, 0);
                        Main.projectile[proj].SetAllProjectilesHome(true);
                        for (int i = 0; i < 4; i++) {
                            float rot1 = MathHelper.PiOver2 * i;
                            Vector2 vr = rot1.ToRotationVector2();
                            for (int j = 0; j < 13; j++) {
                                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, vr * (0.1f + j * 0.14f), Color.BlueViolet, Main.rand.NextFloat(0.5f, 0.7f)).Configure(false, 17);
                            }
                        }
                        Projectile.ai[1] = 0;
                    }

                    Projectile.ai[0]--;
                    if (Projectile.ai[0] <= 0) {
                        Projectile.Kill();
                    }
                }
            }
            if (canatcck2) {
                Projectile.velocity = ToMouse.UnitVector() * 18;
            }
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.velocity.UnitVector() * 40 * Projectile.scale;
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!canatcck && Projectile.ai[0] <= maxatcck) {
                Projectile.ai[0]++;
            }
            if (Projectile.ai[0] >= maxatcck) {
                if (canatcck3) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f }, Projectile.Center);
                    canatcck3 = false;
                }
                Projectile.scale = 1.5f;
            }
            SetHeld();
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 15);
            if (canatcck2) {
                VaultUtils.ClockFrame(ref uiframe, 5, 6);
            }
            float rot = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.direction = Math.Sign(Projectile.velocity.X);
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer() && canatcck2) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + Projectile.velocity.UnitVector() * 255
                    , Vector2.Zero, ModContent.ProjectileType<EXNeutronExplode>(), Projectile.damage * 10, 0);
            }
        }

        public static void DrawBar(Player Owner, float sengs, int uiframe) {
            sengs = MathHelper.Clamp(sengs, 0, maxatcck);
            if (!(sengs <= 0f)) {
                Texture2D barBG = bar3.Value;
                Texture2D barFG = bar1.Value;
                if (sengs >= maxatcck) {
                    barBG = bar4.Value;
                    barFG = bar2.Value;
                }
                float barScale = 1.2f;
                Vector2 drawPos = Owner.GetPlayerStabilityCenter() + new Vector2(0, 90) - Main.screenPosition;
                Rectangle frameCrop = new Rectangle(0, 0, (int)(sengs / maxatcck * barFG.Width), barFG.Height);
                Color color = Color.White;
                Main.spriteBatch.Draw(barBG, drawPos, barBG.GetRectangle(uiframe, 7), color, 0f, VaultUtils.GetOrig(barBG, 7), barScale, 0, 0f);
                Main.spriteBatch.Draw(barFG, drawPos + new Vector2(2, 4), frameCrop, color, 0f, VaultUtils.GetOrig(barFG, 1), barScale, 0, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawBar(Owner, Projectile.ai[0], uiframe);
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, value.GetRectangle(Projectile.frame, 16)
                , Color.White, Projectile.rotation + MathHelper.PiOver4 * Owner.direction, VaultUtils.GetOrig(value, 16) + new Vector2(0, 5 * Owner.direction)
                , Projectile.scale, Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
            return false;
        }
    }

    internal class NeutronsOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.timeLeft = 120;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, Projectile.velocity, Color.BlueViolet, Main.rand.NextFloat(0.2f, 0.3f)).Configure(false, 17);
        }
    }

    internal class NeutronExplode : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 200;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 4;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
        }

        public bool CanDrawCustom() => false;

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 133; j++) {
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (0.1f + i * 0.24f), Color.BlueViolet, Main.rand.NextFloat(1.2f, 2.3f)).Configure(false, 7);
                    }
                }
                Projectile.ai[2]++;
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

            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            float scale = Math.Max(Projectile.localAI[0], 0.01f);
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 400f * scale,
                screenHeight: 400f * scale,
                intensity: Projectile.ai[1] * 0.85f,
                progress: Projectile.ai[1],
                rotation: Projectile.ai[0],
                technique: "GravitationalVortex"
            );
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }

    internal class EXNeutronExplode : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 2000;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 4;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.DamageType = EndlessDamageClass.Instance;
        }

        public bool CanDrawCustom() => false;

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                SoundEngine.PlaySound(CWRSound.Pecharge with { Pitch = -0.1f, Volume = 0.8f }, Projectile.Center);
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 133; j++) {
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (0.1f + j * 0.34f), Color.BlueViolet, Main.rand.NextFloat(2.2f, 2.3f)).Configure(false, 7);
                    }
                }
            }
            if (Projectile.ai[2] % 6 == 0) {
                float randvalue = Main.rand.NextFloat(MathHelper.TwoPi);
                float randvalue2 = Main.rand.NextFloat(0.3f, 1.6f);
                for (int z = 0; z < 4; z++) {
                    Vector2 rand = (MathHelper.PiOver2 * z + randvalue).ToRotationVector2() * 130 * randvalue2;
                    for (int i = 0; i < 4; i++) {
                        float rot1 = MathHelper.PiOver2 * i;
                        Vector2 vr = rot1.ToRotationVector2();
                        for (int j = 0; j < 33; j++) {
                            PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + rand, vr * 0.24f, Color.CadetBlue, Main.rand.NextFloat(0.9f, 1.3f)).Configure(false, 13);
                        }
                    }
                }
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

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            float scale = Math.Max(Projectile.localAI[0], 0.01f);
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 1200f * scale,
                screenHeight: 1200f * scale,
                intensity: Projectile.ai[1] * 1.0f,
                progress: Projectile.ai[1],
                rotation: Projectile.ai[0],
                technique: "GravitationalVortex",
                radius: 0.48f
            );
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }
}
