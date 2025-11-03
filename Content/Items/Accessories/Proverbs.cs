using CalamityMod.Buffs.DamageOverTime;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class Proverbs : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "Proverbs";

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(1, 5, 20, 0);
            Item.rare = ItemRarityID.Red;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.buffImmune[ModContent.BuffType<VulnerabilityHex>()] = true;
            player.buffImmune[BuffID.OnFire] = true;
            player.buffImmune[BuffID.OnFire3] = true;
            player.GetModPlayer<ProverbsPlayer>().HasProverbs = true;
            player.GetModPlayer<ProverbsPlayer>().HideVisual = hideVisual;
        }
    }

    internal class ProverbsPlayer : ModPlayer
    {
        public bool HasProverbs;
        public bool HideVisual;

        public override void ResetEffects() {
            HasProverbs = false;
            HideVisual = false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (HasProverbs) {
                target.AddBuff(ModContent.BuffType<VulnerabilityHex>(), 300);
            }
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) {
            if (HasProverbs) {
                target.AddBuff(ModContent.BuffType<VulnerabilityHex>(), 300);
            }
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            if (HasProverbs) {
                target.AddBuff(ModContent.BuffType<VulnerabilityHex>(), 300);
            }
        }

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            if (HasProverbs && proj.Alives() && proj.CWR().Source is EntitySource_Parent entitySource
                && entitySource.Entity is NPC npc && npc.Alives() && npc.type == CWRLoad.SupremeCalamitas && Main.rand.NextBool(3)) {
                modifiers.FinalDamage *= 10;
            }
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (HasProverbs && npc.type == CWRLoad.SupremeCalamitas && Main.rand.NextBool(3)) {
                modifiers.FinalDamage *= 10;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (HasProverbs && target.type == CWRLoad.SupremeCalamitas) {
                modifiers.FinalDamage *= 2;
            }
        }

        public override void PostUpdate() {
            if (HasProverbs && Player.CountProjectilesOfID<ProverbsCircle>() == 0) {
                Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Center, Vector2.Zero
                    , ModContent.ProjectileType<ProverbsCircle>(), 3000, 0, Player.whoAmI);
            }
        }
    }

    /// <summary>
    /// 箴言硫磺火法阵，玩家背后的视觉效果
    /// </summary>
    internal class ProverbsCircle : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private float rotationAngle = 0f;
        private float circleRadius = 0f;
        private float circleAlpha = 0f;
        private float Time;

        [VaultLoaden(CWRConstant.Masking + "Fire")]
        private static Asset<Texture2D> RuneAsset = null;
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowAsset = null;

        public override void SetDefaults() {
            Projectile.width = 200;
            Projectile.height = 200;
            Projectile.hide = true;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs
            , List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<VulnerabilityHex>(), 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<VulnerabilityHex>(), 300);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, circleRadius, targetHitbox);
        }

        public override void AI() {
            Projectile.timeLeft = 2;

            if (!Owner.Alives() || !Owner.GetModPlayer<ProverbsPlayer>().HasProverbs) {
                Projectile.Kill();
                return;
            }

            bool hideVisual = Owner.GetModPlayer<ProverbsPlayer>().HideVisual;
            if (hideVisual) {
                circleAlpha = MathHelper.Lerp(circleAlpha, 0f, 0.1f);
            }
            else {
                if (circleAlpha < 1f) {
                    circleAlpha += 0.05f;
                }
            }

            //缓慢展开法阵
            if (circleRadius < 120f && !hideVisual) {
                circleRadius += 2f;
            }

            //旋转
            rotationAngle += 0.015f;

            //跟随玩家
            Projectile.Center = Owner.GetPlayerStabilityCenter();

            //生成硫磺火粒子
            if (Main.rand.NextBool(3) && circleAlpha > 0.3f && !VaultUtils.isServer) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(circleRadius * 0.7f, circleRadius);
                Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;

                var prt = PRTLoader.NewParticle<PRT_LavaFire>(
                    spawnPos,
                    Vector2.UnitY * Main.rand.NextFloat(-1f, -0.3f),
                    Color.White,
                    Main.rand.NextFloat(0.6f, 1f)
                );
                if (prt != null) {
                    prt.ai[0] = 3;
                    prt.ai[1] = 0.8f;
                }
            }

            //照明
            float lightIntensity = circleAlpha * 1.2f;
            Lighting.AddLight(Projectile.Center, 1.2f * lightIntensity, 0.4f * lightIntensity, 0.2f * lightIntensity);

            Time++;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (circleAlpha < 0.01f || VaultUtils.isServer) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;

            //硫磺火色彩
            Color coreColor = new Color(255, 180, 80, 0);
            Color midColor = new Color(200, 80, 40, 0);
            Color edgeColor = new Color(120, 40, 30, 0);

            //绘制外层辉光
            for (int i = 0; i < 2; i++) {
                float ringSize = circleRadius * (1.3f + i * 0.2f);
                float alpha = circleAlpha * (0.35f - i * 0.1f);
                float rotation = rotationAngle + i * MathHelper.PiOver4;

                sb.Draw(
                    GlowAsset.Value,
                    center,
                    null,
                    edgeColor * alpha,
                    rotation,
                    GlowAsset.Value.Size() / 2,
                    ringSize / GlowAsset.Value.Width * 2f,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制法阵几何
            DrawCircle(sb, center, circleRadius * 0.9f, 2.5f, midColor * circleAlpha * 0.8f);
            DrawPentagram(sb, center, circleRadius * 0.7f, 2f, coreColor * circleAlpha * 0.9f, rotationAngle);

            //绘制符文
            if (RuneAsset?.IsLoaded ?? false) {
                DrawRunes(sb, RuneAsset.Value, center);
            }

            //绘制中心辉光
            if (GlowAsset?.IsLoaded ?? false) {
                DrawCenterGlow(sb, GlowAsset.Value, center, coreColor, midColor);
            }

            return false;
        }

        private void DrawCircle(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int segments = 48;

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                float nextAngle = MathHelper.TwoPi * (i + 1) / segments;

                Vector2 start = center + angle.ToRotationVector2() * radius;
                Vector2 end = center + nextAngle.ToRotationVector2() * radius;

                DrawLine(sb, pixel, start, end, thickness, color);
            }
        }

        private void DrawPentagram(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color, float rotation) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int points = 5;
            Vector2[] vertices = new Vector2[points];

            for (int i = 0; i < points; i++) {
                float angle = rotation + i * MathHelper.TwoPi / points - MathHelper.PiOver2;
                vertices[i] = center + angle.ToRotationVector2() * radius;
            }

            for (int i = 0; i < points; i++) {
                DrawLine(sb, pixel, vertices[i], vertices[(i + 2) % points], thickness, color);
            }
        }

        private void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;

            sb.Draw(
                pixel,
                start,
                new Rectangle(0, 0, 1, 1),
                color,
                diff.ToRotation(),
                Vector2.Zero,
                new Vector2(length, thickness),
                SpriteEffects.None,
                0f
            );
        }

        private void DrawRunes(SpriteBatch sb, Texture2D runeTex, Vector2 center) {
            int frameWidth = runeTex.Width / 4;
            int frameHeight = runeTex.Height / 4;
            int runeCount = 8;

            for (int i = 0; i < runeCount; i++) {
                float angle = rotationAngle + i * MathHelper.TwoPi / runeCount;
                Vector2 pos = center + angle.ToRotationVector2() * (circleRadius * 0.85f);

                float pulsePhase = Time * 0.08f + i * 0.5f;
                float intensityPulse = (float)Math.Sin(pulsePhase) * 0.3f + 0.7f;

                int frameX = (int)(Time / 5f + i) % 4;
                int frameY = ((int)(Time / 5f + i) / 4) % 4;
                Rectangle fireFrame = new Rectangle(frameX * frameWidth, frameY * frameHeight, frameWidth, frameHeight);

                Color fireColor = Color.Lerp(
                    new Color(200, 80, 40),
                    new Color(120, 40, 30),
                    intensityPulse
                );
                fireColor *= circleAlpha * intensityPulse * 0.8f;
                fireColor.A = 0;

                float scale = 0.6f * (0.9f + intensityPulse * 0.2f);

                sb.Draw(
                    runeTex,
                    pos,
                    fireFrame,
                    fireColor,
                    angle + Time * 0.03f,
                    new Vector2(frameWidth, frameHeight) / 2f,
                    scale,
                    SpriteEffects.None,
                    0f
                );

                //星星核心
                if (StarTexture != null && StarTexture.IsLoaded) {
                    Color coreColor = new Color(255, 150, 80, 0) * circleAlpha * 0.5f;
                    sb.Draw(
                        StarTexture.Value,
                        pos,
                        null,
                        coreColor,
                        angle,
                        StarTexture.Value.Size() / 2f,
                        scale * 0.3f,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        private void DrawCenterGlow(SpriteBatch sb, Texture2D glow, Vector2 center, Color c1, Color c2) {
            float pulse = (float)Math.Sin(Time * 0.1f) * 0.3f + 0.7f;

            sb.Draw(
                glow,
                center,
                null,
                c2 with { A = 0 } * circleAlpha * pulse * 0.5f,
                -rotationAngle * 1.2f,
                glow.Size() / 2,
                circleRadius / glow.Width * 1.2f,
                SpriteEffects.None,
                0
            );

            sb.Draw(
                glow,
                center,
                null,
                c1 with { A = 0 } * circleAlpha * pulse * 0.7f,
                rotationAngle * 1.5f,
                glow.Size() / 2,
                circleRadius / glow.Width * 0.8f,
                SpriteEffects.None,
                0
            );
        }
    }
}
