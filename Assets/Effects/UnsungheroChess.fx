// UnsungheroChess.fx 英雄无冕 时装棋格拖尾
// 世界空间 TriangleStrip：uv.x=沿路径累计弧长(单位=格列，整数处为格线)；uv.y 0..1 横跨条带
// 顶点色 R=剩余寿命 0..1；G=世界光照亮度
// 材质是打磨过的棋盘漆面：黑白格解析 AA 交界、新格自格心翻入(出生带一线白闪)、
// 老格按逐格随机阈值向格心收缩沉没、白格斜向掠光缓行、黑格弱冷光泽、逐格微色差防死平。
// 黑格必须读作黑——预乘输出配 AlphaBlend，加色批画不出黑。
// 全笛卡尔条带坐标，无极角；直线算术无动态分支无贴图；ps_3_0 / vs_3_0

float4x4 transformMatrix;
float uTime;   //秒
float uAA;     //格坐标系下的抗锯齿半宽(由 CPU 按缩放换算，≈1.2 屏幕像素)

static const float Rows = 3.0;  //横向格行数，与 C# 侧 UnsungheroPlayer.Rows 保持一致
static const float3 ColWhite  = float3(0.925, 0.910, 0.862);  //象牙白格
static const float3 ColBlack  = float3(0.052, 0.048, 0.066);  //乌木黑格
static const float3 ColSheenW = float3(1.000, 0.990, 0.950);  //白格掠光
static const float3 ColSheenB = float3(0.340, 0.375, 0.500);  //黑格冷泽

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

//单轴奇偶方波：整数格线处 0↔1 翻转，翻转斜坡半宽 aa(格单位，三角波斜率恰为 1)
float ParityWave(float x, float aa)
{
    float tri = abs(frac((x + 0.5) * 0.5) * 2.0 - 1.0);
    return smoothstep(0.5 - aa, 0.5 + aa, tri);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float u = input.TexCoords.x;
    float v = input.TexCoords.y * Rows;
    float lifeT = input.Color.r;
    float light = input.Color.g;
    float aa = max(uAA, 0.004);

    //---- 黑白奇偶：双轴方波异或，交界处解析 AA，无缝无缝隙 ----
    float pu = ParityWave(u, aa);
    float pv = ParityWave(v, aa);
    float whiteness = pu + pv - 2.0 * pu * pv;

    //---- 逐格相位：出生与死亡都以格为单位量化 ----
    float2 cell = floor(float2(u, v));
    float h = frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);

    //出生：头几帧自格心长满，逐格轻微错相——棋盘在脚后一格格拼合
    float mat = saturate((1.0 - lifeT) * 15.0 - h * 0.35);
    //死亡：寿命逼近逐格随机阈值时收缩——尾端一格格沉没而非整体淡出
    float dieTh = 0.05 + h * 0.24;
    float alive = smoothstep(dieTh, dieTh + 0.14, lifeT);

    float grow = smoothstep(0.0, 1.0, mat);
    float cellScale = alive * (0.30 + 0.70 * grow);

    //收缩盒：满格时半宽越过 0.5+aa 使相邻格紧贴无缝；缩格时格间露缝读作"格在剥落"
    float2 lc = frac(float2(u, v)) - 0.5;
    float m = max(abs(lc.x), abs(lc.y));
    float halfW = cellScale * 0.5 + smoothstep(0.985, 1.0, cellScale) * (aa * 2.0 + 0.02);
    float box = 1.0 - smoothstep(halfW - aa, halfW + aa, m);

    //---- 条带外缘 AA 收边 + 沿两长边一线暗framing(棋盘边框的克制暗示) ----
    float band = smoothstep(0.0, aa * 1.6, v) * smoothstep(Rows, Rows - aa * 1.6, v);
    float frameIn = smoothstep(aa * 1.6, aa * 1.6 + 0.09, v)
        * smoothstep(Rows - aa * 1.6, Rows - aa * 1.6 - 0.09, v);

    //---- 漆面：白格斜向掠光沿条带缓行，黑格反相弱冷泽 ----
    float sweep = pow(saturate(sin((u + v) * 0.72 - uTime * 1.05) * 0.5 + 0.5), 22.0);
    float gloss = pow(saturate(sin((u - v) * 0.64 + uTime * 0.55) * 0.5 + 0.5), 26.0);

    float3 col = lerp(ColBlack, ColWhite, whiteness);
    //逐格微色差：同色格之间 3%~7% 明度起伏，避免大面积死平
    col *= 0.96 + h * 0.07;
    col += ColSheenW * sweep * 0.10 * whiteness;
    col += ColSheenB * gloss * 0.10 * (1.0 - whiteness);
    //边框线：白格上读作细描边，黑格上自然隐没
    col *= lerp(0.62, 1.0, frameIn);

    //出生白闪：翻入未满时过曝一线，数帧内退去
    col += (1.0 - grow) * 0.38;

    //世界光照：暗处整体沉下去但保留可读的微光
    col *= lerp(0.40, 1.0, light);

    float a = box * band * smoothstep(0.0, 0.06, lifeT) * 0.92;
    return float4(col * a, a);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader  = compile ps_3_0 PixelShaderFunction();
    }
}
