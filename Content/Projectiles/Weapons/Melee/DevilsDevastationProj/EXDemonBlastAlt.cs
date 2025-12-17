using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DevilsDevastationProj
{
    internal class EXDemonBlastAlt : BaseHeldProj
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "DemonBlast";
        public override bool IsLoadingEnabled(Mod mod) => CWRRef.Has;
        public Vector2 MoveVector2;
        public Vector2 FromeOwnerMoveSet;
        public Vector2 OrigPos;
        public Vector2 pos = new Vector2(0, -5);
        public ref float Rand => ref Projectile.localAI[0];
        private int Time;
        private int Time2;
        private bool shoot;
        private bool shoot2;
        private float expansionPhase; //扩散阶段

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DontCancelChannelOnKill[Type] = true;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.penetrate = 1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 60;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 1;
            Projectile.penetrate = 8;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Rand = Main.rand.Next(50, 100);
            double angle = Main.rand.NextDouble() * 2d * Math.PI;
            MoveVector2.X = (float)(Math.Sin(angle) * Rand);
            MoveVector2.Y = (float)(Math.Cos(angle) * Rand);
            Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Projectile.spriteDirection = Main.rand.NextBool() ? 1 : -1;
            if (Main.zenithWorld || Main.getGoodWorld) {
                Projectile.originalDamage = Projectile.damage *= 2;
                Projectile.hostile = true;
            }
        }

        public override bool? CanCutTiles() => Projectile.ai[0] != 0;

        public override bool? CanHitNPC(NPC target) => !target.friendly && Projectile.ai[0] != 0 ? null : false;

        public override void AI() {
            if (Time == 0) {
                OrigPos = InMousePos;
                pos = Projectile.velocity;
                FromeOwnerMoveSet = UnitToMouseV * 124;

                //初始爆发音效
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.4f, Volume = 0.7f }, Projectile.Center);
            }

            FromeOwnerMoveSet = Vector2.Lerp(FromeOwnerMoveSet, UnitToMouseV * 124, 0.01f);
            OrigPos = Vector2.Lerp(OrigPos, InMousePos, 0.01f);

            if (!shoot) {
                //扩散旋转
                Projectile.rotation += (Projectile.ai[0] == 0 ? 0.02f : 0.25f) * Projectile.spriteDirection;
                expansionPhase = Time / 60f;
            }
            else {
                float targetA = Projectile.DirectionTo(Owner.Center).ToRotation();
                if (shoot2) {
                    targetA = Projectile.velocity.ToRotation();
                }
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetA + MathHelper.PiOver4, 0.25f);
                if (++Time2 > 50) {
                    Projectile.extraUpdates = 2;
                    shoot2 = true;
                }
            }

            //更快的淡入
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 10;
            }

            //增强的运动逻辑
            if (Projectile.ai[1]++ < 60) {
                pos *= 0.97f;

                //扩散粒子效果
                if (Time % 3 == 0 && Main.rand.NextBool()) {
                    Vector2 dustVel = Projectile.velocity.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * 0.5f;
                    Dust expand = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, dustVel, 0, Color.OrangeRed, 2f);
                    expand.noGravity = true;
                }
            }
            else {
                if (Projectile.localAI[1] == 0) {
                    pos.Y += 0.04f;
                    if (pos.Y > 0.8f) {
                        Projectile.localAI[1] = 1;
                    }
                }
                else if (Projectile.localAI[1] == 1) {
                    pos.Y -= 0.04f;
                    if (pos.Y < -0.8f) {
                        Projectile.localAI[1] = 0;
                    }
                }
            }

            if (Projectile.ai[0] == 0) {
                Projectile.timeLeft = 200;
                Projectile.position = OrigPos + MoveVector2 + FromeOwnerMoveSet;
                MoveVector2 += pos;

                if (shoot && shoot2 && Projectile.alpha <= 0) {
                    SoundEngine.PlaySound(SoundID.Item70 with { Pitch = 0.2f, Volume = 0.8f }, Projectile.position);
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.3f }, Projectile.position);
                    Projectile.velocity = Projectile.DirectionTo(Owner.Center) * 35;
                    Projectile.extraUpdates = 1;
                    Projectile.ai[0] = 1;
                }
            }

            if (!DownLeft || Time > 100) {
                shoot = true;
            }

            Time++;
        }

        public override void OnKill(int timeLeft) {
            //增强爆炸效果
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.2f, Volume = 1.2f }, Projectile.position);
            SoundEngine.PlaySound(SoundID.Item14, Projectile.position);

            //爆炸粒子
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8, 8);
                Dust explosion = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.PurpleMoss, vel.X, vel.Y, Scale: 2.5f);
                explosion.noGravity = true;
            }

            //十字火花特效
            for (int i = 0; i < 4; i++) {
                Vector2 vr = (MathHelper.TwoPi / 4 * i + Projectile.rotation + MathHelper.PiOver4).ToRotationVector2();
                for (int j = 0; j < 16; j++) {
                    BasePRT spark = new PRT_Spark(Projectile.Center, vr * (2 + j * 0.5f), false, 38, 3.5f, Color.Purple);
                    PRTLoader.AddParticle(spark);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() / 2;

            //拖尾效果
            if (Projectile.ai[0] == 1) {
                for (int k = 0; k < Projectile.oldPos.Length; k++) {
                    Vector2 offsetPos = Projectile.oldPos[k].To(Projectile.position);
                    Vector2 drawPos = Projectile.Center - Main.screenPosition - offsetPos;
                    float trailAlpha = (Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length;
                    Color color = Color.Lerp(Color.Red, Color.OrangeRed, k / (float)Projectile.oldPos.Length) * trailAlpha;
                    Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, drawOrigin,
                        Projectile.scale * (1f - k * 0.04f), SpriteEffects.None, 0);
                }
            }

            //光效
            float glowIntensity = shoot ? 0.8f : 0.5f + expansionPhase * 0.3f;
            VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, texture, Time,
                Projectile.Center - Main.screenPosition, null, Color.OrangeRed * glowIntensity,
                Projectile.rotation, drawOrigin, Projectile.scale * (1f + expansionPhase * 0.2f), 0);

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                Projectile.GetAlpha(lightColor), Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
