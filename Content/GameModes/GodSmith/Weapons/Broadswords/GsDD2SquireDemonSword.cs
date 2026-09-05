using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【狱火印】材质：地狱窑口淬出的军团骑士剑，剑格烙着会苏醒的狱火印记。
    /// 签名：①原版举盾完整保留：手持按住右键举盾（原版按物品类型判定，接管后照常生效），
    /// 成功格挡获得格挡增益，下一斩命中结算原版同款 5 倍力度（+4f ScalingBonusDamage，
    /// 接管后物品不再直击，结算重接进手持 ModifyHitExtra）
    /// ②成功格挡点燃狱火印：其后 3 次挥砍放出火焰刃波并点燃敌人
    /// </summary>
    internal class GsDD2SquireDemonSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.DD2SquireDemonSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsDD2SquireDemonSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: raise the shield with right click as ever; a perfect block still " +
            "repays fivefold force on the next strike, and brands the blade with hellfire: " +
            "the next three slashes hurl burning waves that ignite foes";

        //狱火色板
        internal static readonly Color InfernoBright = new(255, 216, 148); //焰亮鎏金
        internal static readonly Color InfernoMain = new(250, 122, 44);    //狱火橙
        internal static readonly Color InfernoHot = new(255, 66, 22);      //熔核赤

        /// <summary>剩余狱火挥砍数（0~3）；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int FlameSwings;

        /// <summary>格挡增益上沿检测（myPlayer 专用）</summary>
        private bool parrySeen;

        //底伤不加成：拍均 1.0/1.05/1.3，三拍循环约 64 帧 = 3.35x/64f，对上原版 3.0x/60f 约 105%；
        //火焰刃波 0.45x 只在格挡后 3 斩出现（技巧条件收益）；格挡 5 倍结算与原版等额，不计入常态包络
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }

        /// <summary>
        /// 镜像基类生成流程，追加 ai[2]=狱火旗（随生成包过线，各端一致）；
        /// 狱火计数在 myPlayer 守门内消耗
        /// </summary>
        public override bool? GsCanUseItem(Item item, Player player) {
            if (player.ownedProjectileCounts[HeldProjID] > 0) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                int beat = comboCounter % ComboBeats;
                float swingSign = comboCounter % 2 == 0 ? 1f : -1f;
                ModifyLocalSwing(item, player, ref beat, ref swingSign);
                comboCounter++;
                comboResetTimer = ComboResetFrames;
                float flame = 0f;
                if (FlameSwings > 0) {
                    FlameSwings--;
                    flame = 1f;
                }
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    HeldProjID, player.GetWeaponDamage(item), item.knockBack, player.whoAmI, beat, swingSign, flame);
            }
            return false;
        }

        /// <summary>格挡成功的瞬间（原版 buff 198 上沿）点燃狱火印：3 次狱火挥砍 + 一记爆响</summary>
        public override void GsHoldItem(Item item, Player player) {
            base.GsHoldItem(item, player); //连段衰减记账
            if (player.whoAmI != Main.myPlayer) {
                return; //myPlayer 守门（服务器上恒不等，天然排除）
            }
            bool parry = player.HasBuff(BuffID.ParryDamageBuff);
            if (parry && !parrySeen) {
                FlameSwings = 3;
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.5f, Pitch = 0.25f }, player.Center);
            }
            parrySeen = parry;
        }
    }

    /// <summary>
    /// 狱火印手持：三拍骑士连段（横斩/返斩/军团重劈）。ai[0]=拍号 ai[1]=交替符号
    /// ai[2]=狱火旗（本斩带火焰刃波，随生成包过线）。
    /// 格挡结算重接：原版 Player.cs 40483 在物品直击里做
    /// parryDamageBuff → ScalingBonusDamage += 4f 后清标志清 buff 198，等效搬进 ModifyHitExtra
    /// </summary>
    internal class GsDD2SquireDemonSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.DD2SquireDemonSword;
        protected override Color EdgeBright => GsDD2SquireDemonSword.InfernoBright;
        protected override Color BodyMain => GsDD2SquireDemonSword.InfernoMain;
        protected override Color HotAccent => GsDD2SquireDemonSword.InfernoHot;

        /// <summary>本斩是否燃着狱火（ai[2] 随生成包过线，各端一致）</summary>
        private bool FlameSwing => Projectile.ai[2] > 0.5f;

        private bool waveFired;
        private bool parrySettled;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 横斩
            0 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 7,
                RaiseBack = 1.85f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.048f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.08f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 7,
                RaiseBack = 1.9f, Follow = 1.02f, ReachScale = 1.02f, LeanAmp = 0.05f,
                DamageMult = 1.05f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.02f,
            },
            //拍2 军团重劈：长举前压
            _ => new GsBroadBeat {
                Raise = 7, Hold = 3, Slash = 5, Recover = 10,
                RaiseBack = 2.15f, Follow = 1.2f, ReachScale = 1.12f, LeanAmp = 0.08f,
                DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 2.6f, SwingPitch = -0.2f,
            },
        };

        /// <summary>狱火斩沿出手向放出火焰刃波（伤 0.45x，点燃敌人）</summary>
        protected override void OnSlashBegin() {
            if (waveFired || !FlameSwing) {
                return;
            }
            waveFired = true;
            Vector2 dir = baseAngle.ToRotationVector2();
            int waveDamage = Math.Max(1, (int)(Projectile.damage * 0.45f));
            SpawnOwnedProj(ModContent.ProjectileType<GsDD2SquireDemonSwordFireWaveProj>(),
                Hand + dir * (FullReach * 0.7f), dir * 13f, waveDamage, Projectile.knockBack * 0.4f, swingDir);
        }

        /// <summary>
        /// 原版格挡结算重接：parryDamageBuff 为 public 字段，直接等效执行
        /// （+4f ScalingBonusDamage，清标志，清 buff 198）
        /// </summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (!Owner.parryDamageBuff) {
                return;
            }
            modifiers.ScalingBonusDamage += 4f;
            Owner.parryDamageBuff = false;
            Owner.ClearBuff(BuffID.ParryDamageBuff);
            parrySettled = true;
        }

        /// <summary>格挡结算命中的一记重响（结算在 ModifyHitExtra，这里只管音效）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!parrySettled) {
                return;
            }
            parrySettled = false;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.55f, Pitch = -0.1f }, target.Center);
            }
        }
    }

    /// <summary>
    /// 狱火刃波：用原版火球贴图，出膛快后缓（13 → 约 5），命中点燃（OnFire 4 秒）。
    /// ai[0]=弯向符号（几何未消费，随包保留）
    /// </summary>
    internal class GsDD2SquireDemonSwordFireWaveProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        private ref float Life => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.BallofFire];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 36;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.45f, Pitch = 0.15f }, Projectile.Center);
            }
            //出膛快后缓
            if (Projectile.velocity.Length() > 5f) {
                Projectile.velocity *= 0.94f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool? CanDamage() => Life >= 1f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 240);
    }
}
