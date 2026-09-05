using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 毒牙双杖共用模板（镜像宝石杖的模板/子类结构）。材质身份：淬毒獠牙。<br/>
    /// rider：毒牙咬中第二个目标时分裂细牙（毒杖分裂更多）
    /// </summary>
    internal abstract class GsMorphFangScheme : GsMorphScheme
    {
        /// <summary>rider：第二目标分裂的细牙枚数</summary>
        protected abstract int SplitFangCount { get; }

        protected override float BaseDamageMult => 1.06f;

        /// <summary>本杖原版毒牙弹类型</summary>
        protected int FangType => ContentSamples.ItemsByType[TargetItemID].shoot;

        //==================== 动画法：举杖压腕 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //蛇杖压腕：出手瞬间杖头向下咬合 3px，随动画进度回抬（绝对剖面 −0.09·p 下压，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(player.direction * 1f, 3f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, -0.09f * progress, -0.09f * ((player.itemAnimation + 1) / n));
        }

        //==================== rider：细牙分裂 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != FangType || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            OnFangBite(target);
            //咬中第二个目标：分裂细牙（0.3 倍，向两侧小扇甩出；numHits 此刻为 1）
            if (proj.numHits != 1 || router.MarkData == 10f) {
                return;
            }
            int splitDamage = Math.Max(1, (int)(proj.damage * 0.3f));
            Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < SplitFangCount; i++) {
                float off = MathHelper.Lerp(-0.5f, 0.5f, SplitFangCount == 1 ? 0.5f : i / (float)(SplitFangCount - 1));
                SpawnMorph(Main.player[proj.owner], Main.player[proj.owner].HeldItem, target.Center,
                    dir.RotatedBy(off) * 8f, FangType, splitDamage, proj.knockBack * 0.3f, 10);
                //细牙承 10 号形态标：不再二次分裂
            }
        }

        /// <summary>本杖专属咬合效果（owner 命中路径）</summary>
        protected abstract void OnFangBite(NPC target);

        protected sealed override void GsMorphOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            //细牙出生改制（owner 端，先于生成包发出）：收窄体型、限寿
            if (proj.type == FangType && router.MarkData == 10f) {
                proj.scale *= 0.62f;
                proj.timeLeft = Math.Min(proj.timeLeft, 60);
            }
        }
    }

    /// <summary>毒杖重铸：细牙分裂更多</summary>
    internal class GsPoisonStaff : GsMorphFangScheme
    {
        public override int TargetItemID => ItemID.PoisonStaff;

        protected override string GsDescFallback =>
            "Reforged: fangs trail venom mist and split into thin fangs on their second bite";
        protected override int SplitFangCount => 2;

        protected override void OnFangBite(NPC target) => target.AddBuff(BuffID.Poisoned, 240);
    }

    /// <summary>剧毒杖重铸：分裂一枚重牙</summary>
    internal class GsVenomStaff : GsMorphFangScheme
    {
        public override int TargetItemID => ItemID.VenomStaff;

        protected override string GsDescFallback =>
            "Reforged: fangs trail venom mist and split a heavy fang on their second bite";
        protected override int SplitFangCount => 1;

        protected override void OnFangBite(NPC target) => target.AddBuff(BuffID.Venom, 240);
    }
}
