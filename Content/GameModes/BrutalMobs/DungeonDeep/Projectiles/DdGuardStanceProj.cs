using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep.Projectiles
{
    /// <summary>
    /// Paladin 举盾格挡姿态实体：受击 30% 概率亮盾，期间承伤 ×0.6。
    /// ai[0]=来源打包（槽位+1|类型&lt;&lt;8） ai[1]=姿态持续帧。
    /// 实体本身已同步（netImportant），承伤门在命中计算端直接扫描本实体判定，各端一致
    /// ——镜像 EliteMove 格挡的「亮姿态=可读减伤窗」语义，独立实现不跨包引用
    /// </summary>
    internal class DdGuardStanceProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color ShieldGold = new Color(255, 215, 120, 0);
        private static readonly Color ShieldDark = new Color(54, 44, 22, 220);

        private int SourcePacked => (int)Projectile.ai[0];
        private int AnchorIndex => (SourcePacked & 255) - 1;
        private int StanceFrames => (int)Projectile.ai[1];

        /// <summary>命中计算端判定：该 Paladin 当前是否处于举盾姿态（盾光可见=减伤生效）</summary>
        internal static bool GuardActiveFor(int npcIndex) {
            int type = ModContent.ProjectileType<DdGuardStanceProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && ((int)proj.ai[0] & 255) == npcIndex + 1) {
                    return true;
                }
            }
            return false;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.netImportant = true;
        }

        /// <summary>纯姿态可视化，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = Math.Max(10, StanceFrames);
                if (!Main.dedServ) {
                    //举盾铿锵：格挡窗开启的可听信号
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = 0.25f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            //来源校验：Paladin 倒下盾随之散
            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!anchor.Alives() || anchor.type != SourcePacked >> 8) {
                Projectile.Kill();
                return;
            }
            //盾面贴在朝向侧
            Projectile.Center = anchor.Center + new Vector2(anchor.direction * (anchor.width * 0.5f + 12f), -2f);

            if (!Main.dedServ && Main.rand.NextBool(4)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 16f),
                    DustID.GoldFlame, Vector2.UnitY * -0.5f, 120, default, 0.9f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.3f, 0.24f, 0.1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D dark = CWRAsset.Extra_98.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float lifeT = MathHelper.Clamp(Projectile.timeLeft / (float)Math.Max(1, StanceFrames), 0f, 1f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f) * (0.6f + 0.4f * lifeT);
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 15f + Projectile.identity);

            //暗盾骨架（真透暗底=有遮挡像素）+ 竖长金光盾面
            Main.EntitySpriteDraw(dark, drawPos, null, ShieldDark * (0.8f * fade), 0f,
                dark.Size() / 2f, new Vector2(0.16f, 0.42f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, ShieldGold * (0.6f * fade * pulse), 0f,
                glow.Size() / 2f, new Vector2(0.22f, 0.6f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 246, 210, 0) * (0.35f * fade * pulse), 0f,
                glow.Size() / 2f, new Vector2(0.12f, 0.4f), SpriteEffects.None, 0);
            return false;
        }
    }
}
