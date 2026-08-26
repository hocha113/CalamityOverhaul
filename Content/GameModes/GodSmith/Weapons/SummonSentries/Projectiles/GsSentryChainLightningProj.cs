using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles
{
    /// <summary>
    /// 链状闪电（闪电光环 T3 超频周期技）：高速追向目标，命中后向 300px 内下一目标续跳。<br/>
    /// ai[0]=目标 NPC 槽位（NPC 数组服务器权威，各端一致）ai[1]=剩余跳数。
    /// 续跳在 owner 命中回调生成，伤害同值不衰减，封顶由初始跳数控制
    /// </summary>
    internal class GsSentryChainLightningProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonSentries";

        private static readonly Color VoltBright = new(210, 240, 255);
        private static readonly Color VoltMain = new(120, 190, 255);

        private ref float TargetIdx => ref Projectile.ai[0];
        private ref float JumpsLeft => ref Projectile.ai[1];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 45;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Age++;
            if (Age == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
            }
            //追向锚定目标；目标失效则直飞至寿终
            int idx = (int)TargetIdx;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC target = Main.npc[idx];
                if (target.active && target.CanBeChasedBy(Projectile)) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 14f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.4f);
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, VoltMain.ToVector3() * 0.35f);
            if (Age % 3f == 0f) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Projectile.velocity * 0.2f, VoltMain, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(4);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //只在 owner 端执行：续跳生成随原生同步
            if (JumpsLeft <= 0f) {
                return;
            }
            NPC next = null;
            float bestDist = 300f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == target.whoAmI || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = npc.Center.Distance(target.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    next = npc;
                }
            }
            if (next == null) {
                return;
            }
            Vector2 vel = (next.Center - target.Center).SafeNormalize(Vector2.UnitX) * 14f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, vel,
                ModContent.ProjectileType<GsSentryChainLightningProj>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner,
                next.whoAmI, JumpsLeft - 1f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                    VoltBright, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(5);
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, VoltMain, 0.14f)?.Configure(8, 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D arc = CWRAsset.ThunderTrail?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (arc == null || glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            //电弧体：速度拉伸 + 帧抖（identity 去同相）
            float jitter = 0.8f + 0.2f * MathF.Sin(Age * 1.7f + Projectile.identity * 0.9f);
            Color body = VoltMain * (0.85f * jitter);
            body.A = 0;
            Main.EntitySpriteDraw(arc, pos, null, body, Projectile.rotation,
                new Vector2(arc.Width * 0.7f, arc.Height * 0.5f),
                new Vector2(MathHelper.Clamp(speed * 0.05f, 0.3f, 0.9f), 0.16f), SpriteEffects.None, 0);
            Color head = VoltBright * (0.7f * jitter);
            head.A = 0;
            Main.EntitySpriteDraw(glow, pos, null, head, 0f, glow.Size() * 0.5f, 0.26f, SpriteEffects.None, 0);
            return false;
        }
    }
}
