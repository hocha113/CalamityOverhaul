using CalamityMod.NPCs.DevourerofGods;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.Longinus
{
    internal class LonginusThrow : ModProjectile
    {
        public override string Texture => CWRConstant.Item + "Rogue/Longinus";
        public Player Owner => Main.player[Projectile.owner];
        private bool SpanPrmst = true;
        private bool StealthStrike => Projectile.ai[0] > 0;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = CWRLoad.RogueDamageClass;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.penetrate = 1;
            Projectile.MaxUpdates = 5;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.timeLeft % 10 == 0) {
                BasePRT pulse = new PRT_DWave(Projectile.Center - Projectile.velocity * 0.52f, Projectile.velocity / 1.5f, Color.Red, new Vector2(1f, 2f) * 0.8f, Projectile.velocity.ToRotation(), 0.82f, 0.32f, 60);
                PRTLoader.AddParticle(pulse);
                BasePRT pulse2 = new PRT_DWave(Projectile.Center - Projectile.velocity * 0.40f, Projectile.velocity / 1.5f * 0.9f, Color.Gold, new Vector2(0.8f, 1.5f) * 0.8f, Projectile.velocity.ToRotation(), 0.58f, 0.28f, 50);
                PRTLoader.AddParticle(pulse2);
                BasePRT pulse3 = new PRT_DWave(Projectile.Center - Projectile.velocity * 0.35f, Projectile.velocity / 1.5f * 0.8f, Color.DarkRed, new Vector2(0.7f, 1.3f) * 0.8f, Projectile.velocity.ToRotation(), 0.58f, 0.22f, 40);
                PRTLoader.AddParticle(pulse3);
                PRT_HeavenfallStar spark = new PRT_HeavenfallStar(Projectile.Center, Projectile.velocity, false, 27, 3, Color.Gold);
                PRTLoader.AddParticle(spark);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int spanPrestMaxWid = (int)(Projectile.width * Projectile.scale);
            if (Projectile.numHits == 0) {
                if (SpanPrmst) {
                    if (StealthStrike) {
                        for (int i = 0; i < 4; i++) {
                            float rot = MathHelper.PiOver2 * i;
                            Vector2 vr = rot.ToRotationVector2() * 10;
                            for (int j = 0; j < 16; j++) {
                                float slp = j / 16f;
                                float slp2 = 16f / j;
                                Vector2 spanPos = Projectile.Center + rot.ToRotationVector2() * 64 * j;
                                BasePRT pulse = new PRT_DWave(spanPos - vr * 0.52f, vr / 1.5f, Color.Red, new Vector2(1f, 2f), vr.ToRotation(), 0.82f * slp, 0.32f * slp2, 60);
                                PRTLoader.AddParticle(pulse);
                                BasePRT pulse2 = new PRT_DWave(spanPos - vr * 0.40f, vr / 1.5f * 0.9f, Color.Gold, new Vector2(0.8f, 1.5f), vr.ToRotation(), 0.58f * slp, 0.28f * slp2, 50);
                                PRTLoader.AddParticle(pulse2);
                            }
                            SoundEngine.PlaySound(DevourerofGodsHead.DeathExplosionSound, Projectile.Center);
                            SoundEngine.PlaySound(SpearOfLonginus.BelCanto, Projectile.Center);
                            SpanPrmst = false;
                            Projectile.Explode(620);
                        }
                        if (target.CWR().LonginusSign) {
                            Projectile.NewProjectile(Projectile.FromObjectGetParent(), target.Center, Vector2.Zero, ModContent.ProjectileType<PilgrimsFury>(), Projectile.damage, 0, Projectile.owner, 0, target.whoAmI);
                        }
                        else {
                            SpanSoulSeeker(target.Center);
                        }
                    }
                    else {
                        for (int i = 0; i < 4; i++) {
                            float rot = MathHelper.PiOver2 * i + MathHelper.PiOver4;
                            Vector2 vr = rot.ToRotationVector2() * 10;
                            for (int j = 0; j < 134; j++) {
                                Vector2 pos = Projectile.Center + new Vector2(Main.rand.Next(-spanPrestMaxWid, spanPrestMaxWid), Main.rand.Next(-spanPrestMaxWid, spanPrestMaxWid));
                                Vector2 particleSpeed = pos.To(Projectile.Center + vr * 130).UnitVector() * Main.rand.NextFloat(11.3f, 54f);
                                BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                                    , Main.rand.NextFloat(0.3f, 2.5f), Main.rand.NextBool(2) ? Color.Red : Color.DarkRed, 60, 1, 1.5f, hueShift: 0.0f);
                                PRTLoader.AddParticle(energyLeak);
                            }
                        }
                        for (int j = 0; j < 64; j++) {
                            Vector2 pos = Projectile.Center;
                            Vector2 particleSpeed = Main.rand.NextVector2Unit() * Main.rand.NextFloat(5.3f, 24f);
                            BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                                , Main.rand.NextFloat(0.3f, 2.5f), Main.rand.NextBool(2) ? Color.Red : Color.Gold, 90, 1, 1.5f, hueShift: 0.0f);
                            PRTLoader.AddParticle(energyLeak);
                        }
                        for (int i = 0; i < 136; i++) {
                            Color outerSparkColor = VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Gold);
                            Vector2 vector = Main.rand.NextVector2Unit() * Main.rand.Next(77);
                            float scaleBoost = MathHelper.Clamp(Main.rand.NextFloat(), 0f, 2f);
                            float outerSparkScale = 3.2f + scaleBoost;
                            PRT_HeavenfallStar spark = new PRT_HeavenfallStar(Projectile.Center, vector, false, 27, outerSparkScale, outerSparkColor);
                            PRTLoader.AddParticle(spark);

                            Color innerSparkColor = VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Goldenrod, Color.Red);
                            float innerSparkScale = 0.6f + scaleBoost;
                            PRT_HeavenfallStar spark2 = new PRT_HeavenfallStar(Projectile.Center, vector, false, 37, innerSparkScale, innerSparkColor);
                            PRTLoader.AddParticle(spark2);
                        }
                        SoundEngine.PlaySound(DevourerofGodsHead.DeathExplosionSound, Projectile.Center);
                        SpanPrmst = false;
                        Projectile.Explode(320);
                    }
                }

            }
        }

        public void SpanSoulSeeker(Vector2 spanPos) {
            SoundEngine.PlaySound(CosmicCalamityProjectile.BelCanto with { Volume = 0.5f, Pitch = 0.2f });
            if (Projectile.IsOwnedByLocalPlayer()) {
                NPC hasGSignTarget = null;
                foreach (NPC npc in Main.npc) {
                    if (npc.type == NPCID.None) {
                        continue;
                    }
                    if (npc.CWR().LonginusSign) {
                        hasGSignTarget = npc;
                    }
                }
                if (hasGSignTarget != null) {
                    for (int i = 0; i < 13; i++) {
                        Vector2 vr = (MathHelper.TwoPi / 13 * i).ToRotationVector2() * 23;
                        Projectile.NewProjectile(Projectile.FromObjectGetParent(), spanPos, vr, ModContent.ProjectileType<SoulSeeker>(), Projectile.damage, 0, Projectile.owner, hasGSignTarget.whoAmI);
                    }
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (StealthStrike && Projectile.numHits == 0) {
                SpanSoulSeeker(Owner.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Item[SpearOfLonginus.ID].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White
                , Projectile.rotation + MathHelper.PiOver4, value.Size() / 2, Projectile.scale * 0.9f, SpriteEffects.None, 0);
            return false;
        }
    }
}
