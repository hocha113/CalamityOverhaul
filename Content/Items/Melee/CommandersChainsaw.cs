using CalamityMod;
using CalamityOverhaul.Content.Buffs;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class CommandersChainsaw : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "CommandersChainsaw";
        public override void SetStaticDefaults() => ItemID.Sets.IsDrill[Type] = true;
        public override void SetDefaults() {
            Item.damage = 140;
            Item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Item.width = 20;
            Item.height = 12;
            Item.useTime = 4;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 0.5f;
            Item.value = Item.buyPrice(0, 1, 60, 2);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item23;
            Item.shoot = ModContent.ProjectileType<CommandersChainsawHeld>();
            Item.shootSpeed = 42f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.tileBoost = -1;
            Item.axe = 20;
            Item.CWR().DeathModeItem = true;
        }
    }

    internal class CommandersChainsawEX : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "CommandersChainsawEX";
        public override void SetStaticDefaults() => ItemID.Sets.IsDrill[Type] = true;
        public override void SetDefaults() {
            Item.damage = 2840;
            Item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Item.width = 20;
            Item.height = 12;
            Item.useTime = 3;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 0.5f;
            Item.value = Item.buyPrice(0, 8, 60, 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item23;
            Item.shoot = ModContent.ProjectileType<CommandersChainsawEXHeld>();
            Item.shootSpeed = 42f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.tileBoost = 2;
            Item.axe = 32;
            Item.CWR().DeathModeItem = true;
        }
    }

    internal class CommandersChainsawHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "CommandersChainsawHeld";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<CommandersChainsaw>()).DisplayName;
        public override bool ShouldUpdatePosition() => false;
        public override void SetStaticDefaults() => ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        public override void SetDefaults() {
            Projectile.width = 62;
            Projectile.height = 62;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.ownerHitCheck = true;
            Projectile.aiStyle = -1;
            Projectile.hide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
            if (Type != ModContent.ProjectileType<CommandersChainsawHeld>()) {
                Projectile.localNPCHitCooldown = 4;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            HitEffect();
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            HitEffect();
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
        }

        private void HitEffect() {
            Projectile.Center += CWRUtils.randVr(2);

            if (Owner.name == "CHAINSAW DEVIL") {
                Owner.HealEffect(2, true);
                Owner.Heal(2);
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustDirect(Projectile.position + Projectile.velocity * Main.rand.Next(0, 10) * 0.15f
                        , Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 80, Color.White, 1f);
                    dust.position.X -= 4f;
                    dust.velocity.X *= 0.5f;
                    dust.velocity.Y = -Main.rand.Next(3, 28);
                }
            }
            else {
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustDirect(Projectile.position + Projectile.velocity * Main.rand.Next(0, 10) * 0.15f
                        , Projectile.width, Projectile.height, DustID.AmberBolt, 0f, 0f, 80, Color.White, 1f);
                    dust.position.X -= 4f;
                    dust.velocity.X *= 0.5f;
                    dust.velocity.Y = -Main.rand.Next(3, 28);
                }
            }
        }

        public override void AI() {
            if (Owner.name == "CHAINSAW DEVIL") {
                Projectile.localNPCHitCooldown = 1;
            }

            Projectile.timeLeft = 2;
            if (Projectile.soundDelay <= 0) {
                SoundEngine.PlaySound(SoundID.Item22, Projectile.Center);
                Projectile.soundDelay = 20;
            }
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 1);
            Vector2 playerCenter = Owner.GetPlayerStabilityCenter();

            if (DownLeft) {
                Projectile.velocity = Item.shootSpeed * Projectile.scale * playerCenter.To(InMousePos).UnitVector();
            }
            else {
                Projectile.Kill();
            }

            if (Projectile.velocity.X > 0f) {
                Owner.ChangeDir(1);
            }
            else if (Projectile.velocity.X < 0f) {
                Owner.ChangeDir(-1);
            }

            Projectile.spriteDirection = Projectile.direction;

            Owner.ChangeDir(Projectile.direction);
            SetHeld();
            Owner.SetDummyItemTime(2);

            Projectile.Center = playerCenter + Projectile.velocity;
            Projectile.Center += CWRUtils.randVr(2);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Owner.itemRotation = (Projectile.velocity * Projectile.direction).ToRotation();
            Projectile.velocity.X *= 1f + Main.rand.Next(-3, 4) * 0.01f;

            if (Main.rand.NextBool(10)) {
                Dust dust = Dust.NewDustDirect(Projectile.position + Projectile.velocity * Main.rand.Next(6, 10) * 0.15f
                    , Projectile.width, Projectile.height, DustID.AmberBolt, 0f, 0f, 80, Color.White, 1f);
                dust.position.X -= 4f;
                dust.noGravity = true;
                dust.velocity.X *= 0.5f;
                dust.velocity.Y = -Main.rand.Next(3, 8) * 0.1f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Rectangle rectangle = CWRUtils.GetRec(TextureValue, Projectile.frame, 2);
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, rectangle
                , lightColor, Projectile.rotation - MathHelper.PiOver2, rectangle.Size() / 2, Projectile.scale
                , Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }
    }

    internal class CommandersChainsawEXHeld : CommandersChainsawHeld
    {
        public override string Texture => CWRConstant.Item_Melee + "CommandersChainsawEXHeld";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<CommandersChainsawEX>()).DisplayName;
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            HitEffect();
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            HitEffect();
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
        }

        private void HitEffect() {
            Projectile.Center += CWRUtils.randVr(2);

            if (Owner.name == "CHAINSAW DEVIL") {
                Owner.HealEffect(2, true);
                Owner.Heal(2);
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustDirect(Projectile.position + Projectile.velocity * Main.rand.Next(0, 10) * 0.15f
                        , Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 80, Color.White, 1f);
                    dust.position.X -= 4f;
                    dust.velocity.X *= 0.5f;
                    dust.velocity.Y = -Main.rand.Next(3, 28);
                }
            }
            else {
                for (int i = 0; i < 6; i++) {
                    Dust dust = Dust.NewDustDirect(Projectile.position + Projectile.velocity * Main.rand.Next(0, 10) * 0.15f
                        , Projectile.width, Projectile.height, DustID.AmberBolt, 0f, 0f, 80, Color.White, 1f);
                    dust.position.X -= 4f;
                    dust.velocity.X *= 0.5f;
                    dust.velocity.Y = -Main.rand.Next(3, 28);
                }
            }
        }
    }
}
