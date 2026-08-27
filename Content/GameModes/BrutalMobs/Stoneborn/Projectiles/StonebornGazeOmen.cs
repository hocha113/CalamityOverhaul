using CalamityOverhaul.Content.Items.Stones;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Stoneborn.Projectiles
{
    /// <summary>
    /// 美杜莎·凝视可读性层：ai[0]=锚NPC索引 ai[1]=锚NPC类型。
    /// 本层不改石化机制本身，只给它补预告与惩罚窗——石化判定仍完全由原版执行。
    /// 原版凝视机（1.4.0.5 反编译核实）：npc.ai[2] 正值=冷却递减、0=就绪待条件、
    /// 负值=起手 20 帧+凝视段（石化逐帧判定），凝视结束/被打断跳回正冷却。
    /// 本实体各端直读同步的 ai[2] 确定性渲染（与原版石化判定读同一个量，新鲜度一致）：
    /// 冷却尾段 ≤<see cref="GazePreludeFrames"/> 帧亮出前奏束（蛇发竖起+声音渐尖，给「背身」充分反应窗）、
    /// 负值段满亮凝视束（束向=她的朝向，石化只打她面前的可视扇区）、
    /// 负→正沿触发 <see cref="FatigueFrames"/> 帧疲劳演出（头低垂=可见惩罚窗，本层不加伤害）。
    /// 锚体死亡即消散。永不参与伤害
    /// </summary>
    internal class StonebornGazeOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>凝视前奏帧（任务契约 ≥40）：读原版冷却尾段，提交前 40 帧起亮</summary>
        internal const int GazePreludeFrames = 40;
        /// <summary>凝视后疲劳演出帧（可见惩罚窗）</summary>
        internal const int FatigueFrames = 60;
        /// <summary>凝视束可视长度（示意原版 700px 判定距离的主要威胁段）</summary>
        private const float BeamLength = 460f;
        /// <summary>扇区须长度：原版石化限 ±45° 水平锥，两根短须标出锥缘</summary>
        private const float WhiskerLength = 150f;
        /// <summary>束芯宽/柔光宽</summary>
        private const float BeamCoreWidth = 14f;
        private const float BeamGlowWidth = 40f;
        /// <summary>自续期（锚体存活时每帧回填，锚体消失后自然收尾）</summary>
        private const int RenewFrames = 90;

        private int AnchorIndex => (int)Projectile.ai[0];
        private int AnchorType => (int)Projectile.ai[1];
        /// <summary>疲劳演出剩余帧（本端演出量，由确定性沿触发）</summary>
        private ref float FatigueLeft => ref Projectile.localAI[0];
        /// <summary>上一帧的原版凝视计时（负→正沿检测）</summary>
        private ref float PrevGazeTimer => ref Projectile.localAI[1];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 700;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = RenewFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯可读性层，永不参与伤害（石化归原版）</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        /// <summary>前奏强度 0..1：冷却尾段线性爬升，负值段恒 1</summary>
        private static float GazeCharge(float gazeTimer) {
            if (gazeTimer < 0f) {
                return 1f;
            }
            if (gazeTimer <= 0f || gazeTimer > GazePreludeFrames) {
                return gazeTimer == 0f ? 0.35f : 0f;//就绪待条件：微亮待机（新刷美杜莎的最低预警）
            }
            return (GazePreludeFrames - gazeTimer) / (float)GazePreludeFrames;
        }

        public override void AI() {
            //锚定校验：索引+类型双校验防槽位复用
            if (!AnchorIndex.TryGetNPC(out NPC anchor) || !anchor.Alives() || anchor.type != AnchorType) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = RenewFrames;//常驻附着，锚体没了立即上面收场
            Projectile.Center = anchor.Top + new Vector2(anchor.spriteDirection * 6f, 12f);
            Projectile.rotation = anchor.spriteDirection >= 0 ? 0f : MathHelper.Pi;

            float gazeTimer = anchor.ai[2];
            //凝视收束沿（负→非负）：疲劳演出开窗。被打断与自然结束同待遇——
            //两种情况原版都会进 ≥120 帧冷却，惩罚窗都是真实的
            if (PrevGazeTimer < 0f && gazeTimer >= 0f) {
                FatigueLeft = FatigueFrames;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 4 }, Projectile.Center);
                }
            }
            PrevGazeTimer = gazeTimer;
            if (FatigueLeft > 0f) {
                FatigueLeft--;
            }

            float charge = GazeCharge(gazeTimer);
            bool gazing = gazeTimer < 0f;

            if (!Main.dedServ) {
                //声音渐尖：前奏期 tick 音调随充能爬升；凝视开启帧一记高音
                if (!gazing && charge > 0.05f && gazeTimer > 0f && (int)gazeTimer % 10 == 0) {
                    SoundEngine.PlaySound(SoundID.MaxMana with {
                        Volume = 0.22f + 0.25f * charge,
                        Pitch = -0.3f + 0.9f * charge,
                        MaxInstances = 5,
                    }, Projectile.Center);
                }
                if (gazing && (int)-gazeTimer % 12 == 0) {
                    //凝视进行中的高频尖音（危险持续提示）
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.3f, Pitch = 0.55f, MaxInstances = 5 }, Projectile.Center);
                }
                //蛇发竖起：头顶金尘上扬，密度随充能（≤2 粒/帧）
                if ((gazing || charge > 0.1f) && Main.rand.NextBool(gazing ? 1 : 2)) {
                    Dust dust = Dust.NewDustPerfect(
                        anchor.Top + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-4f, 2f)),
                        DustID.GoldFlame, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.6f) * (0.4f + 0.6f * charge)),
                        100, default, 0.8f);
                    dust.noGravity = true;
                }
                //疲劳期：头前下坠的灰金尘（读作蛇发垂落）
                if (FatigueLeft > 0f && Main.rand.NextBool(2)) {
                    Dust dust = Dust.NewDustPerfect(
                        anchor.Top + new Vector2(anchor.spriteDirection * Main.rand.NextFloat(2f, 12f), 4f),
                        DustID.Stone, new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)), 120, default, 0.9f);
                    dust.noGravity = false;
                }
            }
            if (gazing || charge > 0.1f) {
                Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleGold.ToVector3() * (0.1f + 0.25f * charge));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!AnchorIndex.TryGetNPC(out NPC anchor) || !anchor.Alives()) {
                return false;
            }
            float gazeTimer = anchor.ai[2];
            float charge = GazeCharge(gazeTimer);
            bool gazing = gazeTimer < 0f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color gold = GraniteMarbleVFX.MarbleGold with { A = 0 };
            Color warm = GraniteMarbleVFX.MarbleCore with { A = 0 };

            //疲劳演出：束灭、头前一团下垂的暗金残光（可见惩罚窗）
            if (FatigueLeft > 0f) {
                float f = FatigueLeft / FatigueFrames;
                Vector2 droop = drawPos + new Vector2(anchor.spriteDirection * 10f, 14f + 8f * (1f - f));
                Main.EntitySpriteDraw(glow, droop, null, gold * (0.35f * f), 0f,
                    glow.Size() / 2f, new Vector2(0.4f, 0.55f), SpriteEffects.None, 0);
                return false;
            }
            if (!gazing && charge <= 0.02f) {
                return false;
            }

            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * (8f + 10f * charge) + Projectile.identity);
            float strength = gazing ? pulse : charge * 0.6f * pulse;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float faceDir = Projectile.rotation;

            //主束：她的朝向即危险朝向（原版石化要求互相对脸）
            Main.EntitySpriteDraw(tex, drawPos, null, gold * (0.55f * strength), faceDir,
                origin, new Vector2(BeamLength / tex.Width, BeamCoreWidth / tex.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, warm * (0.3f * strength), faceDir,
                origin, new Vector2(BeamLength / tex.Width, BeamGlowWidth / tex.Height), SpriteEffects.None, 0);
            //±45° 锥缘须：原版只判水平 ±45° 扇区，须外（头顶正上/正下）是安全角
            for (int side = -1; side <= 1; side += 2) {
                float ang = faceDir + side * MathHelper.PiOver4;
                Main.EntitySpriteDraw(tex, drawPos, null, gold * (0.25f * strength), ang,
                    origin, new Vector2(WhiskerLength / tex.Width, 8f / tex.Height), SpriteEffects.None, 0);
            }
            //凝视中：眼位亮核
            if (gazing) {
                Main.EntitySpriteDraw(glow, drawPos, null, warm * (0.8f * pulse), 0f,
                    glow.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
