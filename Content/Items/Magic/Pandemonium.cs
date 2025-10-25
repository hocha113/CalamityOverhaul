using CalamityMod.Items.Materials;
using CalamityMod.Rarities;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class Pandemonium : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "Pandemonium";

        public override void SetStaticDefaults() {
            Item.staff[Type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 320;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 25;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 5;
            Item.value = Item.sellPrice(platinum: 10);
            Item.rare = ModContent.RarityType<Violet>();
            Item.UseSound = SoundID.Item113;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<PandemoniumChannel>();
            Item.shootSpeed = 10f;
            Item.channel = true;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<PandemoniumChannel>()] == 0;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<CosmiliteBar>(15)
                .AddIngredient(ItemID.SpellTome)
                .AddIngredient(ItemID.FragmentNebula, 20)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    ///<summary>
    ///引导法阵的核心控制器
    ///</summary>
    internal class PandemoniumChannel : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player Owner => Main.player[Projectile.owner];

        private ref float ChargeTimer => ref Projectile.ai[0];
        private const int MaxCharge = 180; //3秒引导
        private const int ScytheReleaseTime = 60;
        private const int FireballReleaseTime = 120;
        private const int EndTime = 180;

        private float progress => MathHelper.Clamp(ChargeTimer / MaxCharge, 0f, 1f);
        private bool releasedScythes = false;
        private bool releasedFireballs = false;

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        private static Asset<Texture2D> RuneAsset = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowAsset = null;

        public override void SetDefaults() {
            Projectile.width = 300;
            Projectile.height = 300;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxCharge + 60;
            Projectile.alpha = 255;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || !Owner.channel || Owner.statMana <= 0) {
                Projectile.Kill();
                return;
            }

            //持续消耗法力
            if (ChargeTimer > 1 && ChargeTimer % 10 == 0) {
                Owner.CheckMana(Owner.inventory[Owner.selectedItem], -1, true);
            }

            Projectile.Center = Owner.Center - new Vector2(0, 100);
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.heldProj = Projectile.whoAmI;

            ChargeTimer++;

            //播放引导声效
            if (ChargeTimer == 1) {
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 1.2f, Pitch = -0.8f }, Projectile.Center);
            }

            //攻击逻辑
            if (ChargeTimer >= ScytheReleaseTime && !releasedScythes) {
                ReleaseScythes();
                releasedScythes = true;
            }

            if (ChargeTimer >= FireballReleaseTime && !releasedFireballs) {
                ReleaseFireballs();
                releasedFireballs = true;
            }

            if (ChargeTimer >= EndTime) {
                Projectile.Kill();
            }

            //粒子效果
            SpawnChargeParticles();

            //照明
            float light = progress * 3f;
            Lighting.AddLight(Projectile.Center, 1.5f * light, 0.2f * light, 0.4f * light);
        }

        private void ReleaseScythes() {
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.0f, Pitch = -0.5f }, Projectile.Center);
            if (Owner.whoAmI == Main.myPlayer) {
                int scytheCount = 18;
                for (int i = 0; i < scytheCount; i++) {
                    float angle = MathHelper.TwoPi * i / scytheCount;
                    Vector2 velocity = angle.ToRotationVector2() * 12f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity,
                        ModContent.ProjectileType<PandemoniumScythe>(), Projectile.damage, Projectile.knockBack, Owner.whoAmI);
                }
            }
        }

        private void ReleaseFireballs() {
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 1.2f, Pitch = -0.3f }, Projectile.Center);
            if (Owner.whoAmI == Main.myPlayer) {
                int fireballCount = 8;
                for (int i = 0; i < fireballCount; i++) {
                    Vector2 targetPos = Main.MouseWorld + Main.rand.NextVector2Circular(80f, 80f);
                    Vector2 dir = (targetPos - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 8f,
                        ModContent.ProjectileType<PandemoniumFireball>(), (int)(Projectile.damage * 1.5f), Projectile.knockBack, Owner.whoAmI);
                }
            }
        }

        private void SpawnChargeParticles() {
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(200f, 350f) * progress;
                Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;
                Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 8f);

                Dust d = Dust.NewDustPerfect(spawnPos, DustID.Vortex, velocity, 100, Color.Red, Main.rand.NextFloat(1.0f, 1.8f));
                d.noGravity = true;
                d.color = Color.Lerp(new Color(255, 50, 80), new Color(180, 20, 40), Main.rand.NextFloat());
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float p = progress;
            float time = Main.GlobalTimeWrappedHourly;

            Color coreColor = new Color(255, 180, 120);
            Color midColor = new Color(255, 60, 30);
            Color edgeColor = new Color(180, 20, 40);
            Color darkColor = new Color(100, 10, 30);

            //绘制多层动态法阵
            DrawDynamicRing(sb, center, 320f * p, 12f, edgeColor, p, time * 1.5f, 16);
            DrawDynamicRing(sb, center, 280f * p, 8f, midColor, p, time * -1.8f, 12);
            DrawDynamicRing(sb, center, 200f * p, 6f, coreColor, p, time * 2.2f, 8);
            DrawDynamicRing(sb, center, 150f * p, 4f, darkColor, p, time * -2.5f, 6);

            //绘制核心六芒星
            DrawHexagram(sb, center, 180f * p, 5f, Color.Lerp(midColor, coreColor, p), time * 0.8f);
            DrawHexagram(sb, center, 160f * p, 3f, edgeColor * p, -time * 1.1f);

            //绘制符文
            if (RuneAsset?.IsLoaded ?? false) {
                DrawRunicCircle(sb, RuneAsset.Value, center, 240f * p, p, coreColor, edgeColor, time);
            }

            //绘制闪烁辉光
            if (GlowAsset?.IsLoaded ?? false) {
                float glowPulse = (float)Math.Sin(time * 10f) * 0.5f + 0.5f;
                sb.Draw(GlowAsset.Value, center, null, coreColor with { A = 0 } * p * 0.5f * glowPulse, 0, GlowAsset.Value.Size() / 2, p * 1.5f, 0, 0);
                sb.Draw(GlowAsset.Value, center, null, midColor with { A = 0 } * p * 0.3f * (1 - glowPulse), 0, GlowAsset.Value.Size() / 2, p * 2.0f, 0, 0);
            }

            return false;
        }

        private void DrawDynamicRing(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color, float p, float rotation, int segments) {
            if (p <= 0) return;
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            for (int i = 0; i < segments; i++) {
                float angle = rotation + MathHelper.TwoPi * i / segments;
                float segmentLength = (MathHelper.TwoPi * radius / segments) * 0.8f; //间断效果
                float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f + i) * 0.5f + 0.5f;
                Vector2 pos = center + angle.ToRotationVector2() * radius;
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * p * pulse, angle + MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(thickness, segmentLength), SpriteEffects.None, 0f);
            }
        }

        private void DrawHexagram(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color, float rotation) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            //正三角形
            DrawPolygon(sb, pixel, center, 3, radius, thickness, color, rotation);
            //倒三角形
            DrawPolygon(sb, pixel, center, 3, radius, thickness, color, rotation + MathHelper.Pi);
        }

        private void DrawPolygon(SpriteBatch sb, Texture2D pixel, Vector2 center, int sides, float radius, float thickness, Color col, float rot) {
            if (sides < 3) return;
            Vector2 prev = center + (rot).ToRotationVector2() * radius;
            for (int i = 1; i <= sides; i++) {
                float ang = rot + i * MathHelper.TwoPi / sides;
                Vector2 curr = center + ang.ToRotationVector2() * radius;
                DrawLine(sb, pixel, prev, curr, thickness, col);
                prev = curr;
            }
        }

        private void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, float thickness, Color col) {
            Vector2 diff = end - start;
            sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), col, diff.ToRotation(), Vector2.Zero, new Vector2(diff.Length(), thickness), SpriteEffects.None, 0f);
        }

        private void DrawRunicCircle(SpriteBatch sb, Texture2D runeTex, Vector2 center, float radius, float p, Color c1, Color c2, float time) {
            int count = 12;
            for (int i = 0; i < count; i++) {
                float ang = time * 1.8f + i * MathHelper.TwoPi / count;
                float distOffset = (float)Math.Sin(ang * 2f + p * 8f) * 20f;
                Vector2 pos = center + ang.ToRotationVector2() * (radius + distOffset * p);

                float colorPhase = (float)Math.Sin(ang + p * 12f) * 0.5f + 0.5f;
                Color runeColor = Color.Lerp(c1, c2, colorPhase) * p * 0.9f;
                runeColor.A = 0;
                float scale = p * (0.2f + 0.1f * (float)Math.Sin(ang * 3f + p * 10f));
                sb.Draw(runeTex, pos, null, runeColor, ang + MathHelper.PiOver2, runeTex.Size() / 2f, scale, SpriteEffects.None, 0f);
            }
        }
    }

    ///<summary>
    ///深渊血色镰刀
    ///</summary>
    internal class PandemoniumScythe : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "BalefulSickle";

        private NPC target;
        private float searchCooldown = 0;

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 300;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.rotation += 0.4f * Math.Sign(Projectile.velocity.X > 0 ? 1 : -1);

            //寻找目标
            if (searchCooldown <= 0) {
                target = FindTarget();
                searchCooldown = 15;
            }
            else {
                searchCooldown--;
            }

            //追踪目标
            if (target != null && target.active && !target.dontTakeDamage) {
                Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * 16f, 0.08f);
            }
            else {
                //如果没有目标，稍微减速
                Projectile.velocity *= 0.98f;
            }

            //拖尾粒子
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, Projectile.velocity * -0.1f, 100, default, 1.2f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.7f, 0.1f, 0.2f);
        }

        private NPC FindTarget() {
            NPC closest = null;
            float maxDist = 800f;
            foreach (NPC npc in Main.npc) {
                if (npc.CanBeChasedBy(this)) {
                    float dist = Projectile.Distance(npc.Center);
                    if (dist < maxDist) {
                        maxDist = dist;
                        closest = npc;
                    }
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Ichor, 180);
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = 0.2f }, Projectile.position);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(lightColor), Projectile.rotation, texture.Size() / 2, Projectile.scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    ///<summary>
    ///混沌魔能火球
    ///</summary>
    internal class PandemoniumFireball : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private bool exploded = false;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //爆炸伤害
        }

        public override void AI() {
            Projectile.velocity *= 1.015f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //拖尾效果
            for (int i = 0; i < 2; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, 261, Vector2.Zero, 100, Color.Lerp(Color.Red, Color.Orange, Main.rand.NextFloat()), 1.5f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(1f, 1f);
            }

            Lighting.AddLight(Projectile.Center, 1.2f, 0.5f, 0.3f);
        }

        public override void OnKill(int timeLeft) {
            if (exploded) return;
            Explode();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Explode();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (exploded) return;
            Explode();
        }

        private void Explode() {
            if (exploded) return;
            exploded = true;

            Projectile.position = Projectile.Center;
            Projectile.width = Projectile.height = 250;
            Projectile.Center = Projectile.position;
            Projectile.Damage();

            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1.0f, Pitch = -0.4f }, Projectile.Center);

            //爆炸粒子
            for (int i = 0; i < 60; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(12f, 12f);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 100, new Color(255, 100, 40), Main.rand.NextFloat(1.5f, 2.5f));
                d.noGravity = true;
            }
            for (int i = 0; i < 30; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, vel, 150, Color.DarkGray, Main.rand.NextFloat(1.5f, 2.0f));
                d.noGravity = true;
            }

            Projectile.active = false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float time = Main.GlobalTimeWrappedHourly;
            float pulse = (float)Math.Sin(time * 20f) * 0.5f + 0.5f;

            Color c1 = new Color(255, 200, 100, 0);
            Color c2 = new Color(255, 100, 30, 0);
            Color c3 = new Color(180, 30, 20, 0);

            Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null, c3 * 0.5f, 0, glow.Size() / 2, Projectile.scale * 1.5f, 0, 0);
            Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null, c2 * 0.8f, 0, glow.Size() / 2, Projectile.scale * (1.0f + pulse * 0.3f), 0, 0);
            Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null, c1, 0, glow.Size() / 2, Projectile.scale * 0.6f * (1.0f + pulse * 0.5f), 0, 0);

            return false;
        }
    }
}
