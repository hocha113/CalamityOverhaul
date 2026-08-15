using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 雷枢电球：本体=原版信徒雷球465真实纹理（4帧），程序化只做叠加层；
    /// 充能（无伤，预告线渐亮=承诺）→激活（球体带电+链弧成网/独球放电锁玩家）→衰散；
    /// ai[0]=充能帧 ai[1]=链接伙伴identity+1（0=独球放电型） ai[2]=激活持续帧
    /// </summary>
    internal class CultistLightningOrb : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int FadeTime = 22;
        private const int ZapInterval = 54;
        private const int ZapCue = 14;
        private const float BeamHitWidth = 22f;

        private int ChargeTime => Math.Max((int)Projectile.ai[0], 20);
        private int ActiveTime => Projectile.ai[2] > 0f ? (int)Projectile.ai[2] : 120;
        private int PartnerIdentity => (int)Projectile.ai[1] - 1;
        private bool Linked => Projectile.ai[1] >= 1f;

        private float Timer => Projectile.localAI[0];
        private bool Activated => Timer > ChargeTime;
        private bool Fading => Timer > ChargeTime + ActiveTime;

        //链弧最长可达~560px：放宽屏外剔除余量，防止锚球出屏时弧线整段消失
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 56;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>找链接伙伴（identity跨端一致）</summary>
        private Projectile FindPartner() {
            if (!Linked) {
                return null;
            }
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == Projectile.type && p.identity == PartnerIdentity) {
                    return p;
                }
            }
            return null;
        }

        /// <summary>
        /// 弧线归属：指向伙伴的一端负责画与判；互指对时由identity小端负责（防双倍）；
        /// 链式布阵（A→B→C）中间球被覆盖成单向指针，仍各自成弧
        /// </summary>
        private bool OwnsBeam(Projectile partner) {
            bool mutual = (int)partner.ai[1] - 1 == Projectile.identity;
            return !mutual || Projectile.identity < partner.identity;
        }

        /// <summary>某球是否处于"激活未衰散"（错拍充能下伙伴可能早我衰散，弧线随之断电）</summary>
        private static bool IsLive(Projectile p) {
            int charge = Math.Max((int)p.ai[0], 20);
            int active = p.ai[2] > 0f ? (int)p.ai[2] : 120;
            return p.localAI[0] > charge && p.localAI[0] <= charge + active;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            Projectile.velocity = Vector2.Zero;

            if ((int)Timer == 1) {
                //缓存伤害并定寿命（各端确定性）
                Projectile.localAI[1] = Projectile.damage;
                Projectile.timeLeft = ChargeTime + ActiveTime + FadeTime;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item121 with { Volume = 0.45f, Pitch = -0.35f, MaxInstances = 6 }, Projectile.Center);
                }
            }

            //充能/衰散期无伤（预告即承诺）。判伤只在受击玩家本端解算，本地门即可；
            //服务端必须保持满伤害：SyncProjectile 每包重写 damage 字段，链接补包
            //（LinkOrbs 的 netUpdate 与生成包同拍发出）若快照到清零值，客户端首帧
            //会把 0 缓存进 localAI[1]——联机中整网电球永久无伤，单机不发包测不出
            if (!VaultUtils.isServer) {
                Projectile.damage = Activated && !Fading ? (int)Projectile.localAI[1] : 0;
            }

            //充能期：符文与电离屑被吸入球心
            if (!Activated && !VaultUtils.isServer) {
                if (Main.rand.NextBool(3)) {
                    Vector2 start = Projectile.Center + Main.rand.NextVector2CircularEdge(60f, 60f);
                    PRTLoader.NewParticle<PRT_CultistRune>(start, Vector2.Zero,
                        CultistPalette.ThunderMain, Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(Projectile.Center, 0.22f, 12);
                }
                if (Main.rand.NextBool(4)) {
                    PRTLoader.NewParticle<PRT_CultistVolt>(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                        Main.rand.NextVector2Circular(0.8f, 0.8f), CultistPalette.ThunderBright,
                        Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(8, 14));
                }
            }

            //激活帧：原版语言的放电启动（Item121全响+短闪）
            if ((int)Timer == ChargeTime + 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item121 with { Volume = 0.9f, Pitch = 0.1f, MaxInstances = 6 }, Projectile.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                    CultistPalette.ThunderBright, 0.07f)?.Configure(0.07f, 0.9f, 14);
            }

            //激活期：独球型周期放电锁玩家（服务端裁决，各端同节拍演出）
            if (Activated && !Fading && !Linked) {
                int zapAge = (int)Timer - ChargeTime + Projectile.identity % 27;
                int untilZap = ZapInterval - zapAge % ZapInterval;
                //放电前奏：球体鼓胀+电须乱窜（可读的倒计时）
                if (untilZap <= ZapCue && !VaultUtils.isServer && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_CultistVolt>(Projectile.Center + Main.rand.NextVector2Circular(26f, 26f),
                        Main.rand.NextVector2Circular(1.6f, 1.6f), CultistPalette.ThunderBright,
                        Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(6, 12));
                }
                if (zapAge % ZapInterval == 0) {
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.55f, Pitch = 0.25f, MaxInstances = 6 }, Projectile.Center);
                        CultistRenderHelper.CastBurst(Projectile.Center, Vector2.UnitY, CultistElement.Thunder, 0.8f);
                    }
                    if (!VaultUtils.isClient) {
                        int idx = Player.FindClosest(Projectile.position, Projectile.width, Projectile.height);
                        Player target = Main.player[idx];
                        if (target.Alives()) {
                            Vector2 aim = (target.Center + target.velocity * 10f - Projectile.Center).SafeNormalize(Vector2.UnitY);
                            Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, aim * 7.6f,
                                ModContent.ProjectileType<CultistArcSpark>(), (int)Projectile.localAI[1], 0f, Main.myPlayer,
                                (float)CultistElement.Thunder, 0f);
                        }
                    }
                }
            }

            //链弧带电嗡鸣的电离屑（激活期沿弧线偶发）
            if (Activated && !Fading && Linked && !VaultUtils.isServer && Main.rand.NextBool(5)) {
                Projectile partner = FindPartner();
                if (partner != null && OwnsBeam(partner) && IsLive(partner)) {
                    Vector2 pos = Vector2.Lerp(Projectile.Center, partner.Center, Main.rand.NextFloat());
                    PRTLoader.NewParticle<PRT_CultistVolt>(pos, Main.rand.NextVector2Circular(1f, 1f),
                        CultistPalette.ThunderBright, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(6, 12));
                }
            }

            //本体消失即收场（防孤儿电网）
            if ((int)Timer % 30 == 0 && !NPC.AnyNPCs(NPCID.CultistBoss)) {
                Projectile.Kill();
                return;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.4f, 0.85f, 0.9f) * Opacity());
        }

        /// <summary>透明度包络：充能渐显→激活全亮→衰散渐隐</summary>
        private float Opacity() {
            float fadeIn = MathHelper.Clamp(Timer / (ChargeTime * 0.6f), 0f, 1f);
            float fadeOut = Fading ? MathHelper.Clamp(1f - (Timer - ChargeTime - ActiveTime) / FadeTime, 0f, 1f) : 1f;
            return fadeIn * fadeOut;
        }

        /// <summary>链弧线判定：两球都激活时，弧线本身是伤害体</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Activated && !Fading && projHitbox.Intersects(targetHitbox)) {
                return true;
            }
            if (Activated && !Fading && Linked) {
                Projectile partner = FindPartner();
                //由弧线归属端负责判定，避免双倍判伤；伙伴衰散即断电
                if (partner != null && OwnsBeam(partner) && IsLive(partner)) {
                    float _ = 0f;
                    if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                        Projectile.Center, partner.Center, BeamHitWidth, ref _)) {
                        return true;
                    }
                }
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Electrified, 60);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            CultistRenderHelper.ElementImpact(Projectile.Center, CultistElement.Thunder, 0.9f);
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.4f, Pitch = -0.1f, MaxInstances = 6 }, Projectile.Center);
            //余韵：放射电痕驻留
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(-0.4f, 0.4f);
                PRTLoader.NewParticle<PRT_CultistArcTrace>(Projectile.Center, Vector2.Zero,
                    CultistPalette.ThunderMain, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(ang, Main.rand.NextFloat(28f, 52f), Main.rand.Next(12, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float opacity = Opacity();
            float chargeT = MathHelper.Clamp(Timer / ChargeTime, 0f, 1f);
            Projectile partner = Linked ? FindPartner() : null;
            bool beamOwner = partner != null && OwnsBeam(partner);

            //放电前奏鼓胀（独球型）
            float swell = 0f;
            if (Activated && !Fading && !Linked) {
                int zapAge = (int)Timer - ChargeTime + Projectile.identity % 27;
                int untilZap = ZapInterval - zapAge % ZapInterval;
                if (untilZap <= ZapCue) {
                    swell = 1f - untilZap / (float)ZapCue;
                }
            }

            //---- 叠加层（加色批）：底晕≤30%+预告线/链弧 ----
            CultistRenderHelper.BeginAdditive(sb);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            sb.Draw(glow, drawPos, null, CultistPalette.ThunderDeep * (0.3f * opacity),
                0f, glow.Size() / 2f, 0.62f + 0.1f * swell, SpriteEffects.None, 0f);

            if (partner != null && beamOwner) {
                if (!Activated) {
                    //充能期预告细线：随充能渐亮（承诺弧线将在此成形）
                    Texture2D line = CWRAsset.LightShot.Value;
                    Vector2 span = partner.Center - Projectile.Center;
                    sb.Draw(line, drawPos, null, CultistPalette.ThunderMain * (0.1f + 0.24f * chargeT),
                        span.ToRotation(), new Vector2(0f, line.Height / 2f),
                        new Vector2(span.Length() / line.Width, 0.07f + 0.05f * chargeT), SpriteEffects.None, 0f);
                }
                else if (!Fading && IsLive(partner)) {
                    //激活链弧：顶点闪电成网（球=真实纹理锚点，弧=特效）
                    CultistRenderHelper.DrawLightningBetween(sb, Projectile.Center, partner.Center,
                        CultistPalette.ThunderMain, CultistPalette.ThunderBright,
                        Projectile.identity * 13 + partner.identity, opacity);
                }
            }
            CultistRenderHelper.EndAdditive(sb);

            //---- 本体：原版雷球465真实纹理（4帧，全亮） ----
            Main.instance.LoadProjectile(ProjectileID.CultistBossLightningOrb);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.CultistBossLightningOrb].Value;
            int fh = tex.Height / 4;
            Rectangle src = new(0, (int)(Timer / 5f) % 4 * fh, tex.Width, fh);
            float scale = (0.55f + 0.45f * chargeT + 0.14f * swell) * Projectile.scale;
            sb.Draw(tex, drawPos, src, new Color(255, 255, 255, 0) * opacity, 0f,
                new Vector2(tex.Width / 2f, fh / 2f), scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}
