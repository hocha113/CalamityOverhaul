using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
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
    /// 蚀祭幻影龙(星尘相专属):司祭自本影中唤出的幻影龙,两段直线冲撞玩家后消散<br/>
    /// ai[0]=宿主npc ai[1]=首段侧翼符号 ai[2]=段 0现身预瞄 1冲撞一 2回转预瞄 3冲撞二 4消散(权威端推进,远端看门对齐)<br/>
    /// 身躯=原版幻影龙贴图链节(剪影/调色白送),头节位置权威广播,体节各端跟随头节本地积分<br/>
    /// 公平阀:每段冲撞前预瞄线 AimShowFrames 帧渐显、末 AimFreezeFrames 帧冻结(预告即承诺),
    /// 冲撞纯直线不追踪=侧移即避;预瞄/回转/消散段无伤;本影楔对它不豁免(独立威胁层,楔内也要侧移)
    /// </summary>
    internal class CultistEclipseDragon : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>现身+首段预瞄时长</summary>
        private const int Aim1Frames = 56;
        /// <summary>回转+二段预瞄时长(已见过一次,略短)</summary>
        private const int Aim2Frames = 44;
        /// <summary>单段冲撞时长(约 1000px 直线)</summary>
        private const int DashFrames = 34;
        /// <summary>消散段时长</summary>
        private const int DissolveFrames = 26;
        /// <summary>预瞄线显示窗(段末渐显)</summary>
        private const int AimShowFrames = 30;
        /// <summary>预瞄冻结窗:此后线不再跟人=承诺</summary>
        private const int AimFreezeFrames = 8;
        private const float DashSpeed = 30f;
        /// <summary>体节数(含头尾)与节距</summary>
        private const int SegmentCount = 11;
        private const float SegmentGap = 30f;
        /// <summary>体节判定半径:窄于贴图可见宽(对玩家宽容)</summary>
        private const float HitRadius = 24f;

        private static readonly int[] BodyIds = [
            NPCID.CultistDragonBody1, NPCID.CultistDragonBody2,
            NPCID.CultistDragonBody3, NPCID.CultistDragonBody4,
        ];

        private int OwnerWho => (int)Projectile.ai[0];
        private float FlankSide => Projectile.ai[1] >= 0f ? 1f : -1f;
        private int Stage => (int)Projectile.ai[2];
        private ref float StageAge => ref Projectile.localAI[0];

        /// <summary>上帧段号:远端由 ai[2] 同步得知换段,本地段龄归零</summary>
        private int lastSeenStage = -1;
        /// <summary>预瞄方向(本地缓存,冻结窗内不再更新)</summary>
        private Vector2 aimDir = Vector2.UnitX;
        /// <summary>本地已按拍预掷(客户端先走本地预瞄,权威 ai[2]+速度包到达后回正)</summary>
        private bool provisionalLaunched;
        /// <summary>本段侧翼锚符号(段首锁定,预瞄期锚点不来回横跳)</summary>
        private float sideCache = 1f;
        /// <summary>现身/消散包络 0~1</summary>
        private float fade;
        /// <summary>体节链(跟随头节的本地积分,纯视觉与判定形体)</summary>
        private readonly Vector2[] segments = new Vector2[SegmentCount];
        private bool segmentsInit;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            //换段看门:远端由 ai[2] 同步得知换段,本地段龄与预掷标记复位
            if (Stage != lastSeenStage) {
                lastSeenStage = Stage;
                StageAge = 0f;
                provisionalLaunched = false;
            }
            StageAge++;

            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            if (owner == null || !owner.active || owner.type != NPCID.CultistBoss) {
                CultistMotion.RuneBurst(Projectile.Center, CultistMotion.StardustCore, 6, 5f);
                Projectile.Kill();
                return;
            }
            Player target = owner.target >= 0 && owner.target < 255 ? Main.player[owner.target] : null;

            //目标失效:直接转消散(权威端)
            if (!VaultUtils.isClient && Stage < 4 && !target.Alives()) {
                Projectile.ai[2] = 4;
                Projectile.netUpdate = true;
            }

            fade = Stage switch {
                0 => MathHelper.Min(1f, StageAge / 18f),
                4 => MathHelper.Max(0f, 1f - StageAge / (DissolveFrames * 0.8f)),
                _ => 1f,
            };

            switch (Stage) {
                case 0:
                case 2: {
                    int aimFrames = Stage == 0 ? Aim1Frames : Aim2Frames;
                    if ((int)StageAge == 1) {
                        //段首锁侧翼:首段用出生侧,二段用当前所在侧(冲过头后自然的回马位)
                        sideCache = Stage == 0 ? FlankSide
                            : target.Alives() && Projectile.Center.X < target.Center.X ? -1f : 1f;
                        if (Stage == 0) {
                            CultistMotion.SigilCommitFX(Projectile.Center, CultistMotion.StardustCore, 1.1f);
                            CultistMotion.RuneBurst(Projectile.Center, CultistMotion.StardustCore, 10, 7f);
                        }
                    }
                    //侧翼缓浮找位:预瞄期悬在玩家侧上方,不贴脸
                    if (target.Alives()) {
                        Vector2 anchor = target.Center + new Vector2(sideCache * 660f, -150f)
                            + CultistMotion.BreathingOffset(seed: 23.7f + Stage, 12f);
                        Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.055f);
                    }
                    Projectile.velocity = Vector2.Zero;

                    //预瞄:显示窗内跟人,冻结窗锁死(预告即承诺)
                    if (target.Alives() && StageAge < aimFrames - AimFreezeFrames) {
                        aimDir = (CultistMotion.PredictTarget(target, Projectile.Center, DashSpeed, 0.35f)
                            - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    }
                    //出手拍:权威定案翻段,客户端先按本地预瞄走(速度包到达即回正)
                    if (StageAge >= aimFrames && !provisionalLaunched) {
                        provisionalLaunched = true;
                        Projectile.velocity = aimDir * DashSpeed;
                        if (!VaultUtils.isClient) {
                            Projectile.ai[2] = Stage + 1;
                            Projectile.netUpdate = true;
                        }
                        CultistMotion.CastFlash(Projectile.Center, CultistMotion.StardustCore, 1.1f);
                        CultistMotion.Shake(Projectile.Center, 4f, 10);
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.95f, Pitch = -0.1f }, Projectile.Center);
                        }
                    }
                    break;
                }
                case 1:
                case 3: {
                    //冲撞:纯直线(锁向即承诺,不追踪),尾段微衰出力竭感
                    if (StageAge > DashFrames * 0.7f) {
                        Projectile.velocity *= 0.985f;
                    }
                    if (!VaultUtils.isClient && StageAge >= DashFrames) {
                        Projectile.ai[2] = Stage + 1;
                        Projectile.netUpdate = true;
                    }
                    break;
                }
                default: {
                    //消散:减速渐隐,沿身躯散回星尘
                    Projectile.velocity *= 0.92f;
                    if ((int)StageAge == 1 && !VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
                    }
                    if ((int)StageAge % 4 == 0 && segmentsInit) {
                        CultistMotion.RuneBurst(segments[Main.rand.Next(SegmentCount)],
                            CultistMotion.StardustCore, 1, 3f);
                    }
                    if (StageAge >= DissolveFrames) {
                        Projectile.Kill();
                        return;
                    }
                    break;
                }
            }

            UpdateSegments();
            Lighting.AddLight(Projectile.Center, CultistMotion.StardustCore.ToVector3() * 0.5f * fade);
        }

        /// <summary>体节跟随:逐节向前节收拢到定距(头节权威广播,链身各端本地一致演化)</summary>
        private void UpdateSegments() {
            if (!segmentsInit) {
                segmentsInit = true;
                for (int i = 0; i < SegmentCount; i++) {
                    segments[i] = Projectile.Center + new Vector2(0f, i * SegmentGap * 0.4f);
                }
            }
            segments[0] = Projectile.Center;
            for (int i = 1; i < SegmentCount; i++) {
                Vector2 toPrev = segments[i - 1] - segments[i];
                if (toPrev.LengthSquared() < 0.001f) {
                    toPrev = Vector2.UnitY;
                }
                segments[i] = segments[i - 1] - toPrev.SafeNormalize(Vector2.UnitY) * SegmentGap;
            }
        }

        /// <summary>伤害窗=冲撞段(现身/预瞄/回转/消散只是幻影)</summary>
        public override bool CanHitPlayer(Player target) {
            return (Stage == 1 || Stage == 3) && StageAge > 2f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if ((Stage != 1 && Stage != 3) || StageAge <= 2f) {
                return false;
            }
            //头节沿速度短刃(高速防穿帧)
            float point = 0f;
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - Projectile.velocity, Projectile.Center + Projectile.velocity * 0.5f,
                HitRadius * 2f, ref point)) {
                return true;
            }
            //链身逐节圆判:身躯可见即危险
            for (int i = 1; i < SegmentCount; i++) {
                Vector2 closest = new(
                    MathHelper.Clamp(segments[i].X, targetHitbox.Left, targetHitbox.Right),
                    MathHelper.Clamp(segments[i].Y, targetHitbox.Top, targetHitbox.Bottom));
                if (Vector2.DistanceSquared(segments[i], closest) < HitRadius * HitRadius) {
                    return true;
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            //余痕:沿身躯散星尘,活过弹体
            for (int i = 0; i < SegmentCount; i += 2) {
                CultistMotion.RuneBurst(segmentsInit ? segments[i] : Projectile.Center,
                    CultistMotion.StardustCore, 2, 5f);
            }
            CultistMotion.ImpactBurst(Projectile.Center, CultistMotion.PhaseLegacyElement(2), 0.8f, false);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (fade <= 0.01f) {
                return false;
            }
            Main.instance.LoadNPC(NPCID.CultistDragonHead);
            Main.instance.LoadNPC(NPCID.CultistDragonTail);
            foreach (int id in BodyIds) {
                Main.instance.LoadNPC(id);
            }
            SpriteBatch sb = Main.spriteBatch;

            //预瞄线:段末渐显,冻结窗白热(线指哪,龙冲哪)
            if (Stage == 0 || Stage == 2) {
                int aimFrames = Stage == 0 ? Aim1Frames : Aim2Frames;
                float show = MathHelper.Clamp((StageAge - (aimFrames - AimShowFrames)) / AimShowFrames, 0f, 1f);
                if (show > 0.01f) {
                    bool frozen = StageAge >= aimFrames - AimFreezeFrames;
                    Color mid = CultistMotion.StardustCore;
                    Color deep = Color.Lerp(CultistMotion.StardustEdge, Color.Black, 0.45f);
                    Color bright = Color.Lerp(mid, Color.White, 0.5f);
                    Vector2 root = Projectile.Center - Main.screenPosition;
                    Vector2[] pts = [root, root + aimDir * 1600f];
                    float[] widths = [8f + show * 4f, 5f];
                    float[] alphas = [0.55f * show, 0.22f * show];
                    sb.End();
                    CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                        deep, mid, bright, 1f, frozen ? 0f : 13f, frozen ? 0.9f : 0.25f,
                        Projectile.identity % 100 * 0.071f, show);
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                        DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                }
            }

            //冲撞余影:整龙同料回影(拖尾横径=本体,消散后星尘余痕接棒)
            if (Stage == 1 || Stage == 3) {
                for (int g = 3; g >= 1; g--) {
                    DrawWorm(sb, -Projectile.velocity * (g * 2.2f), fade * (0.30f - g * 0.07f));
                }
            }

            DrawWorm(sb, Vector2.Zero, fade * 0.92f);
            return false;
        }

        /// <summary>整龙链绘制:原版幻影龙贴图逐节,尾→头压序(头压最上);蠕虫贴图纵向朝上=角度+π/2</summary>
        private void DrawWorm(SpriteBatch sb, Vector2 offset, float alpha) {
            if (!segmentsInit || alpha <= 0.01f) {
                return;
            }
            for (int i = SegmentCount - 1; i >= 0; i--) {
                int npcId = i == 0 ? NPCID.CultistDragonHead
                    : i == SegmentCount - 1 ? NPCID.CultistDragonTail
                    : BodyIds[(i - 1) % BodyIds.Length];
                Texture2D tex = TextureAssets.Npc[npcId].Value;
                Rectangle frame = tex.Frame(1, Main.npcFrameCount[npcId], 0, 0);
                Vector2 dir = i == 0
                    ? (Stage is 1 or 3 && Projectile.velocity.LengthSquared() > 0.01f ? Projectile.velocity : aimDir)
                    : segments[i - 1] - segments[i];
                float rot = dir.ToRotation() + MathHelper.PiOver2;
                //星尘鬼相:偏冷蓝的真 alpha 主体保剪影
                Color body = new Color(190, 225, 245) * alpha;
                sb.Draw(tex, segments[i] + offset - Main.screenPosition, frame, body, rot,
                    frame.Size() * 0.5f, 1f, SpriteEffects.None, 0f);
            }
        }
    }
}
