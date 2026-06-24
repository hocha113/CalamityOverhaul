using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishPrismite : FishSkill
    {
        public override int UnlockFishID => ItemID.Prismite;
        public override int DefaultCooldown => 60 - HalibutData.GetDomainLayer() * 3;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (Cooldown > 0) {
                return null;
            }

            Vector2 shootVel = velocity.SafeNormalize(Vector2.UnitX) * 18f;
            int proj = Projectile.NewProjectile(
                source,
                position,
                shootVel,
                ModContent.ProjectileType<PrismiteWaveProjectile>(),
                (int)(damage * (1f + HalibutData.GetDomainLayer() * 0.25f)),
                knockback * 1.2f,
                player.whoAmI,
                0,
                Main.rand.Next(7)
            );

            if (proj >= 0 && proj < Main.maxProjectiles) {
                Main.projectile[proj].ai[1] = Main.rand.Next(7);
            }

            SetCooldown();
            SoundEngine.PlaySound(SoundID.Item105 with { Volume = 0.7f, Pitch = 0.3f }, position);

            return false;
        }
    }

    /// <summary>
    /// 七彩矿石冲击波：螺旋穿行、撞墙折射弹跳、首发裂变分裂。
    /// 棱彩飘带（顶点逐点变色）+ 顶点冲击波环 + PRT 棱晶碎屑共同呈现折射光感。
    /// </summary>
    internal class PrismiteWaveProjectile : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int MaxLifeTime = 240;

        private float scale = 1f;
        private float spiralPhase;
        private float spiralIntensity;
        private Vector2 baseVelocity;
        private float pulsePhase;
        private float energyWavePhase;
        private float squash = 1f;

        private int generation;
        private int colorSeed;
        private Color primaryColor;
        private Color secondaryColor;
        private Color accentColor;

        private int tileCollideCount;
        private int tileCollideCooltimer;
        private readonly List<FishSkillVFX.ShockRing> rings = new();

        //七彩配色
        private static readonly Color[] PrismColors =
        [
            new Color(255, 60, 120),
            new Color(255, 150, 50),
            new Color(255, 230, 60),
            new Color(80, 255, 120),
            new Color(60, 180, 255),
            new Color(160, 80, 255),
            new Color(255, 80, 200)
        ];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 26;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.penetrate = 3 + (int)(HalibutData.GetLevel() / 4f);
            Projectile.timeLeft = MaxLifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.extraUpdates = 2;
        }

        public override void Initialize() {
            generation = (int)Projectile.ai[0];
            colorSeed = (int)Projectile.ai[1];

            primaryColor = PrismColors[colorSeed % PrismColors.Length];
            secondaryColor = PrismColors[(colorSeed + 2) % PrismColors.Length];
            accentColor = PrismColors[(colorSeed + 4) % PrismColors.Length];

            baseVelocity = Projectile.velocity;
            Projectile.scale = 1f - generation * 0.12f;

            if (!Main.dedServ) {
                for (int i = 0; i < 14; i++) {
                    float a = MathHelper.TwoPi * i / 14f;
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, a.ToRotationVector2() * Main.rand.NextFloat(2f, 5f)
                        , PrismColors[(colorSeed + i) % PrismColors.Length], 0.5f).Configure(20, hueShift: 0.01f);
                }
            }
        }

        public override void AI() {
            spiralPhase += 0.18f;
            float lifeProgress = 1f - Projectile.timeLeft / (float)MaxLifeTime;

            if (lifeProgress < 0.2f) {
                spiralIntensity = MathHelper.Lerp(0f, 1f, lifeProgress / 0.2f);
            }
            else if (lifeProgress > 0.7f) {
                spiralIntensity = MathHelper.Lerp(1f, 0.3f, (lifeProgress - 0.7f) / 0.3f);
            }
            else {
                spiralIntensity = 1f;
            }

            if (tileCollideCooltimer > 0) {
                tileCollideCooltimer--;
            }

            //螺旋推进
            Vector2 perpendicular = baseVelocity.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero);
            float spiralOffset = (float)Math.Sin(spiralPhase) * 3f * spiralIntensity * (1f - generation * 0.3f);
            Projectile.velocity = baseVelocity * 0.99f + perpendicular * spiralOffset;
            baseVelocity = Projectile.velocity;

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            pulsePhase += 0.2f;
            energyWavePhase += 0.12f;
            squash = MathHelper.Lerp(squash, 1f, 0.2f);//弹跳挤压回弹

            //缩放动画（出生回弹 / 末段收束 / 中段呼吸）
            if (lifeProgress < 0.1f) {
                scale = VaultUtils.EaseOutBack(lifeProgress / 0.1f);
            }
            else if (lifeProgress > 0.85f) {
                scale = MathHelper.Lerp(1f, 0.6f, (lifeProgress - 0.85f) / 0.15f);
            }
            else {
                scale = 1f + (float)Math.Sin(pulsePhase * 0.5f) * 0.15f + lifeProgress * 0.2f;
            }

            for (int i = rings.Count - 1; i >= 0; i--) {
                rings[i].Update();
                if (rings[i].Dead) {
                    rings.RemoveAt(i);
                }
            }

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Color c = Color.Lerp(primaryColor, accentColor, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f)
                    , Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(1.2f, 1.2f), c, Main.rand.NextFloat(0.5f, 0.9f))
                    .Configure(false, 16);
            }

            Lighting.AddLight(Projectile.Center, primaryColor.ToVector3() * 0.9f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SplitOnImpact(Projectile.Center, Projectile.velocity);
            SpawnImpactEffect(Projectile.Center, 1f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (tileCollideCooltimer > 0) {
                return false;
            }

            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.9f;
                baseVelocity.X = Projectile.velocity.X;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.9f;
                baseVelocity.Y = Projectile.velocity.Y;
            }

            squash = 0.55f;//撞墙挤压
            SplitOnImpact(Projectile.Center, -oldVelocity);
            SpawnImpactEffect(Projectile.Center, 0.85f);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);

            if (++tileCollideCount > 6) {
                Projectile.Kill();
                Projectile.netUpdate = true;
            }

            tileCollideCooltimer = 22;
            return false;
        }

        private void SplitOnImpact(Vector2 impactPos, Vector2 impactDirection) {
            if (generation > 0 || Projectile.numHits > 0 || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            int splitCount = 3 + HalibutData.GetDomainLayer() / 2;
            Vector2 baseDir = impactDirection.SafeNormalize(Vector2.UnitX);
            float spreadAngle = MathHelper.Pi * 0.8f;

            for (int i = 0; i < splitCount; i++) {
                float angle = -spreadAngle / 2f + (spreadAngle * i / (splitCount - 1));
                Vector2 splitVel = baseDir.RotatedBy(angle) * Main.rand.NextFloat(12f, 16f);
                int newColorSeed = (colorSeed + i + 1) % PrismColors.Length;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), impactPos, splitVel, Projectile.type
                    , (int)(Projectile.damage * 0.7f), Projectile.knockBack * 0.75f, Projectile.owner, generation + 1, newColorSeed);
            }
        }

        private void SpawnImpactEffect(Vector2 pos, float strength) {
            if (Main.dedServ) {
                return;
            }
            rings.Add(new FishSkillVFX.ShockRing(pos, 130f * strength, 12f, primaryColor, 1f, 22, 48));
            rings.Add(new FishSkillVFX.ShockRing(pos, 80f * strength, 7f, secondaryColor, 1f, 18, 40));

            if (generation == 0) {
                FishSkillVFX.Punch(Owner, 3.5f * strength);
            }

            int shards = (int)(18 * strength);
            for (int i = 0; i < shards; i++) {
                float a = MathHelper.TwoPi * i / shards;
                Color c = PrismColors[(colorSeed + i) % PrismColors.Length];
                PRTLoader.NewParticle<PRT_Spark>(pos, a.ToRotationVector2() * Main.rand.NextFloat(4f, 11f) * strength, c, Main.rand.NextFloat(0.7f, 1.3f))
                    .Configure(true, Main.rand.Next(20, 34));
            }
            for (int i = 0; i < 8; i++) {
                Color c = Color.Lerp(accentColor, Color.White, 0.4f);
                PRTLoader.NewParticle<PRT_Light>(pos, Main.rand.NextVector2Circular(6f, 6f) * strength, c, Main.rand.NextFloat(0.5f, 0.9f))
                    .Configure(22, hueShift: 0.02f);
            }
        }

        public float TrailWidth(float t) => MathHelper.Lerp(20f, 2f, t) * scale * Projectile.scale;

        public Color TrailColor(float t) {
            //沿程在七彩之间循环，呈折射棱光
            float hue = (colorSeed / 7f + t * 0.7f + Main.GlobalTimeWrappedHourly * 0.4f) % 1f;
            return Main.hslToRgb(hue, 1f, 0.6f) * (1f - t) * 0.85f;
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawPrismCore();

            bool drawTrail = BuildTrailPoints(out Vector2[] pts);
            bool drawRings = rings.Count > 0;
            if (drawTrail || drawRings) {
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                if (drawTrail) {
                    FishSkillVFX.DrawRibbon(CWRAsset.LightShot.Value, pts, TrailWidth, TrailColor);
                }
                if (drawRings) {
                    Texture2D ringTex = CWRAsset.Placeholder_White.Value;
                    foreach (FishSkillVFX.ShockRing r in rings) {
                        r.Draw(ringTex);
                    }
                }
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        private void DrawPrismCore() {
            Main.instance.LoadItem(ItemID.Prismite);
            Texture2D prismTex = TextureAssets.Item[ItemID.Prismite].Value;
            Texture2D starTex = CWRAsset.StarTexture.Value;

            float pulse = (float)Math.Sin(pulsePhase) * 0.5f + 0.5f;
            float drawScale = scale * Projectile.scale * 0.9f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle src = prismTex.Bounds;
            Vector2 origin = src.Size() * 0.5f;
            Vector2 squashVec = new Vector2(drawScale * (2f - squash), drawScale * squash);

            //旋转的折射光环
            for (int i = 0; i < 4; i++) {
                float ringRot = Projectile.rotation + i * MathHelper.PiOver2 + energyWavePhase;
                Color ringColor = (Color.Lerp(secondaryColor, accentColor, i / 4f) with { A = 0 }) * (0.35f - i * 0.07f);
                Main.spriteBatch.Draw(prismTex, drawPos, src, ringColor, ringRot, origin, drawScale * (1.6f + i * 0.2f + pulse * 0.3f), SpriteEffects.None, 0f);
            }

            //主体双层 + 白核（A=0 加色）
            Main.spriteBatch.Draw(prismTex, drawPos, src, (primaryColor with { A = 0 }) * 0.95f, Projectile.rotation, origin, squashVec * 1.3f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(prismTex, drawPos, src, (Color.Lerp(primaryColor, Color.White, 0.4f) with { A = 0 }) * 0.8f, Projectile.rotation * 0.8f, origin, squashVec, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(prismTex, drawPos, src, Color.White with { A = 0 } * (0.7f + pulse * 0.3f), Projectile.rotation * 0.5f, origin, squashVec * 0.75f, SpriteEffects.None, 0f);

            //十字星光爆发
            float starIntensity = (float)Math.Pow(pulse, 2);
            if (starIntensity > 0.4f) {
                float starScale = drawScale * (starIntensity - 0.4f) * 3.5f;
                Color starColor = (Color.Lerp(primaryColor, Color.White, starIntensity) with { A = 0 }) * 0.7f;
                for (int i = 0; i < 2; i++) {
                    Main.spriteBatch.Draw(starTex, drawPos, null, starColor, i * MathHelper.PiOver2 + Main.GlobalTimeWrappedHourly * 2f
                        , starTex.Size() / 2f, starScale * (i == 0 ? 1f : 0.7f), SpriteEffects.None, 0f);
                }
            }
        }

        private bool BuildTrailPoints(out Vector2[] pts) {
            pts = null;
            if (Main.dedServ || Projectile.oldPos == null || Projectile.oldPos.Length < 4) {
                return false;
            }
            List<Vector2> list = new(Projectile.oldPos.Length);
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                list.Add(Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition);
            }
            if (list.Count < 2) {
                return false;
            }
            pts = list.ToArray();
            return true;
        }
    }
}
