using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses
{
    internal class LonginusThrow : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item + "Melee/Longinus";
        public Player Owner => Main.player[Projectile.owner];
        private bool StealthStrike => Projectile.ai[0] > 0;
        /// <summary>0=飞行 1=命中收势 2=射程耗尽收势，进位后尾迹残存侵蚀</summary>
        private float FadePhase {
            get => Projectile.ai[2];
            set => Projectile.ai[2] = value;
        }

        private float initSpeed;
        private float spinPhase;
        private int fadeTick;
        private Vector2 impactDir;
        private Vector2 impactPos;
        private Vector2[] fadePoints;
        private int fadeCount;
        private Vector2[] renderPoints;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 26;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 5;
            Projectile.timeLeft = 300;
        }

        public override bool ShouldUpdatePosition() => FadePhase == 0;

        public override void AI() {
            if (FadePhase > 0) {
                UpdateFade();
                return;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            if (initSpeed == 0) {
                initSpeed = Projectile.velocity.Length();
                spinPhase = Projectile.identity * 2.399f;
            }

            //飞行中复合加速，出手后持续拧紧
            float speed = Projectile.velocity.Length();
            float maxSpeed = initSpeed * 2.1f;
            if (speed < maxSpeed) {
                Projectile.velocity *= 1.009f;
            }
            float ratio = MathHelper.Clamp((speed - initSpeed) / MathHelper.Max(initSpeed * 1.1f, 1f), 0f, 1f);
            spinPhase += 0.07f + ratio * 0.06f;

            Lighting.AddLight(Projectile.Center, 0.85f, 0.24f, 0.18f);

            //破空声随速度提调
            if (Projectile.timeLeft % 60 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_JavelinThrowersAttack with { Volume = 0.35f, Pitch = -0.3f + ratio * 0.7f }, Projectile.Center);
            }

            //星屑剥落率随速度
            if (Main.rand.NextBool(ratio > 0.5f ? 2 : 3)) {
                Vector2 shedPos = Projectile.Center - Projectile.velocity * 0.6f + Main.rand.NextVector2Circular(6f, 6f);
                PRTLoader.NewParticle<PRT_LonginusStar>(shedPos, -Projectile.velocity * 0.05f, Color.Gold
                    , Main.rand.NextFloat(0.5f, 0.8f))?.Configure(false, Main.rand.Next(14, 20));
            }

            //射程耗尽转收势，让尾迹残存
            if (Projectile.timeLeft <= 2) {
                EnterFade(2);
            }
        }

        /// <summary>进入收势：伤害关闭，位置冻结，尾迹快照待侵蚀</summary>
        private void EnterFade(int phase) {
            FadePhase = phase;
            Projectile.friendly = false;
            Projectile.timeLeft = 80;
            Projectile.netUpdate = true;
        }

        private void UpdateFade() {
            if (fadePoints == null) {
                impactDir = Projectile.velocity.UnitVector();
                if (impactDir == Vector2.Zero) {
                    impactDir = Vector2.UnitX;
                }
                impactPos = Projectile.Center;
                SnapshotTrail();
                Projectile.friendly = false;
                if (FadePhase == 1 && !Main.dedServ) {
                    ImpactGarnish();
                }
            }
            fadeTick++;
            if (FadePhase == 1) {
                float glow = 1f - fadeTick / 70f;
                if (glow > 0) {
                    Lighting.AddLight(impactPos, 1.1f * glow, 0.6f * glow, 0.25f * glow);
                }
            }
            if (Projectile.IsOwnedByLocalPlayer() && fadeTick >= 70) {
                Projectile.Kill();
            }
        }

        private void SnapshotTrail() {
            fadePoints = new Vector2[Projectile.oldPos.Length];
            fadeCount = 0;
            Vector2 half = Projectile.Size / 2f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    break;
                }
                fadePoints[fadeCount++] = Projectile.oldPos[i] + half;
            }
        }

        /// <summary>命中点缀粒子，收势首帧各端各自播放</summary>
        private void ImpactGarnish() {
            for (int i = 0; i < 16; i++) {
                Vector2 v = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 13f);
                PRTLoader.NewParticle<PRT_LonginusStar>(impactPos, v, Color.Gold
                    , Main.rand.NextFloat(0.7f, 1.1f))?.Configure(true, Main.rand.Next(18, 30));
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Light>(impactPos, Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 9f)
                    , Main.rand.NextBool() ? Color.Red : Color.OrangeRed, Main.rand.NextFloat(0.4f, 1.2f))?.Configure(40, 1, 1.5f, hueShift: 0.0f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits > 0 || FadePhase > 0) {
                return;
            }
            Projectile.friendly = false;

            if (StealthStrike) {
                SoundEngine.PlaySound("CalamityMod/Sounds/NPCKilled/DevourerDeathImpact".GetSound(), Projectile.Center);
                SoundEngine.PlaySound(SpearOfLonginus.BelCanto, Projectile.Center);
                Projectile.Explode(620);
                if (target.CWR().LonginusSign) {
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(), target.Center, Vector2.Zero
                        , ModContent.ProjectileType<PilgrimsFury>(), Projectile.damage, 0, Projectile.owner, 0, target.whoAmI);
                }
                else {
                    SpanSoulSeeker();
                }
            }
            else {
                SoundEngine.PlaySound("CalamityMod/Sounds/NPCKilled/DevourerDeathImpact".GetSound(), Projectile.Center);
                Projectile.Explode(320);
            }
            EnterFade(1);
        }

        public void SpanSoulSeeker() => SoundEngine.PlaySound(CWRSound.BelCanto with { Volume = 0.5f, Pitch = 0.2f });

        public override void OnKill(int timeLeft) {
            if (StealthStrike && Projectile.numHits == 0) {
                SpanSoulSeeker();
            }
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (FadePhase > 0) {
                float p = MathHelper.Clamp(fadeTick / 70f, 0f, 1f);
                //尾迹残躯尾先碎
                if (fadePoints != null && fadeCount >= 3) {
                    LonginusVFX.DrawHelixTrail(fadePoints, fadeCount, 8f, 15f, spinPhase, p, 0.85f * (1f - p * 0.4f));
                }
                if (FadePhase == 1) {
                    //轻量AT力场闪现波纹与小十字爆闪
                    float spread = MathHelper.Clamp(p * 4.5f, 0f, 1f);
                    float shatter = MathHelper.Clamp((p - 0.30f) * 2.4f, 0f, 1f);
                    float fieldAlpha = 0.6f * (1f - p);
                    float radius = StealthStrike ? 190f : 130f;
                    LonginusVFX.DrawATField(impactPos + impactDir * 12f, -impactDir, radius, spread, shatter
                        , fieldAlpha, 2, Projectile.identity * 0.173f, 0.5f);
                    float crossGrow = MathHelper.Clamp(p * 3.6f, 0f, 1f);
                    float crossDis = MathHelper.Clamp((p - 0.4f) * 1.9f, 0f, 1f);
                    LonginusVFX.DrawCross(impactPos, -Vector2.UnitY, StealthStrike ? 260f : 170f
                        , StealthStrike ? 120f : 80f, crossGrow, crossDis, 0.75f * (1f - p * 0.4f), 0.20f, 0.5f);
                }
                return;
            }

            //飞行中实时双螺旋尾迹
            renderPoints ??= new Vector2[Projectile.oldPos.Length];
            int count = 0;
            Vector2 half = Projectile.Size / 2f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    break;
                }
                renderPoints[count++] = Projectile.oldPos[i] + half;
            }
            if (count >= 3) {
                float speed = Projectile.velocity.Length();
                float ratio = initSpeed > 0 ? MathHelper.Clamp((speed - initSpeed) / MathHelper.Max(initSpeed * 1.1f, 1f), 0f, 1f) : 0f;
                LonginusVFX.DrawHelixTrail(renderPoints, count, 7f + ratio * 5f, 15f, spinPhase, 0f, 0.85f, 0.2f + ratio * 0.4f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (FadePhase > 0) {
                return false;
            }
            Texture2D value = TextureAssets.Item[SpearOfLonginus.ID].Value;
            //速度残影
            for (int g = 2; g >= 1; g--) {
                int idx = g * 5;
                if (idx < Projectile.oldPos.Length && Projectile.oldPos[idx] != Vector2.Zero) {
                    Vector2 gpos = Projectile.oldPos[idx] + Projectile.Size / 2f - Main.screenPosition;
                    Color gc = (LonginusVFX.Crimson with { A = 0 }) * (0.32f - g * 0.11f);
                    Main.EntitySpriteDraw(value, gpos, null, gc, Projectile.rotation + MathHelper.PiOver4
                        , value.Size() / 2, Projectile.scale * (0.9f - g * 0.04f), SpriteEffects.None, 0);
                }
            }
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White
                , Projectile.rotation + MathHelper.PiOver4, value.Size() / 2, Projectile.scale * 0.9f, SpriteEffects.None, 0);
            return false;
        }
    }
}
