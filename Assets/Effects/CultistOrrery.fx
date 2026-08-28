// ============================================================================
//CultistOrrery.fx 拜月教徒·星轨司祭套件,一件效果一个 technique
//TechRing     浑天仪环带(vs+ps 条带):黑鎏金星铜,蚀刻星文+棱边亮线+充能辉流
//TechStarLine 星轨线(vs+ps 条带):轨道椭圆/星图连线,描绘进度+星屑虚线+过载白热
//TechCorridor 危险走廊(vs+ps 条带):掷星预瞄,平顶淡体+内置双缘轨,一趟画完读作"面"
//TechUmbra    本影楔(vs+ps 条带):蚀祭安全区,真暗遮蔽+硬影缘+钻石环缘线(与 TechShade 冕环同构)
//TechLance    冕矛(ps quad):月总幻影死光语法的星质喷矛,预警细丝→三层亮束+星尘外流,无暗鞘
//TechGaze     凝视死光(ps quad):月瞳湮灭射线,瞳口绽放+过曝白芯+扫向不对称,与冕矛(星质)分野
//TechGazeWarp 凝视死光扭曲位移图(ps quad):R方向 G强度 A混合,供 WarpShader 屏幕折射
//TechShade    蚀盘(ps quad):滑过行星的暗影球,盘径=画布 0.42(与 CultistPlanet 同契约)
//
//混合契约:全部预乘输出进 AlphaBlend;暗成分走 A 通道(真遮蔽),光成分 A≈0 纯加光
//极角审计:无 atan2;闭环 u 参数只喂整数倍频(frac/noise K∈Z),开线段自由
//采样器:噪声固定 s1,消费端 Textures[1]+LinearWrap 显式绑定
// ============================================================================

sampler uImage0 : register(s0);   //画布(SpriteBatch 主贴图,ps quad 技术不采样)
sampler uNoise : register(s1);    //平铺 Perlin

float4x4 transformMatrix;

float uTime;
float uAlpha;        //整体透明度
float3 uColDeep;     //暗底(星铜黑/烟鞘)
float3 uColMid;      //阶段主色
float3 uColBright;   //阶段亮色
float3 uColHot;      //白热芯
float uCharge;       //0~1 充能/过载/全食度
float uProgress;     //0~1 描绘进度/生长
float uDash;         //虚线频率(整数,0=实线)
float uArm;          //0~1 预警蓄势(TechLance/TechGaze)
float uEnv;          //0~1 宽度生命周期包络(TechLance/TechGaze)
float uSeed;         //实例错相
float uAspect;       //quad 长宽比 len/height,TechGaze 像素域特征折算
float uRotation;     //束世界角(仅 TechGazeWarp:位移方向编码需世界系)

//----------------------------------------------------------------------------
struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float noise(float2 uv) {
    return tex2D(uNoise, uv).r;
}

//绑定噪声实测值域 0.227~0.776,阈值一律先过归一
float nrm(float n) {
    return saturate((n - 0.227) / 0.549);
}

//----------------------------------------------------------------------------
//TechRing 浑天仪环带:u=沿环 0~1(闭环,只喂整数倍频),v=横截 0~1
//顶点色 rgb=纵深光照,a=透明度;C# 侧近亮远暗烘进顶点
//----------------------------------------------------------------------------
float4 RingPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float cross_ = (uv.y - 0.5) * 2.0;   //-1~1
    float depthLit = input.Color.r;      //纵深光照(0.35 远~1.15 近)
    float vA = input.Color.a;

    //带体:横截圆柱明暗,中脊微凸
    float body = 1.0 - smoothstep(0.86, 1.0, abs(cross_));
    float cyl = 0.62 + 0.38 * sqrt(saturate(1.0 - cross_ * cross_));

    //蚀刻星文:沿环整数倍频噪声格,格心亮蚀纹(K=6 闭环连续)
    float glyphN = noise(float2(uv.x * 6.0, uSeed * 0.37 + 0.13));
    float glyphCell = smoothstep(0.42, 0.62, nrm(glyphN));
    //纹只住带中段,不吃棱边
    float glyphBand = smoothstep(0.72, 0.5, abs(cross_));
    float glyph = glyphCell * glyphBand;

    //棱边亮线:两缘金属挑光
    float rim = smoothstep(0.55, 0.92, abs(cross_)) * body;

    //充能辉流:两股反向流光(K=2/3 闭环连续)
    float flow1 = pow(saturate(1.0 - abs(frac(uv.x * 2.0 - uTime * 0.55) - 0.5) * 4.0), 3.0);
    float flow2 = pow(saturate(1.0 - abs(frac(uv.x * 3.0 + uTime * 0.34 + 0.5) - 0.5) * 5.0), 3.0);
    float flow = (flow1 * 0.7 + flow2 * 0.5) * uCharge;

    //合成:暗星铜体(遮蔽)+蚀纹按充能透光+棱边挑光+辉流
    float3 metal = uColDeep * (1.05 + 0.75 * cyl) * depthLit;
    metal += uColMid * 0.10 * cyl * depthLit;   //环体底色微透阶段色,黑背景也有剪影
    float3 col = metal;
    col += uColMid * glyph * (0.30 + 0.9 * uCharge) * depthLit;
    col += lerp(uColMid, uColBright, 0.6) * rim * (0.46 + 0.6 * uCharge) * depthLit;
    col += uColBright * flow * glyphBand * 1.1;
    col += uColHot * flow * flow * 0.45;

    float bodyA = body * 0.92 * vA;
    return float4(col * body * vA, bodyA) * uAlpha;
}

technique TechRing
{
    pass RingPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 RingPS();
    }
}

//----------------------------------------------------------------------------
//TechStarLine 星轨线:u=沿线,v=横截;顶点色 rgb=纵深,a=透明度
//uProgress 描绘进度(u>progress 未画),uDash 星屑虚线频率,uCharge 过载白热
//----------------------------------------------------------------------------
float4 StarLinePS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float cross_ = (uv.y - 0.5) * 2.0;
    float depthLit = input.Color.r;
    float vA = input.Color.a;

    //描绘进度:画头一点亮尖
    float visible = smoothstep(uProgress + 0.012, uProgress - 0.03, uv.x);
    float pen = exp(-abs(uv.x - uProgress) * 60.0) * step(uProgress, 0.985) * step(0.01, uProgress);

    //线体:软缘+细芯
    float lineBody = exp(-cross_ * cross_ * 9.0);
    float core = exp(-cross_ * cross_ * 55.0);

    //星屑虚线:亮珠沿线流动(uDash 整数,闭环安全)
    float dashPhase = frac(uv.x * uDash - uTime * 0.5 + uSeed);
    float dash = uDash > 0.5 ? pow(saturate(1.0 - abs(dashPhase - 0.5) * 3.2), 2.0) : 1.0;

    //微光尘:线边缘的散逸星点,沿线缓漂
    float dustN = nrm(noise(float2(uv.x * 5.0 + uSeed * 3.1 - uTime * 0.22, cross_ * 0.7 + uSeed)));
    float dust = pow(dustN, 3.0) * lineBody * 0.5;

    float glow = (lineBody * 0.4 + core) * (0.35 + 0.65 * dash);
    //过载:白热增压+呼吸脉动
    float over = uCharge * (0.86 + 0.14 * sin(uTime * 13.0 + uSeed * 17.0 + uv.x * 4.0));
    glow *= 1.0 + over * 1.6;

    float3 col = uColMid * lineBody * 0.5 + uColBright * glow + uColHot * core * (0.3 + over * 1.2);
    col += uColBright * dust;
    col += uColHot * pen * 2.2;

    float a = saturate(core * 0.30 + over * core * 0.4) * visible * vA;
    return float4(col * visible * vA, a) * uAlpha;
}

technique TechStarLine
{
    pass StarLinePass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 StarLinePS();
    }
}

//----------------------------------------------------------------------------
//TechCorridor 危险走廊(vs+ps 条带):掷星预瞄——平顶淡体+内置双缘亮轨,一趟画完
//u=沿线,v=横截;缘轨住在 |cross|=0.92(C# 半宽按 corridorHalf/0.92 折算,轨恰落走廊边界)
//uCharge 锁定过载白热,uDash 内部星屑流频率,uProgress 描绘进度(与 TechStarLine 同语义)
//----------------------------------------------------------------------------
float4 CorridorPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float cross_ = (uv.y - 0.5) * 2.0;
    float vA = input.Color.a;

    //描绘进度门
    float visible = smoothstep(uProgress + 0.012, uProgress - 0.03, uv.x);

    //平顶体:走廊内均匀淡光,缘轨外快速归零(面读作面,不再是中心高斯的"线")
    float band = 1.0 - smoothstep(0.92, 1.0, abs(cross_));
    //内置缘轨:边界细亮线
    float rail = exp(-pow(abs(abs(cross_) - 0.92), 2.0) * 700.0);

    //内部星屑流:沿线流动亮珠(uDash 频率)+散逸微尘
    float dashPhase = frac(uv.x * uDash - uTime * 0.55 + uSeed);
    float dash = uDash > 0.5 ? pow(saturate(1.0 - abs(dashPhase - 0.5) * 3.0), 2.0) : 0.0;
    float dustN = nrm(noise(float2(uv.x * 5.0 + uSeed * 3.1 - uTime * 0.25, cross_ * 1.1 + uSeed)));
    float dust = pow(dustN, 4.0) * band * 0.6;

    //过载:锁定白热+呼吸脉动
    float over = uCharge * (0.86 + 0.14 * sin(uTime * 12.0 + uSeed * 17.0 + uv.x * 4.0));

    float3 col = uColMid * band * (0.30 + 0.30 * over);
    //星屑随波前明灭:流向读得出,但不把波画成横穿宽带的整根竖棂
    col += uColBright * dust * (0.7 + dash * 1.8);
    col += uColBright * dash * band * 0.10;
    col += lerp(uColMid, uColBright, 0.7) * rail * (0.75 + 0.8 * over);
    col += uColHot * rail * over * 1.4;

    //体淡遮+轨微实:锁定后走廊整体压实
    float a = saturate(band * (0.10 + 0.20 * over) + rail * (0.16 + 0.30 * over));
    return float4(col * visible * vA, a * visible * vA) * uAlpha;
}

technique TechCorridor
{
    pass CorridorPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 CorridorPS();
    }
}

//----------------------------------------------------------------------------
//TechUmbra 本影楔:u=沿影(0 根部/行星侧),v=横截;真暗遮蔽=A 承载
//uProgress=显形度,缘界冕环声明安全区边界(缺口即所见);uCharge=冕矛将至的缘线脉冲
//缘线与蚀盘钻石环同构:半影影缘+高斯白热芯+外泄色辉,几何干净,亮度沿缘呼吸
//(撕边位置抖动已废:三补收半影后仍被判糊,四补对齐星缘锐度,五补白缘裁剪成硬白带→高斯软峰+半影回放)
//----------------------------------------------------------------------------
float4 UmbraPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float cross_ = (uv.y - 0.5) * 2.0;
    float vA = input.Color.a;
    float crossA = abs(cross_);

    //影体:主体平台实暗,缘沿留半影过渡(纯硬切被判生硬,五补放宽)
    float shadowCore = 1.0 - smoothstep(0.755, 0.83, crossA);
    //根部收进行星轮廓,末端收口
    float lenEnv = smoothstep(0.0, 0.05, uv.x) * (1.0 - smoothstep(0.92, 1.0, uv.x));
    //影内星尘:安全区里安静的微光
    float starN = nrm(noise(float2(uv.x * 3.0 + uSeed, cross_ * 1.4 + uSeed * 2.3 - uTime * 0.02)));
    float stars = pow(starN, 6.0) * shadowCore;

    float dark = shadowCore * lenEnv * uProgress;

    //缘界冕环:高斯软缘(exp 尖峰+饱和裁剪被判硬,五补换软峰降增益),色辉只向影外泄
    float edgeD = crossA - 0.793;
    float ring = exp(-edgeD * edgeD * 60.0) * lenEnv * uProgress;
    float ringCore = exp(-edgeD * edgeD * 500.0) * lenEnv * uProgress;
    float outGate = smoothstep(-0.02, 0.05, edgeD);
    //亮度沿缘呼吸:几何不动,活在光里(钻石环 coronaN 同构)
    float breatheN = 0.72 + 0.28 * nrm(noise(float2(uv.x * 4.0 + uSeed, uSeed * 2.3 + uTime * 0.05)));
    float pulse = 1.0 + uCharge * (0.5 + 0.4 * sin(uTime * 16.0 + uv.x * 5.0));

    float3 col = float3(0.004, 0.006, 0.012) * dark;
    col += uColBright * stars * 0.22 * uProgress;
    col += lerp(uColMid, uColBright, 0.6) * ring * outGate * breatheN * 0.42 * pulse;
    col += uColHot * ringCore * (0.55 + 0.25 * (pulse - 1.0));

    float a = (dark * 0.85 + ringCore * 0.08) * vA;
    return float4(col * vA, a) * uAlpha;
}

technique TechUmbra
{
    pass UmbraPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 UmbraPS();
    }
}

//----------------------------------------------------------------------------
//TechLance 冕矛(ps quad):月总幻影死光语法(MLordDeathray 血统)——亮而实的星质束,无暗烟鞘
//uv.x=沿矛(0 根部),uv.y=横截;uArm 预警细丝相,uEnv 宽度生命周期(0 未展~1 全宽),uCharge 炽度
//uProgress 破面渐显段长(u):根埋星体内自零亮起;塌缩期 C# 推高=自根烧断;0=关闭
//横截三层全亮(宽晕/星质体/白热芯)+引力缘光细丝+星尘亮粒外流+相位明灭;
//根部喇叭涨满+出膛增亮,末端噪声撕成星雾;旧版两侧暗烟鞘被判"中间亮两边黑",勿加回
//----------------------------------------------------------------------------
float4 LancePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float u = coords.x;
    float cross_ = (coords.y - 0.5) * 2.0;

    //破面而出:u<uProgress 段自零亮起,束体不横穿星面
    float emergeGate = step(0.001, uProgress);
    float emerge = lerp(1.0, smoothstep(0.0, max(uProgress, 1e-4), u), emergeGate);

    //末端撕散:前沿被噪声撕成星雾,不留平切
    float tipTurb = nrm(noise(float2(u * 2.8 - uTime * 1.1, cross_ * 0.6 + uSeed + 0.5))) - 0.5;
    float uTip = u + tipTurb * 0.14;
    float tipFade = 1.0 - smoothstep(0.70, 0.98, uTip);

    //宽度轮廓:根部喇叭涨满→近恒宽→末端纺锤收窄
    //q=1 即满宽亮体半宽(画布半高的 0.62,quad 高 110 时 ≈34px,判定线藏于亮体内)
    float w = 0.62 * lerp(0.55, 1.0, smoothstep(0.0, 0.12, u));
    w *= lerp(1.0, 0.50, smoothstep(0.72, 0.98, uTip));
    w *= max(uEnv, 1e-3);

    //主轴微呼吸:低频噪声缓拉(引力拽着束身);预警细丝不吃弯(预告即承诺)
    float sway = (nrm(noise(float2(u * 1.6 - uTime * 0.9, uSeed))) - 0.5) * 0.10 * uEnv * smoothstep(0.0, 0.3, u);
    float q = abs(cross_ - sway) / w;

    //---- 预警细丝:全长极细亮线+根部呼吸辉点 ----
    float fil = exp(-cross_ * cross_ * 700.0) * uArm * (0.55 + 0.45 * sin(uTime * 9.0 + uSeed * 13.0));
    float armRoot = exp(-u * u * 40.0) * exp(-cross_ * cross_ * 24.0) * uArm;

    //---- 束体三层:宽晕/星质体/白热芯,全亮 ----
    float halo = exp(-q * q * 0.9) * 0.45;
    float body = exp(-q * q * 2.0);
    float hot = exp(-q * q * 16.0);

    //星尘亮粒:双倍频噪声阈成稀疏星屑,沿束外流(喷发方向)
    float grainA = nrm(noise(float2(u * 6.5 - uTime * 1.8, cross_ * 1.7 + uSeed)));
    float grainB = nrm(noise(float2(u * 11.0 - uTime * 3.1, cross_ * 2.9 + uSeed + 0.37)));
    float grains = smoothstep(0.60, 0.95, grainA * 0.6 + grainB * 0.55) * exp(-q * q * 1.5);

    //引力缘光:亮体边界(q=1)的细亮丝随噪声波动(光在界面弯折)
    float rimWave = (nrm(noise(float2(u * 3.2 - uTime * 1.4, uSeed + 0.71))) - 0.5) * 0.12;
    float fringe = exp(-pow((abs(q - 1.0) + rimWave) * 7.0, 2.0)) * 0.8;

    //泵浪:亮波沿矛外推(能量持续被泵进矛身)
    float surge = pow(saturate(1.0 - abs(frac(u * 3.0 - uTime * 1.3 + uSeed) - 0.5) * 3.2), 3.0);

    //相位明灭:星质不恒亮
    float phase = 0.9 + 0.1 * sin(uTime * 9.0 + uSeed * 21.0 + u * 6.0);

    //根部增亮:出膛处最烈
    float rootBoost = 1.0 + 1.2 * exp(-u * u * 30.0);

    float3 col = uColMid * halo * 1.05;
    col += lerp(uColMid, uColBright, 0.65) * body * (0.75 + 0.55 * uCharge);
    col += uColHot * hot * (0.9 + 0.7 * surge * uCharge);
    col += uColHot * grains * (0.45 + 0.45 * uCharge);
    col += uColBright * fringe * (0.55 + 0.55 * uCharge);
    col *= tipFade * phase * rootBoost;

    //预警层叠加
    col += uColBright * fil * 0.9 + uColMid * armRoot * 0.8;

    //星质束半实:遮挡由亮体自己扛(无暗鞘),塌缩期随 uEnv 变细自然减淡
    float a = (body * 0.34 + hot * 0.28 + grains * 0.18 + halo * 0.07 + fringe * 0.10) * tipFade;
    a += fil * 0.05 + armRoot * 0.10;

    return float4(col * emerge, saturate(a * emerge)) * uAlpha * vertexColor;
}

technique TechLance
{
    pass LancePass
    {
        PixelShader = compile ps_3_0 LancePS();
    }
}

//----------------------------------------------------------------------------
//TechGaze 凝视死光(ps quad):月总幻影死光语法(MLordDeathray 血统,与冕矛同宗)
//uv.x=沿束(0 瞳孔),uv.y=横截;uProgress=瞳口绽放段长(u) uDash=扫向符号(拖尾侧辉光略长)
//uAspect=quad长宽比(len/height,像素域特征折算) uArm 预警倒吸相 uEnv 宽度包络 uCharge 炽度
//横截五层:白热平芯/硬缘亮体/细缘线/色辉长尾/宽软晕(光压场);
//芯缘零位置抖动、无暗烟鞘——糊的主因是抖缘+无白热芯(蚀祭四补教训),质感全走亮度层
//宽度轮廓:锥形喇叭 w=lerp(0.38,1.06,u)——瞳口细针沿束线性放宽(里窄外宽);
//能量涌动:泵浪波包沿束外推时同步鼓宽束身(+10%)+慢涌噪声起伏(±5%)——涌动=持续改宽,
//波是相干低频行波(整缘同鼓,缘线仍锐),不是逐像素噪声抖缘(蚀祭教训的"糊"不会回来);
//C# 判定按基准锥折算(CultistGazeBeam.HitWidthAt,芯缘 q≈0.285、quad 高 460),改此处必同步改那边
//----------------------------------------------------------------------------
float4 GazePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float u = coords.x;
    float cross_ = (coords.y - 0.5) * 2.0;
    //像素域横坐标(高度单位):特征频率不随束长拉稀
    float px = u * uAspect;

    //瞳口绽放:极短渐显,光自瞳孔长满
    float emerge = smoothstep(0.0, max(uProgress, 1e-4), u);

    //重型泵浪:粗波包沿束外推,大口径的脉冲是沉的(提前算:涌动同源鼓宽束身)
    float surge = pow(saturate(1.0 - abs(frac(u * 2.0 - uTime * 0.9 + uSeed) - 0.5) * 2.6), 2.0);
    //慢涌:低频噪声起伏,束身呼吸的底流(与 TechGazeWarp 同式,改一处必改两处)
    float slowSwell = (nrm(noise(float2(px * 0.22 - uTime * 1.6, uSeed * 3.3))) - 0.5) * 2.0;

    //宽度轮廓:锥形喇叭——瞳口 0.38 沿束线性放宽到 1.06(里窄外宽,末端交给撞墙飞溅);
    //涌动=能量包过处束身鼓宽:与泵浪同波(又宽又亮),慢涌垫底
    float w = lerp(0.38, 1.06, u) * (1.0 + 0.10 * surge + 0.05 * slowSwell) * uEnv;
    w *= lerp(0.15, 1.0, emerge);
    float q = abs(cross_) / max(w, 1e-3);
    //画布缘保险:锥形末端 w 大,软晕/色辉长尾在 quad 边界前自然归零,不留硬切
    float edgeGuard = 1.0 - smoothstep(0.84, 1.0, abs(cross_));

    //---- 预警倒吸:细丝+光流向瞳孔回卷(蓄力的因果链) ----
    float fil = exp(-cross_ * cross_ * 520.0) * uArm;
    float inflow = pow(saturate(1.0 - abs(frac(px * 0.45 + uTime * 2.4) - 0.5) * 3.0), 3.0);

    //---- 束体:平顶白热芯(缘 q≈0.285,与判定锥同宽)+平顶亮体(缘 q≈0.47) ----
    float hot = smoothstep(0.32, 0.25, q);
    float body = smoothstep(0.50, 0.44, q);
    //缘线:骑在亮体边界上的细亮丝(死光的"画出来的边")
    float rim = exp(-pow((q - 0.47) * 18.0, 2.0));
    //扫向不对称:拖尾侧(uDash 反侧)辉光略长=残像,前缘紧=危险面锐利
    float trailSide = saturate(cross_ * uDash * 2.0);
    float dOut = max(q - 0.50, 0.0);
    float glow = exp(-dOut * dOut * lerp(15.0, 7.5, trailSide)) * smoothstep(0.38, 0.55, q) * edgeGuard;
    //宽软晕:亮体外的光压场(比色辉更宽更淡),纯加光不进 alpha
    float halo = exp(-q * q * 0.62) * edgeGuard;

    //束内能流:高速外涌的亮纹,只调亮度不动几何(位置抖动=糊)
    float flow = 0.88 + 0.12 * nrm(noise(float2(px * 0.30 - uTime * 2.6 + uSeed * 4.7, cross_ * 0.8 + uSeed)));

    //瞳口辉+镜头横条:眼睛在发射(遮住根部接缝的主力)
    float muzzleEnv = max(uEnv, uArm * 0.55);
    float muzzle = exp(-(px * px + cross_ * cross_ * 0.25) * 4.9) * muzzleEnv;
    float streak = exp(-abs(cross_) * 26.0) * exp(-px * px * 1.4) * muzzleEnv;

    //调色:外辉向深月色沉得更慢(光场更满),亮体月绿,芯过曝成白
    float3 col = lerp(uColMid, uColDeep * 1.6, saturate(dOut * 1.6)) * glow * 0.8;
    col += uColMid * halo * 0.34;
    col += lerp(uColMid, uColBright, 0.7) * body * flow * (0.85 + 0.45 * surge * uCharge);
    col += uColBright * rim * (0.7 + 0.45 * uCharge);
    col += uColHot * hot * flow * (1.05 + 0.5 * surge) * (0.78 + 0.42 * uCharge);
    col += uColHot * (muzzle * 1.6 + streak * 1.0) * (0.55 + 0.45 * uCharge);
    //预警层:细丝上光珠倒流进瞳孔
    col += uColBright * fil * (0.5 + inflow * 1.6);

    //亮体近实:遮挡由亮体自己扛(无暗鞘),芯区 alpha 顶满=实体死光
    float a = (body * 0.78 + hot * 0.24 + rim * 0.08 + glow * 0.10) * uEnv;
    a += fil * 0.05 + muzzle * 0.28;

    //整体门:瞳口绽放+末端软散
    float gate = emerge * (1.0 - smoothstep(0.94, 1.0, u));
    return float4(col * gate, saturate(a * gate)) * uAlpha * vertexColor;
}

technique TechGaze
{
    pass GazePass
    {
        PixelShader = compile ps_3_0 GazePS();
    }
}

//----------------------------------------------------------------------------
//TechGazeWarp 凝视死光扭曲位移图(ps quad):供 WarpShader 的 R=方向 G=强度 A=混合
//与 TechGaze 同画布同锥宽同涌动(常量改一处必改两处),扭曲场住在亮体缘(q=0.5)外一圈:
//束内是实光不扭,缘外空气被能量挤开——垂直束轴向外推挤+涟漪小角摆,涌动包过处提幅;
//uRotation=束世界角;方向编码 frac(角/2π+0.5)=幻象推向的采样反向(毁灭者热浪同约定)
//----------------------------------------------------------------------------
float4 GazeWarpPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float u = coords.x;
    float cross_ = (coords.y - 0.5) * 2.0;
    float px = u * uAspect;

    //与 TechGaze 同锥同涌动
    float surge = pow(saturate(1.0 - abs(frac(u * 2.0 - uTime * 0.9 + uSeed) - 0.5) * 2.6), 2.0);
    float slowSwell = (nrm(noise(float2(px * 0.22 - uTime * 1.6, uSeed * 3.3))) - 0.5) * 2.0;
    float w = lerp(0.38, 1.06, u) * (1.0 + 0.10 * surge + 0.05 * slowSwell) * uEnv;
    float q = abs(cross_) / max(w, 1e-3);

    //扭曲场:贴亮体缘向外衰减的热晕带,束内(q<0.5)归零
    float dOut = max(q - 0.50, 0.0);
    float field = exp(-dOut * dOut * 1.8) * smoothstep(0.34, 0.56, q);
    //画布缘/端部包络:位移在 quad 边界前收干净,不留折射硬切
    field *= 1.0 - smoothstep(0.80, 0.98, abs(cross_));
    field *= smoothstep(0.0, 0.05, u) * (1.0 - smoothstep(0.92, 1.0, u));

    //涟漪:沿束外推的密波,噪声搓相位(空气受热的抖)
    float rippleN = (nrm(noise(float2(px * 0.30 - uTime * 2.6 + uSeed * 4.7, cross_ * 0.8 + uSeed))) - 0.5) * 5.0;
    float ripple = sin(px * 2.4 - uTime * 8.0 + rippleN);

    //方向:垂直束轴向外挤(侧别定号)+涟漪小角摆
    float side = cross_ >= 0.0 ? 1.0 : -1.0;
    float worldAngle = uRotation + side * 1.5708 + ripple * 0.35;
    float direction = frac(worldAngle / 6.28318 + 0.5);

    //强度:缘场×涟漪脉动×涌动包提幅,随宽度包络起落
    float magnitude = field * (0.16 + 0.18 * abs(ripple)) * (0.6 + 0.4 * surge) * saturate(uEnv);

    return float4(direction, saturate(magnitude), 0.0, saturate(field * saturate(uEnv)));
}

technique TechGazeWarp
{
    pass GazeWarpPass
    {
        PixelShader = compile ps_3_0 GazeWarpPS();
    }
}

//----------------------------------------------------------------------------
//TechShade 蚀盘(ps quad):盘径=画布 0.42(CultistPlanet 同契约)
//uCharge=全食度:贴缘冕环随全食亮起(钻石环),盘体真暗遮行星
//----------------------------------------------------------------------------
float4 ShadePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float2 q = p / 0.42;
    float rq = length(q);
    if (rq > 1.5) {
        return float4(0, 0, 0, 0);
    }

    float inDisc = 1.0 - smoothstep(0.985, 1.01, rq);

    //盘体:近黑影球,弱环形山起伏透一点冷光
    float relief = nrm(noise(q * 1.3 + uSeed * 3.7)) * 0.05;
    float3 body = float3(0.006, 0.008, 0.015) + uColDeep * relief;

    //冕环:全食时贴缘一圈炽光泄出
    float corona = exp(-abs(rq - 1.02) * 16.0) * smoothstep(0.985, 1.06, rq);
    float coronaN = 0.7 + 0.3 * nrm(noise(q * 0.9 + uTime * 0.05));
    float3 coronaCol = lerp(uColMid, uColBright, coronaN) * corona * uCharge * 1.4;
    coronaCol += uColHot * corona * corona * uCharge * 0.8;

    float a = inDisc * 0.97;
    float3 C = body * a + coronaCol;
    return float4(C, a + corona * uCharge * 0.08) * uAlpha * vertexColor;
}

technique TechShade
{
    pass ShadePass
    {
        PixelShader = compile ps_3_0 ShadePS();
    }
}
