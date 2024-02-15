using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Common.Effects;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class GildedProboscisKevinLightning : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public ManagedRenderTarget LightningTarget {
            get;
            private set;
        }

        public ManagedRenderTarget TemporaryAuxillaryTarget {
            get;
            private set;
        }

        public Vector2 LightningCoordinateOffset {
            get;
            set;
        }

        public SlotId ElectricitySound;

        public Player Owner => Main.player[Projectile.owner];

        public bool CanUpdate {
            get => Projectile.localAI[0] == 1f;
            set => Projectile.localAI[0] = value.ToInt();
        }

        public ref float Time => ref Projectile.ai[0];

        public ref float TargetIndex => ref Projectile.ai[1];

        public ref float LightningDistance => ref Projectile.localAI[0];

        public static Color LightningColor => Color.Lerp(Color.DarkRed, Color.Gold, 0.7f);

        public override void SetStaticDefaults() {
            // DisplayName.SetDefault("Kevin");
            Main.projFrames[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 3;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 7200;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 3;
        }

        public override void OnSpawn(IEntitySource source) {
            LightningTarget = new(false, (_, _2) => {
                return new(Main.instance.GraphicsDevice, GildedProboscis.LightningArea, GildedProboscis.LightningArea, true, SurfaceFormat.Color, DepthFormat.Depth24, 8, RenderTargetUsage.DiscardContents);
            });
            TemporaryAuxillaryTarget = new(false, (_, _2) => {
                return new(Main.instance.GraphicsDevice, GildedProboscis.LightningArea, GildedProboscis.LightningArea, true, SurfaceFormat.Color, DepthFormat.Depth24, 8, RenderTargetUsage.DiscardContents);
            });
            RenderTargetManager.RenderTargetUpdateLoopEvent += UpdateLightningField;
            LightningCoordinateOffset = Vector2.Zero;
        }

        public override void AI() {
            //防御性代码，任何时候都不希望后续代码访问null值玩家或者非活跃的对象
            if (Owner.Alives() == false) {
                Projectile.Kill();
                return;
            }
            //按键判定，当主玩家释放右键时立刻杀死弹幕
            if (Projectile.IsOwnedByLocalPlayer() && PlayerInput.Triggers.Current.MouseRight)
                Projectile.timeLeft = 2;
            else Projectile.Kill();

            //寻找目标
            TargetIndex = -1;
            NPC potentialTarget = Projectile.Center.InPosClosestNPC(GildedProboscis.TargetingDistance, false);
            if (potentialTarget != null) {
                TargetIndex = potentialTarget.whoAmI;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, Projectile.SafeDirectionTo(potentialTarget.Center), 0.6f);
                LightningDistance = Projectile.Distance(potentialTarget.Center);
            }
            else if (Main.myPlayer == Owner.whoAmI)//如果没找到，那么就让电流对准鼠标
            {
                Vector2 aimDirection = Projectile.SafeDirectionTo(Main.MouseWorld);
                if (Projectile.velocity != aimDirection) {
                    LightningDistance = Projectile.Distance(Main.MouseWorld) * Main.rand.NextFloat(0.9f, 1.1f);
                    Projectile.velocity = aimDirection;
                    Projectile.netUpdate = true;
                }
            }

            //电流的长度限制
            float maxLightningRange = GildedProboscis.LightningArea * 0.5f - 8f;
            if (LightningDistance >= maxLightningRange)
                LightningDistance = maxLightningRange;

            Owner.ChangeDir(Math.Sign(Projectile.velocity.X));

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //不希望闪电穿墙打击对象，这里控制实际距离
            CollTileLine();

            //玩家手臂适配
            AdjustPlayerValues();

            Projectile.frameCounter++;
            Projectile.frame = Projectile.frameCounter / 3 % Main.projFrames[Type];

            Time++;
        }

        public void CollTileLine() {
            Vector2 rotVr = Projectile.Center.To(Main.MouseWorld).UnitVector();
            for (int i = 0; i < LightningDistance / 16f; i++) {
                Vector2 cedPos = Projectile.Center + rotVr * i * 16;
                Vector2 tilePos = CWRUtils.WEPosToTilePos(cedPos);
                Tile tile = CWRUtils.GetTile(tilePos);
                if (tile.HasSolidTile()) {
                    LightningDistance = i * 16;
                }
            }
        }

        public void AdjustPlayerValues() {
            Projectile.spriteDirection = Projectile.direction = -Owner.direction;
            Projectile.timeLeft = 2;
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            Owner.itemRotation = (Projectile.direction * Projectile.velocity).ToRotation();
            //控制玩家手臂
            float frontArmRotation = (MathHelper.PiOver2 - 0.31f) * -Owner.direction;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, frontArmRotation);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            bool collBool = false;
            float point = 0;
            Vector2 endPos;
            if (Time > 51) {
                if (TargetIndex == -1) {
                    endPos = Projectile.Center + Projectile.Center
                        .To(Main.MouseWorld).UnitVector() * LightningDistance;
                }
                else {
                    endPos = Projectile.Center + Projectile.Center
                        .To(Main.npc[(int)TargetIndex].Center).UnitVector() * LightningDistance;
                }

                collBool = Collision.CheckAABBvLineCollision(
                        targetHitbox.TopLeft(),
                        targetHitbox.Size(),
                        Projectile.Center,
                        endPos,
                        16,
                        ref point
                        );
            }
            return collBool;
        }

        public void UpdateLightningField() {
            Main.instance.GraphicsDevice.SetRenderTarget(TemporaryAuxillaryTarget.Target);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);

            Main.Rasterizer = RasterizerState.CullNone;
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.Default, Main.Rasterizer);

            Main.instance.GraphicsDevice.Textures[0] = LightningTarget.Target;
            Main.instance.GraphicsDevice.Textures[1] = CWRUtils.GetT2DValue(CWRConstant.Masking + "Extra_193");//WavyNoise

            float angularOffset = Projectile.oldRot[0] - Projectile.oldRot[1];
            Vector2 lightningDirection = Projectile.velocity.UnitVector();

            LightningCoordinateOffset += lightningDirection * -0.003f;

            var shader = EffectsRegistry.KevinLightningShader.Shader;
            shader.Parameters["uColor"].SetValue(LightningColor.ToVector3());
            shader.Parameters["uTime"].SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["actualSize"].SetValue(LightningTarget.Target.Size());
            shader.Parameters["screenMoveOffset"].SetValue(Main.screenPosition - Main.screenLastPosition);
            shader.Parameters["lightningDirection"].SetValue(lightningDirection);
            shader.Parameters["lightningAngle"].SetValue(angularOffset * 2);
            shader.Parameters["noiseCoordsOffset"].SetValue(LightningCoordinateOffset);
            shader.Parameters["currentFrame"].SetValue(Main.GameUpdateCount);
            shader.Parameters["lightningLength"].SetValue(LightningDistance / LightningTarget.Target.Width + 0.5f);
            shader.Parameters["zoomFactor"].SetValue(75f);
            shader.Parameters["bigArc"].SetValue(Main.rand.NextBool(1));
            shader.CurrentTechnique.Passes["UpdatePass"].Apply();

            Main.spriteBatch.Draw(LightningTarget.Target, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, 1f, 0, 0f);
            Main.spriteBatch.End();

            Main.Rasterizer = RasterizerState.CullNone;
            LightningTarget.Target.CopyContentsFrom(TemporaryAuxillaryTarget.Target);
        }

        public override Color? GetAlpha(Color lightColor) => Color.DarkRed * Projectile.Opacity;

        public void DrawLigOib() {
            Texture2D mainValue = CWRUtils.GetT2DValue(CWRConstant.Masking + "Hexagram2_White");
            int slp = (int)Time * 5;
            if (slp > 255) slp = 255;

            Main.Rasterizer = RasterizerState.CullNone;
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            if (slp >= 255) Main.spriteBatch.Draw(LightningTarget.Target, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation - MathHelper.PiOver2, LightningTarget.Target.Size() * 0.5f, Projectile.scale, 0, 0f);
            for (int i = 0; i < 5; i++) {
                Main.spriteBatch.Draw(
                    mainValue,
                    CWRUtils.WDEpos(Projectile.Center),
                    null,
                    Color.Red,
                    MathHelper.ToRadians(Time * 5 + i * 15),
                    CWRUtils.GetOrig(mainValue),
                    slp / 755f,
                    SpriteEffects.None,
                    0
                    );
            }
            for (int i = 0; i < 5; i++) {
                Main.spriteBatch.Draw(
                    mainValue,
                    CWRUtils.WDEpos(Projectile.Center),
                    null,
                    Color.White,
                    MathHelper.ToRadians(Time * 6 + i * 15),
                    CWRUtils.GetOrig(mainValue),
                    slp / 1055f,
                    SpriteEffects.None,
                    0
                    );
            }
            for (int i = 0; i < 5; i++) {
                Main.spriteBatch.Draw(
                    mainValue,
                    CWRUtils.WDEpos(Projectile.Center),
                    null,
                    Color.Gold,
                    MathHelper.ToRadians(Time * 9 + i * 15),
                    CWRUtils.GetOrig(mainValue),
                    slp / 1355f,
                    SpriteEffects.None,
                    0
                    );
            }
            Main.spriteBatch.ResetBlendState();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (LightningTarget == null || LightningTarget == default)
                return false;
            if (LightningTarget.IsDisposed)
                return false;
            DrawLigOib();
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 120);
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < Main.rand.Next(3, 16); i++) {
                    Vector2 pos = target.Center + Main.rand.NextVector2Unit() * Main.rand.Next(target.width);
                    Vector2 particleSpeed = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(15.5f, 37.7f);
                    Particles.Core.CWRParticle energyLeak = new LightParticle(pos, particleSpeed
                        , 0.3f, Color.Red, 6 + Main.rand.Next(5), 1, 1.5f, hueShift: 0.0f);
                    Particles.Core.CWRParticleHandler.AddParticle(energyLeak);
                }
                SoundEngine.PlaySound(SoundID.Item94, target.position);
            }
            for (int i = 0; i < 8; i++) {
                Vector2 pos = new Vector2(
                    Main.rand.Next(target.width / -2, target.width / 2),
                    Main.rand.Next(target.height / -2, target.height / 2)
                    );
                Dust.NewDust(
                    pos,
                    13,
                    13,
                    DustID.WitherLightning
                    );
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode != NetmodeID.Server) {
                RenderTargetManager.RenderTargetUpdateLoopEvent -= UpdateLightningField;
                LightningTarget?.Dispose();
                TemporaryAuxillaryTarget?.Dispose();
            }

            if (SoundEngine.TryGetActiveSound(ElectricitySound, out var t) && t.IsPlaying)
                t.Stop();
        }
    }
}
