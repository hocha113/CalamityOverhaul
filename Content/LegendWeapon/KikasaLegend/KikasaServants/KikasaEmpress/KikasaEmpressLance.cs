using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEmpress
{
    /// <summary>
    /// 女皇鬼奴的圣舞血矛：借原版 FairyQueenLance 贴图的血水凝矛。
    /// 整排七根一帧布阵在女皇头顶冠状弧线上（ai0=席位 ai1/ai2=各自落点），
    /// 虚影按席位逐根点亮（弧线阵列预告拍），然后按同一顺序逐根激发俯冲，
    /// 落点沿目标横向排开成列；落水成整排错拍水花与涟漪列，图案的收尾也要美
    /// </summary>
    internal class KikasaEmpressLance : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>首根激发帧（自矛生成起算）；女皇的下挥脉冲与此对拍</summary>
        internal const int IgniteStart = 52;

        /// <summary>相邻席位的激发间隔</summary>
        internal const int IgniteGap = 5;

        /// <summary>逐根点亮的间隔</summary>
        private const int LightUpGap = 6;

        /// <summary>激发一帧定速</summary>
        private const float DiveSpeed = 27f;

        /// <summary>席位号：点亮与激发次序的确定性来源</summary>
        private ref float Slot => ref Projectile.ai[0];

        private ref float AimX => ref Projectile.ai[1];
        private ref float AimY => ref Projectile.ai[2];

        private ref float Life => ref Projectile.localAI[0];

        /// <summary>本席激发帧</summary>
        private int IgniteFrame => IgniteStart + (int)Slot * IgniteGap;

        /// <summary>本席点亮帧</summary>
        private int LightFrame => (int)Slot * LightUpGap;

        private bool litDone;
        private bool ignited;
        private bool burstDone;
        private bool lakeSwallowed;

        private static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color PearlBright => KikasaDomain.CoolTint(new(246, 170, 150), new(180, 204, 208));

        private Vector2 AimPoint => new(AimX, AimY);

        /// <summary>虚影亮度：未点亮 0.26，点亮后爬到 0.66，激发瞬间 1</summary>
        private float GhostLight() {
            if (ignited) {
                return 1f;
            }
            if (Life < LightFrame) {
                return 0.26f;
            }
            return MathHelper.Lerp(0.4f, 0.66f, MathHelper.Clamp((Life - LightFrame) / 8f, 0f, 1f));
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 240;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 260;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>预告期不可伤：伤害窗与可见的俯冲严格对齐</summary>
        public override bool? CanDamage() => ignited ? null : false;

        /// <summary>矛体线碰撞：沿 rotation ±40，与原版矛同款</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!ignited) {
                return false;
            }
            float _ = 0f;
            Vector2 axis = Projectile.rotation.ToRotationVector2();
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - axis * 40f, Projectile.Center + axis * 40f, 10f, ref _);
        }

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];

            if (Life <= 1f) {
                //出生即朝向落点，虚影不从零角瞬跳
                Projectile.rotation = (AimPoint - Projectile.Center).ToRotation();
            }

            if (!ignited) {
                //预告期：悬停呼吸，矛尖缓缓校向落点，转率随临近激发收紧（锁线）
                Projectile.velocity = Vector2.Zero;
                float want = (AimPoint - Projectile.Center).ToRotation();
                float k = MathHelper.Clamp(Life / IgniteFrame, 0f, 1f);
                Projectile.rotation = Projectile.rotation.AngleLerp(want, 0.06f + 0.14f * k);

                //点亮拍：铃音单音上阶 + 珠光一闪
                if (!litDone && Life >= LightFrame) {
                    litDone = true;
                    SoundEngine.PlaySound(SoundID.Item161 with {
                        Volume = 0.32f,
                        Pitch = -0.35f + (int)Slot * 0.09f,
                        MaxInstances = 3
                    }, Projectile.Center);
                    if (!Main.dedServ) {
                        PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, Vector2.Zero,
                            KikasaEmpressServant.IridescentTint(Slot * 0.14f) * 0.6f,
                            Main.rand.NextFloat(0.35f, 0.5f))?.Configure(PearlBright * 0.5f, 16, 0.02f, 0.7f);
                        for (int i = 0; i < 3; i++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                                Main.rand.NextVector2Circular(0.6f, 0.6f),
                                BloodMain * 0.5f, Main.rand.NextFloat(0.24f, 0.4f))?.Configure(Main.rand.Next(10, 18));
                        }
                    }
                }

                //激发拍：一帧定速俯冲，不做斜坡；owner 盖章校准远端的时差漂移
                //（不开引擎地形碰撞，湖下真地形被湖面盖住，钉地走 AI 内手动检测）
                if (Life >= IgniteFrame) {
                    ignited = true;
                    Vector2 dive = (AimPoint - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    Projectile.velocity = dive * DiveSpeed;
                    Projectile.rotation = dive.ToRotation();
                    if (Main.myPlayer == Projectile.owner) {
                        Projectile.netUpdate = true;
                    }
                    SoundEngine.PlaySound(SoundID.Item162 with {
                        Volume = 0.5f,
                        Pitch = -0.05f + (int)Slot * 0.03f,
                        MaxInstances = 3
                    }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.3f, Pitch = 0.15f, MaxInstances = 3 }, Projectile.Center);
                }
                return;
            }

            //俯冲：复利续力，直才快
            Projectile.velocity *= 1.02f;
            if (Projectile.velocity.Length() > 44f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * 44f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //沿途甩出速度拉伸的血珠
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center - Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(6f, 6f),
                    -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * 0.55f,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 18));
            }

            float glow = 0.5f;
            Lighting.AddLight(Projectile.Center, 0.5f * glow, 0.14f * glow, 0.17f * glow);

            //落水收尾：整排矛逐根扎进湖面，水花与涟漪排成列
            bool lakeAlive = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            KikasaDomainPlayer kdp = lakeAlive ? owner.GetModPlayer<KikasaDomainPlayer>() : null;
            if (lakeAlive && Projectile.Center.Y >= kdp.LakeWorldY - 2f) {
                lakeSwallowed = true;
                Vector2 hit = new(Projectile.Center.X, kdp.LakeWorldY);
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.55f,
                    Pitch = -0.2f + (int)Slot * 0.04f,
                    MaxInstances = 3
                }, hit);
                if (!Main.dedServ && KikasaDomain.Viewed == kdp) {
                    KikasaDomainDeco.SplashAt(hit, 7);
                    KikasaDomainDeco.RippleAt(hit, 1.0f);
                    //入水角的斜向水花束
                    Vector2 inDir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(hit,
                            new Vector2(inDir.X * Main.rand.NextFloat(0.5f, 1.6f), -Main.rand.NextFloat(2.4f, 5f)),
                            BloodMain * 0.6f, Main.rand.NextFloat(0.32f, 0.55f))?.Configure(Main.rand.Next(18, 30));
                    }
                }
                Projectile.Kill();
                return;
            }

            //钉进地面（机制身份保留）：手动地形检测替代 tileCollide
            //湖线以下的真地形被湖面盖住，交给上面的落水收尾
            if ((!lakeAlive || Projectile.Center.Y < kdp.LakeWorldY - 2f)
                && Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                burstDone = true;
                KikasaEyeBloodShot.SplashBurst(Projectile.Center, Projectile.velocity, onTile: true);
                Projectile.Kill();
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            if (!burstDone) {
                //命中 NPC / 超时坠灭共用
                KikasaEyeBloodShot.SplashBurst(Projectile.Center, Projectile.velocity, onTile: false);
            }
            //矛身失压散珠
            Vector2 axis = Projectile.rotation.ToRotationVector2();
            for (int i = -2; i <= 2; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + axis * i * 16f + Main.rand.NextVector2Circular(4f, 4f),
                    Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.28f, 0.5f))?.Configure(Main.rand.Next(14, 24));
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.FairyQueenLance);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.FairyQueenLance]?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (tex == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;
            float light = GhostLight();
            float fade = MathHelper.Clamp(Life / 6f, 0f, 1f);

            //预告期绘制位带呼吸浮动（物理位不动，图案不散）
            Vector2 bob = ignited ? Vector2.Zero
                : new Vector2(0f, MathF.Sin(Life * 0.09f + Slot * 1.3f) * 3.5f);
            Vector2 pos = Projectile.Center + bob - Main.screenPosition;
            Vector2 axis = Projectile.rotation.ToRotationVector2();

            //瞄准线：点亮后自矛尖向落点淡入，激发前最亮（预告的读线）
            if (!ignited && glow != null && litDone) {
                float near = MathHelper.Clamp((Life - LightFrame) / 10f, 0f, 1f)
                    * MathHelper.Lerp(0.14f, 0.4f, MathHelper.Clamp((Life - (IgniteFrame - 14)) / 14f, 0f, 1f));
                float lineLen = MathF.Min(Vector2.Distance(Projectile.Center, AimPoint), 900f);
                Vector2 lineScale = new(lineLen / glow.Width, 5f / glow.Height);
                Color lineColor = (BloodMain with { A = 0 }) * near;
                sb.Draw(glow, pos + axis * lineLen * 0.5f, null, lineColor, Projectile.rotation,
                    glow.Size() * 0.5f, lineScale, SpriteEffects.None, 0f);
            }

            //俯冲残影列：拖在矛后的渐隐队列（原版矛语法）
            if (ignited) {
                float speedK = MathHelper.Clamp(Projectile.velocity.Length() / DiveSpeed, 0f, 1f);
                for (float k = 1f; k > 0f; k -= 1f / 5f) {
                    Vector2 off = -axis * 96f * k * speedK;
                    sb.Draw(tex, pos + off, null, BloodDeep * (0.5f * (1f - k) * fade), Projectile.rotation,
                        origin, 1f, SpriteEffects.None, 0f);
                }
            }

            //矛体三层：暗血描边 → 血主体 → 珠光矛芯
            for (float a = 0f; a < 1f; a += 0.25f) {
                Vector2 off = (a * MathHelper.TwoPi + Projectile.rotation).ToRotationVector2() * 2f;
                sb.Draw(tex, pos + off, null, BloodDark * (0.65f * light * fade), Projectile.rotation,
                    origin, 1.04f, SpriteEffects.None, 0f);
            }
            sb.Draw(tex, pos, null, BloodMain * (0.95f * light * fade), Projectile.rotation,
                origin, 1f, SpriteEffects.None, 0f);
            Color core = PearlBright with { A = 0 };
            sb.Draw(tex, pos, null, core * (0.4f * light * fade), Projectile.rotation,
                origin, 0.86f, SpriteEffects.None, 0f);

            //矛尖珠光：点亮后常驻一粒微光，虹彩随席位错相
            if (glow != null && litDone) {
                Color tip = KikasaEmpressServant.IridescentTint(Slot * 0.14f) with { A = 0 };
                sb.Draw(glow, pos + axis * 40f, null, tip * (0.5f * light * fade), 0f,
                    glow.Size() * 0.5f, new Vector2(14f * 2f / glow.Width), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
