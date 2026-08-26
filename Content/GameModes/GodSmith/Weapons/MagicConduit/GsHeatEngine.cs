using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 魔法·书与引导族共享框架：持续束通道 + 热量/过载管理。<br/>
    /// 热量是资源不是惩罚条：射击/引导积热，白热带（65~99）吃增益
    /// （伤害乘区 + 蓝耗 ×0.7 + 武器专属白热特效），顶到 100 按政策分流
    /// （Lock 爆发进锁 / Sustain 临界维持 / NoBreak 只涨蓝耗）；
    /// 右键泄压把当前热量转化为一次泄压技（威力 ∝ 热量，0 蓝耗，无锁）。<br/>
    /// 联机纪律：热量全量只存在于 owner 端（GsHeatPlayer），
    /// 远端呈现走弹幕 MarkData（出生热段）与通道弹幕 ai[]（热段里程碑）
    /// </summary>
    internal abstract class GsHeatScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "MagicConduit";

        //==================== 参数面（子类覆写） ====================

        /// <summary>单发积热（射击型武器；通道型给 0 由通道弹幕积热）</summary>
        internal virtual float HeatPerShot => 8f;

        /// <summary>停火后冷却延迟</summary>
        internal virtual int CoolDelayTicks => 25;

        /// <summary>被动冷却速率</summary>
        internal virtual float CoolRatePerTick => 0.8f;

        /// <summary>白热带伤害乘区（65+ 生效，owner 端计算真值）</summary>
        internal virtual float WhiteHotDamageMult => 1.15f;

        /// <summary>主数值行：基础伤害乘区</summary>
        internal virtual float BaseDamageMult => 1f;

        /// <summary>过热锁时长（Lock 政策）</summary>
        internal virtual int OverloadLockTicks => 90;

        /// <summary>过载附带的硬禁施法窗（0 = 无）</summary>
        internal virtual int OverloadHardLockTicks => 0;

        internal virtual GsOverloadPolicy OverloadPolicy => GsOverloadPolicy.Lock;

        /// <summary>是否启用右键泄压（原生右键武器如无限智慧巨著给 false）</summary>
        internal virtual bool VentEnabled => true;

        /// <summary>泄压所需最低热量</summary>
        internal virtual float VentMinHeat => 25f;

        /// <summary>杖尖读数主题色（个人读数，仅 owner 可见）</summary>
        internal virtual Color MuzzleTheme => GsConduitVFX.ForgeMain;

        //==================== 数值钩子 ====================

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            damage *= BaseDamageMult;
            //白热乘区读 owner 本地热量：伤害真值在攻击方端结算，远端实例读到 0 只影响其 tooltip
            GsHeatPlayer hp = player.GetModPlayer<GsHeatPlayer>();
            if (hp.InWhiteHot && hp.BoundItemType == TargetItemID) {
                damage *= WhiteHotDamageMult;
            }
        }

        public override float GsUseSpeedMultiplier(Item item, Player player) {
            GsHeatPlayer hp = player.GetModPlayer<GsHeatPlayer>();
            return hp.Locked && hp.BoundItemType == TargetItemID ? 0.85f : 1f;
        }

        public override void GsModifyManaCost(Item item, Player player, ref float reduce, ref float mult) {
            GsHeatPlayer hp = player.GetModPlayer<GsHeatPlayer>();
            if (hp.BoundItemType == TargetItemID) {
                if (hp.InWhiteHot) {
                    mult *= 0.7f;
                }
                if (hp.Locked) {
                    mult *= 1.5f;
                }
            }
            mult *= ExtraManaCostMult(player, hp);
        }

        /// <summary>附加蓝耗乘区（临界维持/虹溢态覆写）</summary>
        internal virtual float ExtraManaCostMult(Player player, GsHeatPlayer hp) => 1f;

        //==================== 使用流 ====================

        public override bool? GsCanUseItem(Item item, Player player) {
            //硬禁施法只在 owner 端为真（远端热量恒 0 返回 null）；owner 不启用则无任何端产生使用
            GsHeatPlayer hp = player.GetModPlayer<GsHeatPlayer>();
            if (hp.HardLocked) {
                return false;
            }
            return null;
        }

        public override bool? GsShoot(Item item, Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //射击流只在 owner 端执行，积热天然守门
            if (HeatPerShot > 0f) {
                player.GetModPlayer<GsHeatPlayer>().AddHeat(this, HeatPerShot);
            }
            return null;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            GsHeatPlayer hp = player.GetModPlayer<GsHeatPlayer>();
            HoldReadout(item, player, hp);
            TickHold(item, player, hp);

            //右键泄压：走独立输入路径不占用 use 链（0 蓝、不打断左键节奏）
            if (!VentEnabled || hp.VentCooldownLeft > 0 || hp.HardLocked || player.dead || player.CCed) {
                return;
            }
            if (player.mouseInterface || !Main.mouseRight || !Main.mouseRightRelease) {
                return;
            }
            if (hp.Heat < VentMinHeat || !VentReady(player, hp)) {
                return;
            }
            FireVent(player, hp);
            ConsumeVentHeat(hp);
            hp.VentCooldownLeft = 30;
        }

        /// <summary>手持每帧（owner 端；引导型积热放这，覆写记得调用 base）</summary>
        internal virtual void TickHold(Item item, Player player, GsHeatPlayer hp) { }

        /// <summary>泄压额外就绪条件（最后棱镜要求引导中）</summary>
        internal virtual bool VentReady(Player player, GsHeatPlayer hp) => true;

        /// <summary>泄压结算的热量消耗，默认全清；覆写可部分清</summary>
        internal virtual void ConsumeVentHeat(GsHeatPlayer hp) => hp.Heat = 0f;

        /// <summary>泄压技：owner 端生成一次性弹幕，威力 ∝ 调用时的 hp.Heat</summary>
        internal virtual void FireVent(Player player, GsHeatPlayer hp) { }

        //==================== 过载 ====================

        /// <summary>
        /// 过载爆发（Lock 政策触顶，owner 端）。基类给通用演出，
        /// 子类覆写加专属爆发（先调 base）
        /// </summary>
        internal virtual void OnOverload(Player player, GsHeatPlayer hp) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.9f, Pitch = -0.55f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.5f, Pitch = -0.3f }, player.Center);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(player.MountedCenter + Main.rand.NextVector2Circular(10f, 14f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.8f, 1.8f)),
                    Color.DarkGray, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(24, 40), 0.55f, 0.02f);
            }
        }

        /// <summary>Sustain/NoBreak 政策首次触顶（owner 端，去重后只回调一次）</summary>
        internal virtual void OnHeatCapped(Player player, GsHeatPlayer hp) { }

        //==================== 弹幕打标 ====================

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            //出生热段随生成包过线：远端按同一热段渲染（owner 全量 / 远端里程碑拆分）
            Player player = Main.player[proj.owner];
            router.MarkData = player.GetModPlayer<GsHeatPlayer>().HeatStage;
        }

        //==================== 个人读数（杖尖色温，owner 屏独有） ====================

        private void HoldReadout(Item item, Player player, GsHeatPlayer hp) {
            if (VaultUtils.isServer || Main.gameMenu) {
                return;
            }
            //过热锁：武器冒烟
            if (hp.Locked && hp.BoundItemType == TargetItemID && Main.GameUpdateCount % 9 == 0) {
                Vector2 muzzle = player.MountedCenter + new Vector2(player.direction * 16f, -4f);
                PRTLoader.NewParticle<PRT_Smoke>(muzzle, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.1f)),
                    Color.DimGray, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(20, 32), 0.45f, 0.02f);
                return;
            }
            if (hp.Heat < 12f || hp.BoundItemType != TargetItemID) {
                return;
            }
            //杖尖色温三段读数：蓝 → 橙 → 白热；白热带追加心跳微光
            Vector2 aim = (Main.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 tip = player.MountedCenter + aim * 34f;
            if (Main.GameUpdateCount % 6 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(tip + Main.rand.NextVector2Circular(3f, 3f),
                    -aim * 0.3f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    GsConduitVFX.HeatTint(hp.Heat), Main.rand.NextFloat(0.2f, 0.34f))?.Configure(false, Main.rand.Next(8, 14));
            }
            if (hp.InWhiteHot && Main.GameUpdateCount % 12 == 0) {
                PRTLoader.NewParticle<PRT_Light>(tip, Vector2.Zero, GsConduitVFX.HeatTint(hp.Heat), 0.09f)
                    ?.Configure(8, 0.7f);
            }
        }

        //==================== 小工具 ====================

        protected static GsHeatPlayer HeatOf(Player player) => player.GetModPlayer<GsHeatPlayer>();

        /// <summary>owner 视角是否白热（远端恒 false，只用于攻击端真值路径）</summary>
        protected bool LocalWhiteHot(Player player) {
            GsHeatPlayer hp = player.GetModPlayer<GsHeatPlayer>();
            return hp.InWhiteHot && hp.BoundItemType == TargetItemID;
        }

        /// <summary>读原版武器的默认弹幕类型（SetDefaults 已密封，模板数据从 ContentSamples 取）</summary>
        protected int VanillaShootType => ContentSamples.ItemsByType[TargetItemID].shoot;
    }
}
