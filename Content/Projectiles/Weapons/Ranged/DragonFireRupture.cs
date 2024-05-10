using CalamityMod.Buffs.DamageOverTime;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class DragonFireRupture : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "FireCrossburst";

        struct Ncb{
            public Vector2 pos;
            public int frame;

            public Ncb(Vector2 overPos, int overFrame) {
                pos = overPos;
                frame = overFrame;
            }
        }

        List<Ncb> ncbs = new List<Ncb>();

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.damage = 100;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.scale = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public int Status { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int Behavior { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }
        public int ThisTimeValue { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }

        bool upPos = false;
        public override bool ShouldUpdatePosition() {
            return upPos;
        }

        List<Vector2> randomOffsetVr = new List<Vector2>();
        public override void OnSpawn(IEntitySource source) {
            Behavior++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.localAI[2] = Projectile.velocity.Length();

            if (!Main.dedServ) {
                Projectile.frameCounter += Main.rand.Next(6);
                for (int i = 0; i < 6; i++) {
                    randomOffsetVr.Add(CWRUtils.GetRandomVevtor(0, 360, Main.rand.NextFloat(16, 80)));
                }
                for (int i = 0; i < 6; i++) {
                    Vector2 spanPos = randomOffsetVr[i] + Projectile.Center;
                    ncbs.Add(new Ncb(spanPos, Main.rand.Next(6)));
                }
            }
        }

        public override void AI() {
            ThisTimeValue++;
            NPC target = Projectile.Center.FindClosestNPC(360);

            if (target != null) {
                Vector2 toTarget = Projectile.Center.To(target.Center);
                Projectile.EntityToRot(toTarget.ToRotation(), 0.1f);
            }

            if (Status == 0) {
                if (ThisTimeValue > 5) {
                    if (Behavior < 12 && Projectile.IsOwnedByLocalPlayer()) {
                        Vector2 spanPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 160;
                        Projectile.NewProjectile(
                            Projectile.parent(),
                            spanPos,
                            Projectile.velocity,
                            Type,
                            Projectile.damage,
                            Projectile.knockBack,
                            -1,
                            ai1: Behavior
                            );
                    }
                    Status = 1;
                }
            }

            if (Status == 1) {
                upPos = true;
                Projectile.velocity = Projectile.rotation.ToRotationVector2() * Projectile.localAI[2];
                Projectile.localAI[2] *= 0.98f;
            }

            if (!CWRUtils.isServer) {
                CWRUtils.ClockFrame(ref Projectile.frameCounter, 4, 6);
                for (int i = 0; i < 6; i++) {
                    if (i >= 0 && i < ncbs.Count) {
                        Ncb _ncb = ncbs[i];
                        CWRUtils.ClockFrame(ref _ncb.frame, 4, 6);
                        _ncb.pos = Projectile.Center + randomOffsetVr[i];
                        ncbs[i] = _ncb;
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, Color.Gold.ToVector3() * 3);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return CWRUtils.CircularHitboxCollision(Projectile.Center, 72, targetHitbox);
        }

        int dorFireType => ModContent.BuffType<Dragonfire>();
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.damage -= 15;
            Projectile.timeLeft -= 10;
            Projectile.localNPCHitCooldown += 10;
            target.AddBuff(dorFireType, 180);
            base.OnHitNPC(target, hit, damageDone);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Projectile.damage -= 55;
            Projectile.timeLeft -= 15;
            Projectile.localNPCHitCooldown += 30;
            target.AddBuff(dorFireType, 60);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture);
            float slp = Projectile.timeLeft / 60f;
            if (slp > 1) slp = 1;

            Main.EntitySpriteDraw(
                mainValue,
                CWRUtils.WDEpos(Projectile.Center),
                CWRUtils.GetRec(mainValue, Projectile.frameCounter, 7),
                Color.White,
                Projectile.rotation,
                CWRUtils.GetOrig(mainValue, 7),
                Projectile.scale * slp,
                SpriteEffects.None,
                0
                );

            for (int j = 0; j < 6; j++) {
                if (j >= 0 && j < ncbs.Count) {
                    Ncb _ncb = ncbs[j];
                    Main.EntitySpriteDraw(
                    mainValue,
                    CWRUtils.WDEpos(_ncb.pos),
                    CWRUtils.GetRec(mainValue, _ncb.frame, 7),
                    Color.White,
                    Projectile.rotation,
                    CWRUtils.GetOrig(mainValue, 7),
                    Projectile.scale * slp,
                    SpriteEffects.None,
                    0
                    );
                }
            }

            return false;
        }
    }
}
