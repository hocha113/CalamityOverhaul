using CalamityMod.Graphics.Primitives;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips
{
    internal class WhiplashGalacticaProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Summon + "WhiplashGalacticaProjectile";

        private List<Vector2> whipPoints => Projectile.GetWhipControlPoints();//点集

        public override void SetStaticDefaults() {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.DefaultToWhip();
            Projectile.WhipSettings.Segments = 30;
            Projectile.WhipSettings.RangeMultiplier = 1f;
        }

        private float Time {
            get => Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.NewProjectile(
                Projectile.parent(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<WTrail>(),
                0,
                0,
                Projectile.owner,
                ai0: Projectile.whoAmI
                );
        }

        public override bool PreAI() {
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<GodKillsFire>(), 240);

            Main.player[Projectile.owner].MinionAttackTargetNPC = target.whoAmI;
            Projectile.damage = Projectile.damage / 2;

            if (Projectile.numHits == 0) {
                target.CWR().WhipHitNum += 3;
                target.CWR().WhipHitType = (byte)WhipHitTypeEnum.WhiplashGalactica;

                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(
                        Projectile.parent(),

                        target.Center -
                        Main.player[Projectile.owner].Center.To(target.Center).UnitVector()
                        .RotatedBy(MathHelper.ToRadians(Main.rand.Next(-75, 75))) * 300,

                        Vector2.Zero,
                        ModContent.ProjectileType<CosmicFire>(),
                        Projectile.damage,
                        0,
                        Projectile.owner,
                        Projectile.whoAmI
                    );
                }
            }
        }

        private void DrawLine(List<Vector2> list) {
            Texture2D texture = TextureAssets.FishingLine.Value;
            Rectangle frame = texture.Frame();
            Vector2 origin = new Vector2(frame.Width / 2, 2);

            Vector2 pos = list[0];
            for (int i = 0; i < list.Count - 2; i++) {
                Vector2 element = list[i];
                Vector2 diff = list[i + 1] - element;

                float rotation = diff.ToRotation() - MathHelper.PiOver2;
                Color color = new Color(252, 102, 202);
                Vector2 scale = new Vector2(1, (diff.Length() + 2) / frame.Height);

                Main.EntitySpriteDraw(texture, pos - Main.screenPosition, frame, color, rotation, origin, scale, SpriteEffects.None, 0);

                pos += diff;
            }
        }//绘制连接线

        public override bool PreDraw(ref Color lightColor) {
            DrawLine(whipPoints);

            SpriteEffects flip = Projectile.spriteDirection < 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Main.instance.LoadProjectile(Type);
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Texture2D _men = CWRUtils.GetT2DValue(CWRConstant.Projectile_Summon + "WhiplashGalacticaProjectileGlows");

            Vector2 pos = whipPoints[0];

            for (int i = 0; i < whipPoints.Count - 1; i++) {
                Rectangle frame = new Rectangle(0, 0, 40, 88);

                Vector2 origin = new Vector2(20, 40);
                float scale = 1;

                if (i == whipPoints.Count - 2) {
                    frame.Y = 130;
                    frame.Height = 84;
                    origin = Projectile.spriteDirection < 0 ? new Vector2(15, 16) : new Vector2(25, 16);
                    Projectile.GetWhipSettings(Projectile, out float timeToFlyOut, out int _, out float _);
                    float t = Time / timeToFlyOut;
                    scale = MathHelper.Lerp(1.05f, 2f, Utils.GetLerpValue(0.1f, 0.7f, t, true) * Utils.GetLerpValue(0.9f, 0.7f, t, true));
                }
                else if (i > 0) {
                    frame.Y = 90;
                    frame.Height = 38;
                    origin = new Vector2(19, 19);
                    scale = 1 + i / 120f;
                }

                Vector2 element = whipPoints[i];
                Vector2 diff = whipPoints[i + 1] - element;

                float rotation = diff.ToRotation() - MathHelper.PiOver2; // 此投射物的精灵图朝下，因此使用PiOver2进行旋转修正
                Color color = Lighting.GetColor(element.ToTileCoordinates());
                scale *= 0.75f;
                Main.EntitySpriteDraw(texture, pos - Main.screenPosition, frame, color, rotation, origin, scale, flip, 0);
                Main.EntitySpriteDraw(_men, pos - Main.screenPosition, frame, Color.White, rotation, origin, scale, flip, 0);

                pos += diff;
            }
            return false;
        }

        private class WTrail : ModProjectile
        {
            public override string Texture => CWRConstant.Placeholder;

            public override void SetStaticDefaults() {
                ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
                ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            }

            public override void SetDefaults() {
                Projectile.width = 6;
                Projectile.height = 6;
                Projectile.scale = 1;
                Projectile.alpha = 80;
                Projectile.friendly = true;
                Projectile.ignoreWater = true;
                Projectile.tileCollide = false;
                Projectile.penetrate = -1;
                Projectile.timeLeft = 150;
            }

            public int fowerIndex { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }

            public override void AI() {
                Projectile ownProj = CWRUtils.GetProjectileInstance(fowerIndex);
                if (ownProj != null) {
                    List<Vector2> toPos = ownProj.GetWhipControlPoints();
                    int index = toPos.Count - 2;
                    if (index < toPos.Count && index >= 0) {
                        float rot = toPos[toPos.Count - 3].To(toPos[toPos.Count - 2]).ToRotation();
                        Projectile.velocity = Projectile.Center.To(toPos[toPos.Count - 2]) + rot.ToRotationVector2() * 32;
                    }

                    Projectile.timeLeft = 2;
                }
                else Projectile.Kill();
            }

            public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
                return false;
            }

            internal Color ColorFunction(float completionRatio) {
                float amount = MathHelper.Lerp(0.65f, 1f, (float)Math.Cos((0f - Main.GlobalTimeWrappedHourly) * 3f) * 0.5f + 0.5f);
                float num = Utils.GetLerpValue(1f, 0.64f, completionRatio, clamped: true) * Projectile.Opacity;
                Color value = Color.Lerp(new Color(192, 192, 192), new Color(211, 211, 211), (float)Math.Sin(completionRatio * MathF.PI * 1.6f - Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f);
                return Color.Lerp(new Color(255, 255, 255), value, amount) * num;
            }

            internal float WidthFunction(float completionRatio) {
                float amount = (float)Math.Pow(1f - completionRatio, 3.0);
                return MathHelper.Lerp(0f, 62f * Projectile.scale * Projectile.Opacity, amount);
            }

            public override bool PreDraw(ref Color lightColor) {
                GameShaders.Misc["CalamityMod:TrailStreak"].SetMiscShaderAsset_1(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/Trails/ScarletDevilStreak"));
                PrimitiveRenderer.RenderTrail(Projectile.oldPos, new PrimitiveSettings(WidthFunction, ColorFunction
                    , (float _) => Projectile.Size * 0.5f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:TrailStreak"]), 30);
                Texture2D value = ModContent.Request<Texture2D>(Texture).Value;
                Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null,
                    Color.Lerp(lightColor, Color.White, 0.5f),
                    Projectile.rotation + MathHelper.PiOver2,
                    value.Size() / 2f,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                    );
                return false;
            }
        }
    }
}
