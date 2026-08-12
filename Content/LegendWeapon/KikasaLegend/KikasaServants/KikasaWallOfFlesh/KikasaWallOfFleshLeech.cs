using CalamityOverhaul.Common;
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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaWallOfFlesh
{
    /// <summary>
    /// 血肉墙鬼奴口吐的血水蛭：一条会在空中"游泳"的小虫弹——
    /// 头+三节体+尾的微型链体（原版水蛭贴图，CPU 血染双层），
    /// 正弦泳姿（速度脉冲 + 航向摆尾）缓慢追踪，命中/贴壁爆成血雾，
    /// 落回血湖则被湖收走。出膛直冲几帧再入泳姿，弹道是活的
    /// </summary>
    internal class KikasaWallOfFleshLeech : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int SegCount = 5;
        private const float SegSpacing = 12f;
        /// <summary>出膛直冲帧数：先射出去，再开始游</summary>
        private const int BurstFrames = 8;

        private ref float Life => ref Projectile.localAI[0];

        private readonly Vector2[] spine = new Vector2[SegCount];
        private readonly float[] segRot = new float[SegCount];
        private bool spineInit;
        private bool lakeSwallowed;

        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDark => KikasaDomain.CoolTint(new(74, 16, 18), new(42, 52, 56));
        private static Color BloodSheen => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        /// <summary>泳姿相位的确定性种子，各端一致（9.1：转向与速度不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f % 4.13f;

        /// <summary>出生 4 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Life++;

            if (!spineInit) {
                RebuildChain(Projectile.velocity.SafeNormalize(Vector2.UnitX));
            }

            if (Life > BurstFrames) {
                //泳姿：航向朝目标缓慢弯、摆尾左右打舵、速度节律性蹬水
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                float heading = dir.ToRotation();

                int target = FindTarget();
                if (target >= 0) {
                    float want = (Main.npc[target].Center - Projectile.Center).ToRotation();
                    heading = heading.AngleTowards(want, 0.052f);
                }
                //摆尾：确定性正弦打舵，游出 S 线
                heading += MathF.Sin(Life * 0.34f + Seed * 5f) * 0.05f;
                //蹬水脉冲：速度呼吸，弹道是活的
                float speed = 8.2f + 2.4f * MathF.Sin(Life * 0.21f + Seed * 2f);
                Projectile.velocity = heading.ToRotationVector2() * speed;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            UpdateChain();

            //泳出的细血屑：蹬水相位最猛时甩落
            if (!Main.dedServ && Life % 4 == 2) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    spine[SegCount - 1] + Main.rand.NextVector2Circular(3f, 3f),
                    -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.2f, 0.4f))
                    ?.Configure(Main.rand.Next(10, 18));
            }

            float glow = 0.35f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.45f * glow, 0.11f * glow, 0.1f * glow);

            //落回血湖：湖收回自己的血，不迸溅
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
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = -0.25f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
            }
        }

        /// <summary>自寻的：规则纯几何（离弹体最近的可追猎目标），各端同规则重演</summary>
        private int FindTarget() {
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true && owner.HasMinionAttackTargetNPC) {
                NPC picked = Main.npc[owner.MinionAttackTargetNPC];
                if (picked.CanBeChasedBy(Projectile)
                    && Vector2.Distance(picked.Center, Projectile.Center) < 1300f) {
                    return picked.whoAmI;
                }
            }
            int best = -1;
            float bestDist = 1100f;
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

        //==================== 链体 ====================

        private void RebuildChain(Vector2 headDir) {
            spineInit = true;
            Vector2 back = -headDir.SafeNormalize(Vector2.UnitX);
            float wormRot = headDir.ToRotation() + MathHelper.PiOver2;
            for (int i = 0; i < SegCount; i++) {
                spine[i] = Projectile.Center + back * (i * SegSpacing);
                segRot[i] = wormRot;
            }
        }

        private void UpdateChain() {
            Vector2 head = Projectile.Center + Projectile.velocity;
            if (Vector2.Distance(spine[0], head) > 90f) {
                RebuildChain(Projectile.velocity.SafeNormalize(Vector2.UnitX));
                return;
            }
            spine[0] = head;
            segRot[0] = Projectile.rotation;
            //逐节贴前节后方，转差阻尼旋转——小虫的柔体
            for (int i = 1; i < SegCount; i++) {
                Vector2 toPrev = spine[i - 1] - spine[i];
                if (segRot[i - 1] != segRot[i]) {
                    toPrev = toPrev.RotatedBy(MathHelper.WrapAngle(segRot[i - 1] - segRot[i]) * 0.22f);
                }
                segRot[i] = toPrev.ToRotation() + MathHelper.PiOver2;
                spine[i] = spine[i - 1] - toPrev.SafeNormalize(Vector2.Zero) * SegSpacing;
            }
        }

        //==================== 命中与谢幕 ====================

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            //爆成血雾：半球血珠 + 潮雾 + 扩散环，虫身散成残珠
            Vector2 normal = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    normal.RotatedBy(Main.rand.NextFloat(-1.3f, 1.3f)) * Main.rand.NextFloat(1.8f, 5.5f),
                    Main.rand.NextBool(3) ? BloodDark : BloodMain,
                    Main.rand.NextFloat(0.35f, 0.65f))?.Configure(Main.rand.Next(16, 28));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.3f), MistBlood * 0.8f, Main.rand.NextFloat(0.5f, 0.8f))
                ?.Configure(Main.rand.Next(40, 70));
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, BloodDark, 0.07f)
                ?.Configure(new Vector2(0.7f, 1f), normal.ToRotation(), 0.2f, 8);
            if (spineInit) {
                for (int i = 1; i < SegCount; i += 2) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(spine[i],
                        Main.rand.NextVector2Circular(1.2f, 1.2f),
                        BloodMain * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(Main.rand.Next(12, 20), 0.3f);
                }
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.45f, Pitch = -0.05f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.3f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (!spineInit) {
                return false;
            }
            Main.instance.LoadNPC(NPCID.LeechHead);
            Main.instance.LoadNPC(NPCID.LeechBody);
            Main.instance.LoadNPC(NPCID.LeechTail);
            Texture2D headTex = TextureAssets.Npc[NPCID.LeechHead]?.Value;
            Texture2D bodyTex = TextureAssets.Npc[NPCID.LeechBody]?.Value;
            Texture2D tailTex = TextureAssets.Npc[NPCID.LeechTail]?.Value;
            if (headTex == null || bodyTex == null || tailTex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            float fade = VisualFade;
            //蹬水相位驱动的体节压扁拉伸：游泳的呼吸
            float pulse = MathF.Sin(Life * 0.21f + Seed * 2f);

            //尾→头叠绘：暗缘垫底给体积，血红主体盖面
            for (int i = SegCount - 1; i >= 0; i--) {
                Texture2D tex = i == 0 ? headTex : i == SegCount - 1 ? tailTex : bodyTex;
                Vector2 pos = spine[i] - Main.screenPosition;
                float rot = segRot[i];
                Vector2 stretch = new(1f - pulse * 0.08f, 1f + pulse * 0.12f);
                Vector2 origin = tex.Size() * 0.5f;

                sb.Draw(tex, pos, null, BloodDark * (0.8f * fade), rot, origin,
                    stretch * 1.18f, SpriteEffects.None, 0f);
                sb.Draw(tex, pos, null, BloodMain * fade, rot, origin,
                    stretch, SpriteEffects.None, 0f);
            }

            //头部湿反光小亮斑
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                sb.Draw(glow, spine[0] - Main.screenPosition, null,
                    (BloodSheen with { A = 0 }) * (0.35f * fade), 0f,
                    glow.Size() * 0.5f, new Vector2(12f / glow.Width), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
