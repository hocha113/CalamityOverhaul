// ============================================================================
// VictorCyberPortal.fx Victor 出场专用赛博乱流传送门
// 椭圆裂口 + 数据格 + 故障切片 + 撕裂能量边 + 中心 SNAP
// s0 quad 画布 s1 噪声纹理 Additive 预乘 alpha
// ps_3_0
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float openProgress;     //0=刚撕开 1=完全张开 → 收口时回到 0
float emergePulse;      //NPC 浮出闪光 0~1
float collapse;         //关闭/坍缩 0=正常 1=完全坍缩
float seed;             //本实例随机种子
float2 portalSize;      //传送门半轴像素 (宽,高)，用于像素均匀采样
float facing;           //+1 朝右 -1 朝左 控制内部数据流方向

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

//---- 工具 ----

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

//三层噪声采样，模拟乱流
float layeredNoise(float2 uv, float t)
{
    float n1 = tex2D(noiseSamp, frac(uv * 0.9 + float2(t * 0.13, t * 0.09))).r;
    float n2 = tex2D(noiseSamp, frac(uv * 2.1 + float2(-t * 0.17, t * 0.21))).g;
    float n3 = tex2D(noiseSamp, frac(uv * 4.7 + float2(t * 0.31, -t * 0.11))).b;
    return n1 * 0.5 + n2 * 0.35 + n3 * 0.15;
}

//—————————————————————————————————————————————

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    //居中到 [-1,1]，y 朝上为正
    float2 uv = input.TexCoords;
    float2 p = (uv - 0.5) * 2.0;
    p.y = -p.y;

    //collapse 期把整体往中心拉
    float collapseSq = collapse * collapse;
    p /= max(1.0 - collapseSq * 0.85, 0.05);

    //当前开口因子：撕开瞬间小、张开时接近 1
    float openSq = saturate(openProgress);
    //椭圆边形状（竖立门，h > w 看起来更像撕开的口）
    //收口时纵向先收，再水平拍扁
    float2 ellipNorm = p / float2(
        max(openSq * 0.85, 0.05),
        max(openSq, 0.05));
    float ellipR = length(ellipNorm);

    //——————————————————————————————
    //门外彻底丢弃（早期被裂痕外缘咬出毛刺，所以阈值大于 1）
    //——————————————————————————————
    if (ellipR > 1.42) return float4(0, 0, 0, 0);

    //内部局部时间，统一节奏
    float tt = uTime * 1.05;

    //————————————————————————————————————————————
    //A 撕裂能量边 用噪声扰动半径定义"门口锯齿"
    //————————————————————————————————————————————
    float ang = atan2(ellipNorm.y, ellipNorm.x);
    float aN = (ang + 3.14159) / 6.28318;//[0,1]

    //三层不同频率的边缘噪声合成不规则边沿
    float rN1 = tex2D(noiseSamp, frac(float2(aN * 3.0 + seed * 0.31, tt * 0.18))).r;
    float rN2 = tex2D(noiseSamp, frac(float2(aN * 7.0 + seed * 0.77, -tt * 0.22))).g;
    float rN3 = tex2D(noiseSamp, frac(float2(aN * 17.0 - seed * 0.41, tt * 0.34))).b;
    float rimNoise = rN1 * 0.55 + rN2 * 0.30 + rN3 * 0.15;
    rimNoise = rimNoise - 0.5;

    //撕开期边缘最不稳，全开后趋稳
    float instability = (1.0 - openSq) * 0.6 + 0.15 + collapse * 0.5;
    //rimSdf < 0 = 门内, > 0 = 门外
    float rimSdf = ellipR - (1.0 + rimNoise * 0.18 * instability);

    //门口"咬出"的尖刺裂纹
    float spike = pow(max(rN1 * rN2 * 2.0, 0.0), 4.0);
    rimSdf -= spike * 0.07 * instability;

    if (rimSdf > 0.0)
    {
        //门外只画边缘辉光
        float outerGlow = exp(-rimSdf * 4.5);
        float flick = 0.6 + 0.4 * sin(tt * 18.0 + aN * 47.0 + seed * 13.0);
        float3 rim = float3(1.0, 0.42, 0.18) * outerGlow * flick;
        rim += float3(0.92, 0.08, 0.045) * pow(outerGlow, 1.7) * 0.6;
        float a = saturate(outerGlow * 0.85);
        return float4(rim * a, a) * input.Color;
    }

    //—— 以下都在门内 ——
    //inside 强度：靠门口=0 中心=1
    float inside = saturate(-rimSdf * 1.6);
    float depth = saturate(1.0 - ellipR);//0=门口 1=门中心
    float depthSq = depth * depth;

    //背景渐变：深黑→暗红
    float3 bg = float3(0.012, 0.002, 0.005) * (0.4 + depth * 1.4);
    bg += float3(0.18, 0.012, 0.008) * pow(depth, 2.5) * 0.8;

    //————————————————————————————————————————————
    //B 内部赛博乱流 旋涡 + 视差噪声层
    //————————————————————————————————————————————
    float2 polarUV = float2(
        ang / 6.28318 + tt * 0.06 * facing,
        depth * 0.8 - tt * 0.18);
    float swirlN = layeredNoise(polarUV * float2(2.0, 1.5), tt);
    float swirl = pow(swirlN, 1.8);

    //深处暗红色"虚空"
    float3 voidCol = float3(0.55, 0.06, 0.035) * swirl * depthSq;
    voidCol += float3(0.92, 0.18, 0.10) * pow(swirl, 3.0) * depthSq * 0.5;
    bg += voidCol;

    //————————————————————————————————————————————
    //C 乱码数据格 类似 Matrix 但更密更乱
    //————————————————————————————————————————————
    //世界 cell 用像素尺寸归一化，让格子在不同 portal 大小下视觉一致
    float2 pxPos = p * portalSize;
    //横长方形格 (字符高瘦感)
    const float2 cellSize = float2(7.0, 11.0);
    float2 cellId = floor(pxPos / cellSize);
    float2 cellFrac = frac(pxPos / cellSize);

    //每格独立 hash
    float cHash = hash21(cellId * 0.137 + seed * 1.7);

    //时间分片：每个格按自己的时钟跳变
    float cellClock = floor(tt * 4.0 + cHash * 7.0);
    float cellState = hash21(cellId + cellClock * 0.51);

    //仅一小部分格活跃 (alive)
    float alive = step(0.55, cellState);

    //"代码字符"用格内 4 段水平条 + 边缘
    //模拟低分辨率字符像素
    float chy = floor(cellFrac.y * 5.0);
    float chx = floor(cellFrac.x * 3.0);
    float pixHash = hash21(float2(chx, chy) + cellId + cellClock * 0.13);
    float pix = step(0.40, pixHash);

    //格活跃 才显示该 cell 的字符像素
    float ch = alive * pix;

    //字符密度跟深度走，深处更密
    float densityMask = smoothstep(0.05, 0.85, depthSq);
    ch *= densityMask;

    //每格独立 1Hz 心跳
    float cellPulse = 0.55 + 0.45 * sin(tt * 6.0 + cHash * 25.0);
    float charAmt = ch * (0.6 + cellPulse * 0.55);

    //字符颜色 = 鲜红主调 + 少数高亮(数据头部) 偶尔青色变异
    float isHot = step(0.93, cellState);
    float isCyan = step(0.97, cellState);
    float3 charBase = float3(0.92, 0.12, 0.07);
    float3 charHot = float3(1.0, 0.55, 0.30);
    float3 charCyan = float3(0.20, 0.85, 0.95);
    float3 charCol = lerp(charBase, charHot, isHot);
    charCol = lerp(charCol, charCyan, isCyan);

    //————————————————————————————————————————————
    //D 数据列下落 像 Matrix 的纵向流，但用 RGB 切片错位制造故障
    //————————————————————————————————————————————
    //每列独立流速
    float colId = cellId.x;
    float colSeed = hash11(colId * 0.71 + seed * 3.7);
    float fallSpeed = 0.5 + colSeed * 1.5;
    float colY = pxPos.y / cellSize.y + tt * fallSpeed * facing;
    float colCellY = floor(colY);
    //每个 cell 内位置
    float colInCell = frac(colY);
    //头部高亮指针 (字符串头)
    float headHash = hash11(colCellY + colSeed * 17.0);
    float head = step(0.78, headHash);
    //头部光斑：cell顶部 20%
    float headLit = head * (1.0 - smoothstep(0.0, 0.20, colInCell));
    headLit *= densityMask;

    //————————————————————————————————————————————
    //E 横向切片故障 几条扫描线随时间错位，造成 RGB 色差
    //————————————————————————————————————————————
    float sliceH = 0.08 + hash11(floor(tt * 2.3)) * 0.10;
    float sliceI = floor((uv.y) / sliceH);
    float sliceR = hash11(sliceI + floor(tt * 5.0) * 137.0);
    //偶尔横向偏移
    float sliceShift = 0.0;
    if (sliceR > 0.78)
        sliceShift = (hash11(sliceI * 7.13 + tt * 1.7) - 0.5) * 0.16 * (0.5 + collapse);

    //带颜色差的红/青色横纹（数据腐烂）
    float bandH = 0.012 + hash11(floor(tt * 6.0)) * 0.02;
    float yWrap = frac(uv.y * (1.0 / bandH) - tt * 0.5 + sliceI * 0.137);
    float band = smoothstep(0.0, 0.10, yWrap) * smoothstep(0.30, 0.10, yWrap);
    float bandOn = step(0.78, hash11(sliceI + floor(tt * 7.0)));
    band *= bandOn;

    //————————————————————————————————————————————
    //F 中央 SNAP 闪光 NPC 浮现时使用
    //————————————————————————————————————————————
    //垂直撕裂光带 + 中心圆盘
    float vertical = exp(-pow(ellipNorm.x * 5.0, 2.0));
    float horizontal = exp(-pow(ellipNorm.y * 3.5, 2.0));
    float snapCore = exp(-ellipR * 3.5) * (0.4 + 0.6 * vertical);
    snapCore += vertical * horizontal * 1.4;
    float snap = snapCore * emergePulse;

    //短促撕裂光线（多条斜向）
    float rays = 0.0;
    for (int i = 0; i < 4; i++)
    {
        float rAng = float(i) * 0.785 + seed * 1.7 + tt * 0.15;
        float2 rDir = float2(cos(rAng), sin(rAng));
        float along = dot(p, rDir);
        float side = abs(dot(p, float2(-rDir.y, rDir.x)));
        rays += exp(-side * 12.0) * smoothstep(1.0, 0.0, abs(along) * 1.2);
    }
    rays *= emergePulse * 0.7;

    //————————————————————————————————————————————
    //G 角度扫描线 一条转动的雷达扫描臂
    //————————————————————————————————————————————
    float scanAng = ang + tt * 0.85 * facing;
    float scanLine = exp(-pow(sin(scanAng * 0.5) * 8.0, 2.0));
    scanLine *= smoothstep(0.0, 0.6, depth);

    //————————————————————————————————————————————
    //H 边缘内侧 - 紧贴撕裂边的高亮能量条
    //————————————————————————————————————————————
    float innerRim = exp(-pow(-rimSdf * 3.0, 1.4));
    //撕开期 + 坍塌期更亮
    float rimEnergy = innerRim * (0.7 + 0.5 * (1.0 - openSq) + 0.5 * collapse);

    //————————————————————————————————————————————
    //合成 颜色 + alpha (Additive 预乘)
    //————————————————————————————————————————————
    float3 col = bg;
    //字符 / 数据列
    col += charCol * charAmt * 1.4;
    col += float3(1.0, 0.55, 0.30) * headLit * 0.9;
    //横向故障带 (颜色错位)
    col += float3(0.95, 0.10, 0.08) * band * 0.9;
    col += float3(0.20, 0.85, 0.95) * band * 0.30;
    //扫描臂
    col += float3(0.92, 0.40, 0.20) * scanLine * 0.45 * (0.5 + sin(tt * 5.0) * 0.5);
    //贴边能量
    col += float3(1.0, 0.55, 0.30) * rimEnergy * 1.4;
    col += float3(0.95, 0.10, 0.08) * pow(rimEnergy, 1.8) * 0.6;
    //SNAP & rays
    col += float3(1.0, 0.85, 0.65) * snap * 1.5;
    col += float3(1.0, 0.40, 0.22) * rays * 1.2;

    //sliceShift 横向像素错位 (RGB 色差)
    //通过对位置做位移再二次乘上颜色饱和加权，简化为对 col 做 R/B 分离
    float caStr = abs(sliceShift) * 3.5 + 0.04 * collapse;
    float lum = dot(col, float3(0.299, 0.587, 0.114));
    col.r += caStr * 0.8 * (col.r - lum * 0.7);
    col.b -= caStr * 0.5 * (col.b - lum * 0.4);

    //alpha 组合
    float a = saturate(
          inside * 0.4
        + charAmt * 0.45
        + headLit * 0.55
        + band * 0.45
        + scanLine * 0.18
        + rimEnergy * 0.85
        + snap * 0.9
        + rays * 0.75);

    //坍塌期外圈先熄火
    float collapseMask = 1.0 - smoothstep(0.5 - collapse * 0.5, 1.0 - collapse * 0.5, ellipR);
    a *= lerp(1.0, collapseMask, collapse);
    col *= lerp(1.0, collapseMask, collapse);

    return float4(col * a, a) * input.Color;
}

technique Technique1
{
    pass VictorCyberPortalPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
