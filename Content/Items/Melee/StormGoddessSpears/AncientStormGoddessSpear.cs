using CalamityMod;
using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.StormGoddessSpears
{
    internal class AncientStormGoddessSpear : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "AncientStormGoddessSpear";
        public override void SetStaticDefaults() {
            //this.GetLocalization("Legend");
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 5));
            ItemID.Sets.ShimmerTransformToItem[Type] = ModContent.ItemType<StormGoddessSpear>();
        }
        public override void SetDefaults() {
            Item.width = 100;
            Item.height = 100;
            Item.damage = 440;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.useTurn = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.useAnimation = 19;
            Item.useTime = 19;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 9.75f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(0, 18, 25, 0);
            Item.shoot = ModContent.ProjectileType<AncientStormGoddessSpearHeld>();
            Item.shootSpeed = 15f;
            Item.rare = ModContent.RarityType<DarkBlue>();
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 7;

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<AncientStormGoddessSpearHeld>()] <= 0;

        public override void ModifyTooltips(List<TooltipLine> tooltips) => CWRUtils.SetItemLegendContentTops(ref tooltips, Name);

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.ThunderSpear)
                .AddIngredient<StormRuler>()
                .AddIngredient<StormlionMandible>(5)
                .AddIngredient(ItemID.LunarBar, 15)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    internal class AncientStormGoddessSpearHeld : BaseHeldProj
    {
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<AncientStormGoddessSpear>();
        public Color Light => Lighting.GetColor(Projectile.Center.ToTileCoordinates());
        public override string Texture => CWRConstant.Item_Melee + "AncientStormGoddessSpearHeld";
        protected float HoldoutRangeMin => -24f;
        protected float HoldoutRangeMax => 96f;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7;
            Projectile.hide = true;
        }

        public override void AI() {
            SetHeld();
            int duration = Owner.itemAnimationMax;
            if (Projectile.timeLeft > duration) {
                Projectile.timeLeft = duration;
            }
            Projectile.velocity = Vector2.Normalize(Projectile.velocity);
            float halfDuration = duration * 0.5f;
            float progress = Projectile.timeLeft < halfDuration ? Projectile.timeLeft / halfDuration : (duration - Projectile.timeLeft) / halfDuration;
            if (Projectile.timeLeft == duration / 2) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + Projectile.velocity, Projectile.velocity * 15
                    , ModContent.ProjectileType<AncientStormLightning>(), (int)(Projectile.damage * 0.8f), Projectile.knockBack, Projectile.owner, Projectile.velocity.ToRotation());
                }
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < Main.rand.Next(13, 26); i++) {
                        Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(13);
                        Vector2 particleSpeed = Projectile.velocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)).UnitVector() * Main.rand.NextFloat(15.5f, 37.7f);
                        BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                            , 0.3f, Light, 6 + Main.rand.Next(5), 1, 1.5f, hueShift: 0.0f);
                        PRTLoader.AddParticle(energyLeak);
                    }
                }
            }
            Projectile.Center = Owner.GetPlayerStabilityCenter() +
                Vector2.SmoothStep(Projectile.velocity * HoldoutRangeMin, Projectile.velocity * HoldoutRangeMax, progress);
            Projectile.rotation = Projectile.velocity.ToRotation();
            SetDirection();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center
                , Projectile.rotation.ToRotationVector2() * Projectile.height * 3 + Projectile.Center, Projectile.width, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            const float piOver3 = MathHelper.TwoPi / 3f;
            int index = 3;
            float damageset = 0.5f;
            bool isadrenal = false;
            if (Owner.Calamity().adrenalineModeActive) {
                index = 6;
                damageset = 1;
                isadrenal = true;
            }
            for (int i = 0; i < index; i++) {
                Vector2 spanPos = Projectile.Center + (piOver3 * i + Main.rand.NextFloat(MathHelper.TwoPi)).ToRotationVector2() * 560;
                Vector2 vr = spanPos.To(target.Center).UnitVector() * 15;
                Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), spanPos, vr
                    , ModContent.ProjectileType<AncientStormArc>(), (int)(Projectile.damage * damageset), Projectile.knockBack, Projectile.owner);
                proj.timeLeft = 30;
                proj.penetrate = 3;
                proj.tileCollide = false;
                if (isadrenal) {
                    proj.usesIDStaticNPCImmunity = true;
                    proj.localNPCHitCooldown = 6;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            float rot = Projectile.rotation + MathHelper.PiOver4;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2;
            Main.EntitySpriteDraw(texture, drawPosition, null, Projectile.GetAlpha(lightColor), rot + (Owner.direction > 0 ? 0 : MathHelper.PiOver2)
                , origin, Projectile.scale * 0.8f, Owner.direction > 0 ? 0 : SpriteEffects.FlipHorizontally, 0);
            return false;
        }
    }

    internal class AncientStormLightning : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        public Color Light => Lighting.GetColor((int)(Projectile.position.X + Projectile.width * 0.5) / 16, (int)((Projectile.position.Y + Projectile.height * 0.5) / 16.0));
        private Trail Trail;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 30;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.alpha = 255;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 6;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120 * (Projectile.extraUpdates + 1);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                projHitbox.X = (int)Projectile.oldPos[i].X;
                projHitbox.Y = (int)Projectile.oldPos[i].Y;
                if (projHitbox.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return base.Colliding(projHitbox, targetHitbox);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (!VaultUtils.isServer && Projectile.localAI[0] == 0) {
                Projectile.localAI[0]++;

                SoundStyle sound = SoundID.Item94;
                sound.MaxInstances = 6;
                sound.SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest;
                sound.Pitch = 0.6f;
                sound.Volume = 0.6f;
                SoundEngine.PlaySound(sound, Projectile.position);

                for (int i = 0; i < Main.rand.Next(13, 26); i++) {
                    Vector2 pos = Projectile.Center;
                    Vector2 particleSpeed = oldVelocity.RotatedByRandom(1.9f).UnitVector() * -Main.rand.NextFloat(12, 64);
                    BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                        , 0.3f, Light, 6 + Main.rand.Next(5), 1, 1.5f, hueShift: 0.0f);
                    PRTLoader.AddParticle(energyLeak);
                }
            }
            Projectile.velocity = new Vector2(0, Math.Sign(Projectile.velocity.Y));
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                if (!VaultUtils.isServer) {
                    SoundStyle sound = SoundID.Item94;
                    sound.MaxInstances = 6;
                    sound.SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest;
                    SoundEngine.PlaySound(sound, Projectile.position);

                    for (int i = 0; i < Main.rand.Next(13, 26); i++) {
                        Vector2 pos = Projectile.Center;
                        Vector2 particleSpeed = Projectile.velocity.RotatedByRandom(1.9f).UnitVector() * -Main.rand.NextFloat(12, 64);
                        BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                            , 0.3f, Light, 6 + Main.rand.Next(5), 1, 1.5f, hueShift: 0.0f);
                        PRTLoader.AddParticle(energyLeak);
                    }
                }

                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity * 15
                , ModContent.ProjectileType<AncientStormArc>(), Projectile.damage / 2, Projectile.knockBack, Projectile.owner);
            }
        }

        public override void AI() {
            Projectile.localAI[1] += Main.rand.NextFloat(1.4f);
            if (Projectile.localAI[1] > 12f) {
                Projectile.velocity = Projectile.velocity.RotatedByRandom(0.6f);
                Projectile.localAI[1] = 0f;
            }

            if (Math.Abs(Projectile.velocity.X) < 0.1f) {
                if (Math.Abs(Projectile.velocity.Y) < 1) {
                    Projectile.velocity.Y *= 1.01f;
                }
            }
            Lighting.AddLight(Projectile.Center, Color.Magenta.R / 255, Color.Magenta.G / 255, Color.Magenta.B / 255);
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < Main.rand.Next(13, 26); i++) {
                    Vector2 pos = Projectile.Center;
                    Vector2 particleSpeed = Main.rand.NextVector2Unit() * Main.rand.NextFloat(15.5f, 37.7f);
                    BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                        , 0.3f, Light, 6 + Main.rand.Next(5), 1, 1.5f, hueShift: 0.0f);
                    PRTLoader.AddParticle(energyLeak);
                }
                SoundStyle sound = SoundID.Item94;
                sound.MaxInstances = 6;
                sound.SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest;
                SoundEngine.PlaySound(sound, Projectile.position);
            }
            if (Projectile.numHits == 0 && Projectile.IsOwnedByLocalPlayer()) {
                int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Main.rand.NextVector2Unit() * 15
                , ModContent.ProjectileType<AncientStormArc>(), Projectile.damage / 3, Projectile.knockBack, Projectile.owner);
                Main.projectile[proj].timeLeft = 15;
            }
        }

        public float GetWidthFunc(float completionRatio) {
            float progress = completionRatio > 0.5f ? 1f - completionRatio : completionRatio;
            return progress * 2f * Projectile.scale * Projectile.width * 1.2f;
        }

        public Color GetColorFunc(Vector2 completionRatio) {
            float colorInterpolant = (float)Math.Sin(Projectile.identity / 3f + completionRatio.X * 20f + Main.GlobalTimeWrappedHourly * 1.1f) * 0.5f + 0.5f;
            Color color = VaultUtils.MultiStepColorLerp(colorInterpolant, Light);
            return color;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Projectile.oldPos == null || Projectile.oldPos.Length == 0) {
                return;
            }

            // 准备轨迹点
            Vector2[] positions = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    Projectile.oldPos[i] = Projectile.Center;
                }
                positions[i] = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }

            // 创建或更新 Trail
            Trail ??= new Trail(positions, GetWidthFunc, GetColorFunc);
            Trail.TrailPositions = positions;

            // 使用 InnoVault 的绘制方法
            Effect effect = EffectLoader.GradientTrail.Value;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRUtils.GetT2DValue(CWRConstant.Masking + "SlashFlatBlurHVMirror"));
            effect.Parameters["uFlow"].SetValue(CWRAsset.Airflow.Value);
            effect.Parameters["uGradient"].SetValue(CWRUtils.GetT2DValue(CWRConstant.ColorBar + "AbsoluteZero_Bar"));
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Extra_193.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }
    }

    internal class AncientStormArc : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private Color light => Lighting.GetColor((int)(Projectile.position.X + Projectile.width * 0.5) / 16, (int)((Projectile.position.Y + Projectile.height * 0.5) / 16.0));
        private Trail Trail;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 30;
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 120;
            Projectile.penetrate = 13;
            Projectile.MaxUpdates = 3;
            Projectile.tileCollide = true;
        }

        private HashSet<NPC> shockedbefore = [];
        private int prevX = 0;
        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                AdjustMagnitude(ref Projectile.velocity);
                Projectile.localAI[0] = 1f;
            }

            Vector2 move = Vector2.Zero;
            float distance = 160f;
            bool target = false;
            NPC npc = null;
            bool pastNPC = false;
            if (Projectile.timeLeft < 118) {
                for (int k = 0; k < Main.maxNPCs; k++) {
                    if (Main.npc[k].active && !Main.npc[k].dontTakeDamage && !Main.npc[k].friendly && Main.npc[k].lifeMax > 5 && !shockedbefore.Contains(Main.npc[k])) {
                        Vector2 newMove = Main.npc[k].Center - (Projectile.velocity + Projectile.Center);
                        float distanceTo = (float)Math.Sqrt(newMove.X * newMove.X + newMove.Y * newMove.Y);
                        if (distanceTo < distance) {
                            move = newMove;
                            distance = distanceTo;
                            target = true;
                            npc = Main.npc[k];

                        }
                    }
                }
            }

            if (!target) {
                foreach (NPC pastnpc in shockedbefore) {
                    Vector2 newMove = pastnpc.Center - (Projectile.velocity + Projectile.Center);
                    float distanceTo = (float)Math.Sqrt(newMove.X * newMove.X + newMove.Y * newMove.Y);
                    if (distanceTo < distance) {
                        move = newMove;
                        distance = distanceTo;
                        target = true;
                        npc = pastnpc;
                        pastNPC = true;
                    }
                }
            }

            Vector2 current = Projectile.Center;
            if (target) {
                shockedbefore.Add(npc);
                npc.HitEffect(0, Projectile.damage);
                move += new Vector2(Main.rand.Next(-10, 11), Main.rand.Next(-10, 11)) * distance / 30;
                if (pastNPC) {
                    prevX++;
                    move += new Vector2(Main.rand.Next(-10, 11), Main.rand.Next(-10, 11)) * prevX;
                }
            }
            else {
                move = (Projectile.velocity + new Vector2(Main.rand.Next(-5, 6), Main.rand.Next(-5, 6))) * 5;
            }

            for (int i = 0; i < 20; i++) {
                current += move / 20f;
            }

            Projectile.position = current;
        }
        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity;
            Projectile.timeLeft -= 32;
            return false;
        }

        private void AdjustMagnitude(ref Vector2 vector) {
            float magnitude = (float)Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
            if (magnitude > 6f) {
                vector *= 6f / magnitude;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 120);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < Main.rand.Next(3, 16); i++) {
                    Vector2 pos = target.Center + Main.rand.NextVector2Unit() * Main.rand.Next(target.width);
                    Vector2 particleSpeed = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(15.5f, 37.7f);
                    BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                        , 0.3f, light, 6 + Main.rand.Next(5), 1, 1.5f, hueShift: 0.0f);
                    PRTLoader.AddParticle(energyLeak);
                }
                if (Projectile.numHits == 0) {
                    SoundStyle sound = SoundID.Item94;
                    sound.MaxInstances = 6;
                    sound.SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest;
                    SoundEngine.PlaySound(sound, target.position);
                }
            }
        }

        public float GetWidthFunc(float completionRatio) {
            float progress = completionRatio > 0.5f ? 1f - completionRatio : completionRatio;
            return progress * 2f * Projectile.scale * Projectile.width;
        }

        public Color GetColorFunc(Vector2 completionRatio) {
            float colorInterpolant = (float)Math.Sin(Projectile.identity / 3f + completionRatio.X * 20f + Main.GlobalTimeWrappedHourly * 1.1f) * 0.5f + 0.5f;
            Color color = VaultUtils.MultiStepColorLerp(colorInterpolant, light);
            return color;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Projectile.oldPos == null || Projectile.oldPos.Length == 0) {
                return;
            }

            // 准备轨迹点
            Vector2[] positions = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    Projectile.oldPos[i] = Projectile.Center;
                }
                positions[i] = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }

            // 创建或更新 Trail
            Trail ??= new Trail(positions, GetWidthFunc, GetColorFunc);
            Trail.TrailPositions = positions;

            // 使用 InnoVault 的绘制方法
            Effect effect = EffectLoader.GradientTrail.Value;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRUtils.GetT2DValue(CWRConstant.Masking + "SlashFlatBlurHVMirror"));
            effect.Parameters["uFlow"].SetValue(CWRAsset.Airflow.Value);
            effect.Parameters["uGradient"].SetValue(CWRUtils.GetT2DValue(CWRConstant.ColorBar + "AbsoluteZero_Bar"));
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Extra_193.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }
    }
}
