using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaArmsPalette;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaYoyos
{
    /// <summary>
    /// 湖水悠悠球：球奴的自研球体。出程直飞→驻留磨伤（追着敌人小圆游走，
    /// 结阵档按手序相位差绕猎物公转）→沿线收回掷出它的那只鬼手。
    /// 血水线从鬼手垂到球心（带垂弧），驻留高频跳由 localNPCHitCooldown 承担。
    /// 相位全在本地推进（速度随生成包到位，各端独立推导一致），
    /// authority 判定归手消亡，远端超时兜底。
    /// ai0=父球手 identity、ai1=手序+结阵旗打包、ai2=武器物品类型（贴图与档案之源）
    /// </summary>
    internal class KikasaYoyoProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>归手判定半径：进圈即被收线</summary>
        private const float ReelDist = 30f;

        /// <summary>驻留期敌人搜索半径</summary>
        private const float DwellSeekDist = 300f;

        /// <summary>结阵公转半径</summary>
        private const float RingRadius = 64f;

        private int ParentIdentity => (int)Projectile.ai[0];
        private int PackedHand => (int)Projectile.ai[1];
        private int HandIndex => UnpackHand(PackedHand);
        private bool RingMode => PackedHand >= 8;
        private int ArmsItemType => (int)Projectile.ai[2];

        /// <summary>手序与结阵旗共用 ai1：低 3 位手序、bit3 结阵</summary>
        internal static int PackHand(int hand, bool ring) => hand + (ring ? 8 : 0);

        internal static int UnpackHand(int packed) => packed & 7;

        private KikasaYoyoProfile? profileCache;

        private KikasaYoyoProfile Profile => profileCache ??= KikasaArmsProfiler.YoyoProfileOf(ArmsItemType);

        //相位：0 出程 / 1 驻留 / 2 回程（各端本地推进）
        private int phase;
        private int phaseTimer;
        /// <summary>出程累计里程：到档案放线距离即驻留</summary>
        private float mileage;
        /// <summary>驻留期空窗计帧：追不到敌人太久就提前收线</summary>
        private int idleTimer;
        /// <summary>缓存父球手弹幕位（whoAmI），identity 校验失配再重扫</summary>
        private int parentCache = -1;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 420;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            phaseTimer++;
            //球体永远高速自转：悠悠球的身份姿态
            Projectile.rotation += 0.55f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            switch (phase) {
                case 0: {
                    //出程：直飞，近敌或放线到长即驻留
                    mileage += Projectile.velocity.Length();
                    if (mileage >= Profile.MaxReach || NearestNpc(60f) >= 0) {
                        phase = 1;
                        phaseTimer = 0;
                        idleTimer = 0;
                    }
                    break;
                }
                case 1: {
                    UpdateDwell();
                    if (phaseTimer >= Profile.DwellTime || idleTimer > 45) {
                        phase = 2;
                        phaseTimer = 0;
                    }
                    break;
                }
                default: {
                    //回程：渐加速沿线收回，近了加大转向权重防绕圈
                    Vector2 home = FindReelPoint();
                    Vector2 to = home - Projectile.Center;
                    float dist = to.Length();
                    if (dist < ReelDist) {
                        if (Main.myPlayer == Projectile.owner) {
                            Projectile.Kill();
                        }
                        return;
                    }
                    float speed = MathF.Min(Profile.TopSpeed * (0.6f + phaseTimer * 0.035f),
                        Profile.TopSpeed * 1.4f);
                    float steer = dist < 90f ? 0.32f : 0.14f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                        to.SafeNormalize(Vector2.UnitX) * speed, steer);
                    break;
                }
            }

            //尾迹滴珠：高速段甩
            if (!Main.dedServ && Projectile.velocity.Length() > 4f
                && phaseTimer % 5 == 0 && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center - Projectile.velocity * 0.4f,
                    -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    BloodMain * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.22f, 0.36f))
                    ?.Configure(Main.rand.Next(10, 16), 0f);
            }

            const float glow = 0.24f;
            Lighting.AddLight(Projectile.Center, 0.9f * glow, 0.28f * glow, 0.24f * glow);
        }

        /// <summary>驻留：追着敌人小圆游走磨伤；结阵档按手序相位差绕猎物公转</summary>
        private void UpdateDwell() {
            int target = NearestNpc(DwellSeekDist);
            if (target < 0) {
                idleTimer++;
                Projectile.velocity *= 0.9f;
                return;
            }
            idleTimer = 0;
            NPC npc = Main.npc[target];
            Vector2 slot;
            if (RingMode) {
                //公转：三球相位差 2π/3，绕猎物旋切一轮
                float orbit = phaseTimer * 0.085f + HandIndex * MathHelper.TwoPi / 3f;
                slot = npc.Center + orbit.ToRotationVector2() * (RingRadius + npc.width * 0.3f);
            }
            else {
                //游走：贴着猎物身侧小圆震荡，磨着打
                float wob = phaseTimer * 0.17f + Projectile.identity * 1.7f;
                slot = npc.Center + new Vector2(MathF.Cos(wob) * 46f, MathF.Sin(wob * 1.37f) * 38f);
            }
            Vector2 desired = (slot - Projectile.Center) * 0.16f;
            float cap = Profile.TopSpeed * 1.15f;
            if (desired.Length() > cap) {
                desired = desired.SafeNormalize(Vector2.Zero) * cap;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.25f);
        }

        private int NearestNpc(float within) {
            int best = -1;
            float bestDist = within;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>归手点：父球手在场读本手实时位，编队没了退回主人身侧</summary>
        private Vector2 FindReelPoint() {
            if (parentCache >= 0 && parentCache < Main.maxProjectiles) {
                Projectile cached = Main.projectile[parentCache];
                if (cached.active && cached.identity == ParentIdentity
                    && cached.ModProjectile is KikasaYoyoServant cachedPack) {
                    return cachedPack.ReelPointOf(HandIndex);
                }
                parentCache = -1;
            }
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.identity == ParentIdentity && proj.owner == Projectile.owner
                    && proj.ModProjectile is KikasaYoyoServant pack) {
                    parentCache = proj.whoAmI;
                    return pack.ReelPointOf(HandIndex);
                }
            }
            Player owner = Main.player[Projectile.owner];
            return owner?.active == true ? owner.Center : Projectile.Center;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int k = 0; k < 3; k++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    dir.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(1.5f, 3.6f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.26f, 0.44f))?.Configure(Main.rand.Next(10, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //归手/超时都散成水：球本就是湖水凝的
            for (int k = 0; k < 4; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(Projectile.Center,
                    Main.rand.NextVector2Circular(1.6f, 1.6f) + new Vector2(0f, 0.8f),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.55f),
                    Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(Main.rand.Next(10, 18), 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ArmsItemType);
            Texture2D tex = TextureAssets.Item[ArmsItemType]?.Value;
            if (tex == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //血水线：鬼手到球心，中点下垂的软弧（离得越近垂得越明显）
            Vector2 hand = FindReelPoint();
            DrawLine(sb, hand, Projectile.Center);

            //旋切水光：驻留磨伤时球缘泛圈
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                float ringGlow = phase == 1 ? 0.5f : 0.3f;
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                sb.Draw(glowTex, pos, null, BloodMain * ringGlow, 0f,
                    glowTex.Size() * 0.5f, new Vector2(26f * 2f / glowTex.Width), SpriteEffects.None, 0f);
                //速度拉伸尾：收放两程带出线感
                float speedT = MathHelper.Clamp(Projectile.velocity.Length() / Profile.TopSpeed, 0f, 1.2f);
                if (speedT > 0.25f) {
                    float rot = Projectile.velocity.ToRotation();
                    sb.Draw(glowTex, pos, null, BloodDeep * (0.45f * speedT), rot,
                        glowTex.Size() * 0.5f,
                        new Vector2(30f * speedT * 2f / glowTex.Width, 6f * 2f / glowTex.Height),
                        SpriteEffects.None, 0f);
                }
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //球体：借原物品贴图高速自转，血湖染色（湖水凝的球，不是原物）
            Color lit = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            Color color = Color.Lerp(lit, BloodMain, 0.45f);
            sb.Draw(tex, pos, null, color, Projectile.rotation, tex.Size() * 0.5f,
                Profile.DrawScale, SpriteEffects.None, 0f);
            return false;
        }

        /// <summary>手到球的血水线：8 段折出中点垂弧，钓线贴图逐段拉伸染血</summary>
        private static void DrawLine(SpriteBatch sb, Vector2 from, Vector2 to) {
            Texture2D lineTex = TextureAssets.FishingLine?.Value;
            if (lineTex == null) {
                return;
            }
            float dist = Vector2.Distance(from, to);
            if (dist < 8f || dist > 1600f) {
                return;
            }
            //垂弧幅度：线越松（越近）垂得越深
            float sag = MathHelper.Clamp((420f - dist) * 0.06f, 4f, 26f);
            const int segCount = 8;
            Rectangle lineFrame = lineTex.Frame();
            Vector2 lineOrigin = new(lineFrame.Width / 2, 2f);
            Vector2 prev = from;
            for (int i = 1; i <= segCount; i++) {
                float t = i / (float)segCount;
                Vector2 point = Vector2.Lerp(from, to, t);
                point.Y += MathF.Sin(t * MathHelper.Pi) * sag;
                Vector2 seg = point - prev;
                float rot = seg.ToRotation() - MathHelper.PiOver2;
                Color lit = Lighting.GetColor(prev.ToTileCoordinates(), BloodDeep);
                Vector2 scale = new(1f, (seg.Length() + 2f) / lineFrame.Height);
                sb.Draw(lineTex, prev - Main.screenPosition, lineFrame, lit * 0.7f,
                    rot, lineOrigin, scale, SpriteEffects.None, 0f);
                prev = point;
            }
        }
    }
}
