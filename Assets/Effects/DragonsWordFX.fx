// ============================================================================
//DragonsWordFX.fx 龙言武器四技法,金橙龙火色系
//TechTear   左键龙泪本体 quad,+x=速度方向;熔金泪滴,凝聚成形,熔壳结皮,张力亮缘
//TechTrail  龙泪熔火缎带 TriangleStrip,UV.x 0尾→1头;焦烟缘+熔火中体+白热芯线
//TechDecree 右键龙令敕环;火舌撕裂缘+28格龙语符环(逐拍重写)+宣谕行波+拍后过曝
//TechBrand  敕域内敌人的龙瞳烙印 TriangleList 批绘;灼刻蚀入→竖瞳收缩→拍点破印
//预乘输出,消费端 BlendState.AlphaBlend;直线算术+plain tex2D,无动态分支
//极角纪律: 符环只走 floor/frac 整数分格,爪痕用 cos3θ 多项式(无 atan2 进噪声),
//径向拉丝取单位方向向量,内域对流走刚体旋转笛卡尔坐标
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;      //每实例相位种子
float uForm;      //生长包络 0~1(泪滴凝聚/敕环展开)
float uHeat;      //点燃度 0~1
float uFade;      //整体透明度 0~1
float uLenPx;     //缎带保留段弧长(px)
float uOffPx;     //缎带尾侧已蚀弧长(px),相位锚定防纹理滑动
float uRadius;    //敕环半径(半画布归一)
float uThickness; //敕环带厚(同单位)
float uBeat;      //拍相位 0~1,0=刚落拍
float uBeatSeed;  //拍序号,符文逐拍重写

//s1=PerlinNoise 512 灰度,消费端 LinearWrap
sampler uNoise : register(s1);

//PerlinNoise 实测值域约 0.22~0.78,阈值消费前归一
float nrm(float n)
{
    return saturate((n - 0.22) * 1.786);
}

//离散格 hash,仅喂整数输入
float hash21(float2 q)
{
    return frac(sin(dot(q, float2(127.1, 311.7))) * 43758.545);
}

//火焰冷却斜坡 焦暗→深红→橙→金
float3 FireRamp(float h)
{
    float3 c = lerp(float3(0.10, 0.035, 0.025), float3(0.48, 0.09, 0.035), saturate(h * 3.0));
    c = lerp(c, float3(0.98, 0.40, 0.09), saturate(h * 2.0 - 0.6));
    c = lerp(c, float3(1.05, 0.80, 0.34), saturate(h * 2.2 - 1.25));
    return c;
}

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VS(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

// ---------------------------------------------------------------- TechTear
//读 uTime uSeed uForm uHeat uFade
float4 PSTear(PSInput input) : COLOR0
{
    float2 p = input.TexCoords * 2.0 - 1.0; //x -1尾 +1头
    float xr = input.TexCoords.x;           //0尾 1头

    //泪滴轮廓: 半宽沿身抬升,头端圆帽收拢
    float rise = pow(smoothstep(0.02, 0.74, xr), 0.62);
    float cap = sqrt(saturate(1.0 - pow(saturate((xr - 0.74) / 0.26), 2.0)));
    float w = 0.56 * rise * cap;
    float sd = abs(p.y) - w;

    //内部熔流对流,刚体旋转坐标
    float cs = cos(uTime * 0.5 + uSeed);
    float sn = sin(uTime * 0.5 + uSeed);
    float2 rc = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
    float churn = nrm(tex2D(uNoise, rc * 0.6 + uSeed * 3.1).g);

    //尾端撕裂,向尾平流的噪声吃掉颈部
    float tearN = nrm(tex2D(uNoise, float2(xr * 2.4 - uTime * 2.0 + uSeed * 7.0, p.y * 2.2 + uSeed)).r);
    float tail = smoothstep(-0.08, 0.42, xr + (tearN - 0.5) * 0.55);

    //凝聚: 出生被噪声蚀散,uForm 升满后成滴
    float formGate = smoothstep(churn - 0.35, churn + 0.02, uForm);

    float body = (1.0 - smoothstep(-0.03, 0.05, sd)) * tail * formGate;

    //熔壳: 贴缘低频暗斑,液面结皮
    float crustN = nrm(tex2D(uNoise, rc * 1.4 + uSeed * 5.7).b);
    float crust = smoothstep(-0.16, -0.02, sd) * smoothstep(0.38, 0.72, crustN) * (0.75 - uHeat * 0.3);

    //张力亮缘: 液面弯月高光,上缘偏置
    float rim = (1.0 - smoothstep(0.0, 0.09, abs(sd + 0.05))) * saturate(0.55 - p.y * 0.45);

    //白热核: 头核内域,点燃后抬满
    float core = (1.0 - smoothstep(0.10, 0.34, length(float2((xr - 0.66) * 1.4, p.y * 1.6))))
               * (0.30 + 0.70 * uHeat);

    float heatV = saturate(uHeat * (0.55 + 0.45 * xr) + churn * 0.18);
    float3 col = FireRamp(heatV) * (0.62 + 0.5 * churn);
    col = lerp(col, float3(0.14, 0.05, 0.03), saturate(crust));
    col += float3(1.05, 0.80, 0.34) * rim * 0.8;
    col += float3(1.15, 1.02, 0.72) * core * body;

    float a = saturate(body * (0.92 + rim * 0.35));

    //画布护栏
    float guard = smoothstep(1.0, 0.92, max(abs(p.x), abs(p.y)));
    a *= uFade * guard * input.Color.a;
    return float4(col * a, a);
}

// ---------------------------------------------------------------- TechTrail
//读 uTime uHeat uFade uLenPx uOffPx
float4 PSTrail(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float age = uv.x;                //1头新 0尾老
    float cy = uv.y * 2.0 - 1.0;     //-1..1 横截

    //弧长像素尺度采样,相位锚定尾蚀推进不滑动
    float sx = (uOffPx + uv.x * max(uLenPx, 40.0)) / 600.0;

    float n1 = nrm(tex2D(uNoise, float2(sx * 1.7 - uTime * 1.2, uv.y * 0.9 + uTime * 0.10)).r);
    float n2 = nrm(tex2D(uNoise, float2(sx * 3.9 - uTime * 2.1, uv.y * 2.3 - uTime * 0.45)).g);
    float flow = n1 * 0.62 + n2 * 0.38;

    //横截外缘被火舌噪声啃出参差,啃深钳非负防裁出平滑硬边
    float bite = max((flow - 0.44) * 0.75, 0.0);
    float edge = 1.0 - smoothstep(0.50 - bite, 0.95 - bite, abs(cy));

    //尾端老化+撕碎成缕
    float shred = smoothstep(0.28, 0.62, flow + age * 1.6 - 0.55);
    float ageMask = smoothstep(0.0, 0.30, age);
    ageMask *= lerp(shred, 1.0, smoothstep(0.30, 0.62, age));

    float body = edge * ageMask;

    //三层: 焦烟缘(暗) 熔火中体 白热芯线
    float heatV = saturate(age * (0.5 + 0.5 * uHeat));
    float3 mid = FireRamp(heatV) * (0.55 + 0.5 * flow);
    float charZone = smoothstep(0.35, 0.85, abs(cy));
    float coreLine = (1.0 - smoothstep(0.02, 0.15, abs(cy))) * pow(age, 1.6) * (0.35 + 0.65 * uHeat);

    float3 col = mid;
    col = lerp(col, float3(0.12, 0.045, 0.03), charZone * 0.65);
    col = col * body + float3(1.12, 0.96, 0.60) * coreLine * body;

    float a = saturate(body * 0.82 + coreLine * body * 0.5);
    a *= uFade * input.Color.a;
    return float4(col * a, a);
}

// ---------------------------------------------------------------- TechDecree
//读 uTime uSeed uForm uFade uRadius uThickness uBeat uBeatSeed
float4 PSDecree(PSInput input) : COLOR0
{
    float2 raw = input.TexCoords * 2.0 - 1.0;
    float dist = length(raw);
    float2 udir = raw / max(dist, 1e-4);

    //撕裂位移+径向外流,环缘不许是干净数学圆
    float n1 = tex2D(uNoise, raw * 0.55 + float2(uTime * 0.10, -uTime * 0.06) + uSeed).r;
    float n2 = tex2D(uNoise, raw * 1.55 - udir * (uTime * 0.30)).g;
    float th = max(uThickness, 1e-3);
    float adj = dist + (n1 * 0.6 + n2 * 0.4 - 0.5) * th * 1.3;
    float ring = adj - uRadius;

    //带厚不均
    float n3 = tex2D(uNoise, raw * 0.85 + float2(uTime * 0.05, uTime * 0.08)).b;
    float thV = th * (0.6 + 0.8 * n3);

    //主火带: 外锐内拖
    float band = min(1.0 - smoothstep(0.0, thV * 0.5, ring), 1.0 - smoothstep(0.0, thV * 1.8, -ring));

    //外舔火舌: 径向拉丝噪声侵蚀出舌形
    float tongueN = nrm(tex2D(uNoise, raw * 1.05 - udir * (uTime * 0.55)).r);
    float tongue = smoothstep(thV * 2.6, 0.0, ring) * smoothstep(-thV * 0.2, thV * 0.35, ring)
                 * smoothstep(0.52, 0.88, tongueN);

    //焦痕暗带: 贴带内沿的烧蚀线
    float charB = (1.0 - smoothstep(0.0, thV * 1.1, abs(ring + thV * 1.15))) * 0.6;

    //符环: 整数 28 格,拍序重写;atan2 只进 floor/frac 离散格,无连续噪声消费
    float a01 = atan2(raw.y, raw.x) / 6.2831853 + 0.5;
    float cellF = a01 * 28.0;
    float cell = floor(cellF);
    float lu = frac(cellF);
    float rGly = (uRadius - adj) / th; //0环缘→内为正,带厚归一
    float glyBand = smoothstep(0.5, 0.8, rGly) * (1.0 - smoothstep(1.6, 2.0, rGly));
    float h1 = hash21(float2(cell, floor(uBeatSeed) * 3.0));
    float h2 = hash21(float2(cell * 1.7 + 13.0, floor(uBeatSeed) * 2.3));
    //每格两竖笔一横笔,少数格留白
    float bar1 = 1.0 - smoothstep(0.05, 0.10, abs(lu - (0.20 + h1 * 0.25)));
    float bar2 = 1.0 - smoothstep(0.04, 0.09, abs(lu - (0.58 + h2 * 0.26)));
    float barH = (1.0 - smoothstep(0.10, 0.20, abs(rGly - (0.85 + h1 * 0.55))))
               * smoothstep(0.10, 0.22, lu) * (1.0 - smoothstep(0.72, 0.90, lu));
    float cellGate = step(0.22, h1 + h2 * 0.5);
    float glyph = saturate(bar1 + bar2 * 0.85 + barH * 0.7) * glyBand * cellGate;

    //拍点编舞: 行波在窗口后 45% 自中心传至环缘,抵达即落拍
    float flare = 1.0 - smoothstep(0.0, 0.16, uBeat);   //拍后余辉
    float pT = smoothstep(0.55, 1.0, uBeat);
    float wavR = uRadius * pT;
    float wave = (1.0 - smoothstep(0.0, th * 1.4, abs(adj - wavR)))
               * smoothstep(0.55, 0.70, uBeat) * (0.35 + 0.65 * pT);

    //符文亮度随拍呼吸
    float gBright = 0.42 + 0.58 * smoothstep(0.45, 0.95, uBeat) + flare * 0.9;

    //内域暖洗+缓对流,刚体旋转坐标
    float ics = cos(uTime * 0.06);
    float isn = sin(uTime * 0.06);
    float2 irc = float2(raw.x * ics - raw.y * isn, raw.x * isn + raw.y * ics);
    float inCh = nrm(tex2D(uNoise, irc * 0.8 + uSeed * 2.0).g);
    float interior = smoothstep(uRadius, uRadius * 0.1, adj);
    float wash = interior * (0.030 + 0.045 * inCh);

    float3 col = FireRamp(0.55 + 0.25 * n3) * band
               + FireRamp(0.85) * tongue * 0.8
               + float3(1.05, 0.82, 0.38) * glyph * gBright
               + float3(1.05, 0.80, 0.34) * wave * 0.9
               + FireRamp(0.40) * wash;
    col = lerp(col, float3(0.10, 0.04, 0.03), charB);
    col += float3(1.15, 1.02, 0.72) * band * flare * 0.9;

    float a = saturate(band * 0.80 + tongue * 0.50 + glyph * gBright * 0.70
            + wave * 0.45 + charB * 0.50 + wash);

    float guard = smoothstep(1.0, 0.86, max(abs(raw.x), abs(raw.y)));
    a *= uFade * uForm * guard * input.Color.a;
    return float4(col * a, a);
}

// ---------------------------------------------------------------- TechBrand
//读 uTime uBeat uBeatSeed;顶点色 R=实例种子 A=边缘淡出
float4 PSBrand(PSInput input) : COLOR0
{
    float2 p = input.TexCoords * 2.0 - 1.0;
    float seed = input.Color.r * 11.7;
    float d = length(p);

    //三道爪痕: cos3θ 多项式取代极角,先刚体旋转再求方向余弦,天然无接缝
    float rotA = seed * 2.4 + floor(uBeatSeed) * 0.9;
    float cs = cos(rotA);
    float sn = sin(rotA);
    float2 pr = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
    float cth = pr.x / max(length(pr), 1e-4);
    float cos3 = 4.0 * cth * cth * cth - 3.0 * cth;
    float claw = smoothstep(0.80, 0.97, cos3)
               * smoothstep(0.30, 0.50, d) * (1.0 - smoothstep(0.82, 0.98, d));

    //虹膜环
    float iris = 1.0 - smoothstep(0.03, 0.11, abs(length(p * float2(1.0, 1.30)) - 0.50));

    //竖瞳: 落拍前收缩成刃缝,捕食者聚焦
    float pw = lerp(0.15, 0.045, smoothstep(0.45, 0.96, uBeat));
    float pupil = (1.0 - smoothstep(pw * 0.5, pw, abs(p.x)))
                * (1.0 - smoothstep(0.26, 0.40, abs(p.y)));

    //灼刻蚀入: 燃纸式生长;破印瞬间借 flare 保持满形一起过曝
    float n = nrm(tex2D(uNoise, p * 1.2 + seed).r);
    float flare = 1.0 - smoothstep(0.0, 0.13, uBeat);
    float grow = smoothstep(0.06, 0.62, uBeat);
    float burnGate = smoothstep(n - 0.30, n, max(grow, flare * 1.1));

    float mark = saturate(iris * 0.9 + pupil * 1.1 + claw * 0.85) * burnGate;

    float3 col = FireRamp(0.72 + 0.2 * flare) * mark
               + float3(1.15, 1.02, 0.72) * mark * (flare * 1.4 + 0.10);
    float a = saturate(mark * (0.50 + 0.42 * smoothstep(0.30, 0.90, uBeat) + flare * 0.55));

    float guard = smoothstep(1.0, 0.90, max(abs(p.x), abs(p.y)));
    a *= guard * input.Color.a;
    return float4(col * a, a);
}

technique TechTear
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PSTear();
    }
}

technique TechTrail
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PSTrail();
    }
}

technique TechDecree
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PSDecree();
    }
}

technique TechBrand
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PSBrand();
    }
}
