namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core
{
    /// <summary>战斗调参中心，推进死线的节拍表</summary>
    internal static class WofDirector
    {
        #region 推进
        /// <summary>基础推进速度 px/f</summary>
        public const float BaseAdvanceSpeed = 2.3f;
        /// <summary>随失血追加的推进速度上限</summary>
        public const float LifeAdvanceBonus = 4.6f;
        /// <summary>目标远离时每像素加速</summary>
        public const float CatchUpPerPixel = 0.0012f;
        /// <summary>开始追赶的领先距离</summary>
        public const float CatchUpDistance = 980f;
        /// <summary>目标贴脸时的减速下限系数</summary>
        public const float CloseEaseFloor = 0.42f;
        /// <summary>贴脸减速起算距离</summary>
        public const float CloseEaseDistance = 620f;
        /// <summary>脱屏激怒判定距离</summary>
        public const float FarEnrageDistance = 1500f;
        /// <summary>脱屏激怒累计帧</summary>
        public const int FarEnrageFrames = 540;
        /// <summary>脱屏激怒速度倍率</summary>
        public const float FarEnrageMultiplier = 2.7f;
        #endregion

        #region 突进
        /// <summary>突进预告帧(压缩蓄势)</summary>
        public const int SurgeTelegraph = 52;
        /// <summary>突进冲刺帧</summary>
        public const int SurgeDashFrames = 26;
        /// <summary>突进速度 px/f</summary>
        public const float SurgeSpeed = 27f;
        /// <summary>突进刹车帧</summary>
        public const int SurgeBrakeFrames = 22;
        /// <summary>突进期墙面带状追加伤害(普通模式基准)</summary>
        public const int SurgeBandDamage = 26;
        #endregion

        #region 漩涡
        public const int VortexWindup = 46;
        public const int VortexDuration = 250;
        /// <summary>吸引作用距离</summary>
        public const float VortexRange = 1500f;
        /// <summary>最大吸引加速度 px/f^2</summary>
        public const float VortexPullMax = 0.42f;
        #endregion

        #region 扫描协议
        public const int ScanCharge = 66;
        public const int ScanDuration = 240;
        /// <summary>扫描半弧(弧度)</summary>
        public const float ScanArcHalf = 0.44f;
        #endregion

        #region 饥饿网
        public const int NetWeaveFrames = 80;
        public const int NetHoldFrames = 300;
        /// <summary>网面距墙面距离</summary>
        public const float NetForwardOffset = 540f;
        /// <summary>网链伤害(基准)</summary>
        public const int NetLinkDamage = 14;
        /// <summary>成网最少饥饿者数</summary>
        public const int NetMinHungries = 3;
        #endregion

        #region 水蛭浪
        public const int RetchWindup = 34;
        /// <summary>单场水蛭上限(含原版余量)</summary>
        public const int LeechCap = 10;
        #endregion

        #region 血肉尖刺
        /// <summary>尖刺波数</summary>
        public const int SpikeWaveCount = 5;
        /// <summary>波间隔帧</summary>
        public const int SpikeWaveInterval = 26;
        /// <summary>波内列间距 px</summary>
        public const float SpikeColumnSpacing = 190f;
        public const int SpikeDamage = 22;
        #endregion

        #region 舌鞭
        public const int TongueTelegraph = 38;
        public const int TongueDamage = 18;
        #endregion

        #region 舌卷回吞(投技)
        /// <summary>抓取预告帧(比普通舌鞭更长更重)</summary>
        public const int GrabTelegraph = 52;
        /// <summary>甩舌窗口帧(伸出+落空回收)</summary>
        public const int GrabLashFrames = 44;
        /// <summary>回卷帧(前8帧绷紧顿帧)</summary>
        public const int GrabReelFrames = 62;
        /// <summary>咀嚼帧</summary>
        public const int GrabChewFrames = 84;
        /// <summary>吐出后舌头回吞帧</summary>
        public const int GrabSpitTail = 14;
        /// <summary>收尾恢复帧</summary>
        public const int GrabRecoverFrames = 40;
        /// <summary>抓取舌伸出速度 px/f</summary>
        public const float GrabExtendSpeed = 40f;
        /// <summary>抓取舌最大射程</summary>
        public const float GrabMaxReach = 1080f;
        /// <summary>回卷速度上限 px/f</summary>
        public const float GrabReelSpeed = 24f;
        /// <summary>咀嚼保持点距墙心前伸距离</summary>
        public const float GrabMouthInset = 110f;
        /// <summary>抓取舌接触伤害(基准)</summary>
        public const int GrabSnagDamage = 12;
        /// <summary>沿途撕咬伤害(基准，单口)</summary>
        public const int GrabBiteDamage = 9;
        /// <summary>咀嚼伤害(基准，前两口)</summary>
        public const int GrabChewDamage = 20;
        /// <summary>终结咬伤害(基准，最后一口)</summary>
        public const int GrabChewFinalDamage = 26;
        /// <summary>投技冷却帧(两条触发路径共享)</summary>
        public const int GrabCooldownFrames = 1080;
        /// <summary>绕后惩罚升级判定距离(被舌拖玩家距嘴)</summary>
        public const float GrabPunishRange = 260f;
        #endregion

        #region 大迁徙
        public const int ExodusWindup = 96;
        public const int ExodusDuration = 560;
        /// <summary>血幕初始落后距离</summary>
        public const float CurtainStartGap = 1650f;
        /// <summary>血幕最小口袋宽(公平阀)</summary>
        public const float CurtainMinGap = 720f;
        /// <summary>血幕相对墙的追近速度 px/f</summary>
        public const float CurtainCloseRate = 1.9f;
        /// <summary>血幕触碰伤害(基准)</summary>
        public const int CurtainDamage = 30;
        /// <summary>大迁徙后的喘息窗口帧</summary>
        public const int ExodusRestFrames = 130;
        #endregion

        #region 节奏(推进枢纽波形)
        /// <summary>重招后间隔倍率(长喘息，波谷)</summary>
        public const float GapHeavyMul = 1.35f;
        /// <summary>轻招后间隔倍率(快衔接，压迫收紧)</summary>
        public const float GapLightMul = 0.7f;
        /// <summary>间隔前段喘息占比(上一招余韵，墙身松弛)</summary>
        public const float GapLullFraction = 0.38f;
        /// <summary>喘息期起始速度系数</summary>
        public const float GapLullFactor = 0.7f;
        /// <summary>间隔末段蓄势占比</summary>
        public const float GapChargeFraction = 0.28f;
        /// <summary>蓄势末峰值速度系数，出招瞬间的减速与之形成对比刹车</summary>
        public const float GapChargePeak = 1.5f;
        #endregion

        #region 饥饿长城(颚浪)
        /// <summary>颚道数：墙域纵向均分</summary>
        public const int JawLaneCount = 5;
        /// <summary>入招紧咬蓄势帧</summary>
        public const int JawIntroFrames = 30;
        /// <summary>颚芽自墙面隆起帧</summary>
        public const int JawGrowFrames = 26;
        /// <summary>成形到首咬的最短开口期</summary>
        public const int JawGapeMin = 18;
        /// <summary>相邻颚咬合错拍帧：≥伤害窗(伸8+扣6)，相邻车道永不同时热(公平阀)</summary>
        public const int JawSnapStagger = 16;
        /// <summary>咬合前颤动预告帧(嘶声+喉光)</summary>
        public const int JawPreSnapFrames = 12;
        /// <summary>咬合伸出帧</summary>
        public const int JawLungeFrames = 8;
        /// <summary>咬死扣合帧</summary>
        public const int JawClampFrames = 6;
        /// <summary>缩回帧</summary>
        public const int JawRetractFrames = 14;
        /// <summary>咬程 px：贴墙压迫圈，退出圈外即安全</summary>
        public const float JawReach = 430f;
        /// <summary>颚体判定厚度 px</summary>
        public const float JawHitWidth = 40f;
        /// <summary>噬咬基准伤害</summary>
        public const int JawDamage = 28;
        /// <summary>两轮之间喘息帧</summary>
        public const int JawVolleyGap = 36;
        /// <summary>收尾恢复帧</summary>
        public const int JawOutroFrames = 30;
        /// <summary>单轮颚浪全长(帧)：成形+开口+末位错拍+咬合全程+余韵</summary>
        public const int JawVolleyLife = JawGrowFrames + JawGapeMin
            + (JawLaneCount - 1) * JawSnapStagger
            + JawLungeFrames + JawClampFrames + JawRetractFrames + 12;
        #endregion

        #region 腐眼断头闸
        /// <summary>腐眼成形帧(期间跟踪玩家高度)</summary>
        public const int GuillotineGrow = 40;
        /// <summary>阶段3第二只腐眼的缩短成形帧</summary>
        public const int GuillotineGrow2 = 28;
        /// <summary>锁定闪烁帧(高度已冻结，预告即承诺)</summary>
        public const int GuillotineLockFlash = 12;
        /// <summary>击发前静默拍</summary>
        public const int GuillotineSilence = 8;
        /// <summary>斩束持续帧</summary>
        public const int GuillotineSustain = 34;
        /// <summary>斩束衰减帧(无伤害)</summary>
        public const int GuillotineDecay = 14;
        /// <summary>两束间歇帧(阶段3)</summary>
        public const int GuillotineInterval = 16;
        /// <summary>收尾恢复帧</summary>
        public const int GuillotineRecover = 30;
        /// <summary>斩束判定半厚 px：低于跳跃高度，原地起跳可越(公平阀)</summary>
        public const float GuillotineHalfHeight = 30f;
        /// <summary>斩束基准伤害</summary>
        public const int GuillotineDamage = 24;
        #endregion

        #region 通用
        /// <summary>触发死亡演出的生命阈值</summary>
        public const int DeathTriggerLife = 10;
        /// <summary>转阶段生命比例</summary>
        public const float Phase2LifeRatio = 0.66f;
        /// <summary>低血大招生命比例</summary>
        public const float ExodusLifeRatio = 0.33f;
        /// <summary>眼部平射预告帧</summary>
        public const int EyePotshotTelegraph = 42;
        /// <summary>眼部平射基准伤害</summary>
        public const int EyeLaserDamage = 12;
        /// <summary>扫描光束基准伤害</summary>
        public const int ScanBeamDamage = 16;
        /// <summary>血凝块基准伤害</summary>
        public const int BloodClotDamage = 14;
        #endregion

        /// <summary>阶段间隔帧：两招之间的推进喘息</summary>
        public static int AdvanceGapFrames(int phase, bool death) {
            int baseGap = phase switch {
                1 => 150,
                2 => 112,
                _ => 84,
            };
            if (death) {
                baseGap = (int)(baseGap * 0.8f);
            }
            return baseGap;
        }

        /// <summary>重招判定：这些招之后玩家需要长喘息</summary>
        public static bool IsHeavyAttack(WofStateIndex idx) {
            return idx is WofStateIndex.SurgeDash or WofStateIndex.TongueGrab
                or WofStateIndex.MawVortex or WofStateIndex.JawRipple;
        }

        /// <summary>招后间隔倍率：重招长谷、轻招短峰，把匀速平推组织成波形</summary>
        public static float AttackGapMul(WofStateIndex prev) {
            if (IsHeavyAttack(prev)) {
                return GapHeavyMul;
            }
            if (prev is WofStateIndex.TongueLash or WofStateIndex.LeechWave) {
                return GapLightMul;
            }
            return 1f;
        }
    }
}
