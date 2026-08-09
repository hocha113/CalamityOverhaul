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
    internal class LonginusThrow : ModProjectile, IPrimitiveDrawable, IWarpDrawable
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
        private bool impactHandled;
        private Vector2 impactDir;
        private Vector2 impactPos;
        private Vector2[] fadePoints;
        private int fadeCount;
        private Vector2[] renderPoints;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 45;
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

            //飞行轨迹全交给螺旋条带，不再剥落粒子

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

        /// <summary>命中点缀，收势首帧各端各自播放；碎面为主替代旧星屑</summary>
        private void ImpactGarnish() {
            int shardCount = StealthStrike ? 24 : 18;
            for (int i = 0; i < shardCount; i++) {
                Vector2 v = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 11f) - impactDir * Main.rand.NextFloat(1f, 5f);
                PRTLoader.NewParticle<PRT_ATShard>(impactPos + Main.rand.NextVector2Circular(20f, 20f), v
                    , Main.rand.NextBool() ? LonginusVFX.Amber : LonginusVFX.HolyGold, Main.rand.NextFloat(0.7f, 1.5f))
                    ?.Configure(Main.rand.Next(24, 42), Main.rand.NextFloat(-0.22f, 0.22f));
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Light>(impactPos, Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 12f)
                    , Main.rand.NextBool() ? Color.Red : Color.OrangeRed, Main.rand.NextFloat(0.6f, 1.6f))?.Configure(40, 1, 1.5f, hueShift: 0.0f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //Explode 内部 Damage() 会嵌套回调本钩子且 numHits 未自增，布尔防重入
            if (Projectile.numHits > 0 || FadePhase > 0 || impactHandled) {
                return;
            }
            impactHandled = true;

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
                //快照未就绪(EnterFade 与首个 AI tick 之间)不画，防画在原点
                if (fadePoints == null) {
                    return;
                }
                float p = MathHelper.Clamp(fadeTick / 70f, 0f, 1f);
                //尾迹残躯尾先碎
                if (fadeCount >= 3) {
                    LonginusVFX.DrawHelixTrail(fadePoints, fadeCount, 17f, 26f, spinPhase, p
                        , 0.95f * (1f - p * 0.35f), 0.3f, 2.1f, withWake: true);
                }
                if (FadePhase == 1) {
                    //AT力场闪现碎裂与十字爆闪
                    float spread = MathHelper.Clamp(p * 4.5f, 0f, 1f);
                    float shatter = MathHelper.Clamp((p - 0.30f) * 2.4f, 0f, 1f);
                    float fieldAlpha = 0.72f * (1f - p);
                    float radius = StealthStrike ? 260f : 190f;
                    LonginusVFX.DrawATField(impactPos + impactDir * 12f, -impactDir, radius, spread, shatter
                        , fieldAlpha, 3, Projectile.identity * 0.173f, 0.5f);
                    float crossGrow = MathHelper.Clamp(p * 3.6f, 0f, 1f);
                    float crossDis = MathHelper.Clamp((p - 0.4f) * 1.9f, 0f, 1f);
                    LonginusVFX.DrawCross(impactPos, -Vector2.UnitY, StealthStrike ? 460f : 300f
                        , StealthStrike ? 210f : 140f, crossGrow, crossDis, 0.85f * (1f - p * 0.35f), 0.13f, 0.9f * (1f - p));
                    //命中点残留圣痕光轮
                    float haloReveal = MathHelper.Clamp(p * 5f, 0f, 1f);
                    LonginusVFX.DrawHalo(impactPos, StealthStrike ? 58f : 42f, 0.40f, haloReveal
                        , 0.4f * (1f - p), 0.8f * (1f - p * 0.5f));
                }
                return;
            }

            //飞行中实时双螺旋尾迹：宽幅体股垫底 + 双股缠绕
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
                LonginusVFX.DrawHelixTrail(renderPoints, count, 17f + ratio * 13f, 26f + ratio * 6f
                    , spinPhase, 0f, 0.95f, 0.25f + ratio * 0.5f, 2.1f, withWake: true);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (FadePhase > 0) {
                return false;
            }
            Texture2D value = TextureAssets.Item[SpearOfLonginus.ID].Value;
            float speed = Projectile.velocity.Length();
            float ratio = initSpeed > 0 ? MathHelper.Clamp((speed - initSpeed) / MathHelper.Max(initSpeed * 1.1f, 1f), 0f, 1f) : 0f;
            float rot = Projectile.rotation + MathHelper.PiOver4;
            Vector2 origin = value.Size() / 2;

            //四重速度残影
            for (int g = 4; g >= 1; g--) {
                int idx = g * 4;
                if (idx < Projectile.oldPos.Length && Projectile.oldPos[idx] != Vector2.Zero) {
                    Vector2 gpos = Projectile.oldPos[idx] + Projectile.Size / 2f - Main.screenPosition;
                    Color gc = (LonginusVFX.Crimson with { A = 0 }) * ((0.42f - g * 0.09f) * (0.7f + ratio * 0.5f));
                    Main.EntitySpriteDraw(value, gpos, null, gc, rot
                        , origin, Projectile.scale * (0.9f - g * 0.03f), SpriteEffects.None, 0);
                }
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            //绯红自发光鞘与白热芯，随速度增强
            Color sheath = (LonginusVFX.Crimson with { A = 0 }) * (0.50f + ratio * 0.30f);
            Main.EntitySpriteDraw(value, drawPos, null, sheath, rot, origin, Projectile.scale * 1.06f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(value, drawPos, null, Color.White
                , rot, origin, Projectile.scale * 0.9f, SpriteEffects.None, 0);
            Color hotCore = (Color.White with { A = 0 }) * (0.16f + ratio * 0.30f);
            Main.EntitySpriteDraw(value, drawPos, null, hotCore, rot, origin, Projectile.scale * 0.92f, SpriteEffects.None, 0);
            return false;
        }

        bool IWarpDrawable.CanDrawCustom() => false;

        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) { }

        /// <summary>命中收势首段的小型屏幕涟漪</summary>
        void IWarpDrawable.Warp() {
            if (FadePhase != 1 || fadePoints == null || fadeTick > 24) {
                return;
            }
            float p = MathHelper.Clamp(fadeTick / 24f, 0f, 1f);
            float size = (StealthStrike ? 260f : 190f) * 4.5f;
            NeutronWarpHelper.DrawWarp(impactPos, size, size, 0.30f * (1f - p * 0.5f), p, 0f, "ShockwaveRing", 0.40f);
        }
    }
}
