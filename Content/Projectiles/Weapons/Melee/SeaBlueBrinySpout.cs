using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    public class SeaBlueBrinySpout : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "BrinySpout";

        public int MaxTierLimit {
            get => (int)Projectile.localAI[1]; set => Projectile.localAI[1] = value;
        }
        public float Magnifying {
            get => Projectile.localAI[2]; set => Projectile.localAI[2] = value;
        }


        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
        }

        public override void SetDefaults() {
            Projectile.width = 140;
            Projectile.height = 40;
            Projectile.damage = 100;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
        }

        public int Status { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int Behavior { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }
        public int ThisTimeValue { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }
        public int OwnerProJindex {
            get => (int)Projectile.localAI[0]; set => Projectile.localAI[0] = value;
        }

        public override bool ShouldUpdatePosition() {
            return false;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(Projectile.localAI[0]);
            writer.Write(Projectile.localAI[1]);
            writer.Write(Projectile.localAI[2]);
            writer.Write(Projectile.alpha);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Projectile.localAI[0] = reader.ReadInt32();
            Projectile.localAI[1] = reader.ReadInt32();
            Projectile.localAI[2] = reader.ReadInt32();
            Projectile.alpha = reader.ReadInt32();
        }

        public override void AI() {
            ThisTimeValue++;

            if (Status == 0) {
                if (Magnifying == 0)
                    Magnifying = 0.12f;
                Behavior++;
                Status = 1;
            }

            if (Status == 1) {
                if (Behavior == 1) {
                    OwnerProJindex = Projectile.whoAmI;
                    if (Projectile.IsOwnedByLocalPlayer())
                        Projectile.alpha = CWRUtils.rands.Next(0, 10000);
                    Projectile.netUpdate = true;
                }

                if (Behavior <= MaxTierLimit && ThisTimeValue > 5 && Projectile.IsOwnedByLocalPlayer()) {
                    int proj = Projectile.NewProjectile(
                        Projectile.parent(),
                        Projectile.Center + new Vector2(0, -Projectile.height * Projectile.scale),
                        Vector2.Zero,
                        Projectile.type,
                        Projectile.damage,
                        Projectile.knockBack,
                        Projectile.owner
                        );

                    Projectile newProj = CWRUtils.GetProjectileInstance(proj);
                    if (newProj != null) {
                        newProj.ai[1] = Behavior;
                        newProj.localAI[0] = OwnerProJindex;
                        newProj.localAI[1] = MaxTierLimit;
                        newProj.scale *= 1 + Behavior * Magnifying;
                        newProj.alpha = Projectile.alpha;
                        newProj.netUpdate = true;
                    }
                    else {
                        Projectile.Kill();
                    }

                    Status = 2;
                    Projectile.netUpdate = true;
                }
            }
            if (Status == 2) {
                if (Behavior == 1) {
                    Player target = Projectile.NPCFindingPlayerTarget(-1);
                    if (target != null) {
                        //Vector2 toTarget = Projectile.Center.To(target.Center).SafeNormalize(Vector2.Zero);
                        Projectile.Center += Projectile.velocity;
                    }
                }
            }
            if (Status == 3) {
                Projectile.velocity = Vector2.Zero;
                Projectile.Center += new Vector2((float)Math.Sin(MathHelper.ToRadians(ThisTimeValue * 5)) * 3 * Projectile.scale, 0);
            }

            if (Behavior != 1 && Status != 3) {
                Projectile OwnerProj = CWRUtils.GetProjectileInstance(OwnerProJindex);
                if (OwnerProj != null && Projectile.alpha == OwnerProj.alpha) {
                    Projectile.timeLeft = Behavior * 6;
                    float offsetY = 0;
                    for (int i = 1; i < Behavior; i++) {
                        offsetY += -Projectile.height * (1 + i * Magnifying);
                    }
                    Projectile.velocity = Vector2.Zero;
                    Projectile.Center = new Vector2(OwnerProj.Center.X + (float)Math.Sin(MathHelper.ToRadians(ThisTimeValue * 5)) * 60 * Projectile.scale, OwnerProj.Center.Y + offsetY);
                }
                else {
                    Status = 3;
                }
            }
            else {

            }

            if (PlayerInput.Triggers.Current.MouseRight) Projectile.Kill();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return null;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture);
            CWRUtils.ClockFrame(ref Projectile.frameCounter, 3, 5);

            Main.EntitySpriteDraw(
                mainValue,
                CWRUtils.WDEpos(Projectile.Center),
                CWRUtils.GetRec(mainValue, Projectile.frameCounter, 6),
                Color.White,
                Projectile.rotation,
                CWRUtils.GetOrig(mainValue, 6),
                Projectile.scale,
                SpriteEffects.None,
                0
                );
            return false;
        }
    }
}
