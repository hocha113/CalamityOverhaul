using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Projectiles
{
    /// <summary>
    /// 冲刺导引线(无伤害纯预警)：蓄势期跟踪瞄准→锁向后绷直加亮→起扑瞬间消失。
    /// ai[0]=源NPC索引 ai[1]=蓄势总帧 ai[2]=锁向标记；velocity=方向载体(不位移)。
    /// 权威端生成并在锁向帧写入方向+netUpdate，各端方向一致(预告即承诺)
    /// </summary>
    internal class EowLungeOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const float LineWidth = 58f;

        private int SourceIndex => (int)Projectile.ai[0];
        private int ChargeFrames => Math.Max((int)Projectile.ai[1], 6);
        private bool Locked => Projectile.ai[2] == 1f;

        private int Age => (int)Projectile.localAI[0];
        private float ChargeT => MathHelper.Clamp(Age / (float)ChargeFrames, 0f, 1f);
        /// <summary>展示长度(平滑缓动存localAI[1])</summary>
        private float LineLength => Projectile.localAI[1];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        private static bool IsEowSegment(NPC npc) => npc.type == NPCID.EaterofWorldsHead
            || npc.type == NPCID.EaterofWorldsBody || npc.type == NPCID.EaterofWorldsTail;

        /// <summary>解析源NPC，无效返回null</summary>
        private NPC ResolveSource() {
            if (SourceIndex < 0 || SourceIndex >= Main.maxNPCs) {
                return null;
            }
            NPC src = Main.npc[SourceIndex];
            return src.active && IsEowSegment(src) ? src : null;
        }

        /// <summary>解析追踪目标：体节先折算到头再取其target</summary>
        private static Player ResolveTarget(NPC source) {
            NPC head = source;
            if (source.realLife >= 0 && source.realLife < Main.maxNPCs
                && Main.npc[source.realLife].active) {
                head = Main.npc[source.realLife];
            }
            if (head.target < 0 || head.target >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[head.target];
            return player.Alives() ? player : null;
        }

        /// <summary>锁向(权威端)：给该源NPC所有未锁定导引线写入最终方向并同步</summary>
        internal static void Lock(int sourceIndex, Vector2 dir) {
            int type = ModContent.ProjectileType<EowLungeOmen>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == sourceIndex && proj.ai[2] == 0f) {
                    proj.velocity = dir;
                    proj.ai[2] = 1f;
                    proj.netUpdate = true;
                }
            }
        }

        /// <summary>
        /// 清除该头部及其分组首节名下的全部导引线。
        /// 状态被蜕皮/死亡演出中途打断时调用，防止残留的线继续承诺一次不会发生的冲刺；
        /// 各端本地执行(服务端Kill自带29号包广播，客户端仅移除本地副本)
        /// </summary>
        internal static void ClearFor(int headIndex) {
            int type = ModContent.ProjectileType<EowLungeOmen>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != type) {
                    continue;
                }
                int src = (int)proj.ai[0];
                if (src == headIndex
                    || (src >= 0 && src < Main.maxNPCs && Main.npc[src].realLife == headIndex)) {
                    proj.Kill();
                }
            }
        }

        public override void AI() {
            NPC source = ResolveSource();
            if (source == null) {
                Projectile.Kill();
                return;
            }

            if (Projectile.localAI[2] == 0f) {
                Projectile.localAI[2] = 1f;
                Projectile.timeLeft = ChargeFrames;
            }
            Projectile.localAI[0]++;

            //跟随口部
            Projectile.Center = EowSpitBarrageState.MouthPos(source);

            Player target = ResolveTarget(source);

            //蓄势期本地跟踪瞄准(仅展示)；锁向后方向由同步包钉死
            if (!Locked && target != null) {
                Vector2 aim = target.Center + target.velocity * 12f;
                Projectile.velocity = (aim - Projectile.Center).SafeNormalize(Projectile.velocity);
            }

            //展示长度：盖过目标一段，缓动防跳变
            float wantLen = 760f;
            if (target != null) {
                wantLen = MathHelper.Clamp(
                    Vector2.Distance(Projectile.Center, target.Center) + 320f, 480f, 1150f);
            }
            Projectile.localAI[1] = Projectile.localAI[1] == 0f
                ? wantLen : MathHelper.Lerp(Projectile.localAI[1], wantLen, 0.25f);

            if (VaultUtils.isServer || !EowMotionFX.OnScreen(Projectile.Center, 600f)) {
                return;
            }

            //沿线飘散的酸屑(密度随充能)
            if (Main.rand.NextFloat() < 0.35f + ChargeT * 0.4f) {
                float along = Main.rand.NextFloat(0.1f, 0.9f) * LineLength;
                Vector2 pos = Projectile.Center + Projectile.velocity * along;
                Dust dust = Dust.NewDustDirect(pos, 4, 4, DustID.CursedTorch, 0, 0, 130, default,
                    Main.rand.NextFloat(0.7f, 1.2f));
                dust.velocity = Projectile.velocity * Main.rand.NextFloat(1f, 3f)
                    + Main.rand.NextVector2Circular(0.5f, 0.5f);
                dust.noGravity = true;
                dust.noLight = true;
            }
            Lighting.AddLight(Projectile.Center, EowMotionFX.AcidGreen.ToVector3() * (0.2f + ChargeT * 0.5f));
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC source = ResolveSource();
            if (source == null || LineLength <= 0f) {
                return false;
            }

            Vector2 origin = Projectile.Center - Main.screenPosition;
            float rot = Projectile.velocity.ToRotation();
            //锁向闪：ai同步为准，客户端末段兜底(丢包时也有承诺帧)
            float lockGlow = Locked ? 1f : MathHelper.Clamp((ChargeT - 0.86f) / 0.14f, 0f, 1f);

            Effect effect = EffectLoader.EowGeyser?.Value;
            if (effect != null) {
                effect.CurrentTechnique = effect.Techniques["TechGuide"];
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI % 79 * 0.137f);
                effect.Parameters["uProgress"]?.SetValue(ChargeT);
                effect.Parameters["uFade"]?.SetValue(0f);
                effect.Parameters["uAspect"]?.SetValue(LineLength / LineWidth);
                effect.Parameters["uLock"]?.SetValue(lockGlow);
                effect.Parameters["uDirtColor"]?.SetValue(EowMotionFX.DirtBrown.ToVector3());
                effect.Parameters["uAcidColor"]?.SetValue(EowMotionFX.AcidGreen.ToVector3());

                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
                effect.CurrentTechnique.Passes[0].Apply();

                Texture2D pixel = VaultAsset.placeholder2.Value;
                Vector2 scale = new Vector2(LineLength / pixel.Width, LineWidth / pixel.Height);
                sb.Draw(pixel, origin, null, Color.White, rot,
                    new Vector2(0f, pixel.Height / 2f), scale, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                return false;
            }

            //回退：软光横拉(径向贴图两端自带渐隐，无平切)
            Texture2D softGlow = CWRAsset.SoftGlow.Value;
            Color warn = EowMotionFX.AcidGreen with { A = 0 }
                * ((0.2f + 0.6f * ChargeT * ChargeT) * (1f + lockGlow * 0.6f));
            Main.EntitySpriteDraw(softGlow, origin, null, warn, rot,
                new Vector2(0f, softGlow.Height / 2f),
                new Vector2(LineLength / softGlow.Width, 26f / softGlow.Height), SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI) {
            behindNPCs.Add(index);
        }
    }
}
