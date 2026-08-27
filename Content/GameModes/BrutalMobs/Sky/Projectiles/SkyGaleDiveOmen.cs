using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Sky.Projectiles
{
    /// <summary>
    /// 风压俯冲预告线：ai[0]=来源NPC+1|类型&lt;&lt;8 ai[1]=档位 ai[2]=锁定方向+10（0=未锁定）。
    /// 追踪期直读目标方向，锁定帧后方向冻结（预告即承诺）；权威端在锁定帧写 ai[2] 作纠偏。
    /// 突进期保留为淡出余痕兼推挤判定窗载体（<see cref="TryGetStrikeDir"/>），永不造成伤害
    /// </summary>
    internal class SkyGaleDiveOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>预告总帧（≥30 契约，档位不缩短）/末段锁定帧/突进余痕窗帧</summary>
        internal const int TelegraphFrames = 36;
        internal const int LockFrames = 14;
        internal const int StrikeFrames = 40;
        internal const float LaneLength = 520f;

        /// <summary>预告线芯宽与柔光宽：画宽于怪体判定，把突进期残余转向也包进警示范围</summary>
        private const float LaneCoreWidth = 24f;
        private const float LaneGlowWidth = 58f;

        private static readonly Color Warn = new Color(168, 216, 255, 0);

        private int SourcePacked => (int)Projectile.ai[0];
        private int AnchorIndex => (SourcePacked & 255) - 1;
        private int AnchorType => SourcePacked >> 8;
        private int Elapsed => TelegraphFrames + StrikeFrames - Projectile.timeLeft;
        internal bool InStrike => Elapsed >= TelegraphFrames;
        private bool Locked => Elapsed >= TelegraphFrames - LockFrames;

        /// <summary>受害端判定：该鸟妖处于俯冲余痕窗时给出锁定方向（推挤沿此向，读同步实体不读私产）</summary>
        internal static bool TryGetStrikeDir(int npcIndex, int npcType, out float dir) {
            dir = 0f;
            int packed = (npcIndex + 1) | (npcType << 8);
            int type = ModContent.ProjectileType<SkyGaleDiveOmen>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && (int)proj.ai[0] == packed
                    && proj.ModProjectile is SkyGaleDiveOmen omen && omen.InStrike) {
                    dir = proj.ai[2] != 0f ? proj.ai[2] - 10f : proj.rotation;
                    return true;
                }
            }
            return false;
        }

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
                //追踪期：直读目标方向（各端从同步数据确定性推得）
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers && Main.player[target].Alives()) {
                    Projectile.rotation = (Main.player[target].Center - Projectile.Center).ToRotation();
                }
            }
            //锁定后 rotation 冻结在最后追踪值，等待/无需 ai[2] 纠偏

            if (Elapsed == TelegraphFrames - LockFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.42f, Pitch = 0.1f }, Projectile.Center);
            }
            if (Elapsed == TelegraphFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.55f, Pitch = 0.45f }, Projectile.Center);
            }

            //追踪期风旋尘（≤1 粒/帧）
            if (!InStrike && !Main.dedServ && Main.rand.NextBool(3)) {
                Vector2 dir = Projectile.rotation.ToRotationVector2();
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(20f, 60f),
                    DustID.Cloud, dir * Main.rand.NextFloat(1f, 2.4f), 140, default, 0.9f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, Warn.R / 255f * 0.14f, Warn.G / 255f * 0.14f, Warn.B / 255f * 0.14f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            float strength;
            if (InStrike) {
                //突进期余痕：可见窗与判定窗同一实体
                strength = MathHelper.Clamp(1f - (Elapsed - TelegraphFrames) / (float)StrikeFrames, 0f, 1f) * 0.22f;
            }
            else {
                strength = fadeIn * (Locked ? 1f : 0.55f);
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
                //追踪期/余痕期：细芯 + 宽柔光
                Main.EntitySpriteDraw(tex, drawPos, null, Warn * (0.5f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneCoreWidth / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, Warn * (0.3f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneGlowWidth / tex.Height), SpriteEffects.None, 0);
            }
            else {
                //锁定期：白热窄闪，宣告轨迹已承诺
                float lockT = MathHelper.Clamp((Elapsed - (TelegraphFrames - LockFrames)) / (float)LockFrames, 0f, 1f);
                float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                Color core = new Color(240, 250, 255, 0) * (0.85f * flash * strength);
                Main.EntitySpriteDraw(tex, drawPos, null, Warn * (0.65f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneGlowWidth + 18f) / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, core, Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneCoreWidth - 8f) / tex.Height), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
