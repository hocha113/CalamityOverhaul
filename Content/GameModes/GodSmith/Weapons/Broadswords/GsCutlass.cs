using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【劫掠连舞·鎏金弯刀】材质：海盗船长的鎏金弯刀，海风与赃金养出的快剑。
    /// 签名：①四拍流水快斩，拍拍音调攀升，连段命中攒「风头」
    /// ②攒足风头后第四拍变旋风双弧斩：绕身整两周、判定两轮
    /// ③攒层伴金币脆响
    /// </summary>
    internal class GsCutlass : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Cutlass;

        protected override int HeldProjID => ModContent.ProjectileType<GsCutlassHeld>();

        protected override int ComboBeats => 4;

        protected override string GsDescFallback =>
            "Reforged: a four-beat plunder dance with rising tempo; land hits to build Panache, and with enough swagger the fourth beat becomes a whirling double-arc slash that strikes twice all around";
        internal static readonly Color GoldBright = new(255, 244, 208); //鎏金刃缘
        internal static readonly Color GoldMain = new(232, 186, 96);    //赃金体色
        internal static readonly Color SeaSpray = new(72, 214, 225);    //海沫青

        /// <summary>风头层数（0~3）；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int Panache;

        //底伤不加成：四拍 0.9/0.9/0.9/1.1x 按 61 帧循环摊算约原版 100%；
        //攒足风头（前三拍命中 ≥2 记）后第四拍换旋风双弧斩（0.95x×两轮判定、29 帧、绕身 360°），
        //循环 70 帧摊算约 105%，多出的部分拿连段命中换，且旋风是全向 AoE 收益
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }

        /// <summary>攒足风头的第四拍换旋风：交替符号幅值置 2 当旗标（基类只读符号位）</summary>
        protected override void ModifyLocalSwing(Item item, Player player, ref int beat, ref float swingSign) {
            if (beat == 3 && Panache >= 2) {
                Panache = 0;
                swingSign *= 2f;
            }
        }

        public override void GsHoldItem(Item item, Player player) {
            base.GsHoldItem(item, player);
            //断手回拍：风头散场
            if (player.whoAmI == Main.myPlayer && comboResetTimer == 0 && Panache > 0) {
                Panache = 0;
            }
        }
    }

    /// <summary>
    /// 劫掠连舞手持：四拍流水快斩，0/1/2 短促轻斩音调渐升，3 收官斩；
    /// 风头攒够时拍 3 变旋风双弧斩（UpdateBladeTransform 整替为绕身两周，
    /// 半程重置命中冷却做第二轮判定）。ai[0]=拍号 ai[1]=交替符号（|值|>1.5 = 旋风旗标）
    /// </summary>
    internal class GsCutlassHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Cutlass;
        protected override int BeatCount => 4;
        protected override Color EdgeBright => GsCutlass.GoldBright;
        protected override Color BodyMain => GsCutlass.GoldMain;
        protected override Color HotAccent => GsCutlass.SeaSpray;

        //快剑触距略短
        protected override float BaseReach => 112f;

        /// <summary>本拍是否旋风双弧斩（旗标随 ai[1] 幅值过线，各端一致）</summary>
        private bool Whirl => ComboStage == BeatCount - 1 && MathF.Abs(Projectile.ai[1]) > 1.5f;

        /// <summary>第二轮判定已开启</summary>
        private bool secondPass;
        /// <summary>一拍只攒一次风头</summary>
        private bool panacheGained;

        private GsCutlass Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsCutlass : null;

        /// <summary>四拍流水：三记短促快斩音调渐升，第四拍收官（或旋风）</summary>
        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 顺手快斩
            0 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 6,
                RaiseBack = 1.6f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.035f,
                DamageMult = 0.9f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.0f,
            },
            //拍1 返手快斩
            1 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 6,
                RaiseBack = 1.65f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 0.9f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.1f,
            },
            //拍2 踏步疾斩：最短促，小前压
            2 => new GsBroadBeat {
                Raise = 3, Hold = 1, Slash = 3, Recover = 6,
                RaiseBack = 1.55f, Follow = 0.9f, ReachScale = 0.98f, LeanAmp = 0.04f,
                DamageMult = 0.9f, Hitstop = 1, LungeSpeed = 1.2f, SwingPitch = 0.2f,
            },
            //拍3 收官斩 / 旋风双弧斩
            _ => Whirl
                ? new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 12, Recover = 9,
                    RaiseBack = 2.0f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.07f,
                    DamageMult = 0.95f, Hitstop = 2, LungeSpeed = 2.2f, SwingPitch = 0.3f,
                }
                : new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                    RaiseBack = 2.0f, Follow = 1.15f, ReachScale = 1.08f, LeanAmp = 0.06f,
                    DamageMult = 1.1f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = 0.28f,
                },
        };

        /// <summary>旋风双弧：斩切期整替为绕身两周旋转，慢起快中缓收</summary>
        protected override void UpdateBladeTransform(int phase) {
            if (!Whirl || phase != PhaseSlash) {
                base.UpdateBladeTransform(phase);
                return;
            }
            float p = (timer - raiseDur - holdDur) / (float)slashDur;
            slashProgress = p;
            float spin = SmoothStep01(p) * MathHelper.TwoPi * 2f;
            mainAngle = ArcStart + (swingDir * spin);
            mainReach = FullReach * (0.9f + 0.1f * MathF.Sin(MathHelper.Clamp(p, 0f, 1f) * MathHelper.Pi));
            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        /// <summary>旋风半程（第一周转完）：重置命中冷却，开第二轮判定</summary>
        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            if (!Whirl || phase != PhaseSlash || secondPass || slashProgress < 0.5f) {
                return;
            }
            secondPass = true;
            for (int i = 0; i < Main.maxNPCs; i++) {
                Projectile.localNPCImmunity[i] = 0;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = 0.25f }, Owner.Center);
            }
        }

        //==================== 劫掠演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (Whirl) {
                //旋风起手的海风啸声
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.45f, Pitch = 0.1f }, Owner.Center);
            }
        }

        /// <summary>前三拍命中攒风头（一拍一层，金币脆响逐层升调）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer || panacheGained || ComboStage >= 3) {
                return;
            }
            panacheGained = true;
            GsCutlass scheme = Scheme;
            if (scheme == null) {
                return;
            }
            int old = scheme.Panache;
            scheme.Panache = Math.Min(3, scheme.Panache + 1);
            if (!VaultUtils.isServer && scheme.Panache > old) {
                SoundEngine.PlaySound(SoundID.CoinPickup with { Volume = 0.5f, Pitch = 0.05f + 0.18f * scheme.Panache }, Owner.Center);
            }
        }
    }
}
