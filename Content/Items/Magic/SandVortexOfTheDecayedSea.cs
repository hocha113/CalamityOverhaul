using CalamityOverhaul.Content.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    /// <summary>腐化深海漩涡，光标处吸扯并抛追踪珠</summary>
    internal class SandVortexOfTheDecayedSea : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "SandVortexOfTheDecayedSea";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 32;
            Item.height = 32;
            Item.damage = 308;
            Item.useTime = 36;
            Item.useAnimation = 36;
            Item.mana = 18;
            Item.knockBack = 4.5f;
            Item.shoot = ModContent.ProjectileType<DecayedSeaVortex>();
            Item.shootSpeed = 1;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(1, 62, 0, 5);
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<SandVortexOfTheDecayedSeaHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<SandVortexOfTheDecayedSeaHeld>(player, source);
    }

    internal class SandVortexOfTheDecayedSeaHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Magic + "SandVortexOfTheDecayedSea";
        public override int TargetID => ModContent.ItemType<SandVortexOfTheDecayedSea>();
        /// <summary>开火余韵 0~1</summary>
        private int glowPulse;
        public override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            AlwaysAimPose = true;
        }

        public override void AI() {
            UpdateHeldPose(WantsFireLeft);

            if (glowPulse > 0) {
                glowPulse--;
            }
            if (CanFire) {
                HoldManaRegenDelay();
            }

            if (WantsFireLeft && FireCooldown <= 0 && PayMana()) {
                Fire();
                SetFireCooldown();
            }
            Time++;
        }

        private void Fire() {
            glowPulse = 36;
            SnapToAimPose();
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Pitch = -0.35f, Volume = 0.85f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item84 with { Pitch = -0.4f, Volume = 0.7f }, InMousePos);

            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Source, InMousePos, Vector2.Zero
                    , ModContent.ProjectileType<DecayedSeaVortex>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }

            for (int i = 0; i < 24; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 7f);
                int dustType = Main.rand.NextBool(3) ? CWRID.Dust_SulphurousSeaAcid : DustID.Sand;
                int d = Dust.NewDust(ShootPos, 1, 1, dustType, vel.X, vel.Y, 100, default, 1.2f);
                Main.dust[d].noGravity = true;
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
            Color color = Color.GreenYellow;
            color.A = 0;
            float slp = 1 + 0.014f * glowPulse;
            Main.EntitySpriteDraw(TextureValue, drawPos, null, color
                , Projectile.rotation + offsetRot, TextureValue.Size() / 2, Projectile.scale * slp
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            base.GunDraw(drawPos, ref lightColor);
        }
    }

    /// <summary>深海漩涡本体，牵引+抛珠，消散爆一圈</summary>
    internal class DecayedSeaVortex : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public const int Lifetime = 360;
        public const int RefreshDuration = 240;
        private const float SuctionRadius = 480f;
        private const float DamageRadius = 220f;
        private const int DamageTickInterval = 14;
        private const int OrbSpawnInterval = 32;

        //true=跳过结束爆裂(预留)
        public bool SuppressDeathBurst { get; set; }

        private ref float OrbTimer => ref Projectile.ai[0];
        private ref float DamageTimer => ref Projectile.ai[1];
        private ref float SwirlTime => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = (int)(DamageRadius * 2);
            Projectile.height = (int)(DamageRadius * 2);
            Projectile.DamageType = DamageClass.Magic;
            //伤害走 SimpleStrikeNPC
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            SwirlTime += 0.07f;
            Projectile.velocity = Vector2.Zero;

            ApplySuction();

            if (++DamageTimer >= DamageTickInterval) {
                DamageTimer = 0;
                ApplyDamageTick();
            }

            if (++OrbTimer >= OrbSpawnInterval) {
                OrbTimer = 0;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    SpawnOrbs();
                }
            }

            if (!Main.dedServ) {
                SpawnAmbientDust();
                Lighting.AddLight(Projectile.Center, new Color(130, 210, 110).ToVector3() * 1.3f * Main.essScale);
            }
        }

        private void ApplySuction() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) {
                    continue;
                }
                if (npc.knockBackResist <= 0f) {
                    continue;
                }
                Vector2 toCenter = Projectile.Center - npc.Center;
                float dist = toCenter.Length();
                if (dist > SuctionRadius || dist < 1f) {
                    continue;
                }
                float pullT = (SuctionRadius - dist) / SuctionRadius;
                Vector2 pull = toCenter.SafeNormalize(Vector2.Zero) * pullT * 5f * npc.knockBackResist;
                Vector2 tangent = toCenter.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                Vector2 swirl = tangent * pullT * 2.5f * npc.knockBackResist;
                //平滑混速防跳变
                npc.velocity = Vector2.Lerp(npc.velocity, npc.velocity + pull + swirl, 0.35f);
            }

            //顺带扰敌方弹幕
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile hostile = Main.projectile[i];
                if (!hostile.active || !hostile.hostile || hostile.friendly) {
                    continue;
                }
                if (Vector2.DistanceSquared(hostile.Center, Projectile.Center) > SuctionRadius * SuctionRadius) {
                    continue;
                }
                hostile.velocity *= 0.94f;
            }
        }

        private void ApplyDamageTick() {
            int baseDmg = Math.Max(Projectile.damage / 5, 1);
            float damageRadiusSq = DamageRadius * DamageRadius;
            Player owner = Main.player[Projectile.owner];
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) {
                    continue;
                }
                if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > damageRadiusSq) {
                    continue;
                }
                int finalDmg = baseDmg;
                if (CWRUtils.IsWormBody(npc)) {
                    finalDmg = (int)(finalDmg * 0.55f);
                }
                if (CWRLoad.ExoMechAresSegments.Contains(npc.type)) {
                    finalDmg = (int)(finalDmg * 0.7f);
                }
                npc.SimpleStrikeNPC(Math.Max(finalDmg, 1)
                    , Math.Sign(npc.Center.X - Projectile.Center.X), false, 0f
                    , DamageClass.Magic, false, owner.luck, true);
                if (Main.rand.NextBool(10)) {
                    npc.AddBuff(BuffID.Confused, 90);
                }
            }
        }

        private void SpawnOrbs() {
            int orbType = ModContent.ProjectileType<DecayedSeaOrb>();
            int orbDmg = Math.Max(Projectile.damage / 2, 1);
            float baseRot = Main.rand.NextFloat(MathHelper.TwoPi);
            const int orbCount = 2;
            for (int i = 0; i < orbCount; i++) {
                float ang = baseRot + i * (MathHelper.TwoPi / orbCount);
                Vector2 dir = ang.ToRotationVector2();
                Vector2 spawnPos = Projectile.Center + dir * 64f;
                //切向外抛
                Vector2 vel = dir.RotatedBy(MathHelper.PiOver4 * 0.5f) * 9.5f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawnPos, vel
                    , orbType, orbDmg, Projectile.knockBack, Projectile.owner);
            }
        }

        private void SpawnAmbientDust() {
            int count = Main.rand.NextBool(2) ? 3 : 2;
            for (int i = 0; i < count; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float r = DamageRadius * Main.rand.NextFloat(0.55f, 1.0f);
                Vector2 dir = ang.ToRotationVector2();
                Vector2 pos = Projectile.Center + dir * r;
                Vector2 tangent = dir.RotatedBy(MathHelper.PiOver2);
                Vector2 vel = -dir * 3.5f + tangent * 3f;
                int dustType = Main.rand.NextBool(3) ? CWRID.Dust_SulphurousSeaAcid : DustID.Sand;
                int d = Dust.NewDust(pos, 1, 1, dustType, vel.X, vel.Y, 100, default, 1.15f);
                Main.dust[d].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float lifePct = MathHelper.Clamp(Projectile.timeLeft / (float)Lifetime, 0f, 1f);
            float fadeIn = MathHelper.Clamp((Lifetime - Projectile.timeLeft) / 20f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 40f, 0f, 1f);
            float alpha = fadeIn * fadeOut;
            float endPulse = 1f + (1f - lifePct) * 0.35f;
            float t = SwirlTime;

            Color acidGreen = new Color(115, 200, 95);
            Color acidYellow = new Color(200, 220, 100);
            Color sand = new Color(220, 185, 110);

            Texture2D cyclone = CWRAsset.Cyclone?.Value;
            if (cyclone != null) {
                Vector2 origin = cyclone.Size() * 0.5f;
                Color c1 = acidGreen * alpha * 0.55f;
                c1.A = 0;
                Main.spriteBatch.Draw(cyclone, baseScreen, null, c1
                    , t * 1.6f, origin, DamageRadius / cyclone.Width * 2.7f * endPulse, SpriteEffects.None, 0f);
                Color c2 = sand * alpha * 0.45f;
                c2.A = 0;
                Main.spriteBatch.Draw(cyclone, baseScreen, null, c2
                    , -t * 0.8f, origin, DamageRadius / cyclone.Width * 1.8f * endPulse, SpriteEffects.None, 0f);
            }

            //5 条气流带
            Texture2D airflow = CWRAsset.Fog?.Value;
            if (airflow != null) {
                Vector2 origin = airflow.Size() * 0.5f;
                const int airflowCount = 5;
                for (int i = 0; i < airflowCount; i++) {
                    float a = i * (MathHelper.TwoPi / airflowCount) + t * (i % 2 == 0 ? 1.1f : -0.85f);
                    Vector2 offset = a.ToRotationVector2() * DamageRadius * 0.55f;
                    Color c = Color.Lerp(acidYellow, sand, i / (float)airflowCount) * alpha * 0.32f;
                    c.A = 0;
                    Main.spriteBatch.Draw(airflow, baseScreen + offset, null, c
                        , a + t * 0.7f, origin, DamageRadius / airflow.Width * 1.5f, SpriteEffects.None, 0f);
                }
            }

            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog != null) {
                Vector2 origin = fog.Size() * 0.5f;
                int seed = Projectile.whoAmI * 7919;
                const int fogCount = 6;
                for (int i = 0; i < fogCount; i++) {
                    float fa = ((seed + i * 173) % 360) * MathHelper.Pi / 180f + t * 0.4f;
                    float fr = ((seed + i * 211) % 100) / 100f;
                    Vector2 offset = fa.ToRotationVector2() * DamageRadius * (0.3f + fr * 0.6f);
                    Color c = Color.Lerp(acidGreen, sand, fr) * alpha * 0.32f;
                    c.A = 0;
                    Main.spriteBatch.Draw(fog, baseScreen + offset, null, c
                        , fa + t * 0.3f * (i % 3 - 1), origin, 0.5f + fr * 0.65f, SpriteEffects.None, 0f);
                }
            }

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Vector2 origin = glow.Size() / 2f;
                Color inner = new Color(180, 240, 140, 0) * alpha * 0.55f;
                Main.spriteBatch.Draw(glow, baseScreen, null, inner, 0f, origin, DamageRadius / 32f * 0.9f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(glow, baseScreen, null, inner * 0.5f, 0f, origin, DamageRadius / 32f * 1.55f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.9f, Pitch = -0.5f }, Projectile.Center);

            //结束爆 8 向珠
            if (!SuppressDeathBurst && Projectile.IsOwnedByLocalPlayer()) {
                int orbType = ModContent.ProjectileType<DecayedSeaOrb>();
                int orbDmg = Math.Max((int)(Projectile.damage * 0.65f), 1);
                const int finalOrbs = 8;
                for (int i = 0; i < finalOrbs; i++) {
                    Vector2 vel = (MathHelper.TwoPi * i / finalOrbs).ToRotationVector2() * 11f;
                    Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, vel
                        , orbType, orbDmg, Projectile.knockBack, Projectile.owner);
                }
            }

            for (int i = 0; i < 80; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(9f, 9f);
                int dustType = Main.rand.NextBool(3) ? CWRID.Dust_SulphurousSeaAcid : DustID.Sand;
                int d = Dust.NewDust(Projectile.Center, 1, 1, dustType, vel.X, vel.Y, 100, default, 1.4f);
                Main.dust[d].noGravity = true;
            }
        }
    }

    /// <summary>深海珠，短飞后追踪</summary>
    internal class DecayedSeaOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "DecayedSeaOrb";
        private HashSet<NPC> onHitNPCs = [];
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 6;
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.timeLeft = 500;
            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }
        public override void AI() {
            Projectile.velocity.Y += 0.01f;
            Projectile.rotation += Projectile.velocity.X * 0.01f;
            if (!Main.dedServ) {
                if (Main.rand.NextBool(5)) {
                    int dustnumber = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Gold, 0f, 0f, 150, Color.Gold, 1f);
                    Main.dust[dustnumber].velocity *= 0.3f;
                    Main.dust[dustnumber].noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, Color.PaleGoldenrod.ToVector3() * 1.75f * Main.essScale);
            }

            if (Projectile.timeLeft < 200) {
                Projectile.rotation = Projectile.velocity.ToRotation();
                NPC target = Projectile.Center.FindClosestNPC(1600, false, false, onHitNPCs);
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1f, 0.1f);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!onHitNPCs.Contains(target)) {
                onHitNPCs.Add(target);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRUtils.IsWormBody(target)) {
                modifiers.FinalDamage *= 0.4f;
            }
            if (CWRLoad.ExoMechAresSegments.Contains(target.type)) {
                modifiers.FinalDamage *= 0.6f;
            }
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode();
            CreateDustEffect(CWRID.Dust_SulphurousSeaAcid, 80);
        }

        private void CreateDustEffect(int dustType, int amount) {
            for (int i = 0; i < amount; i++) {
                int dustIndex = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , dustType, 0f, -2f, 0, default, 0.8f);
                Dust dust = Main.dust[dustIndex];
                dust.noGravity = true;
                dust.position.X += Main.rand.Next(-150, 151) * 0.05f - 1.5f;
                dust.position.Y += Main.rand.Next(-150, 151) * 0.05f - 1.5f;

                if (dust.position != Projectile.Center) {
                    dust.velocity = Projectile.DirectionTo(dust.position) * 6f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            SpriteEffects spriteEffects = SpriteEffects.None;
            float drawRot = Projectile.rotation;
            Rectangle rectangle = texture.GetRectangle();
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = Color.White * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, drawRot, drawOrigin, Projectile.scale, spriteEffects, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle, Color.White, drawRot, drawOrigin, Projectile.scale, spriteEffects, 0);
            return false;
        }
    }
}
