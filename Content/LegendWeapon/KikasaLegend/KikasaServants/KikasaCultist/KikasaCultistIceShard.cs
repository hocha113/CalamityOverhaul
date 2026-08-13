using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaCultist
{
    /// <summary>
    /// 鬼奴邪教徒的血冰簇弹。材质身份：冻血晶棱——湖血在激发帧被冻成的多面晶刃，
    /// 冰壳里封着一线未冻透的暖血。签名行为：晶面按自转相位打出锐星闪；
    /// 飞行中冰壳持续剥落寒雾；尾段冰壳渐融、暖血珠自尾端甩落（血比冰重）。
    /// 弹体 = Extra_98 真 alpha 多层（暗缘/晶身/斜十字棱面/暖血芯/霜白芯），非光斑叠层。
    /// 出膛短暂复利加速（激发的锐气），中段泄劲、尾段微坠；
    /// 命中/超时皆碎裂——冰屑四溅里裹着几粒解冻的血珠。落回血湖则被湖收走；
    /// 鬼物穿行地形不受阻
    /// </summary>
    internal class KikasaCultistIceShard : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int AccelFrames = 10;
        private const int SinkStart = 34;

        private ref float Life => ref Projectile.localAI[0];

        private bool lakeSwallowed;

        private float Seed => Projectile.identity * 0.7391f % 4.7f;

        /// <summary>出生 3 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 3f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            //鬼物冰晶穿地飞：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //碎裂统一走 OnKill（命中/超时），不再依赖撞地
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //激发后短暂复利续力，随后泄劲；尾段被寒重拽落
            if (Life <= AccelFrames) {
                Projectile.velocity *= 1.012f;
            }
            else {
                Projectile.velocity *= 0.996f;
            }
            if (Life > SinkStart) {
                Projectile.velocity.Y += 0.06f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //冰尘尾迹：细小的寒芒缓落
            if (!Main.dedServ && Life % 3 == 1) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(4f, 4f),
                    -Projectile.velocity * 0.06f + new Vector2(0f, Main.rand.NextFloat(0.2f, 0.7f)),
                    KikasaCultistServant.IceTint * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.22f, 0.36f))?.Configure(Main.rand.Next(10, 18), 0f);
            }
            //寒气剥落：冰壳一路蒸出细雾，向后下方散开
            if (!Main.dedServ && Life % 5 == 2) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Projectile.Center - Projectile.velocity * 0.7f + Main.rand.NextVector2Circular(3f, 3f),
                    -Projectile.velocity * 0.03f + new Vector2(0f, Main.rand.NextFloat(0.1f, 0.4f)),
                    Color.Lerp(KikasaCultistServant.MistBlood, KikasaCultistServant.IceTint, 0.55f) * 0.45f,
                    Main.rand.NextFloat(0.22f, 0.34f))?.Configure(Main.rand.Next(16, 28));
            }
            //尾段解冻滴血：泄劲后冰壳渐融，暖血珠自尾端甩落
            if (!Main.dedServ && Life > SinkStart && Life % 6 == 3) {
                Vector2 tail = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(6f, 14f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(tail,
                    Projectile.velocity * 0.12f + new Vector2(0f, Main.rand.NextFloat(0.3f, 0.8f)),
                    KikasaCultistServant.BloodMain * 0.6f,
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(16, 26), 0.3f);
            }

            float glow = 0.4f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.28f * glow, 0.4f * glow, 0.5f * glow);

            //落回血湖：湖收回自己的血，不碎裂
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.Center.Y >= domain.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.6f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 3);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.15f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
            }
        }

        //==================== 碎裂 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.NPCHit5 with { Volume = 0.4f, Pitch = 0.15f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (lakeSwallowed) {
                return;
            }
            Shatter(Projectile.velocity);
        }

        /// <summary>碎裂：冰屑扇 + 解冻血珠 + 一记清脆碎冰声；晶体死后寒雾多活一拍</summary>
        private void Shatter(Vector2 impactVel) {
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            Vector2 back = -impactVel.SafeNormalize(Vector2.UnitY);
            //冰屑：亮片打着旋飞散
            for (int i = 0; i < 7; i++) {
                Vector2 vel = back.RotatedByRandom(1.2f) * Main.rand.NextFloat(1.5f, 4.5f)
                    + impactVel * 0.08f;
                PRTLoader.NewParticle<PRT_Sparkle>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), vel,
                    KikasaCultistServant.IceTint, Main.rand.NextFloat(0.22f, 0.4f))
                    ?.Configure(KikasaCultistServant.IceTint * 0.5f, Main.rand.Next(14, 26), 0.2f, 0.7f);
            }
            //解冻的血珠：冰里冻着血
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    back.RotatedByRandom(0.9f) * Main.rand.NextFloat(1f, 3f),
                    KikasaCultistServant.BloodMain * Main.rand.NextFloat(0.45f, 0.6f),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.3f, 0.3f));
            }
            //寒雾余韵：比弹体活得久的痕迹
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center, back * 0.4f,
                Color.Lerp(KikasaCultistServant.MistBlood, KikasaCultistServant.IceTint, 0.4f) * 0.6f,
                Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(30, 50));
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            if (fade <= 0.01f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;
            //Extra_98 针体沿 Y 拉长，长轴对齐飞行向
            float rot = Projectile.rotation + MathHelper.PiOver2;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0.5f, 1.3f);

            //残影拖尾：速度门控，旧位残棱一线排开（真 alpha 直染，主批直接画）
            if (Projectile.velocity.Length() > 10f) {
                for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                    Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                    if (Projectile.oldPos[k] == Vector2.Zero) {
                        continue;
                    }
                    float fall = 1f - k / (float)Projectile.oldPos.Length;
                    sb.Draw(tex, oldCenter - Main.screenPosition, null,
                        KikasaCultistServant.IceTint * (0.22f * fall * fade), rot, origin,
                        new Vector2(0.08f, (0.26f + stretch * 0.24f) * fall), SpriteEffects.None, 0f);
                }
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //冰壳内的暖血芯反相微晃：表面张力还活着
            float wob = MathF.Sin(Life * 0.5f + Seed * 4f);
            Vector2 perp = rot.ToRotationVector2();

            //冻血暗缘：晶壳外圈的深血冻色
            sb.Draw(tex, pos, null, KikasaCultistServant.BloodDeep * (0.8f * fade), rot, origin,
                new Vector2(0.19f, 0.4f + stretch * 0.34f), SpriteEffects.None, 0f);
            //寒冰晶身：速度拉伸的主棱
            sb.Draw(tex, pos, null, KikasaCultistServant.IceTint * (0.95f * fade), rot, origin,
                new Vector2(0.14f, 0.34f + stretch * 0.3f), SpriteEffects.None, 0f);
            //斜十字棱面：两道短斜棱读出晶体的多面切面
            for (int s = -1; s <= 1; s += 2) {
                sb.Draw(tex, pos, null, KikasaCultistServant.IceTint * (0.55f * fade), rot + s * 0.44f, origin,
                    new Vector2(0.065f, 0.17f + stretch * 0.1f), SpriteEffects.None, 0f);
            }
            //未冻透的暖血芯：一线红在冰壳里晃
            sb.Draw(tex, pos + perp * (wob * 1.4f), null, KikasaCultistServant.BloodMain * (0.85f * fade), rot, origin,
                new Vector2(0.05f, 0.22f + stretch * 0.2f), SpriteEffects.None, 0f);
            //霜白亮芯（A=0 预乘加色）
            Color core = KikasaCultistServant.RuneCore with { A = 0 };
            sb.Draw(tex, pos, null, core * (0.55f * fade), rot, origin,
                new Vector2(0.04f, 0.17f + stretch * 0.16f), SpriteEffects.None, 0f);

            //晶面锐闪：自转相位扫过光角时打一粒星（黑底星图只走 A=0 加色）
            Texture2D star = CWRAsset.StarGlow01?.Value;
            float glint = MathF.Max(0f, MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Seed * 5f));
            glint = glint * glint * glint;
            if (star != null && glint > 0.15f) {
                sb.Draw(star, pos, null, (Color.White with { A = 0 }) * (0.65f * glint * fade), rot,
                    star.Size() * 0.5f, 0.22f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
