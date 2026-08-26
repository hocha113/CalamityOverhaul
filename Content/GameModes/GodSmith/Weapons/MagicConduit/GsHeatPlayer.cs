using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>热量触顶政策</summary>
    internal enum GsOverloadPolicy
    {
        /// <summary>过载爆发 + 过热锁（蓝耗 ×1.5、射速 -15%）</summary>
        Lock,
        /// <summary>顶格维持不锁，临界期蓝耗陡增（激光机枪）</summary>
        Sustain,
        /// <summary>顶格不打断不惩罚射速，只涨蓝耗（最后棱镜，经典不毁）</summary>
        NoBreak,
    }

    /// <summary>
    /// 热量状态（每玩家）。诚实同步模型：所有字段是本地玩家态，
    /// 只在 player.whoAmI == Main.myPlayer 的路径读写，远端玩家实例恒为默认值；
    /// 远端呈现不读这里，走弹幕 MarkData / ai[] 的里程碑（owner 全量、远端里程碑拆分）
    /// </summary>
    internal class GsHeatPlayer : ModPlayer
    {
        internal const float HeatMax = 100f;
        /// <summary>白热带下沿（65~99 吃增益）</summary>
        internal const float SoftBandLow = 65f;

        /// <summary>当前热量 0~100</summary>
        internal float Heat;
        /// <summary>绑定武器（换武器清热）</summary>
        internal int BoundItemType;
        /// <summary>过热锁剩余：期间蓝耗 ×1.5、射速 -15%</summary>
        internal int OverloadLockLeft;
        /// <summary>硬禁施法剩余（书自燃/泡管堵塞等武器专属窗口）</summary>
        internal int HardLockLeft;
        /// <summary>泄压内置冷却（防连点）</summary>
        internal int VentCooldownLeft;
        /// <summary>停火后冷却延迟剩余</summary>
        internal int CoolDelayLeft;

        internal bool InWhiteHot => Heat >= SoftBandLow;
        internal bool Locked => OverloadLockLeft > 0;
        internal bool HardLocked => HardLockLeft > 0;

        /// <summary>当前热段：0 常态 / 1 白热（弹幕 MarkData 与通道 ai[] 的里程碑值）</summary>
        internal int HeatStage => InWhiteHot ? 1 : 0;

        /// <summary>按绑定武器查热量方案</summary>
        internal bool TryGetBoundScheme(out GsHeatScheme scheme) {
            scheme = null;
            if (BoundItemType > 0 && GodSmithScheme.TryGetScheme(BoundItemType, out GodSmithScheme raw)) {
                scheme = raw as GsHeatScheme;
            }
            return scheme != null;
        }

        public override void PostUpdateMiscEffects() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (OverloadLockLeft > 0) {
                OverloadLockLeft--;
            }
            if (HardLockLeft > 0) {
                HardLockLeft--;
            }
            if (VentCooldownLeft > 0) {
                VentCooldownLeft--;
            }

            //换武器清热重绑；锁期冻结绑定，切走再切回锁仍在（防切装逃锁）
            if (OverloadLockLeft <= 0 && Player.HeldItem.type != BoundItemType) {
                BoundItemType = 0;
                Heat = 0f;
                CoolDelayLeft = 0;
            }

            //被动冷却：停火延迟后按方案速率回落
            if (CoolDelayLeft > 0) {
                CoolDelayLeft--;
            }
            else if (Heat > 0f) {
                float rate = TryGetBoundScheme(out GsHeatScheme scheme) ? scheme.CoolRatePerTick : 0.8f;
                Heat = Math.Max(0f, Heat - rate);
            }
        }

        public override void UpdateDead() {
            //死亡全清（含锁，代价已付）
            Heat = 0f;
            BoundItemType = 0;
            OverloadLockLeft = 0;
            HardLockLeft = 0;
            VentCooldownLeft = 0;
            CoolDelayLeft = 0;
        }

        /// <summary>
        /// 积热（只在本地玩家路径调用；射击流/通道 owner tick）。
        /// 触顶按政策分流：Lock 政策清热进锁并回调过载爆发，Sustain/NoBreak 顶格维持
        /// </summary>
        internal void AddHeat(GsHeatScheme scheme, float amount) {
            if (Player.whoAmI != Main.myPlayer || amount <= 0f) {
                return;
            }
            BoundItemType = scheme.TargetItemID;
            CoolDelayLeft = scheme.CoolDelayTicks;
            if (Locked) {
                return;
            }
            bool wasCapped = Heat >= HeatMax;
            Heat += amount;
            if (Heat < HeatMax) {
                return;
            }
            if (scheme.OverloadPolicy == GsOverloadPolicy.Lock) {
                Heat = 0f;
                CoolDelayLeft = 0;
                OverloadLockLeft = scheme.OverloadLockTicks;
                HardLockLeft = scheme.OverloadHardLockTicks;
                scheme.OnOverload(Player, this);
            }
            else {
                Heat = HeatMax;
                if (!wasCapped) {
                    scheme.OnHeatCapped(Player, this);
                }
            }
        }

        /// <summary>消耗热量（泄压结算）</summary>
        internal void ConsumeHeat(float amount) => Heat = Math.Max(0f, Heat - amount);
    }

    /// <summary>
    /// 族内逐 NPC 状态：石纹层（美杜莎）与熔融层（热射线）。
    /// 层数是攻击方本地量（命中钩子只在攻击方端执行），不入包；
    /// 触发的可见结果（石化 buff / 小爆弹幕）走原生同步链
    /// </summary>
    internal class GsConduitNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        internal int PetrifyStacks;
        internal uint PetrifyLastTick;
        internal int MeltStacks;
        internal uint MeltLastTick;

        /// <summary>叠层并返回新层数；窗口（240t）内无新增自动清零重计</summary>
        internal static int Bump(ref int stacks, ref uint lastTick, uint now, int add) {
            if (now - lastTick > 240) {
                stacks = 0;
            }
            lastTick = now;
            stacks += add;
            return stacks;
        }
    }

    /// <summary>族内共享绘制与命中工具</summary>
    internal static class GsConduitVFX
    {
        //石化绿灰色板（美杜莎/蛇发射线）
        internal static readonly Color StoneBright = new(214, 234, 196);
        internal static readonly Color StoneMain = new(150, 192, 142);
        internal static readonly Color StoneDeep = new(78, 100, 74);
        //炉橙色板（地狱叉/热射线）
        internal static readonly Color ForgeBright = new(255, 214, 128);
        internal static readonly Color ForgeMain = new(255, 122, 42);
        internal static readonly Color ForgeDeep = new(140, 44, 16);
        //血红色板（生命吸取）
        internal static readonly Color BloodBright = new(255, 120, 110);
        internal static readonly Color BloodMain = new(196, 34, 40);
        internal static readonly Color BloodDeep = new(88, 10, 18);
        //磁品红色板（磁球）
        internal static readonly Color MagnetBright = new(255, 150, 235);
        internal static readonly Color MagnetMain = new(214, 62, 196);
        internal static readonly Color MagnetDeep = new(96, 22, 92);
        //海沫蓝色板（泡泡枪/台风，Duke 血统）
        internal static readonly Color SeaBright = new(170, 240, 232);
        internal static readonly Color SeaMain = new(66, 196, 188);
        internal static readonly Color SeaDeep = new(18, 74, 92);

        /// <summary>圆域对命中盒判定（最近点法），判定与可见半径同源的共用入口</summary>
        internal static bool CircleVsRect(Vector2 center, float radius, Rectangle rect) {
            float nx = MathHelper.Clamp(center.X, rect.Left, rect.Right);
            float ny = MathHelper.Clamp(center.Y, rect.Top, rect.Bottom);
            return new Vector2(nx - center.X, ny - center.Y).LengthSquared() <= radius * radius;
        }

        /// <summary>热量读数色温：冷蓝 → 灼橙 → 白热</summary>
        internal static Color HeatTint(float heat) {
            if (heat >= 65f) {
                return Color.Lerp(new Color(255, 190, 120), new Color(255, 242, 205), (heat - 65f) / 35f);
            }
            if (heat >= 35f) {
                return Color.Lerp(new Color(255, 150, 70), new Color(255, 190, 120), (heat - 35f) / 30f);
            }
            return Color.Lerp(new Color(110, 170, 255), new Color(255, 150, 70), heat / 35f);
        }

        /// <summary>
        /// 三层加色线束（外沿/中层/白芯，参照 PrimeHeatRay 后备画法）。
        /// 调用方处于实体绘制批；width 为可见核心宽
        /// </summary>
        internal static void DrawBeam(SpriteBatch sb, Vector2 startWorld, float rot, float length,
            float width, Color outer, Color mid, float alpha = 1f) {
            Texture2D line = CWRAsset.MaskLaserLine?.Value;
            if (line == null || length < 8f || width < 0.5f || alpha <= 0.02f) {
                return;
            }
            Vector2 drawPos = startWorld - Main.screenPosition;
            Vector2 origin = new(0f, line.Height / 2f);
            float lenScale = length / line.Width;
            Color o = outer with { A = 0 } * (0.45f * alpha);
            Color m = Color.Lerp(outer, mid, 0.5f) with { A = 0 } * (0.85f * alpha);
            Color core = Color.White with { A = 0 } * (0.95f * alpha);
            sb.Draw(line, drawPos, null, o, rot, origin, new Vector2(lenScale, width / line.Height * 3.2f), SpriteEffects.None, 0);
            sb.Draw(line, drawPos, null, m, rot, origin, new Vector2(lenScale, width / line.Height * 1.7f), SpriteEffects.None, 0);
            sb.Draw(line, drawPos, null, core, rot, origin, new Vector2(lenScale, width / line.Height * 0.8f), SpriteEffects.None, 0);
        }

        /// <summary>
        /// 石纹叠层（owner 命中路径调用）：满 4 层触发 0.5s 小石化并清层。
        /// Boss 与不可追击目标全豁免硬控，只吃伤害；石化 buff 走原生 NPC buff 同步链
        /// </summary>
        internal static void ApplyPetrify(NPC target, int add) {
            GsConduitNPC g = target.GetGlobalNPC<GsConduitNPC>();
            int stacks = GsConduitNPC.Bump(ref g.PetrifyStacks, ref g.PetrifyLastTick, Main.GameUpdateCount, add);
            if (stacks < 4) {
                return;
            }
            g.PetrifyStacks = 0;
            if (!target.boss && target.CanBeChasedBy()) {
                target.AddBuff(ModContent.BuffType<MarblePetrify>(), 30);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = -0.4f, MaxInstances = 3 }, target.Center);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextVector2Circular(3f, 2f) - new Vector2(0f, 1.6f),
                        StoneBright, Main.rand.NextFloat(0.5f, 0.9f));
                }
            }
        }
    }
}
