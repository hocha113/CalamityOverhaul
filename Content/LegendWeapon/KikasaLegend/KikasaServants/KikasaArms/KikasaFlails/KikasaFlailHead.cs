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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaFlails
{
    /// <summary>
    /// 湖水锤头：锤奴的自研锤头弹幕。直掷档无重力冲向猎物、到程或砸中驻一拍；
    /// 高抛档带坠加速走弧线、弧顶后越坠越快（齐砸的画面骨架）；
    /// 驻拍后沿链拽回掷出它的那只鬼手。链条画血水珠链（本就是血水凝的，
    /// 不映射原版 chain 纹理），锤体借原连枷弹幕贴图。
    /// 相位全在本地推进（速度随生成包到位，各端独立推导一致），
    /// authority 判定归手消亡，远端超时兜底。
    /// ai0=父锤手 identity、ai1=手序+高抛旗打包、ai2=武器物品类型（贴图与档案之源）
    /// </summary>
    internal class KikasaFlailHead : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>归手判定半径：进圈即被拽住</summary>
        private const float HaulDist = 32f;

        /// <summary>驻拍时长：砸中的顿感</summary>
        private const int StunFrames = 10;

        /// <summary>高抛档坠加速</summary>
        private const float SlamGravity = 0.34f;

        private int ParentIdentity => (int)Projectile.ai[0];
        private int PackedHand => (int)Projectile.ai[1];
        private int HandIndex => UnpackHand(PackedHand);
        private bool SlamMode => PackedHand >= 8;
        private int ArmsItemType => (int)Projectile.ai[2];

        /// <summary>手序与高抛旗共用 ai1：低 3 位手序、bit3 高抛</summary>
        internal static int PackHand(int hand, bool slam) => hand + (slam ? 8 : 0);

        internal static int UnpackHand(int packed) => packed & 7;

        private KikasaFlailProfile? profileCache;

        private KikasaFlailProfile Profile => profileCache ??= KikasaArmsProfiler.FlailProfileOf(ArmsItemType);

        //相位：0 掷出 / 1 驻拍 / 2 拽回（各端本地推进）
        private int phase;
        private int phaseTimer;
        /// <summary>直掷累计里程：到档案甩程即驻拍折返</summary>
        private float mileage;
        /// <summary>缓存父锤手弹幕位（whoAmI），identity 校验失配再重扫</summary>
        private int parentCache = -1;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
        }

        public override void AI() {
            phaseTimer++;
            //锤体翻滚：飞得越快滚得越急
            Projectile.rotation += 0.09f * MathF.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X)
                * MathHelper.Clamp(Projectile.velocity.Length() / 10f, 0.4f, 1.6f);

            switch (phase) {
                case 0: {
                    if (SlamMode) {
                        //高抛：坠加速拉出砸落弧，坠速见顶或超时即驻拍（多半先砸中）
                        Projectile.velocity.Y += SlamGravity;
                        if (Projectile.velocity.Y > 15f || phaseTimer > 80) {
                            EnterStun();
                        }
                    }
                    else {
                        //直掷：到程即驻拍折返
                        mileage += Projectile.velocity.Length();
                        if (mileage >= Profile.Reach) {
                            EnterStun();
                        }
                    }
                    break;
                }
                case 1: {
                    //驻拍：速度骤衰，砸势的顿
                    Projectile.velocity *= 0.72f;
                    if (phaseTimer >= StunFrames) {
                        phase = 2;
                        phaseTimer = 0;
                    }
                    break;
                }
                default: {
                    //拽回：渐加速沿链收回，近了加大转向权重防绕圈
                    Vector2 home = FindHaulPoint();
                    Vector2 to = home - Projectile.Center;
                    float dist = to.Length();
                    if (dist < HaulDist) {
                        if (Main.myPlayer == Projectile.owner) {
                            Projectile.Kill();
                        }
                        return;
                    }
                    float speed = MathF.Min(Profile.FlightSpeed * (0.5f + phaseTimer * 0.03f),
                        Profile.FlightSpeed * 1.3f);
                    float steer = dist < 100f ? 0.3f : 0.12f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                        to.SafeNormalize(Vector2.UnitX) * speed, steer);
                    break;
                }
            }

            //尾迹滴珠：飞行段甩、驻拍不甩
            if (!Main.dedServ && phase != 1 && phaseTimer % 4 == 0 && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center - Projectile.velocity * 0.4f,
                    -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    BloodMain * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.26f, 0.42f))
                    ?.Configure(Main.rand.Next(10, 16), 0f);
            }

            const float glow = 0.28f;
            Lighting.AddLight(Projectile.Center, 0.9f * glow, 0.28f * glow, 0.24f * glow);
        }

        private void EnterStun() {
            if (phase != 0) {
                return;
            }
            phase = 1;
            phaseTimer = 0;
        }

        /// <summary>归手点：父锤手在场读本手实时位，编队没了退回主人身侧</summary>
        private Vector2 FindHaulPoint() {
            if (parentCache >= 0 && parentCache < Main.maxProjectiles) {
                Projectile cached = Main.projectile[parentCache];
                if (cached.active && cached.identity == ParentIdentity
                    && cached.ModProjectile is KikasaFlailServant cachedPack) {
                    return cachedPack.HaulPointOf(HandIndex);
                }
                parentCache = -1;
            }
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.identity == ParentIdentity && proj.owner == Projectile.owner
                    && proj.ModProjectile is KikasaFlailServant pack) {
                    parentCache = proj.whoAmI;
                    return pack.HaulPointOf(HandIndex);
                }
            }
            Player owner = Main.player[Projectile.owner];
            return owner?.active == true ? owner.Center : Projectile.Center;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //砸中即驻拍：重锤命中的顿感
            EnterStun();
            SoundEngine.PlaySound(SoundID.NPCHit42 with {
                Volume = 0.4f,
                Pitch = -0.35f,
                MaxInstances = 3
            }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int k = 0; k < 6; k++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    dir.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(2f, 5.5f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.32f, 0.55f))?.Configure(Main.rand.Next(12, 24));
            }
            //砸点冲击环：锤的分量
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, BloodBright, 0.05f)
                ?.Configure(new Vector2(0.6f, 1f), dir.ToRotation(), 0.2f, 8);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //归手/超时都散成水：锤本就是湖水凝的
            for (int k = 0; k < 5; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(Projectile.Center,
                    Main.rand.NextVector2Circular(1.8f, 1.8f) + new Vector2(0f, 0.8f),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.55f),
                    Main.rand.NextFloat(0.28f, 0.44f))
                    ?.Configure(Main.rand.Next(10, 18), 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Profile.HeadProjType);
            Texture2D tex = TextureAssets.Projectile[Profile.HeadProjType]?.Value;
            if (tex == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //血水珠链：鬼手到锤头，隔距撒珠（本就是血水凝的链）
            Vector2 hand = FindHaulPoint();
            DrawBeadChain(sb, hand, Projectile.Center);

            //速度拉伸血尾 + 驻拍崩珠光
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                float speedT = MathHelper.Clamp(Projectile.velocity.Length() / Profile.FlightSpeed, 0f, 1.2f);
                if (speedT > 0.2f) {
                    float rot = Projectile.velocity.ToRotation();
                    float len = 40f * speedT;
                    sb.Draw(glowTex, pos - Projectile.velocity.SafeNormalize(Vector2.Zero) * len * 0.4f, null,
                        BloodDeep * (0.5f * speedT), rot, glowTex.Size() * 0.5f,
                        new Vector2(len * 1.2f / glowTex.Width * 2f, 9f * 1.6f / glowTex.Height * 2f), SpriteEffects.None, 0f);
                    sb.Draw(glowTex, pos, null, BloodMain * (0.65f * speedT), rot, glowTex.Size() * 0.5f,
                        new Vector2(len / glowTex.Width * 2f, 9f / glowTex.Height * 2f), SpriteEffects.None, 0f);
                }
                //驻拍水光：砸势的一瞬崩亮
                if (phase == 1) {
                    float stunT = 1f - phaseTimer / (float)StunFrames;
                    sb.Draw(glowTex, pos, null, BloodBright * (0.55f * stunT), 0f,
                        glowTex.Size() * 0.5f, new Vector2(34f * 2f / glowTex.Width), SpriteEffects.None, 0f);
                }
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //锤体：借原连枷弹幕贴图翻滚，血湖染色（湖水凝的锤，不是原物）
            Color lit = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            Color color = Color.Lerp(lit, BloodMain, 0.45f);
            sb.Draw(tex, pos, null, color, Projectile.rotation, tex.Size() * 0.5f,
                Profile.DrawScale, SpriteEffects.None, 0f);
            return false;
        }

        /// <summary>手到锤头的血水珠链：隔距撒珠、靠锤端珠更大，Additive 一批画完</summary>
        private static void DrawBeadChain(SpriteBatch sb, Vector2 from, Vector2 to) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float dist = Vector2.Distance(from, to);
            if (dist < 12f || dist > 1600f) {
                return;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Vector2 origin = glow.Size() * 0.5f;
            int beads = Math.Clamp((int)(dist / 20f), 2, 30);
            for (int k = 0; k <= beads; k++) {
                float t = k / (float)beads;
                Vector2 at = Vector2.Lerp(from, to, t);
                float size = MathHelper.Lerp(4.5f, 7.5f, t);
                sb.Draw(glow, at - Main.screenPosition, null,
                    BloodDeep * 0.6f, 0f, origin,
                    new Vector2(size * 2f / glow.Width), SpriteEffects.None, 0f);
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
