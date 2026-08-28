using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Meteorite.Projectiles
{
    /// <summary>
    /// 灼热俯冲预告：ai[0]=来源NPC+1|类型&lt;&lt;8 ai[1]=档位 ai[2]=锁定方向+10（0=未锁定）。
    /// 蓄热期在陨石头身上叠加升温亮壳（体色升温发亮信号）并直读目标方向，
    /// 锁定帧后方向冻结（预告即承诺）；权威端在锁定帧写 ai[2] 作纠偏。
    /// 突进期保留为淡出余痕（俯冲窗=标线余痕窗），永不造成伤害
    /// </summary>
    internal class MeteoriteDiveOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>预告总帧（≥30 契约，档位不缩短）/末段锁定帧/俯冲余痕窗帧</summary>
        internal const int TelegraphFrames = 34;
        internal const int LockFrames = 12;
        internal const int StrikeFrames = 48;
        internal const float LaneLength = 460f;

        /// <summary>预告线芯宽与柔光宽：画宽于怪体判定，把俯冲期残余转向也包进警示范围</summary>
        private const float LaneCoreWidth = 24f;
        private const float LaneGlowWidth = 56f;

        private static readonly Color Warn = new Color(255, 150, 70, 0);
        private static readonly Color HeatCore = new Color(255, 208, 120, 0);

        private int SourcePacked => (int)Projectile.ai[0];
        private int AnchorIndex => (SourcePacked & 255) - 1;
        private int AnchorType => SourcePacked >> 8;
        private int Elapsed => TelegraphFrames + StrikeFrames - Projectile.timeLeft;
        private bool InStrike => Elapsed >= TelegraphFrames;
        private bool Locked => Elapsed >= TelegraphFrames - LockFrames;
        /// <summary>蓄热强度 0..1：预告期爬升，俯冲期满档保持</summary>
        private float Heat => MathHelper.Clamp(Elapsed / (float)TelegraphFrames, 0f, 1f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 720;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + StrikeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //来源校验：索引+存活+类型比对（防槽位复用）；锚定怪没了 → 俯冲不会发生，预告随之消散
            int idx = AnchorIndex;
            NPC anchor = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
            if (anchor == null || !anchor.active || anchor.type != AnchorType) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;

            if (Projectile.ai[2] != 0f) {
                //权威端已写入锁定方向
                Projectile.rotation = Projectile.ai[2] - 10f;
            }
            else if (!Locked) {
                //蓄热期：直读目标方向（各端从同步数据确定性推得）
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers && Main.player[target].Alives()) {
                    Projectile.rotation = (Main.player[target].Center - Projectile.Center).ToRotation();
                }
            }
            //锁定后 rotation 冻结在最后追踪值，等待/无需 ai[2] 纠偏

            if (Elapsed == TelegraphFrames - LockFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.42f, Pitch = -0.2f }, Projectile.Center);
            }
            if (Elapsed == TelegraphFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.55f, Pitch = 0.15f, MaxInstances = 4 }, Projectile.Center);
            }

            //蓄热火星（≤2 粒/帧，升腾读作体温上抬）
            if (!Main.dedServ && Main.rand.NextBool(InStrike ? 2 : 3)) {
                Dust spark = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.8f)), 100, default,
                    0.9f + 0.6f * Heat);
                spark.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.5f * Heat, 0.25f * Heat, 0.08f * Heat);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            float strength;
            if (InStrike) {
                //俯冲期余痕：可见窗与俯冲窗同一实体
                strength = MathHelper.Clamp(1f - (Elapsed - TelegraphFrames) / (float)StrikeFrames, 0f, 1f) * 0.22f;
            }
            else {
                strength = fadeIn * (Locked ? 1f : 0.55f);
            }

            //升温亮壳：陨石头自身贴图的加色重影，随蓄热变亮（体色升温发亮信号，画在本体之上）
            int idx = AnchorIndex;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC anchor = Main.npc[idx];
                if (anchor.active && anchor.type == AnchorType) {
                    Main.instance.LoadNPC(anchor.type);
                    Texture2D npcTex = TextureAssets.Npc[anchor.type].Value;
                    float heat = Heat * (InStrike ? 1f : 0.4f + 0.6f * Heat);
                    float pulseHeat = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 16f + Projectile.identity);
                    SpriteEffects fx = anchor.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                    Vector2 npcPos = anchor.Center - Main.screenPosition;
                    Main.EntitySpriteDraw(npcTex, npcPos, anchor.frame, Warn * (0.7f * heat * pulseHeat),
                        anchor.rotation, anchor.frame.Size() / 2f, anchor.scale * 1.04f, fx, 0);
                    Main.EntitySpriteDraw(npcTex, npcPos, anchor.frame, HeatCore * (0.35f * heat * pulseHeat),
                        anchor.rotation, anchor.frame.Size() / 2f, anchor.scale * 0.92f, fx, 0);
                }
            }

            if (strength <= 0.01f) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float scaleX = LaneLength / tex.Width;
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);

            if (!Locked || InStrike) {
                //蓄热期/余痕期：细芯 + 宽柔光
                Main.EntitySpriteDraw(tex, drawPos, null, Warn * (0.5f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneCoreWidth / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, Warn * (0.3f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneGlowWidth / tex.Height), SpriteEffects.None, 0);
            }
            else {
                //锁定期：白热窄闪，宣告轨迹已承诺
                float lockT = MathHelper.Clamp((Elapsed - (TelegraphFrames - LockFrames)) / (float)LockFrames, 0f, 1f);
                float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                Color core = new Color(255, 236, 200, 0) * (0.85f * flash * strength);
                Main.EntitySpriteDraw(tex, drawPos, null, Warn * (0.65f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneGlowWidth + 18f) / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, core, Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneCoreWidth - 8f) / tex.Height), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
