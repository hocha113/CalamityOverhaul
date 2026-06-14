// ============================================================================
//GlitchHead.fx 鬼乱码头部斑块
//1x1 白图拉伸；噪声+像素量化；ps_3_0
// ============================================================================

sampler uImage0 : register(s0);

float uTime;          //噪声/扫描相位
float intensity;      //0~1 淡入淡出
float pixelSize;      //UV 量化块尺寸

//廉价2D哈希，返回[0,1]
float hash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//2D值噪声
float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = hash12(i);
    float b = hash12(i + float2(1, 0));
    float c = hash12(i + float2(0, 1));
    float d = hash12(i + float2(1, 1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

//多层fbm，构造云状形状遮罩
float fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++)
    {
        v += a * valueNoise(p);
        p *= 2.03;
        a *= 0.5;
    }
    return v;
}

float4 MainPS(float2 coords : TEXCOORD0) : COLOR0
{
    //coords 0到1，将中心移到0,0
    float2 uv = coords - 0.5;

    //像素量化：按pixelSize对齐，制造方块感
    float2 qUV = floor(uv / pixelSize) * pixelSize + pixelSize * 0.5;

    //每块使用其左下角坐标的哈希作为"数据"
    float2 blockKey = floor(uv / pixelSize);

    //径向距离与基础软圆
    float r = length(qUV);

    //团状遮罩：fbm随时间缓慢演变形成不规则边缘
    float shape = fbm(blockKey * 0.25 + uTime * 0.3);
    //半径阈值：fbm扰动半径边界形成毛刺状外轮廓
    float radius = 0.36 + shape * 0.15;
    //软边缘衰减
    float mask = 1.0 - smoothstep(radius * 0.6, radius, r);

    //边缘地带额外飞溅：半径略大的离散块以概率点亮，形成溢出碎屑
    float splatterZone = smoothstep(radius, radius * 1.55, r);
    float splatterProb = hash12(blockKey + floor(uTime * 12.0));
    float splatter = step(1.0 - 0.25 * saturate(1.0 - (r - radius) / (radius * 0.55)), splatterProb);
    splatter *= (1.0 - splatterZone);

    //总掩码
    float m = max(mask, splatter * 0.9);
    if (m < 0.02)
    {
        return float4(0, 0, 0, 0);
    }

    //色彩：每块独立的高频闪烁随机色
    float flickerSeed = hash12(blockKey + floor(uTime * 24.0));
    float colorRand = hash12(blockKey * 1.7 + floor(uTime * 16.0));

    //三色家族：深猩红、洋红、荧光紫
    float3 crimson = float3(0.95, 0.08, 0.18);
    float3 magenta = float3(1.00, 0.15, 0.75);
    float3 purple  = float3(0.65, 0.20, 1.00);
    float3 col;
    if (colorRand < 0.4)       col = crimson;
    else if (colorRand < 0.75) col = purple;
    else                       col = magenta;

    //亮度抖动：部分块极亮部分块偏暗制造闪烁
    float bright = 0.55 + 0.45 * flickerSeed;
    col *= bright;

    //稀疏数据缺失黑洞：少量块直接置黑
    float blackChance = hash12(blockKey * 3.3 + floor(uTime * 9.0));
    if (blackChance > 0.93)
    {
        col = float3(0.0, 0.0, 0.0);
    }

    //水平错位色散：同一像素向左右采样不同通道
    //用时间+y行哈希制造条带整行位移感
    float rowShift = (hash12(float2(floor(coords.y * 40.0), floor(uTime * 18.0))) - 0.5) * pixelSize * 1.8;
    //简单模拟色散：按位移偏移对掩码再采样，合成外围紫红flash
    float2 uvShift = uv + float2(rowShift, 0);
    float shiftShape = fbm(floor(uvShift / pixelSize) * 0.25 + uTime * 0.3);
    float shiftR = length(floor(uvShift / pixelSize) * pixelSize + pixelSize * 0.5);
    float shiftRadius = 0.36 + shiftShape * 0.15;
    float shiftMask = 1.0 - smoothstep(shiftRadius * 0.6, shiftRadius, shiftR);
    //仅在本体mask较弱区域用偏移形状补充紫色残影
    float ghost = saturate(shiftMask - mask) * 0.6;
    col += float3(0.8, 0.1, 0.9) * ghost;

    //中心近核处更亮更偏红白高热
    float core = smoothstep(0.22, 0.0, r);
    col = lerp(col, lerp(col, float3(1.0, 0.8, 0.9), 0.35), core);

    //稀疏白色高光点：极稀疏热点模拟数据溢出
    float hotProb = hash12(blockKey * 5.1 + floor(uTime * 20.0));
    if (hotProb > 0.985)
    {
        col = float3(1.0, 0.95, 1.0);
    }

    float alpha = saturate(m) * intensity;
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass GlitchHeadPass
    {
        PixelShader = compile ps_3_0 MainPS();
    }
}
