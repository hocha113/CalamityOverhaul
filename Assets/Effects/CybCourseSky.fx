// ============================================================================
//CybCourseSky.fx 编译中的超梦——训练空间天穹
//材质：被扫描线维持的全息构造体悬在未渲染的记忆虚空里
//三个元素：虚空底色雾 / 地平线巨型六角构造核心(琥珀心跳) / 六角编译带+上升数据尘
//纯 ALU 直线算术：无采样器、无动态分支、无 atan2(噪声全走笛卡尔)
// ============================================================================

float uTime;          //秒
float uIntensity;     //0..1 淡入
float uAspectRatio;
float uCamX;          //相机X / 视口高（归一化，供层间横向视差）
float uCamY;          //(相机中心Y - 甲板锚点Y) / 视口高（纵向视差：构造核心要钉在世界里，不许跟镜头飞）

#define TAU 6.28318530

//SHPC 系列色板：青为体，琥珀只作核心心跳点缀
#define VOID_TOP  float3(0.004, 0.008, 0.020)
#define VOID_LOW  float3(0.014, 0.036, 0.062)
#define CYAN      float3(0.337, 0.863, 0.941)
#define CYAN_HI   float3(0.667, 0.961, 1.000)
#define AMBER     float3(1.000, 0.667, 0.235)

//Hash / Noise（笛卡尔输入）

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.11369, 0.13787));
    p3 += dot(p3, p3.yzx + 19.19);
    return frac(float2((p3.x + p3.y) * p3.z, (p3.x + p3.z) * p3.y));
}

float vnoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm2(float2 p)
{
    return vnoise(p) * 0.625 + vnoise(p * 2.1 + float2(3.7, 8.1)) * 0.375;
}

//Hex 工具（与入场揭示 CybCourseEntryReveal 同一套语言）

//两套方格 Voronoi 等价六角网格
void hexCellInfo(float2 p, float scale, out float2 local, out float2 cellId)
{
    p *= scale;
    const float2 s = float2(1.0, 1.7320508);
    float2 iA = floor(p / s + 0.5);
    float2 iB = floor(p / s);
    float2 cA = iA * s;
    float2 cB = iB * s + s * 0.5;
    float2 dA = p - cA;
    float2 dB = p - cB;
    float pick = step(dot(dB, dB), dot(dA, dA)); //1=取B
    local  = lerp(dA, dB, pick);
    cellId = lerp(iA, iB + float2(0.37, 0.41), pick);
}

//单元内部到最近边的垂直距离(中心0.866→边0)
float hexEdgeDist(float2 p)
{
    p = abs(p);
    return 0.86602540 - max(p.x * 0.86602540 + p.y * 0.5, p.y);
}

//正六边形距离度量(边界=R)
float hexDist(float2 p)
{
    p = abs(p);
    return max(p.x * 0.86602540 + p.y * 0.5, p.y);
}

float2 rot2(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

//数据尘单层：cell 点阵沿 y 缓慢上升
float dustLayer(float2 q, float scale, float thresh)
{
    float2 id = floor(q * scale);
    float2 f = frac(q * scale) - 0.5;
    float h = hash21(id);
    float2 off = (hash22(id + 7.31) - 0.5) * 0.52;
    float d = length(f - off);
    float tw = 0.70 + 0.30 * sin(uTime * (0.7 + h * 1.6) + h * TAU);
    return step(thresh, h) * smoothstep(0.085, 0.0, d) * tw;
}

//Main

float4 PSCybCourseSky(float2 uv : TEXCOORD0) : COLOR0
{
    float2 uvW = float2(uv.x * uAspectRatio, uv.y);
    float t = uTime;

    //各层纵向视差档位：越远跟随越少（相机升高时远景在屏幕上下沉）
    float yFar  = uv.y + uCamY * 0.15;   //核心/雾/地平线一组
    float yMid  = uv.y + uCamY * 0.25;   //六角编译带
    float yNear = uv.y + uCamY * 0.45;   //近层数据尘
    float yFarD = uv.y + uCamY * 0.20;   //远层数据尘

    //=
    //元素 1 —— 未渲染的虚空：上黑下微亮的纵向渐变 + 地平线雾
    //=
    float3 col = lerp(VOID_TOP, VOID_LOW, pow(saturate(uv.y), 1.35));

    //低频雾，向地平线聚拢；随时间极缓漂移
    float2 fogUV = float2(uvW.x * 0.55 + uCamX * 0.019, yFar * 1.05) + float2(t * 0.010, 0.0);
    float fog = fbm2(fogUV * 1.4);
    float fogBand = smoothstep(0.30, 0.86, yFar);
    col += float3(0.020, 0.070, 0.110) * fog * fogBand * 0.55;

    //=
    //元素 2 —— 巨型六角构造核心（唯一大形体，慢呼吸+琥珀心跳）
    //=
    //核心锚在世界坐标里，行走/升降时按远景档位滑移
    float coreX = 0.5 * uAspectRatio - uCamX * 0.024;
    float coreY = 0.735 - uCamY * 0.15;
    float2 pc = float2(uvW.x - coreX, uv.y - coreY);

    //心跳：~6.5s 一拍，攻击瞬间起、指数衰减
    float beat = frac(t * 0.1538);
    float pulse = exp(-beat * 6.0);

    //六角外环 + 内环，缓慢自转
    float2 pr = rot2(pc, t * 0.030);
    float hd = hexDist(pr);
    float breathe = 0.74 + 0.26 * sin(t * 0.42);
    float ringO = smoothstep(0.0085, 0.0, abs(hd - 0.340));
    float ringI = smoothstep(0.0060, 0.0, abs(hd - 0.292));
    col += CYAN * ringO * 0.42 * breathe;
    col += CYAN * ringI * 0.20 * breathe;
    //环身向内的弱结构辉光（构造体在虚空里的体积暗示）
    float shell = smoothstep(0.345, 0.20, hd) * smoothstep(0.10, 0.24, hd);
    col += CYAN * shell * 0.045;

    //琥珀核：大半沉在地平线雾下，只以辉光存在
    float coreGlow = exp(-length(pc * float2(1.0, 1.55)) * 4.2);
    col += AMBER * coreGlow * (0.16 + pulse * 0.55);
    //心跳峰值的短暂暖白（非常驻）
    col += float3(1.0, 0.88, 0.70) * exp(-length(pc) * 9.0) * pulse * 0.22;

    //心跳沿地平线向两侧扫过的琥珀波（雾被点亮）
    float horizLine = exp(-pow((yFar - 0.740) * 16.0, 2.0));
    float dxc = abs(uvW.x - coreX);
    float sweep = exp(-pow((dxc - beat * 1.75) * 5.5, 2.0)) * exp(-beat * 3.2);
    col += AMBER * horizLine * sweep * (0.30 + fog * 0.25);

    //=
    //元素 3 —— 六角编译带 + 上升数据尘
    //=
    {
        //地平线附近一条蜂窝带：多数单元是虚空，少数常驻亮边，极少数正在编译就位
        float2 hp = float2(uvW.x + uCamX * 0.065, yMid);
        float2 cLocal, cId;
        hexCellInfo(hp, 15.0, cLocal, cId);
        float rnd = hash21(cId);
        float rnd2 = hash21(cId + float2(7.13, 1.71));

        float bandM = smoothstep(0.52, 0.64, yMid) * smoothstep(0.88, 0.76, yMid);
        float gridLine = smoothstep(0.085, 0.0, hexEdgeDist(cLocal));
        float cellCore = smoothstep(0.16, 0.0, length(cLocal));

        //常驻已编译单元：极淡青边
        float resident = step(0.60, rnd) * step(rnd, 0.82);
        float residentGlow = 0.22 + 0.10 * sin(t * 0.35 + rnd * TAU);
        col += CYAN * gridLine * resident * residentGlow * bandM;

        //编译中单元：快速点亮→驻留衰减，永远有一小片"没编译完"
        float compiling = step(0.86, rnd);
        float ph = frac(t * 0.055 + rnd2 * 5.0);
        float env = smoothstep(0.0, 0.045, ph) * exp(-max(ph - 0.045, 0.0) * 5.5);
        col += CYAN_HI * gridLine * compiling * env * bandM * 1.10;
        col += CYAN_HI * cellCore * compiling * env * bandM * 0.75;
    }

    //数据尘两层：近层快、远层慢，缓缓上浮
    float dustLow = smoothstep(0.08, 0.45, uv.y) * 0.75 + 0.25;
    float2 qNear = float2(uvW.x + uCamX * 0.130, yNear + t * 0.0110);
    float2 qFar  = float2(uvW.x + uCamX * 0.043 + 13.7, yFarD + t * 0.0050);
    col += float3(0.55, 0.85, 0.95) * dustLayer(qNear, 13.0, 0.935) * 0.34 * dustLow;
    col += float3(0.42, 0.66, 0.78) * dustLayer(qFar, 27.0, 0.945) * 0.22 * dustLow;

    //=
    //输出
    //=
    col *= uIntensity;
    return float4(saturate(col), 1.0);
}

technique CybCourseSky
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSCybCourseSky();
    }
}
