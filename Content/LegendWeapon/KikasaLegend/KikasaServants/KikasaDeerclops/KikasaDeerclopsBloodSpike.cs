using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaDeerclops
{
    /// <summary>
    /// 鬼奴鹿角怪的血冰刺：跺脚刺列的单根。借原版鹿角怪冰刺贴图（5 变体），
    /// 经冻血材质着色器上屏——湖水在蹄震里被冻成灰蓝的刺，尖端淌未冻透的暖血。
    /// 从水下炸出（涟漪+水花+冰啸）→ 驻立滴血 → 碎化沉回湖里。
    /// 生成时携带全部参数（ai0=体量，velocity=倾斜方向），伤害窗口与可见刺体严格对齐；
    /// 首帧演出各端本地播（弹幕同步包送达远端后自己报到）
    /// </summary>
    internal class KikasaDeerclopsBloodSpike : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 时序 ====================

        private const int EruptEnd = 8;
        private const int CollapseStart = 40;
        private const int LifeEnd = 56;

        /// <summary>刺体可视长度基准（贴图指向沿 velocity，原版语义）</summary>
        private const float TipLength = 165f;

        /// <summary>体量（0.78~1.33，跺脚序列越远越高），spawn 即定</summary>
        private ref float SpikeScale => ref Projectile.ai[0];

        private int life;
        private bool burstDone;
        private bool crackDone;

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 400;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 45;
            Projectile.timeLeft = LifeEnd + 8;
        }

        /// <summary>velocity 只存倾斜方向，刺立在原地不走</summary>
        public override bool ShouldUpdatePosition() => false;

        /// <summary>伤害窗口与可见刺体对齐：炸出即危险，碎化后无害</summary>
        public override bool? CanDamage() => life is >= 1 and <= CollapseStart + 2 ? null : false;

        /// <summary>沿刺轴的线碰撞：贴合倾斜的刺体而非方框</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float rise = RiseProgress();
            if (rise < 0.2f) {
                return false;
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(-Vector2.UnitY);
            Vector2 tip = Projectile.Center + dir * TipLength * SpikeScale * rise;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, tip, 15f * SpikeScale, ref _);
        }

        public override bool? CanCutTiles() => false;

        /// <summary>炸出进度：前 8 帧非匀速拔起，末段带一丝过冲</summary>
        private float RiseProgress() {
            if (life >= EruptEnd) {
                return 1f;
            }
            float u = MathHelper.Clamp(life / (float)EruptEnd, 0f, 1f);
            return MathF.Min(1f, 1.06f * (1f - MathF.Pow(1f - u, 2.4f)));
        }

        /// <summary>碎化进度：驻立期 0，收场 16 帧内蚀尽</summary>
        private float CollapseProgress()
            => MathHelper.Clamp((life - CollapseStart) / 16f, 0f, 1f);

        public override void AI() {
            life++;

            if (!burstDone) {
                //破水拍：一根刺从湖里炸出来（远端靠同步包收到后同帧报到）
                burstDone = true;
                SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with {
                    Pitch = 0.25f - SpikeScale * 0.35f,
                    MaxInstances = 5
                }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.1f, MaxInstances = 5 }, Projectile.Center);
                if (ViewedOwner) {
                    KikasaDomainDeco.RippleAt(Projectile.Center, 0.7f + SpikeScale * 0.35f);
                    KikasaDomainDeco.SplashAt(Projectile.Center, 3 + (int)(SpikeScale * 3f));
                }
                if (!Main.dedServ) {
                    //出水碎珠顺着刺身两侧甩出
                    Vector2 dir = Projectile.velocity.SafeNormalize(-Vector2.UnitY);
                    for (int i = 0; i < 6; i++) {
                        Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(2f, 5.5f);
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            Projectile.Center + Main.rand.NextVector2Circular(8f, 4f),
                            vel, Main.rand.NextBool(4)
                                ? KikasaDeerclopsServant.FrostDeep
                                : KikasaDeerclopsServant.FrostMain,
                            Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(16, 26));
                    }
                }
            }

            //驻立期尖端淌血：未冻透的暖血顺着刺尖滑下来
            if (!Main.dedServ && life > EruptEnd && life < CollapseStart && life % 7 == (int)(Seed * 2f) % 7) {
                Vector2 dir = Projectile.velocity.SafeNormalize(-Vector2.UnitY);
                Vector2 tip = Projectile.Center + dir * TipLength * SpikeScale * Main.rand.NextFloat(0.72f, 0.98f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(tip + Main.rand.NextVector2Circular(3f, 3f),
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(0.4f, 0.9f)),
                    KikasaDeerclopsServant.WoundBlood * 0.6f,
                    Main.rand.NextFloat(0.28f, 0.45f))?.Configure(Main.rand.Next(20, 32), 0.24f);
            }

            //碎化拍：脆响一声，散珠回湖
            if (!crackDone && life >= CollapseStart) {
                crackDone = true;
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 5 }, Projectile.Center);
                if (!Main.dedServ) {
                    Vector2 dir = Projectile.velocity.SafeNormalize(-Vector2.UnitY);
                    for (int i = 0; i < 5; i++) {
                        Vector2 pos = Projectile.Center + dir * TipLength * SpikeScale * Main.rand.NextFloat(0.2f, 0.95f);
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                            new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2f)),
                            KikasaDeerclopsServant.FrostMain * 0.6f,
                            Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(14, 24));
                    }
                }
                if (ViewedOwner) {
                    KikasaDomainDeco.RippleAt(Projectile.Center, 0.45f);
                }
            }

            float glow = (1f - CollapseProgress()) * RiseProgress() * 0.4f;
            if (glow > 0.02f) {
                //光锚放在刺身中段（velocity 是指向刺尖的单位向量）
                Lighting.AddLight(Projectile.Center + Projectile.velocity * TipLength * SpikeScale * 0.5f,
                    0.14f * glow, 0.2f * glow, 0.22f * glow);
            }

            if (life >= LifeEnd) {
                Projectile.Kill();
            }
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //刺中：冻珠迸开，伤口见暖血
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(16f, 16f),
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0.8f, 2.8f)),
                    (Main.rand.NextBool(3) ? KikasaDeerclopsServant.WoundBlood : KikasaDeerclopsServant.FrostMain) * 0.6f,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24), 0.3f);
            }
            SoundEngine.PlaySound(SoundID.NPCHit5 with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //谢幕：基座一圈残珠沉回湖面
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-6f, 0f)),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.6f, 1.6f)),
                    KikasaDeerclopsServant.FrostDeep * 0.55f,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.DeerclopsIceSpike);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.DeerclopsIceSpike]?.Value;
            if (tex == null) {
                return false;
            }
            float rise = RiseProgress();
            float collapse = CollapseProgress();
            if (rise <= 0.02f || collapse >= 1f) {
                return false;
            }

            //原版刺贴图 5 行变体，指向沿 velocity（贴图原生朝右，基点在 x=16）
            int variant = Projectile.identity % 5;
            Rectangle frame = tex.Frame(1, 5, 0, variant);
            Vector2 origin = new(16f, frame.Height / 2f);
            float rotation = Projectile.velocity.ToRotation();
            //碎化期整根往水里沉
            Vector2 drawPos = Projectile.Center + new Vector2(0f, collapse * 26f) - Main.screenPosition;
            //长度随炸出拔起，厚度慢半拍跟上；碎化期先瘦
            Vector2 scale = new(SpikeScale * rise * (TipLength + 40f) / frame.Width,
                SpikeScale * (0.7f + 0.3f * rise) * (1f - collapse * 0.5f));

            Effect form = EffectLoader.KikasaDeerclopsFrost?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Color color;
            if (shaderOk) {
                //炸出期由全水态凝成半实血冰，碎化期噪声蚀尽
                float uForm = MathHelper.Lerp(1f, 0.5f, rise);
                KikasaDeerclopsServant.ApplyFrostShader(form, noise, tex, frame, uForm, 0f, collapse, Seed);
                color = Color.White;
            }
            else {
                color = Color.Lerp(Color.White, KikasaDeerclopsServant.FrostMain, 0.6f) * (1f - collapse);
            }

            sb.Draw(tex, drawPos, frame, color, rotation, origin, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
