using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.BrainOfCthulhu
{
    /// <summary>
    /// 镜心复现拦截与镜像弹演出。
    /// 拦截只在所有者端(OnSpawn 仅随 NewProjectile 触发)且只在换位后的复现窗口内，
    /// 登记进 <see cref="MirrorheartPlayer"/> 的延迟队列；复现弹经 extraAI 一位标记跨端，
    /// 各端叠冷色负片残影与血雾细尾
    /// </summary>
    internal class MirrorheartEchoGlobal : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        /// <summary>本弹是镜心复现出来的；出生端点亮，随 extraAI 到达各端</summary>
        public bool IsMirrorEcho;

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            //镜心射出的复现弹：打标记供演出，绝不再次复现(防递归)
            if (source is EntitySource_Misc misc && misc.Context == MirrorheartPlayer.EchoSourceContext) {
                IsMirrorEcho = true;
                return;
            }

            //拦截登记只在所有者本人端
            if (projectile.owner != Main.myPlayer || VaultUtils.isServer) {
                return;
            }
            Player player = Main.player[projectile.owner];
            if (player == null || !player.active || player.dead) {
                return;
            }
            MirrorheartPlayer mp = player.GetModPlayer<MirrorheartPlayer>();
            //复现窗口(换位后5秒)之外直接放行不登记；窗口与碎裂重聚期重叠，不看 ShatterTimer
            if (!mp.Equipped || mp.EchoWindowTimer <= 0) {
                return;
            }

            //来源门：物品使用(含耗弹)，或本人持械 HeldProj 的直接子弹
            bool fromItemUse = source is EntitySource_ItemUse;
            bool fromHeldProj = !fromItemUse
                && source is EntitySource_Parent parentSource
                && parentSource.Entity is Projectile parent
                && parent.owner == projectile.owner
                && parent.ModProjectile is BaseHeldProj;
            if (!fromItemUse && !fromHeldProj) {
                return;
            }
            if (!IsEchoable(projectile)) {
                return;
            }
            mp.QueueEcho(projectile);
        }

        /// <summary>
        /// 复现排除规则(设计卡要求写明)：
        /// 1. 无伤害或非友方弹(钓钩、钩爪、纯演出弹)；
        /// 2. 召唤物本体与哨兵、召唤物射出的弹(镜心复读的是玩家的手，不是仆从的手)；
        /// 3. 手持武器实体(BaseHeldProj)与贴身持械类(ownerHitCheck：矛/链锯/持续光束，
        ///    这类弹的 AI 吸附所有者位置，复制体会瞬移回玩家身上)；
        /// 4. 鞭子(渲染锚定玩家手臂，从镜位复制必然画崩)；
        /// 5. 爆炸物(原版爆炸类 aiStyle 与 Explosive 集合，含炸药/雷管类，防拆家翻倍)
        /// </summary>
        private static bool IsEchoable(Projectile projectile) {
            if (!projectile.friendly || projectile.hostile || projectile.damage <= 0) {
                return false;
            }
            if (projectile.minion || projectile.sentry || projectile.minionSlots > 0f) {
                return false;
            }
            if (ProjectileID.Sets.MinionShot[projectile.type] || ProjectileID.Sets.SentryShot[projectile.type]) {
                return false;
            }
            if (projectile.ModProjectile is BaseHeldProj || projectile.ownerHitCheck) {
                return false;
            }
            if (projectile.aiStyle == ProjAIStyleID.HeldProjectile) {
                return false;
            }
            if (ProjectileID.Sets.IsAWhip[projectile.type]) {
                return false;
            }
            if (projectile.aiStyle == ProjAIStyleID.Explosive || ProjectileID.Sets.Explosive[projectile.type]) {
                return false;
            }
            return true;
        }

        //extraAI 无分段头，必须无条件写读，防全线错位
        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
            => bitWriter.WriteBit(IsMirrorEcho);

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
            => IsMirrorEcho |= bitReader.ReadBit();

        public override void PostAI(Projectile projectile) {
            if (!IsMirrorEcho || VaultUtils.isServer) {
                return;
            }
            //冷色血雾细尾，低频撒点
            if (projectile.timeLeft % 7 == 0 && BrainMotion.OnScreen(projectile.Center)) {
                var mist = PRTLoader.NewParticle<PRT_BrainBloodMist>(projectile.Center,
                    -projectile.velocity * 0.06f,
                    BrainMotion.MirrorCold * 0.6f, 0.32f);
                mist?.Configure(Main.rand.Next(16, 26));
            }
        }

        /// <summary>冷色负片残影盖在复现弹本体之上，读作镜中之物</summary>
        public override void PostDraw(Projectile projectile, Color lightColor) {
            if (!IsMirrorEcho || VaultUtils.isServer) {
                return;
            }
            Main.instance.LoadProjectile(projectile.type);
            Texture2D tex = TextureAssets.Projectile[projectile.type].Value;
            if (tex == null) {
                return;
            }
            int frames = Main.projFrames[projectile.type];
            if (frames <= 0) {
                frames = 1;
            }
            Rectangle frame = tex.Frame(1, frames, 0, projectile.frame % frames);
            Vector2 drawPos = projectile.Center - Main.screenPosition;
            SpriteEffects flip = projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            //A=0 加色技法：预乘批里只加光，冷紫罩色
            Color cold = new Color(158, 92, 148, 0) * 0.45f;
            Main.spriteBatch.Draw(tex, drawPos, frame, cold, projectile.rotation,
                frame.Size() * 0.5f, projectile.scale * 1.12f, flip, 0f);
        }
    }
}
