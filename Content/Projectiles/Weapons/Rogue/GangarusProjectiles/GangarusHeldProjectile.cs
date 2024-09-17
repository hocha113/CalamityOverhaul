using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Rogue.Extras;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.GangarusProjectiles
{
    internal class GangarusHeldProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Item + "Rogue/Gangarus";
        private Player Owner => Main.player[Projectile.owner];
        private Vector2 toMou = Vector2.Zero;
        private int Time {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = ModContent.GetInstance<RogueDamageClass>();
            Projectile.penetrate = -1;
            Projectile.hide = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.WriteVector2(toMou);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            toMou = reader.ReadVector2();
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => false;

        private bool isProj() => (Main.projectile.Count((Projectile p)
                => p.type == ModContent.ProjectileType<GangarusProjectile>()
                && p.Center.To(Owner.Center).LengthSquared() < 9000) == 0);

        public override void AI() {
            if (Owner.HeldItem.type != CWRLoad.Gangarus || !isProj()) {
                Projectile.Kill();
                return;
            }
            Projectile.velocity = Projectile.rotation.ToRotationVector2();
            if (Projectile.IsOwnedByLocalPlayer()) {
                StickToOwner();
                Charge();
            }
            NPC npc = Projectile.Center.FindClosestNPC(1900);
            int slp = Time;
            if (slp > 600)
                slp = 600;
            if (npc != null) {
                if (Time % 30 == 0) {
                    Vector2 vr = new Vector2(0, 13);
                    PRT_GangarusWave pulse = new PRT_GangarusWave(npc.Center + new Vector2(0, -360), vr, Color.Red, new Vector2(1.2f, 3f) * 0.6f, vr.ToRotation(), 0.32f, 0.82f + (slp * 0.001f), 180, npc);
                    PRTLoader.AddParticle(pulse);
                    Vector2 vr2 = new Vector2(0, -13);
                    PRT_GangarusWave pulse2 = new PRT_GangarusWave(npc.Center + new Vector2(0, 360), vr2, Color.Red, new Vector2(1.2f, 3f) * 0.6f, vr2.ToRotation(), 0.32f, 0.82f + (slp * 0.001f), 180, npc);
                    PRTLoader.AddParticle(pulse2);
                }
                npc.CWR().GangarusSign = true;
                foreach (NPC overNPC in Main.npc) {
                    if (overNPC.whoAmI != npc.whoAmI && overNPC.type != NPCID.None) {
                        overNPC.CWR().GangarusSign = false;
                    }
                }
            }
            Time++;
        }

        public void Charge() {
            Gangarus gangarus = (Gangarus)Owner.HeldItem.ModItem;
            CalamityPlayer modPlayer = Owner.Calamity();
            if (modPlayer.rogueStealth > 0) {
                Vector2 spanStarPos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(33) + Projectile.velocity * 55;
                Vector2 vr = spanStarPos.To(Projectile.velocity * 198 + Projectile.Center).UnitVector() * 3;
                PRT_GangarusStar spark = new PRT_GangarusStar(spanStarPos, vr, false, Main.rand.Next(17, 25), Main.rand.NextFloat(0.9f, 1.1f), Color.Red, Projectile);
                PRTLoader.AddParticle(spark);
                if (modPlayer.rogueStealth >= modPlayer.rogueStealthMax && gangarus.ChargeGrade < 6) {
                    gangarus.ChargeGrade += 1;
                    SoundStyle lightningStrikeSound = HeavenlyGale.LightningStrikeSound;
                    lightningStrikeSound.Volume = 0.25f;
                    SoundEngine.PlaySound(lightningStrikeSound, Projectile.Center);
                    SoundEngine.PlaySound(HeavenlyGale.FireSound, Projectile.Center);
                    for (int i = 0; i < gangarus.ChargeGrade; i++) {
                        PRT_GangarusWave pulse = new PRT_GangarusWave(Projectile.Center + Projectile.velocity * (-0.52f + i * 23), Projectile.velocity / 1.5f, Color.Red, new Vector2(1.5f, 3f) * (0.8f - i * 0.1f), Projectile.velocity.ToRotation(), 0.82f, 0.32f, 60, Projectile);
                        PRTLoader.AddParticle(pulse);
                    }
                    if (gangarus.ChargeGrade > 6)
                        gangarus.ChargeGrade = 6;
                    modPlayer.rogueStealth = 0;
                }
            }
        }

        public void StickToOwner() {
            toMou = Owner.Center.To(Main.MouseWorld);
            Owner.heldProj = Projectile.whoAmI;
            Projectile.rotation = toMou.ToRotation();
            Owner.direction = Math.Sign(toMou.X);
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + toMou.UnitVector() * 70;
            Projectile.timeLeft = 2;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Item[CWRLoad.Gangarus].Value;
            int dir = Owner.direction;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition + Owner.CWR().SpecialDrawPositionOffset, null, lightColor
                , Projectile.rotation + MathHelper.PiOver4 + (dir > 0 ? MathHelper.PiOver2 : 0)
                , value.Size() / 2, Projectile.scale * 0.9f, dir > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }
}
