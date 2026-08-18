// ============================================================================
//CyberwarePanel.fx 赛博义体管理面板背景
//AlphaBlend 预乘 alpha
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;  //绘制矩形像素尺寸
float uEdgePad;      //面板内缩边距
float2 uBodyCenter;  //人体中心局部像素坐标，侧栏模式不用
float uBodyRadius;   //人体能量光场半径，<=1 无中央光场
float uMode;         //0主面板 1侧栏轻量层

float hash11(float p) {
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float vnoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm(float2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
        v += a * vnoise(p);
        p *= 2.05;
        a *= 0.5;
    }
    return v;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 px = coords * uResolution;
    float2 inMin = float2(uEdgePad, uEdgePad);
    float2 inMax = uResolution - float2(uEdgePad, uEdgePad);
    float2 inSize = max(inMax - inMin, float2(1.0, 1.0));

    //内框SDF
    float sdf = min(min(px.x - inMin.x, inMax.x - px.x), min(px.y - inMin.y, inMax.y - px.y));
    if (sdf < -uEdgePad) return float4(0, 0, 0, 0);

    float2 uv = saturate((px - inMin) / inSize);
    bool isMain = uMode < 0.5;

    //投影舱归一坐标：竖长椭圆贴合人体轮廓（32x56 网格 4.5 倍 ≈ 144x252 像素）
    //step 门乘替代 if(uBodyRadius>1)：大块 uniform 动态分支会被 FNA3D/MojoShader
    //静默整块丢弃（2026-08-18 沙盒定罪，OniWorldGrade 案同族）
    float bodyGate = step(1.0, uBodyRadius);
    float2 bodyDelta = px - uBodyCenter;
    float2 en = float2(bodyDelta.x / max(uBodyRadius * 0.78, 1.0),
                       bodyDelta.y / max(uBodyRadius * 1.12, 1.0));
    float er = length(en);
    //舱内掩码：内部让位人体模型，纹理噪声一并压低
    float capsuleIn = (1.0 - smoothstep(0.80, 1.0, er)) * bodyGate;

    //═══ 1. 深红黑底色，竖直渐变 ═══
    float3 col = lerp(float3(0.045, 0.012, 0.012), float3(0.022, 0.008, 0.012), uv.y);

    //fbm 雾化纹理（侧栏减弱，舱内压低保持干净）
    float n = fbm(uv * 3.2 + uTime * 0.06);
    float calm = 1.0 - capsuleIn * 0.55;
    col *= 0.85 + n * (isMain ? 0.30 : 0.18) * calm;
    col += float3(0.030, 0.008, 0.008) * (n - 0.5) * (isMain ? 1.0 : 0.5) * calm;

    //═══ 2. 数据网格背景（静态细密点阵 + 中等方格细线，无任何闪烁/扫描） ═══
    if (isMain)
    {
        //细密点阵 ： 均匀分布的静态参考点，提供"芯片刻印"的科技纹理
        float2 dotUV = uv * float2(60.0, 36.0);
        float2 dotF = frac(dotUV);
        float dotMask = step(0.85, dotF.x) * step(0.85, dotF.y);
        col += float3(0.42, 0.10, 0.10) * dotMask * 0.20 * calm;

        //方格细线 ： 同样静态，提供网格结构感
        float2 grid = uv * float2(12.0, 7.0);
        float2 g = abs(frac(grid) - 0.5);
        float gridLine = step(0.46, max(g.x, g.y)) - step(0.49, max(g.x, g.y));
        col += float3(0.50, 0.12, 0.12) * gridLine * 0.22 * calm;
    }
    else
    {
        //侧栏：仅极淡的横向数据线，避免与列表项干扰
        float subtleGrid = abs(frac(uv.y * 14.0) - 0.5);
        float ln = step(0.46, subtleGrid) - step(0.49, subtleGrid);
        col += float3(0.30, 0.06, 0.06) * ln * 0.20;
    }

    //═══ 3. 中央全息投影舱 ═══
    //中心不再堆亮斑（旧版 halo+环+指针+射线全删）——亮度让给上层人体模型；
    //舱只提供容器：内部微沉降、椭圆舱壁收口、足下基座供光、上升光尘

    //内部微沉降：舱内比外部略暗略净，红色人体线条自然浮起
    col *= 1.0 - capsuleIn * 0.16;

    //舱壁：锐芯+紧晕双层，底部受基座光照更亮、向顶衰减；内伴线恒淡
    float breathe = 0.82 + 0.18 * sin(uTime * 1.1);
    float baseLit = 0.72 + 0.28 * saturate(en.y * 0.8 + 0.5);
    float wallCore = exp(-pow(abs(er - 1.00) * 70.0, 1.8));
    float wallHalo = exp(-pow(abs(er - 1.00) * 16.0, 1.6));
    float wall2 = exp(-pow(abs(er - 0.93) * 60.0, 1.8));
    col += float3(1.00, 0.30, 0.22) * wallCore * 0.55 * breathe * baseLit * bodyGate;
    col += float3(0.70, 0.16, 0.12) * wallHalo * 0.13 * breathe * baseLit * bodyGate;
    col += float3(0.50, 0.12, 0.10) * wall2 * 0.15 * bodyGate;

    //舱壁静态刻度豁口 x12：机械收口细节，不旋转（12 整除极角一周，接缝连续）
    float ang = atan2(en.y, en.x);
    float tt = frac(ang / 6.2832 * 12.0);
    float tick = step(0.962, max(tt, 1.0 - tt));
    float tickBand = smoothstep(1.005, 1.03, er) * (1.0 - smoothstep(1.07, 1.11, er));
    col += float3(0.90, 0.24, 0.18) * tick * tickBand * 0.45 * bodyGate;

    //足下基座：亮缝芯线（发射条）+ 暖光溢散，光源具象化（红为主、芯偏金）
    //smoothstep 边界加下限：uBodyRadius=0 时两边重合会产生 0/0=NaN，乘 0 门也救不回
    float baseY = uBodyRadius * 1.12;
    float slitX = 1.0 - smoothstep(max(uBodyRadius * 0.40, 1.0), max(uBodyRadius * 0.52, 2.0), abs(bodyDelta.x));
    float slit = exp(-pow(abs(bodyDelta.y - baseY) / 2.4, 2.0)) * slitX;
    col += float3(1.00, 0.45, 0.20) * slit * 0.50 * bodyGate;
    float2 baseD = float2(bodyDelta.x / max(uBodyRadius * 0.62, 1.0),
                          (bodyDelta.y - baseY) / max(uBodyRadius * 0.16, 1.0));
    float baseGlow = exp(-dot(baseD, baseD));
    col += float3(0.90, 0.30, 0.14) * baseGlow * 0.30 * bodyGate;
    col += float3(0.45, 0.30, 0.08) * baseGlow * baseGlow * 0.18 * bodyGate;

    //舱内上升光丝：竖长细丝缓慢上浮，底部最浓及腰而没（团状噪点读作污渍，故拉成丝）
    float mote = vnoise(float2(px.x * 0.30, px.y * 0.030 - uTime * 0.8));
    mote = pow(mote, 5.0);
    float moteW = saturate(en.y * 1.2 + 0.3) * capsuleIn;
    col += float3(0.95, 0.38, 0.20) * mote * moteW * 0.14;

    //═══ 4. 顶部色带高光 ═══
    col += float3(0.60, 0.15, 0.15) * (1.0 - smoothstep(0.0, 0.06, uv.y)) * (isMain ? 0.65 : 0.40);

    //═══ 5. 边缘暗角 ═══
    float vig = saturate(sdf / (uEdgePad + 28.0));
    col *= 0.62 + 0.38 * vig;

    //═══ 6. 内边线柔光 ═══
    float frameInner = smoothstep(uEdgePad + 6.0, uEdgePad + 4.0, sdf)
                     * smoothstep(uEdgePad + 2.0, uEdgePad + 4.0, sdf);
    col += float3(1.00, 0.28, 0.28) * frameInner * 0.55;

    //外边框柔光（向外扩散一小段）
    float frameGlow = smoothstep(uEdgePad + 12.0, 0.0, sdf);
    col += float3(0.60, 0.16, 0.16) * frameGlow * 0.30;

    float fa = uAlpha;
    return float4(col * fa, fa) * vertexColor;
}

technique Technique1
{
    pass CyberwarePanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
