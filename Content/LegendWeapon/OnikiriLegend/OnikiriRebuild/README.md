# OnikiriRebuild

鬼切特效从 CoVFX 迁移到这里。新增代码和资源统一放在 `Content/LegendWeapon/OnikiriLegend/OnikiriRebuild`，不要放到 `LegendWeapon` 的同级目录。

## 迁移规则

1. 用到的贴图、Effect、音效、JSON 等资源一律复制进来；运行时不得引用 CoVFX、KL 或其他项目中的资源。测试刀的 `OnikiriItem.png` 也是本目录的独立副本。
2. 用到其他项目的方法、粒子或绘制效果时，在本目录独立实现；灾厄大修不依赖 KL，不为迁移新增 KL 依赖。
3. 已确定的贴图、Effect 和定义数据在 `ModSystem.Load` 时读取一次；自有图形对象在 `Unload` 释放。不要保留每帧资源请求、磁盘读取或调试热重载监听。
4. 每迁入一种效果，先通过 test 物品独立验证，再考虑接入鬼切正式武器。不要顺带修改无关武器行为或自动生成的本地化文件。
5. 源项目中的效果已验证可行，迁移时默认不需要重新预览效果，也不要搭建或运行离线 FNA / 实时预览器。只核对源码与资源是否正确复制、参数和时序是否保留、依赖替换及资源生命周期是否符合规则，并进行源码编译；仅在用户明确要求预览时才做预览。test 物品仍保留为游戏内独立测试入口，不要求迁移过程中自动运行视觉验证。

## 当前测试入口

`SakuraSlashTest` 使用一个泥土块制作。左键直接生成 `SakuraSlashProj`，右键直接生成 `SakuraSlash2Proj`。两者共用本地刀身贴图、刀光贴图、噪声图和 Shader，但分别使用本目录的 `SakuraSlash.json` / `SakuraSlash2.json` 和对应的 `*.weapon.anim.json`。

slash1 和 slash2 在刀光预出后沿各自 JSON 外缘发射樱花粒子，使用本地粒子模拟、花瓣网格绘制与 `SakuraBloom.xnb` 泛光；不需要 KL。花瓣跟随刀光时间倍率，生成后独立飘落，世界卸载时清空。碰撞和伤害仍只由弹幕控制，粒子仅作视觉表现。

伤害碰撞以玩家 `MountedCenter` 为扇形原点；加载时从各自 JSON 外缘曲线提取少量采样点，命中时使用与刀光相同的朝向、重力、旋转和倾斜变换。扇形边缘留 14 像素宽容差，`ModifyDamageHitbox` 提供覆盖整段刀光的粗判范围；伤害帧仍为原来的第 6 至 20 帧。

## 冲刺斩击 SakuraDashProj

`SakuraDashTest` 使用一个泥土块制作，左键朝鼠标方向施放 `SakuraDashProj`。这是独立的纯效果/位移测试物品，不造成伤害，不添加无敌帧，不接入正式鬼切；原有 `SakuraSlashTest` 的左右键入口保持不变。

- 保留原版 800 像素最大位移、首帧单点扫路与遇障退让、7 帧冲击行进、4 帧现身、12 帧残影及 40 帧本地运镜。沿用源项目的单点碰撞规则，不把它改为玩家全身碰撞检测。
- 同时迁入双冲刺环、黑红冲击锥、终点瞬斩及消蚀斩线，并保留持刀姿态、玩家图层、左右朝向与反重力处理。死亡、失控或切换物品会结束弹幕。
- `DashSlash.json`、`DashSlash.png`、`DashSlash_Edge.png`、`SakuraDashProj.png`、`air_037_4r.png`、`Eff_Trail_518_02.png` 复制自 CoVFX 的 `Content/Projectiles`；`FuzzyEdge.fx` / `FuzzyEdge.xnb`、`ShotLine.png` 和 `voronoiOrigin.png` 复制自 KL。运行时只使用本目录的副本，并复用此前迁入的本地 `SakuraSlash.xnb`、`PerlinX.png`、`OnikiriItem.png`。
- 原 KL 冲击锥助手由本地 `SakuraDashWindRenderer` 独立实现，保留二次贝塞尔采样、UV 滚动及非预乘混合。渲染器集中由 `SakuraDashSystem` 管理，资源与 JSON 在 `Load` 缓存，自有图形对象在 `Unload` 释放；没有逐帧资源请求、磁盘读取或热重载。
- 此次迁移按上述规则只做复制/依赖/源码编译检查，不生成预览、不运行游戏，不把源效果已验证等同于迁移后联机、碰撞及玩家图层已经实测通过。

## 预备姿态 SakuraReadyStanceProj

`SakuraReadyStanceTest` 使用一个泥土块制作，左键施放 60 帧的预备姿态。保留刀身发光、短促前刺与恢复动作；第 30 帧在鼠标位置生成 `SakuraRangedSlashProj`，并播放双层爆发粒子、30 帧径向模糊及随后 20 帧的十字星芒消蚀。测试发射的剑气伤害为 0，不接入正式鬼切。

第 30 帧依赖的剑气乱斩也独立迁入，包括 `SakuraRangedSlashProj.Pattern.cs` 中的手工刀序、`SakuraRangedSlashSequence`、`SakuraRangedSlash.json`、每刀碰撞/命中记录及花瓣发射；默认按源项目的 2 倍速播放。保留玩家身体旋转、左右朝向与反重力的刀身/星芒变换。死亡、失控或切换物品会结束预备姿态，已经发射的剑气和粒子独立完成播放。

## 居合大招 SakuraKiriPlayerProj

`SakuraKiriTest` 使用一个泥土块制作，左键施放整段居合演出。保留 60 帧蓄力、8 帧残影消失、手工空间斩、末段掠过、中心现身与收刀；人物实际位置不移动，掠过仅修改玩家绘制数据。八条蓄力气流分前后层绘制，刀身发光、左右/反重力、斩击镜头冲击和末段收刀参数沿用源项目。

收尾保留三层斩线的膨胀/爆发/收缩、150 个爆发/冲击粒子、球形花瓣爆发、流动光晕及 30 帧径向模糊。粒子是本地视觉列表，不占用弹幕槽，不发送网络包。演出时沿用源项目的输入与移动接管、免伤，结束/死亡/失控/切换物品时释放玩家接管；测试不造成伤害，也不修改正式鬼切。

## 本次资源与生命周期

- 新增的 `SakuraKiriMesh.fx` / `.xnb`、`PolarFlow.fx` / `.xnb`、`noi_1.png`、`Eff_Trail_2015.png`、`Eff_Streak_5555.png`、弹幕贴图及乱斩 JSON 复制自 CoVFX；`RadialBlur.fx` / `.xnb`、`ReColor.fx` / `.xnb`、`SakuraFinaleBurstDust.png`、`SakuraFinaleShockDust.png` 复制自 KL。粒子贴图分别来自 `Dusts/Burst/BurstDust.png` 和 `Dusts/ShockDust.png`，名称改为本地独立名称。
- `SakuraFinaleAssets.Load` 一次读取固定资源和乱斩定义；刀身发光遮罩、FuzzyEdge 的独立星芒副本、蓄力气流绘制器和乱斩绘制器按需创建一次，在 `Unload` 统一释放。涉及 FNA `GraphicsDevice` 的对象只在主线程首次绘制时创建，不能在 tModLoader 后台 `Load` 线程调用 `Effect.Clone`；已迁入的刀身、刀光、噪声、FuzzyEdge 和花瓣实现继续复用；没有源目录磁盘读取或 JSON 热重载。
- KL 的绘制助手、爆发/冲击粒子与屏幕反馈在本目录独立实现。局部粒子通过普通 `ModSystem.PostDrawTiles` 绘制；径向模糊在主帧捕获结束时拷屏并回写，使用独立的 `PreserveContents` 临时 RT，按当帧 `Main.screenTarget` 的实际宽高和格式创建或重建，不使用按 `Main.screenWidth/Height` 创建的共享 `screenSwap`。分辨率和 HDR 目标切换时检查实际尺寸，卸载时在主线程释放。世界卸载时清空本地粒子和模糊脉冲。
- 四个测试物品共享 `SakuraActionGuard`，防止斩击、冲刺、预备姿态和居合大招同时接管玩家。原有测试刀左右键功能及伤害参数不变，仅补充技能之间的互斥检查。
- 按迁移规则只核对资源副本、源码参数、依赖与编译，不搭建或运行预览器，不自动修改本地化文件，不声称已完成游戏内/联机实测。
