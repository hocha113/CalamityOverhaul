using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DestroyersBladeEXs
{
    /// <summary>
    /// 黑色影子弹幕:吸光暗体拖着红缘游动,蛇形游走强力咬向目标。
    /// 歼灭协议下首穿不亡:贯体续飞一小段泄劲,回身死咬补上第二口。
    /// ai[0]=状态(0未初始化 1追猎 2贯体续飞 3回身二咬) ai[1]=歼灭协议
    /// ai[2]=追猎期游走相位,回身期改存目标编号+1(转移发生在主人端命中拍,随包过线)
    /// </summary>
    internal class DestroyerShadowBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        private static Asset<Texture2D> ShadowTex = null;

        private const float StateSeek = 1f;
        private const float StateOverfly = 2f;
        private const float StateReturn = 3f;
        /// <summary>贯体续飞的更新数(extraUpdates=1,约0.18秒)</summary>
        private const int OverflyUpdates = 22;
        /// <summary>Extra_98 长轴在竖向,绘制统一转90度让长轴贴合航向</summary>
        private const float TexAxisFix = MathHelper.PiOver2;

        private bool Empowered => Projectile.ai[1] > 0f;
        private ref float State => ref Projectile.ai[0];
        //下面两个属性共用 ai[2]:追猎期存游走相位,进入回身流程后改存目标编号+1
        private ref float WavePhase => ref Projectile.ai[2];
        private ref float ReturnTargetSlot => ref Projectile.ai[2];
        private ref float OverflyTimer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.timeLeft = 240;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (State == 0f) {
                //出膛不响,声音留给挥砍拍
                State = StateSeek;
                WavePhase = Projectile.identity * 1.37f % MathHelper.TwoPi;
                if (Empowered) {
                    Projectile.penetrate = 2;
                    Projectile.scale = 1.15f;
                }
            }

            if (State == StateOverfly) {
                UpdateOverfly();
            }
            else if (State == StateReturn) {
                UpdateReturn();
            }
            else {
                UpdateSeek();
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            EmitFlightSmoke();
            Lighting.AddLight(Projectile.Center,
                new Vector3(0.25f, 0.03f, 0.03f) * (State == StateReturn ? 1.6f : 1f));
        }

        /// <summary>追猎:强力追踪死咬最近猎物,蛇形游走,贴近收摆免得晃丢咬口</summary>
        private void UpdateSeek() {
            float steer = Empowered ? 0.12f : 0.07f;
            float range = Empowered ? 1000f : 800f;
            float speedCap = Empowered ? 17f : 13.5f;
            float gain = Empowered ? 0.09f : 0.05f;

            float waveDamp = 1f;
            int target = FindTarget(range);
            if (target >= 0) {
                Vector2 toTarget = Main.npc[target].Center - Projectile.Center;
                float speed = MathF.Min(Projectile.velocity.Length() + gain, speedCap);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                    toTarget.SafeNormalize(Vector2.Zero) * speed, steer);
                waveDamp = MathHelper.Clamp(toTarget.Length() / 160f, 0.15f, 1f);
            }

            //蛇形游走:侧向加速度摆动航向后归一回原速,身体真转向而不是贴图横移
            WavePhase += 0.23f;
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 side = forward.RotatedBy(MathHelper.PiOver2);
            float keepSpeed = Projectile.velocity.Length();
            Projectile.velocity += side * (MathF.Cos(WavePhase) * 0.45f * waveDamp);
            Projectile.velocity = Projectile.velocity.SafeNormalize(forward) * keepSpeed;
        }

        /// <summary>贯体续飞:穿透后泄速滑行一小段读作贯体耗劲,滑完解除免疫回身补咬</summary>
        private void UpdateOverfly() {
            OverflyTimer++;
            Projectile.velocity *= 0.965f;
            //回程续钟逐帧兜底,各端凭同一节拍走,不依赖命中帧的包时序
            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 200);
            if (OverflyTimer < OverflyUpdates) {
                return;
            }
            State = StateReturn;
            Projectile.netUpdate = true;
            int idx = (int)ReturnTargetSlot - 1;
            if (idx >= 0 && idx < Main.maxNPCs) {
                //解除本地一次命中免疫,回身允许再咬同一目标(判伤只在主人端,各端清了也无害)
                Projectile.localNPCImmunity[idx] = 0;
            }
            DoTurnFlourish();
        }

        /// <summary>回身二咬:死咬记账目标,目标失效换最近猎物,速度回攀急转扑回</summary>
        private void UpdateReturn() {
            int idx = (int)ReturnTargetSlot - 1;
            NPC npc = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
            if (npc?.active != true || !npc.CanBeChasedBy()) {
                idx = FindTarget(1100f);
                ReturnTargetSlot = idx + 1;
                npc = idx >= 0 ? Main.npc[idx] : null;
            }
            if (npc == null) {
                return;
            }
            float speed = MathF.Min(MathF.Max(Projectile.velocity.Length(), 8f) + 0.14f, 17.5f);
            Vector2 want = (npc.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * speed;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.24f);
        }

        /// <summary>尾迹排烟:小口黑烟撒在本帧掠过的路径上,出生带弹体冲量、几帧内泄劲悬停</summary>
        private void EmitFlightSmoke() {
            if (VaultUtils.isServer || !Main.rand.NextBool(3)) {
                return;
            }
            Vector2 side = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Vector2 at = Projectile.Center - Projectile.velocity * Main.rand.NextFloat(1.2f)
                + side * Main.rand.NextFloat(-7f, 7f);
            PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(at,
                Projectile.velocity * 0.16f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                new Color(14, 3, 6) * 0.9f,
                Main.rand.NextFloat(0.22f, 0.36f))?.Configure(Main.rand.Next(16, 26), 0.005f);
        }

        /// <summary>回身拍演出:红环重锁 + 几粒火花,提示回马枪开咬</summary>
        private void DoTurnFlourish() {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                new Color(255, 50, 35), 0f)?.Configure(0.04f, 0.4f, 10);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(Projectile.Center, Main.rand.NextVector2Circular(5f, 5f),
                    Main.rand.NextBool(3) ? Color.White : new Color(255, 45, 30),
                    Main.rand.NextFloat(0.8f, 1.3f))?.Configure(false, Main.rand.Next(8, 12));
            }
        }

        private int FindTarget(float range) {
            int best = -1;
            float bestDist = range;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy()) {
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

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //歼灭协议首穿不亡:记下猎物进贯体续飞,回身补第二口(命中钩子只在主人端跑,状态随包过线)
            if (!Empowered || State >= StateOverfly) {
                return;
            }
            State = StateOverfly;
            ReturnTargetSlot = target.whoAmI + 1;
            OverflyTimer = 0f;
            Projectile.netUpdate = true;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            if (!VaultUtils.isServer) {
                //同帧多发齐灭只留一声闷响
                SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.26f, Pitch = -0.5f, MaxInstances = 2, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew }, Projectile.Center);
            }
            //暗爆:小口黑烟带径向冲量崩出,强阻力下泄劲悬停再散
            for (int i = 0; i < 7; i++) {
                Vector2 burst = Main.rand.NextVector2Circular(4.5f, 4.5f);
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(Projectile.Center + burst * 2f, burst,
                    new Color(16, 4, 6) * 0.9f, Main.rand.NextFloat(0.28f, 0.46f))?.Configure(Main.rand.Next(18, 30), 0.008f);
            }
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(Projectile.Center, Main.rand.NextVector2Circular(6f, 6f),
                    new Color(255, 45, 30), Main.rand.NextFloat(1.1f, 1.8f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ShadowTex.Value;
            Vector2 origin = tex.Size() / 2f;
            float speed = Projectile.velocity.Length();
            //速度拉伸落在贴图长轴(竖向)上,配合 TexAxisFix 转正后越快越长越扁,读作高速掠影
            Vector2 stretch = new(0.62f, 1f + speed * 0.05f);
            bool returning = State == StateReturn;

            //拖影:沿旧航向的暗色纺锤逐级缩小收细,是尾迹不是烟堆
            if (Projectile.oldPos != null) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float t = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Main.EntitySpriteDraw(tex, pos, null, new Color(8, 2, 4) * (0.55f * t * t),
                        Projectile.oldRot[i] + TexAxisFix, origin, Projectile.scale * (0.32f + 0.56f * t) * stretch, SpriteEffects.None, 0);
                }
            }

            //本体:红缘勾边 + 吸光暗核,紧贴48px判定体,回身拍红缘更凶
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color rim = new Color(255, 35, 25) * (returning ? 0.85f : 0.6f);
            rim.A = 0;
            Main.EntitySpriteDraw(tex, drawPos, null, rim, Projectile.rotation + TexAxisFix, origin,
                Projectile.scale * 1.2f * stretch, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, new Color(6, 1, 3) * 0.96f, Projectile.rotation + TexAxisFix, origin,
                Projectile.scale * 1f * stretch, SpriteEffects.None, 0);
            //核心里一点红灯,读作影里的机械眼
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color eye = new Color(255, 40, 30) * (returning ? 1f : 0.8f);
            eye.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, eye, 0f, glow.Size() / 2f,
                0.3f * Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
