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
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaArmsPalette;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaBoomerangs
{
    /// <summary>
    /// 湖水回旋镖：镖奴的自研镖体。不走原版镖 AI（其回收目标是玩家本人会穿帮），
    /// 去程减速→悬滞旋定→回程加速追掷出它的那只鬼手；去程命中提前折返（打了就回）。
    /// 累计里程与相位全在本地推进（速度随生成包到位，各端独立推导一致），
    /// authority 判定归手消亡，远端超时兜底。
    /// ai0=父镖手 identity、ai1=手序、ai2=武器物品类型（贴图与档案之源）
    /// </summary>
    internal class KikasaBoomerangProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int HoverFrames = 9;

        /// <summary>归手判定半径：进圈即被接住</summary>
        private const float CatchDist = 30f;

        private int ParentIdentity => (int)Projectile.ai[0];
        private int HandIndex => (int)Projectile.ai[1];
        private int ArmsItemType => (int)Projectile.ai[2];

        private KikasaBoomerangProfile? profileCache;

        private KikasaBoomerangProfile Profile => profileCache ??= KikasaArmsProfiler.BoomerangProfileOf(ArmsItemType);

        //相位：0 去程 / 1 悬滞 / 2 回程（各端本地推进）
        private int phase;
        private int phaseTimer;
        /// <summary>去程累计里程：到档案射程即折返</summary>
        private float mileage;
        /// <summary>自转角速度：悬滞时最快，携带出手方向的旋向</summary>
        private float spin;
        private bool spinInit;
        /// <summary>缓存父镖手弹幕位（whoAmI），identity 校验失配再重扫</summary>
        private int parentCache = -1;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 260;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            if (!spinInit) {
                spinInit = true;
                spin = (Projectile.velocity.X >= 0f ? 1f : -1f) * 0.42f;
            }
            phaseTimer++;

            switch (phase) {
                case 0: {
                    //去程：轻减速拉出"掷远渐乏"的弧，里程或乏速任一到点即折返
                    mileage += Projectile.velocity.Length();
                    Projectile.velocity *= 0.985f;
                    if (mileage >= Profile.Range || Projectile.velocity.Length() < Profile.FlightSpeed * 0.45f) {
                        EnterHover();
                    }
                    break;
                }
                case 1: {
                    //悬滞：速度骤收、旋速拉满，一拍停在空中
                    Projectile.velocity *= 0.8f;
                    spin = MathF.Sign(spin) * MathHelper.Lerp(MathF.Abs(spin), 0.72f, 0.3f);
                    if (phaseTimer >= HoverFrames) {
                        phase = 2;
                        phaseTimer = 0;
                    }
                    break;
                }
                default: {
                    //回程：渐加速追归手点，近了加大转向权重防绕圈
                    Vector2 home = FindCatchPoint();
                    Vector2 to = home - Projectile.Center;
                    float dist = to.Length();
                    if (dist < CatchDist) {
                        if (Main.myPlayer == Projectile.owner) {
                            Projectile.Kill();
                        }
                        return;
                    }
                    float speed = MathF.Min(Profile.FlightSpeed * (0.55f + phaseTimer * 0.03f),
                        Profile.FlightSpeed * 1.35f);
                    float steer = dist < 90f ? 0.3f : 0.12f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                        to.SafeNormalize(Vector2.UnitX) * speed, steer);
                    break;
                }
            }

            Projectile.rotation += spin;

            //尾迹滴珠：飞行两程甩、悬滞不甩
            if (!Main.dedServ && phase != 1 && phaseTimer % 4 == 0 && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center - Projectile.velocity * 0.4f,
                    -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    BloodMain * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(Main.rand.Next(10, 16), 0f);
            }

            float glow = 0.26f;
            Lighting.AddLight(Projectile.Center, 0.9f * glow, 0.28f * glow, 0.24f * glow);
        }

        private void EnterHover() {
            phase = 1;
            phaseTimer = 0;
            SoundEngine.PlaySound(SoundID.Item7 with {
                Volume = 0.2f,
                Pitch = 0.45f,
                MaxInstances = 3
            }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            //折返点小圈水花：镖在空中拧了个身
            for (int k = 0; k < 4; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(Projectile.Center,
                    (MathHelper.TwoPi * k / 4f + Main.rand.NextFloat(0.5f)).ToRotationVector2()
                        * Main.rand.NextFloat(1.2f, 2.4f),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.22f, 0.38f))
                    ?.Configure(Main.rand.Next(8, 14), 0f);
            }
        }

        /// <summary>归手点：父镖手在场读本手实时位，编队没了退回主人身侧</summary>
        private Vector2 FindCatchPoint() {
            if (parentCache >= 0 && parentCache < Main.maxProjectiles) {
                Projectile cached = Main.projectile[parentCache];
                if (cached.active && cached.identity == ParentIdentity
                    && cached.ModProjectile is KikasaBoomerangServant cachedPack) {
                    return cachedPack.CatchPointOf(HandIndex);
                }
                parentCache = -1;
            }
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.identity == ParentIdentity && proj.owner == Projectile.owner
                    && proj.ModProjectile is KikasaBoomerangServant pack) {
                    parentCache = proj.whoAmI;
                    return pack.CatchPointOf(HandIndex);
                }
            }
            Player owner = Main.player[Projectile.owner];
            return owner?.active == true ? owner.Center : Projectile.Center;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //打了就回：去程命中提前折返，镖的往返节奏更紧
            if (phase == 0) {
                EnterHover();
            }
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int k = 0; k < 4; k++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    dir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(1.8f, 4.2f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 22));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //归手/超时都散成水：镖本就是湖水凝的
            for (int k = 0; k < 4; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(Projectile.Center,
                    Main.rand.NextVector2Circular(1.6f, 1.6f) + new Vector2(0f, 0.8f),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.55f),
                    Main.rand.NextFloat(0.26f, 0.42f))
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

            //速度拉伸血尾：悬滞收拢、两程随速展开
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            float speedT = MathHelper.Clamp(Projectile.velocity.Length() / Profile.FlightSpeed, 0f, 1.2f);
            if (glowTex != null && speedT > 0.15f) {
                Vector2 glowOrigin = glowTex.Size() * 0.5f;
                float rot = Projectile.velocity.ToRotation();
                float len = 34f * speedT;
                float wid = 7f;
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                sb.Draw(glowTex, pos - Projectile.velocity.SafeNormalize(Vector2.Zero) * len * 0.4f, null,
                    BloodDeep * (0.5f * speedT), rot, glowOrigin,
                    new Vector2(len * 1.25f / glowTex.Width * 2f, wid * 1.7f / glowTex.Height * 2f), SpriteEffects.None, 0f);
                sb.Draw(glowTex, pos, null, BloodMain * (0.7f * speedT), rot, glowOrigin,
                    new Vector2(len / glowTex.Width * 2f, wid / glowTex.Height * 2f), SpriteEffects.None, 0f);
                //旋切亮圈：悬滞与高速时镖缘泛一圈水光
                sb.Draw(glowTex, pos, null, BloodBright * (0.3f + 0.2f * (phase == 1 ? 1f : speedT)), 0f,
                    glowOrigin, new Vector2(30f * 2f / glowTex.Width), SpriteEffects.None, 0f);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //镖体：借原物品贴图旋飞，血湖染色（湖水凝的镖，不是原物）
            Color lit = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            Color color = Color.Lerp(lit, BloodMain, 0.45f);
            sb.Draw(tex, pos, null, color, Projectile.rotation, tex.Size() * 0.5f,
                Profile.DrawScale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
