using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 黄道环:司祭划下的天穹界(战场边界),同时是合相的空间化读数<br/>
    /// 环上五颗行星信标随充能(owner.ai[3])向天顶汇聚,连珠即大祭<br/>
    /// ai[0]=宿主npc ai[1]=阶段 0展开 1常驻 2收拢;推回只作用于本机玩家,无伤害软墙(收拢期不推)
    /// </summary>
    internal class CultistZodiacRing : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int GrowFrames = 70;
        /// <summary>信标散布半角(充能 0 时相对天顶的最大偏角)</summary>
        private const float BeaconSpread = 2.2f;

        private int OwnerWho => (int)Projectile.ai[0];
        private int Stage => (int)Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];
        /// <summary>当前可见半径</summary>
        private ref float Radius => ref Projectile.localAI[1];
        /// <summary>撞墙脉冲(本地演出量)</summary>
        private float wallPulse;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.netImportant = true;
            //必须配合 DrawBehind 设 hide:否则原版普通弹幕层会再画一遍,
            //穹膜内侧亮带糊在低槽位弹幕上面,贴墙时全屏弹幕"消失"
            Projectile.hide = true;
        }

        public override void AI() {
            Timer++;
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            bool ownerAlive = owner != null && owner.active && owner.type == NPCID.CultistBoss;

            if (!ownerAlive && Stage != 2) {
                Projectile.ai[1] = 2;
                Timer = 0;
            }

            switch (Stage) {
                case 0: {
                    float t = MathHelper.Clamp(Timer / GrowFrames, 0f, 1f);
                    Radius = CultistStateContext.ArenaRadius * (1f - (1f - t) * (1f - t));
                    if (Timer >= GrowFrames && !VaultUtils.isClient) {
                        Projectile.ai[1] = 1;
                        Projectile.netUpdate = true;
                    }
                    break;
                }
                case 1:
                    Radius = CultistStateContext.ArenaRadius;
                    break;
                default:
                    Radius *= 0.94f;
                    if (Radius < 40f) {
                        Projectile.Kill();
                        return;
                    }
                    break;
            }
            if (Projectile.timeLeft < 120 && Stage != 2) {
                Projectile.timeLeft = 120;
            }

            wallPulse *= 0.90f;
            PushLocalPlayerInside();
        }

        /// <summary>软墙:出界的本机玩家被持续推回,越远推力越大;收拢期界职已卸,不推(缩环扫场会把玩家加速甩向场心再抛飞)</summary>
        private void PushLocalPlayerInside() {
            if (Main.dedServ || Stage == 2 || Radius < 200f) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (!player.Alives()) {
                return;
            }
            Vector2 delta = player.Center - Projectile.Center;
            float dist = delta.Length();
            float wall = Radius - 30f;
            if (dist <= wall) {
                return;
            }
            Vector2 inward = (-delta).SafeNormalize(Vector2.UnitY);
            float overshoot = dist - wall;
            player.velocity += inward * MathHelper.Clamp(0.55f + overshoot * 0.02f, 0.55f, 3.2f);
            wallPulse = MathHelper.Max(wallPulse, 0.8f);
            if (Main.GameUpdateCount % 10 == 0) {
                int ownerPhase = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? (int)Main.npc[OwnerWho].ai[0] : 0;
                CultistMotion.RuneBurst(player.Center + inward * -20f, CultistMotion.PhaseCore(ownerPhase), 1, 2f);
            }
        }

        /// <summary>穹膜受击脉冲(各端本地演出):星球砸上结界时点亮环膜</summary>
        internal static void PulseWall(int ownerWho, float amount = 1f) {
            int type = ModContent.ProjectileType<CultistZodiacRing>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == ownerWho && proj.ModProjectile is CultistZodiacRing ring) {
                    ring.wallPulse = MathHelper.Max(ring.wallPulse, amount);
                }
            }
        }

        /// <summary>命令收拢(权威端)</summary>
        internal static void BeginCollapse(int ownerWho) {
            int type = ModContent.ProjectileType<CultistZodiacRing>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == ownerWho && (int)proj.ai[1] != 2) {
                    proj.ai[1] = 2;
                    proj.localAI[0] = 0f;
                    proj.netUpdate = true;
                }
            }
        }

        public override bool ShouldUpdatePosition() => false;

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Radius < 30f) {
                return false;
            }
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            int phase = owner != null && owner.active ? (int)owner.ai[0] : 0;
            float align = owner != null && owner.active
                ? MathHelper.Clamp(owner.ai[3] / CultistStateContext.AlignMax, 0f, 1f) : 0f;
            float reveal = MathHelper.Clamp(Radius / CultistStateContext.ArenaRadius, 0f, 1f);
            Color core = CultistMotion.PhaseCore(phase);

            SpriteBatch sb = Main.spriteBatch;
            DrawDome(sb, core, reveal, align);
            DrawBeacons(sb, core, reveal, align);
            return false;
        }

        /// <summary>穹膜(CultistBoundary.fx):主环=画布 0.88,quad 按半径折算</summary>
        private void DrawDome(SpriteBatch sb, Color core, float reveal, float align) {
            Effect effect = EffectLoader.CultistBoundary?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || canvas == null || noise == null) {
                return;
            }

            Color rim = Color.Lerp(core, Color.White, 0.55f);
            effect.CurrentTechnique = effect.Techniques["TechBoundary"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(0.9f * reveal);
            effect.Parameters["uFill"]?.SetValue(0f);
            effect.Parameters["uPulse"]?.SetValue(MathHelper.Clamp(wallPulse + (align > 0.97f ? 0.35f : 0f), 0f, 1f));
            effect.Parameters["uColMain"]?.SetValue(core.ToVector3());
            effect.Parameters["uColRim"]?.SetValue(rim.ToVector3());

            float quadSize = Radius / 0.88f * 2f;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White, 0f,
                canvas.Size() * 0.5f, quadSize / canvas.Width, SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 合相信标:五颗行星色星珠沿环行走,随充能向天顶收拢;连珠时齐聚脉动<br/>
        /// 充能读数不再是抽象条,是天上五颗星的距离
        /// </summary>
        private void DrawBeacons(SpriteBatch sb, Color core, float reveal, float align) {
            const float Zenith = -MathHelper.PiOver2;
            float breathe = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.4f) * 0.04f;
            for (int i = 0; i < 5; i++) {
                //散布:0 号居中,其余对称展开;充能收拢
                float offset = (i - 2) * 0.5f * BeaconSpread * (1f - align);
                float drift = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 0.6f + i * 1.3f) * 0.06f * (1f - align);
                float angle = Zenith + offset + drift;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Radius - Main.screenPosition;
                Color mid = CultistMotion.PhaseCore(i);
                Color edge = CultistMotion.PhaseEdge(i);
                float pulse = align > 0.97f ? 1.35f + breathe * 6f : 1f + breathe;
                CultistOrreryRenderer.DrawStarBead(sb, pos, mid, edge,
                    0.30f * pulse, (0.55f + 0.45f * align) * reveal, Main.GlobalTimeWrappedHourly * 0.7f + i);
            }

            //十二宫刻痕:环上静默的分度(加大加亮:环缘远观也有锚点)
            for (int i = 0; i < 12; i++) {
                float angle = i / 12f * MathHelper.TwoPi;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Radius - Main.screenPosition;
                CultistOrreryRenderer.DrawStarBead(sb, pos, CultistMotion.RuneGold,
                    CultistMotion.RuneGold, 0.17f, 0.55f * reveal, angle);
            }
        }
    }
}
