// ============================================================================
//CultistPlanet.fx 拜月教徒召唤的巨型天体,一颗星球一个 technique
//TechVortex 星旋·气态巨行星:纬度剪切条带 + 风暴眼 + 云层内部闪电 + 大气缘光
//
//球体契约:球盘占画布半径 0.42(晕圈余量),C# 折算 quadPx = 可见半径px / 0.42 * 2
//混合契约:AlphaBlend 预乘输出;盘体 A≈1 遮挡背景(实心天体),盘外晕圈 A≈0 纯加光
//光照契约:uLightDir 固定在屏幕系,球面在光下自转;自转只进采样坐标,勿转 quad
//极角审计:atan2 只在前半球(N.z>=0)取值 (-0.25,0.25) 圈,±π 缝在背面永不可见;
//         经度只喂 LinearWrap 平铺噪声;条带/剪切全是纬度函数,纬度天然无缝
//采样器:显式 register(FishronStormSky 事故);s1 由消费端 Textures[1]+LinearWrap 绑定
// ============================================================================

sampler uImage0 : register(s0);   //画布(SpriteBatch 主贴图,本 shader 不采样)
sampler uNoise : register(s1);    //平铺 Perlin

float uTime;
float uAlpha;        //整体透明度,降临/退场淡入出
float uSpin;         //自转速度(圈/秒)
float uShear;        //纬度剪切强度 0~1,赤道快极区慢
float uTilt;         //自转轴倾角(弧度)
float3 uLightDir;    //光向(屏幕空间),shader 内归一化
float3 uColDeep;     //带隙墨蓝,暗部主体
float3 uColMid;      //风暴青
float3 uColBright;   //亮带冰青
float3 uColStorm;    //电光白青,闪电与风暴眼芯
float uSolidity;     //星云实体度:真身约 0.62,幻象约 0.25,识真线索=遮挡程度
float uPupil;        //月明竖瞳开度 0~1
float uCrack;        //裂解进度 0~1(TechCrack 覆层用)

//球盘画布半径,C# 侧同名常量必须同步
static const float DiscR = 0.42;

float noise(float2 uv) {
    return tex2D(uNoise, uv).r;
}

//环绕差(单位:圈),结果 |d|<=0.5,平方使用时跨缝连续
float wrapDelta(float a, float b) {
    return frac(a - b + 0.5) - 0.5;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    //轴倾只旋转采样坐标,光照仍在屏幕系
    float cs = cos(uTilt);
    float sn = sin(uTilt);
    float2 q = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs) / DiscR;
    float rq = length(q);
    if (rq > 1.6) {
        return float4(0, 0, 0, 0);   //晕圈已衰减到不可见,边界保险
    }

    //---- 球面几何 ----
    float r2 = dot(q, q);
    float3 N = float3(q.x, q.y, sqrt(saturate(1.0 - r2)));
    //盘缘硬边:可见边界即碰撞边界
    float inDisc = 1.0 - smoothstep(0.995, 1.005, rq);
    float3 L = normalize(uLightDir);
    float lit = smoothstep(-0.10, 0.42, dot(N, L));   //宽软终结线

    //---- 球面坐标:经度(圈) + 纬度(弧度) ----
    float lon = atan2(N.x, N.z) * 0.15915494;
    float latA = asin(clamp(N.y, -1.0, 1.0));

    //纬度剪切:赤道快极区慢,再叠六股逆向急流
    float flowMul = 1.0 - uShear * N.y * N.y + 0.12 * sin(latA * 6.0);
    float lonF = lon + uTime * uSpin * flowMul;

    //---- 条带(全部纬度函数 + 平铺噪声,无缝) ----
    float warp = noise(float2(lonF * 1.1, latA * 0.42 + 0.19));
    float latW = latA + (warp - 0.5) * 0.30;
    float bandSin = sin(latW * 9.0);
    //亮区/暗带硬交替,边缘带湍流
    float belt = smoothstep(-0.30, 0.30, bandSin);
    float det1 = noise(float2(lonF * 2.3, latW * 0.55 + 0.53));
    float det2 = noise(float2(lonF * 4.7 + 0.37, latW * 1.15 + 0.21));
    float detail = det1 * 0.62 + det2 * 0.38;

    float3 base = lerp(uColDeep, uColMid, belt * (0.45 + 0.55 * detail));
    base = lerp(base, uColBright, smoothstep(0.55, 0.92, detail) * belt * 0.65);
    //带界湍流增亮:剪切最强的地方云被搅亮
    base += uColBright * (1.0 - abs(bandSin)) * detail * 0.12;
    //极区压暗收口
    base = lerp(base, uColDeep, smoothstep(0.72, 0.97, abs(N.y)) * 0.55);

    //---- 风暴眼:表面特征,随自转进出视野,近缘自动透视压缩 ----
    float2 eyeLocal = float2(wrapDelta(lonF, 0.10) * 5.84, latA + 0.40);
    float eyeR = length(eyeLocal) * 3.4;
    float eyeMask = 1.0 - smoothstep(0.78, 1.0, eyeR);
    float swirlA = (1.0 - saturate(eyeR)) * 2.6 + uTime * 0.4;
    float sc = cos(swirlA);
    float ss = sin(swirlA);
    float2 swuv = float2(eyeLocal.x * sc - eyeLocal.y * ss, eyeLocal.x * ss + eyeLocal.y * sc);
    float swirl = noise(swuv * 1.7 + float2(0.61, 0.37));
    float3 eyeCol = lerp(uColDeep * 0.7, lerp(uColMid, uColStorm, 0.45), swirl);
    base = lerp(base, eyeCol, eyeMask * 0.95);
    //环沟压暗 + 眼芯增亮
    base = lerp(base, uColDeep * 0.5, smoothstep(0.55, 0.85, eyeR) * eyeMask * 0.7);
    base += uColStorm * (1.0 - smoothstep(0.0, 0.45, eyeR)) * swirl * 0.30;

    //---- 云层内部闪电:节拍化落点锁定可见半球,夜面同样可见 ----
    float beat = uTime * 1.6;
    float seed = floor(beat);
    float ph = frac(beat);
    //可见窗口随自转漂移,落点以窗口中心为基准撒
    float viewCenter = uTime * uSpin * 0.9;
    float fLon = viewCenter + (noise(float2(seed * 0.0731 + 0.113, 0.207)) - 0.5) * 0.40;
    float fLat = (noise(float2(seed * 0.0947 + 0.611, 0.741)) - 0.5) * 1.3;
    float gate = step(0.28, noise(float2(seed * 0.0577 + 0.313, 0.457)));
    float env = smoothstep(0.02, 0.07, ph) * (1.0 - smoothstep(0.10, 0.45, ph));
    float2 fLocal = float2(wrapDelta(lonF, fLon) * 5.0, latA - fLat);
    float fd2 = dot(fLocal, fLocal);
    //双尺度:小而烈的白芯 + 宽而淡的云晕
    float blob = exp(-fd2 * 60.0) + exp(-fd2 * 10.0) * 0.40;
    float flash = blob * env * gate;

    //---- 光照合成 ----
    float limb = 0.62 + 0.38 * N.z;   //边缘减光
    float3 body = base * (0.06 + 1.15 * lit) * limb;
    //昼侧缘光大气
    float3 atmoCol = lerp(uColBright, uColStorm, 0.35);
    float fres = pow(1.0 - N.z, 2.8);
    body += atmoCol * fres * (0.20 + 0.80 * lit) * 0.75;
    //闪电从云底透出,按云密度调制
    body += uColStorm * flash * (0.45 + 0.75 * detail);

    //---- 盘外大气晕:纯加光不遮挡 ----
    float halo = exp(-max(rq - 1.0, 0.0) * 8.0) * smoothstep(0.985, 1.05, rq);
    float rimLit = saturate(dot(normalize(p + float2(1e-5, 0.0)), normalize(L.xy + float2(1e-5, 0.0))));
    halo *= 0.35 + 0.65 * rimLit;
    float3 haloC = atmoCol * halo * 0.5 + uColStorm * flash * halo * 0.3;

    //---- 预乘合成 ----
    float bodyA = inDisc * 0.98;
    float3 C = body * bodyA + haloC;
    float A = bodyA + halo * 0.10;

    return float4(C, A) * uAlpha * vertexColor;
}

technique TechVortex
{
    pass VortexPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}

//===========================================================================
//TechNebula 星云·半透明星云团:三层视差云絮+神经触须缘+星点透体+热芯
//唯一半透明天体,实体度走 uSolidity;径向拉丝用单位向量做坐标,无极角无缝
//===========================================================================
float4 NebulaPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float2 q = p / DiscR;
    float rq = length(q);
    if (rq > 1.45) {
        return float4(0, 0, 0, 0);
    }

    float t = uTime;
    //星云球:差速旋转把云絮搅成旋臂,有亮核有盘缘,读作"一颗由星云构成的行星"
    float2 unit = rq > 0.001 ? q / rq : float2(0.0, 1.0);
    float swirlAng = (1.35 - rq) * 2.2 + t * 0.16;
    float cs2 = cos(swirlAng);
    float sn2 = sin(swirlAng);
    float2 v = float2(q.x * cs2 - q.y * sn2, q.x * sn2 + q.y * cs2);
    float2 unitV = rq > 0.001 ? v / rq : float2(0.0, 1.0);
    //旋臂:径向条纹被差速拧成螺旋
    float arm = noise(unitV * 1.3 + rq * 1.6);
    float armMask = smoothstep(0.38, 0.80, arm);
    float puff = noise(v * 1.6 + t * float2(0.018, 0.012) + 3.7);

    //盘体:软缘但有明确边界
    float body = 1.0 - smoothstep(0.88, 1.06, rq);
    float density = body * (0.42 + armMask * 0.55 + puff * 0.25);

    //色阶:臂间深紫,旋臂魔紫,芯白热
    float3 col = lerp(uColDeep, uColMid, saturate(armMask * 1.1 + puff * 0.25));
    col = lerp(col, uColBright, pow(armMask, 2.0) * 0.75);
    float core = exp(-rq * rq * 3.0);
    col += uColStorm * core * (0.85 + 0.30 * puff);
    //缘辉:盘缘一圈魔紫气光,球的轮廓
    float rim = exp(-abs(rq - 0.97) * 9.0);
    col += uColBright * rim * 0.55;

    float A = saturate(density) * uSolidity;
    float3 C = col * A;
    C += uColMid * rim * 0.30;
    A = saturate(A + rim * 0.18);
    //外围散逸星点
    float starN = noise(q * 4.7 + 11.3);
    float star = pow(smoothstep(0.72, 0.92, starN), 2.0);
    C += uColStorm * star * saturate(rq - 0.9) * smoothstep(1.45, 1.0, rq) * 0.7;

    return float4(C, A) * uAlpha * vertexColor;
}

technique TechNebula
{
    pass NebulaPass
    {
        PixelShader = compile ps_3_0 NebulaPS();
    }
}

//===========================================================================
//TechStardust 星尘·结晶天体+环系:经纬晶格恒亮面+晶棱亮线+游走镜闪+倾斜晶屑环
//环前半压盘上后半被盘挡;环外缘收在画布 ~92% 内
//===========================================================================
float4 StardustPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float cs = cos(uTilt);
    float sn = sin(uTilt);
    float2 q = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs) / DiscR;
    float rq = length(q);
    if (rq > 2.30) {
        return float4(0, 0, 0, 0);
    }

    float t = uTime;
    float r2 = dot(q, q);
    float3 N = float3(q.x, q.y, sqrt(saturate(1.0 - r2)));
    float inDisc = 1.0 - smoothstep(0.99, 1.01, rq);
    float3 L = normalize(uLightDir);
    float lit = smoothstep(-0.08, 0.45, dot(N, L));

    float lon = atan2(N.x, N.z) * 0.15915494 + t * uSpin;
    float latV = asin(clamp(N.y, -1.0, 1.0)) * 0.31830989 + 0.5;

    //晶格:格界抖动打破经纬直线,棱线只在相邻格亮度差大处出现
    float2 cellUv = float2(lon * 6.0, latV * 6.0);
    cellUv += (noise(cellUv * 0.53 + 2.9) - 0.5) * 0.55;
    float2 cellId = floor(cellUv);
    float2 cellFr = frac(cellUv);
    float facet = noise((cellId + 0.5) * 0.113);
    float facetR = noise((cellId + float2(1.5, 0.5)) * 0.113);
    float facetU = noise((cellId + float2(0.5, 1.5)) * 0.113);
    float2 edgeD = min(cellFr, 1.0 - cellFr);
    float eX = (1.0 - smoothstep(0.015, 0.075, edgeD.x)) * saturate(abs(facet - facetR) * 3.0);
    float eY = (1.0 - smoothstep(0.015, 0.075, edgeD.y)) * saturate(abs(facet - facetU) * 3.0);
    float edgeLine = max(eX, eY);

    float3 base = lerp(uColDeep, uColMid, 0.35 + facet * 0.6);
    base = lerp(base, uColBright, smoothstep(0.62, 0.90, facet) * 0.6);
    base += uColBright * edgeLine * 0.22 * inDisc;
    //晶面镜闪:相位游走的镜面窗
    float spec = pow(saturate(dot(N, L)), 10.0) * smoothstep(0.40, 0.90, frac(facet * 7.0 + t * 0.07));
    base += uColStorm * spec * 0.8;

    float limb = 0.55 + 0.45 * N.z;
    float3 body = base * (0.10 + 1.05 * lit) * limb;
    float fres = pow(1.0 - N.z, 3.0);
    body += uColBright * fres * (0.2 + 0.8 * lit) * 0.5;

    //环系:压扁椭圆带,环面波动+环向流转(单位向量坐标,无极角无缝),盘内只画前半(q.y>0)
    float2 e = float2(q.x, q.y / 0.24);
    float re = length(e);
    float2 unitE = re > 0.001 ? e / re : float2(0.0, 1.0);
    //环半径低频起伏:环在呼吸
    float wobble = (noise(unitE * 0.9 + t * 0.05) - 0.5) * 0.16;
    float reW = re + wobble;
    float ringBand = smoothstep(1.45, 1.70, reW) * (1.0 - smoothstep(2.00, 2.20, reW));
    //环纹沿环向流动=晶屑公转
    float ringGrain = noise(unitE * 1.6 + float2(t * 0.14, -t * 0.11) + reW * 2.2);
    ringBand *= 0.40 + 0.85 * ringGrain;
    float ringVis = rq > 1.0 ? 1.0 : step(0.0, q.y);
    float2 unitQ = rq > 0.001 ? q / rq : float2(0.0, 1.0);
    float ringLit = 0.5 + 0.5 * saturate(dot(float3(unitQ, 0.0), L));
    float ring = ringBand * ringVis * ringLit;

    float bodyA = inDisc * 0.98;
    float3 C = body * bodyA;
    C += lerp(uColMid, uColBright, ringGrain) * ring * 0.5;
    float A = bodyA + ring * 0.22;
    return float4(C, A) * uAlpha * vertexColor;
}

technique TechStardust
{
    pass StardustPass
    {
        PixelShader = compile ps_3_0 StardustPS();
    }
}

//===========================================================================
//TechSolar 日耀·恒星:米粒组织沸腾+黑子+强边缘减光+日珥火舌+冕
//自发光,无终结线;日珥用单位向量坐标径向撕裂,无极角无缝
//===========================================================================
float4 SolarPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float2 q = p / DiscR;
    float rq = length(q);
    if (rq > 1.60) {
        return float4(0, 0, 0, 0);
    }

    float t = uTime;
    float r2 = dot(q, q);
    float3 N = float3(q.x, q.y, sqrt(saturate(1.0 - r2)));
    float inDisc = 1.0 - smoothstep(0.99, 1.01, rq);

    float lon = atan2(N.x, N.z) * 0.15915494 + t * uSpin;
    float latV = asin(clamp(N.y, -1.0, 1.0)) * 0.31830989 + 0.5;

    //米粒组织:两尺度快速沸腾
    float g1 = noise(float2(lon * 3.2, latV * 3.2) + float2(t * 0.050, -t * 0.040));
    float g2 = noise(float2(lon * 6.8 + 4.2, latV * 6.8) + float2(-t * 0.070, t * 0.060));
    float gran = g1 * 0.6 + g2 * 0.4;
    //黑子:低频阈值,少而深
    float spot = smoothstep(0.34, 0.22, noise(float2(lon * 1.15 + 9.1, latV * 1.15 + 3.3)));

    float3 base = lerp(uColMid, uColBright, smoothstep(0.35, 0.85, gran));
    base = lerp(base, uColDeep, spot * 0.85);
    base += uColStorm * smoothstep(0.75, 0.98, gran) * 0.5;

    //边缘减光收敛+中心增亮:真实恒星特征,但缘上留色温不留暗壕
    float limb = 0.48 + 0.52 * N.z;
    float3 body = base * limb * (1.0 + 0.35 * exp(-rq * rq * 1.8));
    //色球缘线:贴着盘缘的一圈炽色,把盘和冕缝起来
    body += uColMid * smoothstep(0.88, 0.995, rq) * 0.40;

    //日珥:贴缘稀疏火舌,角向撕裂而非径向环纹
    float2 unit = rq > 0.001 ? q / rq : float2(0.0, 1.0);
    float prom = noise(unit * 2.2 + rq * 1.2 - t * 0.06);
    float promMask = smoothstep(0.58, 0.85, prom)
        * exp(-max(rq - 1.0, 0.0) * 6.0) * smoothstep(0.99, 1.05, rq);
    //冕:干净柔光,不带纹理
    float corona = exp(-max(rq - 1.0, 0.0) * 2.8) * smoothstep(0.985, 1.05, rq);

    float bodyA = inDisc * 0.99;
    float3 C = body * bodyA;
    C += lerp(uColMid, uColBright, prom) * promMask * 1.0;
    C += uColMid * corona * 0.28;
    float A = bodyA + corona * 0.06;
    return float4(C, A) * uAlpha * vertexColor;
}

technique TechSolar
{
    pass SolarPass
    {
        PixelShader = compile ps_3_0 SolarPS();
    }
}

//===========================================================================
//TechMoon 月明·死岩+瞳:环形山浮雕(沿光向差分)+月海暗斑+竖瞳开合 uPupil
//几乎不自转;瞳孔亮起时整体向 uColStorm 渗光
//===========================================================================
float4 MoonPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float2 q = p / DiscR;
    float rq = length(q);
    if (rq > 1.50) {
        return float4(0, 0, 0, 0);
    }

    float t = uTime;
    float r2 = dot(q, q);
    float3 N = float3(q.x, q.y, sqrt(saturate(1.0 - r2)));
    float inDisc = 1.0 - smoothstep(0.99, 1.01, rq);
    float3 L = normalize(uLightDir);
    float lit = smoothstep(-0.06, 0.50, dot(N, L));

    float lon = atan2(N.x, N.z) * 0.15915494 + t * uSpin;
    float latV = asin(clamp(N.y, -1.0, 1.0)) * 0.31830989 + 0.5;
    float2 suv = float2(lon * 2.2, latV * 2.2);

    //环形山浮雕:沿光向偏移差分出明暗坡
    float2 lightUv = normalize(L.xy + float2(1e-4, 0.0)) * 0.016;
    float h1 = noise(suv);
    float h2 = noise(suv + lightUv);
    float relief = (h1 - h2) * 5.0;
    //月海:低频暗斑
    float mare = smoothstep(0.62, 0.85, noise(suv * 0.45 + 4.7));

    float3 base = lerp(uColMid, uColDeep, mare * 0.6);
    base = lerp(base, uColBright, smoothstep(0.68, 0.95, h1) * 0.35);
    float limb = 0.70 + 0.30 * N.z;
    float3 body = base * (0.05 + 1.00 * saturate(lit + relief * 0.35)) * limb;

    //竖瞳:开度 uPupil,虹环亮起,睁眼时整体渗光
    float2 pe = q / float2(0.14, 0.10 + 0.55 * uPupil);
    float pd = dot(pe, pe);
    float slit = 1.0 - smoothstep(0.72, 1.05, pd);
    float irisRing = (1.0 - smoothstep(1.60, 2.60, pd)) * smoothstep(0.80, 1.35, pd);
    body = lerp(body, float3(0.008, 0.010, 0.020), slit * uPupil);
    body += uColStorm * irisRing * uPupil * 0.8;
    body += uColStorm * exp(-pd * 0.5) * uPupil * 0.12;

    float halo = exp(-max(rq - 1.0, 0.0) * 9.0) * smoothstep(0.985, 1.05, rq);
    float3 haloC = lerp(uColBright, uColStorm, uPupil) * halo * (0.25 + 0.45 * uPupil);

    float bodyA = inDisc * 0.99;
    float3 C = body * bodyA + haloC;
    float A = bodyA + halo * 0.08;
    return float4(C, A) * uAlpha * vertexColor;
}

technique TechMoon
{
    pass MoonPass
    {
        PixelShader = compile ps_3_0 MoonPS();
    }
}

//===========================================================================
//TechCrack 星球裂解覆层:转阶段爆炸前叠画在星球上——裂纹沿球面生长,
//缝里透出内核熔岩光;uCrack 0~1 = 裂解进度(宽度+亮度+暗化外壳)
//===========================================================================
float4 CrackPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float2 q = p / DiscR;
    float r2 = dot(q, q);
    float rq = length(q);
    if (rq > 1.02) {
        return float4(0, 0, 0, 0);
    }
    float inDisc = 1.0 - smoothstep(0.99, 1.01, rq);
    float3 N = float3(q.x, q.y, sqrt(saturate(1.0 - r2)));
    float lon = atan2(N.x, N.z) * 0.15915494;
    float latV = asin(clamp(N.y, -1.0, 1.0)) * 0.31830989 + 0.5;

    //脊线裂纹:|噪声-0.5| 低谷即缝,进度加宽
    float cn = noise(float2(lon * 2.6 + 3.3, latV * 2.6));
    float cn2 = noise(float2(lon * 5.1 + 8.8, latV * 5.1 + 2.2));
    float ridge = abs(cn - 0.5) * 0.7 + abs(cn2 - 0.5) * 0.3;
    float w = 0.020 + uCrack * 0.11;
    float crack = 1.0 - smoothstep(w * 0.4, w, ridge);
    crack *= inDisc * step(0.02, uCrack);

    //熔核透光:星球自身亮色系向白热增压(硬编码橙色会让每颗星的裂缝都透出日耀色);外壳同步压暗
    float3 lava = lerp(uColBright, uColStorm * 1.08, crack * uCrack);
    float glow = crack * (0.35 + uCrack * 1.1);
    float darken = inDisc * uCrack * 0.38;

    //预乘:缝加光,壳减光(暗化用 A 载,颜色趋黑)
    float3 C = lava * glow;
    float A = darken + crack * uCrack * 0.25;
    return float4(C, saturate(A)) * uAlpha * vertexColor;
}

technique TechCrack
{
    pass CrackPass
    {
        PixelShader = compile ps_3_0 CrackPS();
    }
}
