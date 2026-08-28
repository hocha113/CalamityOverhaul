using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>
    /// 血湖湖面的物理面：只对玩家生效的单向平台。玩家移动是客户端权威，
    /// 每端用已同步的领域快照对所有玩家跑同一条钳制规则，各端自然一致，判定不走服务器。
    /// NPC 有意不上平台——它们沉入湖中吃介质交互（减速/水花/血泡，<see cref="KikasaLakeNPC"/>，
    /// 服务器靠领域镜像跟跑）；拖敌入湖的处决走 KikasaDrown 通道。
    /// </summary>
    internal static class KikasaLakeSurface
    {
        /// <summary>湖面物理半宽（世界像素）。确定性常量、与 KikasaDrown.MaxRange 同源，
        /// 覆盖最大缩放下的整屏可视范围，不随各端屏幕尺寸漂移</summary>
        public const float HalfWidth = 4000f;

        /// <summary>结算落水水花的最低下落速度（像素/帧），日常站立的重力微沉不出水花</summary>
        private const float SplashSpeed = 3f;

        /// <summary>站立判定的脚底嵌入容差：世界坐标下 float 精度有限，精确落线后
        /// 脚底可能带亚像素误差，容差内视为仍站在面上并平滑归位</summary>
        private const float FootTolerance = 4f;

        /// <summary>行走涟漪节流，纯本机表现量（涟漪只在 Viewed 端出现，无需同步）</summary>
        private static readonly int[] walkRippleTimers = new int[Main.maxPlayers];

        /// <summary>该域此刻是否托得住人：水位满即成面。谓词与 NPC 没入判定同源
        /// （<see cref="KikasaDomainPlayer.LakeBodySolid"/>）。
        /// 鬼梦的梦侧湖不存在（拉入结算帧起脚下落空，归返结算帧湖面重新接人）</summary>
        private static bool SurfaceSolid(KikasaDomainPlayer domain)
            => domain.LakeBodySolid;

        /// <summary>
        /// 逐帧钳制，移动应用前调用：本帧脚底将下穿湖面且未主动下潜时，把纵速截到精确落线。
        /// 落线后纵速自然归零，跳跃/站立走原版判定；单向语义，水下可向上跳穿回湖面
        /// </summary>
        public static void ApplyStanding(Player player) {
            if (Main.dedServ || Main.gameMenu) {
                //服务器域状态恒 Closed，跑了也全空
                return;
            }
            //上升不拦（单向平台）；反重力下语义倒置，湖面不接倒着走的人
            if (player.dead || player.ghost || player.velocity.Y < 0f || player.gravDir < 0f) {
                return;
            }
            //穿透态让位：舌头拖拽与微光下沉在原版里无视一切固体（tongued 直接 position+=velocity）
            if (player.tongued || player.shimmering || player.shimmerWet) {
                return;
            }
            //主动下潜：按下或钩爪拖拽让位，保下潜与水下路径
            if (player.controlDown || player.grapCount > 0) {
                return;
            }

            float feet = player.Bottom.Y;

            //取所有能接住脚的湖里最高的那面（同帧可能跨越多面湖）
            float bestLakeY = float.MaxValue;
            KikasaDomainPlayer bestDomain = null;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player caster = Main.player[i];
                if (caster?.active != true
                    || !caster.TryGetModPlayer(out KikasaDomainPlayer domain)
                    || !SurfaceSolid(domain)) {
                    continue;
                }
                float lakeY = domain.LakeWorldY;
                //脚已在面下（潜过了）或本帧够不到面，都不接
                if (feet > lakeY + FootTolerance || feet + player.velocity.Y < lakeY) {
                    continue;
                }
                if (MathF.Abs(player.Center.X - caster.Center.X) > HalfWidth) {
                    continue;
                }
                if (lakeY < bestLakeY) {
                    bestLakeY = lakeY;
                    bestDomain = domain;
                }
            }
            if (bestDomain == null) {
                return;
            }

            float impact = player.velocity.Y;
            //精确落线：位置应用后脚底恰在水面，下一帧重力增量再被截为零，原版站立/跳跃判定成立
            player.velocity.Y = bestLakeY - feet;
            //血湖接住人，不结算摔落伤害
            player.fallStart = player.fallStart2 = (int)(player.position.Y / 16f);

            LandingFx(player, bestDomain, impact);
            WalkRipples(player, bestDomain);
        }

        //落湖冲击反馈：水花与涟漪按冲击分级；只在把这面湖画在屏上的客户端播（同现有 FX 可见性规则）

        private static void LandingFx(Player player, KikasaDomainPlayer domain, float impact) {
            if (impact < SplashSpeed || !ReferenceEquals(KikasaDomain.Viewed, domain)) {
                return;
            }
            float k = MathHelper.Clamp((impact - SplashSpeed) / 13f, 0f, 1f);
            Vector2 hit = new(player.Center.X, domain.LakeWorldY);
            KikasaDomainDeco.SplashAt(hit, 6 + (int)(10f * k));
            KikasaDomainDeco.RippleAt(hit, 0.7f + 0.9f * k);
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Volume = 0.35f + 0.4f * k,
                Pitch = -0.4f + 0.25f * k,
                MaxInstances = 3,
            }, hit);
        }

        //沿湖面行走的踏水节拍：涟漪圈 + 水线行波 + 踢起的碎水星，步频随移速

        private static void WalkRipples(Player player, KikasaDomainPlayer domain) {
            if (!ReferenceEquals(KikasaDomain.Viewed, domain)) {
                return;
            }
            int who = player.whoAmI;
            float speed = MathF.Abs(player.velocity.X);
            if (speed < 1.5f) {
                walkRippleTimers[who] = 0;
                return;
            }
            int interval = (int)MathHelper.Clamp(15f - speed * 1.2f, 6f, 13f);
            if (++walkRippleTimers[who] < interval) {
                return;
            }
            walkRippleTimers[who] = 0;
            float k = MathHelper.Clamp((speed - 1.5f) / 6f, 0f, 1f);
            Vector2 at = new(player.Center.X + Main.rand.NextFloat(-4f, 4f), domain.LakeWorldY);
            KikasaDomainDeco.RippleAt(at, 0.45f + 0.35f * k + Main.rand.NextFloat(0.1f));
            KikasaDomainDeco.FootSplash(at, k, player.velocity.X);
        }
    }
}
