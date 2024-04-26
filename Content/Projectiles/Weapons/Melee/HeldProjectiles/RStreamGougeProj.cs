using CalamityMod;
using CalamityMod.Projectiles.BaseProjectiles;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RStreamGougeProj : BaseSpearProjectile
    {
        public int Time;

        public override float InitialSpeed => 3f;

        public override float ReelbackSpeed => 2.4f;

        public override float ForwardSpeed => 0.95f;

        public Player Owner => Main.player[Projectile.owner];

        public float SpinCompletion => Utils.GetLerpValue(0f, 45f, Time, clamped: true);

        public ref float InitialDirection => ref Projectile.ai[1];

        public ref float SpinDirection => ref Projectile.ai[2];

        public override string Texture => CWRConstant.Projectile_Melee + "StreamGougeProj";

        public override Action<Projectile> EffectBeforeReelback => delegate {
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity * 0.8f
                , ModContent.ProjectileType<StreamBeams>(), Projectile.damage, Projectile.knockBack * 0.85f, Projectile.owner);
        };

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Projectile.timeLeft = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 4;
        }

        public override void OnSpawn(IEntitySource source) {
            base.OnSpawn(source);
        }

        public override void OnKill(int timeLeft) {
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(Time);
            writer.Write(Projectile.localAI[1]);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Time = reader.ReadInt32();
            Projectile.localAI[1] = reader.ReadInt32();
        }

        public override void AI() {

            if (Projectile.localAI[1] == 0)
                base.AI();
            if (Projectile.localAI[1] == 1) {
                Projectile.MaxUpdates = 2;
                if (Time == 0f) {
                    SoundEngine.PlaySound(in CommonCalamitySounds.MeatySlashSound, Projectile.Center);
                }

                if (InitialDirection == 0f) {
                    InitialDirection = Projectile.velocity.ToRotation();
                    SpinDirection = Main.rand.NextBool().ToDirectionInt();
                    Projectile.netUpdate = true;
                }
                else {
                    float num = (float)Math.Sin(Time / 3f) * 15f;
                    float num2 = MathHelper.Lerp(-10f, num + 90f, Utils.GetLerpValue(0f, 24f, Time - 45, clamped: true));
                    Projectile.velocity = InitialDirection.ToRotationVector2() * num2;
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        InitialDirection = Owner.Center.To(Main.MouseWorld).ToRotation();
                        #region 添加随行激光的设计好坏性存疑，所以暂且注释这段代码
                        //Projectile ray = AiBehavior.GetProjectileInstance((int)Projectile.localAI[2]);
                        //if (ray == null && ray.type != ModContent.ProjectileType<CosmicRay>())
                        //{
                        //    Projectile.localAI[2] = Projectile.NewProjectile(
                        //    AiBehavior.GetEntitySource_Parent(Projectile),
                        //    Projectile.Center,
                        //    Vector2.Zero,
                        //    ModContent.ProjectileType<CosmicRay>(),
                        //    Projectile.damage,
                        //    0,
                        //    Owner.whoAmI
                        //    );
                        //}
                        //if (ray != null)
                        //{
                        //    ray.Center = Owner.Center + InitialDirection.ToRotationVector2() * 132;
                        //    ray.rotation = InitialDirection;
                        //}
                        #endregion
                    }
                }

                Projectile.rotation = (float)Math.Pow(SpinCompletion, 0.82) * MathF.PI * SpinDirection * 4f + InitialDirection - MathF.PI / 4f + MathF.PI;
                DeterminePlayerVariables();
                if (Projectile.IsOwnedByLocalPlayer() && Time >= 69 && Time % 9 == 8f && Owner.GetItem().type != ItemID.None) {
                    Vector2 vector = Main.MouseWorld + Main.rand.NextVector2Unit() * Main.rand.NextFloat(50f, 140f);
                    Vector2 velocity = vector.To(Main.MouseWorld).UnitVector();
                    Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), vector, velocity, ModContent.ProjectileType<StreamGougePortal>()
                        , (int)(Owner.GetWeaponDamage(Owner.GetItem()) * 1.1f), Projectile.knockBack, Projectile.owner);
                    proj.ArmorPenetration = 10;
                }

                Time++;
            }
        }

        public void DeterminePlayerVariables() {
            Owner.direction = (Math.Cos(Projectile.rotation - MathF.PI + MathF.PI / 4f) > 0.0).ToDirectionInt();
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = CalamityUtils.WrapAngle90Degrees(MathHelper.WrapAngle(Projectile.rotation - MathF.PI + MathF.PI / 4f));
            Projectile.Center = Owner.Center;
            Projectile.timeLeft = 2;
            if (!Owner.PressKey(false)) {
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            float tomousRot = Owner.Center.To(Main.MouseWorld).ToRotation();
            if (Projectile.numHits == 0) {
                for (int i = 0; i < 2; i++) {
                    Vector2 spanPos = (tomousRot + MathHelper.ToRadians(120 + Main.rand.Next(6) * 20)).ToRotationVector2() * 280 + Owner.Center;
                    Vector2 vr = spanPos.To(target.Center).UnitVector() * 16;
                    int proj = Projectile.NewProjectile(
                            Projectile.parent(), spanPos, vr,
                            ModContent.ProjectileType<GodKillers>(),
                            Projectile.damage / 2, 0,
                            Projectile.owner);
                    Main.projectile[proj].timeLeft = 90;
                }
            }
            StreamBeams.StarRT(Projectile, target);
        }

        public void DrawPortal(Vector2 drawPosition, float opacity) {
            Texture2D value = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/StreamGougePortal").Value;
            Vector2 origin = value.Size() * 0.5f;
            Color white = Color.White;
            float num = Main.GlobalTimeWrappedHourly * 6f;
            Color color = Color.Lerp(white, Color.Black, 0.55f).MultiplyRGB(Color.DarkGray) * opacity;
            Main.EntitySpriteDraw(value, drawPosition, null, color, num, origin, Projectile.scale * 1.2f, SpriteEffects.None);
            Main.EntitySpriteDraw(value, drawPosition, null, color, 0f - num, origin, Projectile.scale * 1.2f, SpriteEffects.None);
            Main.spriteBatch.ModifyBlendState(BlendState.Additive);
            color = Color.Lerp(white, Color.Cyan, 0.55f) * opacity * 1.6f;
            Main.EntitySpriteDraw(value, drawPosition, null, color, num * 0.6f, origin, Projectile.scale * 1.2f, SpriteEffects.None);
            color = Color.Lerp(white, Color.Fuchsia, 0.55f) * opacity * 1.6f;
            Main.EntitySpriteDraw(value, drawPosition, null, color, num * -0.6f, origin, Projectile.scale * 1.2f, SpriteEffects.None);
            Main.spriteBatch.ModifyBlendState(BlendState.AlphaBlend);
        }

        public override void PostDraw(Color lightColor) {
            if (SpearAiType == SpearType.TypicalSpear && Projectile.localAI[1] == 0) {
                Main.EntitySpriteDraw(ModContent.Request<Texture2D>(CWRConstant.Projectile_Melee + "StreamGougeGlow").Value, Projectile.Center - Main.screenPosition, origin: Vector2.Zero, sourceRectangle: null, color: Color.White, rotation: Projectile.rotation, scale: Projectile.scale, effects: SpriteEffects.None);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.localAI[1] == 0) {
                base.PreDraw(ref lightColor);
            }
            if (Projectile.localAI[1] == 1) {
                if (SpinCompletion >= 0f && SpinCompletion < 1f) {
                    Texture2D value = ModContent.Request<Texture2D>("CalamityMod/Particles/SemiCircularSmear").Value;
                    Main.spriteBatch.EnterShaderRegion(BlendState.Additive);
                    float num = Projectile.rotation - MathF.PI / 5f;
                    if (SpinDirection == -1f) {
                        num += MathF.PI;
                    }

                    Main.EntitySpriteDraw(color: Color.Fuchsia * CalamityUtils.Convert01To010(SpinCompletion) * 0.9f, origin: value.Size() * 0.5f, texture: value, position: Owner.Center - Main.screenPosition, sourceRectangle: null, rotation: num, scale: Projectile.scale * 1.45f, effects: SpriteEffects.None);
                    Main.spriteBatch.ExitShaderRegion();
                }

                float lerpValue = Utils.GetLerpValue(0.6f, 1f, SpinCompletion, clamped: true);
                bool num2 = lerpValue >= 1f;
                Vector2 vector = Owner.Center + InitialDirection.ToRotationVector2() * 130f - Main.screenPosition;
                Texture2D value2 = ModContent.Request<Texture2D>(Texture).Value;
                if (num2) {
                    Main.spriteBatch.EnterShaderRegion();
                    Vector2 value3 = vector + Main.screenPosition - Projectile.Center;
                    Vector2 vector2 = Projectile.rotation.ToRotationVector2() * Utils.GetLerpValue(0f, 24f, Time - 45, clamped: true) * 80f;
                    Vector2 vector3 = (vector + Main.screenPosition - Projectile.Center) * -1.5f;
                    GameShaders.Misc["CalamityMod:IntersectionClip"].Shader.Parameters["uIntersectionPosition"].SetValue(vector + vector3);
                    GameShaders.Misc["CalamityMod:IntersectionClip"].Shader.Parameters["uIntersectionNormal"].SetValue(value3);
                    GameShaders.Misc["CalamityMod:IntersectionClip"].Shader.Parameters["uIntersectionCutoffDirection"].SetValue(1f);
                    GameShaders.Misc["CalamityMod:IntersectionClip"].Shader.Parameters["uWorldPosition"].SetValue(Projectile.Center - Main.screenPosition + vector2);
                    GameShaders.Misc["CalamityMod:IntersectionClip"].Shader.Parameters["uSize"].SetValue(value2.Size());
                    GameShaders.Misc["CalamityMod:IntersectionClip"].Shader.Parameters["uRotation"].SetValue(Projectile.rotation);
                    GameShaders.Misc["CalamityMod:IntersectionClip"].Apply();
                }

                Main.EntitySpriteDraw(position: Projectile.Center - Main.screenPosition, origin: value2.Size() * 0.5f, texture: value2, sourceRectangle: null, color: Projectile.GetAlpha(lightColor), rotation: Projectile.rotation, scale: 1f, effects: SpriteEffects.None);
                if (num2) {
                    Main.spriteBatch.ExitShaderRegion();
                }

                DrawPortal(vector, lerpValue);
            }
            return false;
        }
    }
}
