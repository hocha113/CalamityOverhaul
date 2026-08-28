// ============================================================================
//KiyumeGroundFog.fx 鬼梦贴地残雾（TechGroundBand）与瀑布雾（TechFogFall）
//母本 KikasaDreamFog.fx 距离场版（结构照搬：距离场定高 × 冠线/雾体双频错速侵蚀 × 跳变闸），
//为鬼梦潮汐改造：潮汐露出门控逐像素解析（groundY=像素Y+dist，雾线式与 KiyumeFogTide.SurfaceAt 同源），
//驱散抑制/采光烬染走距离场 G/B 通道（KiyumeGroundField 逐场元烘焙），主题带色走 s3 条带。
//带密度源=带符号离地距离场（s2，R 通道 128=地表、4px/单位）：空气为正=离地高，岩内为负=沉深，
//任意地形逐像素贴合；跳变闸压掉竖壁/薄板底/崖口的双线性假雾膜（真实地面接触处场连续、梯度恒 32px/样距）。
//瀑（TechFogFall）保留旧契约：C# 探柱建帘，TEXCOORD0.y=沿瀑01（0=瀑口），TEXCOORD0.x=横向01，
//COLOR0.rgb=染色后雾色，COLOR0.a=断崖×露出×抑制复合渐隐
//直线算术、带 PS 平 tex2D 八采样（场5+主题1+噪声2）、无 atan2 无动态分支（FNA3D 翻译纪律）；
//绑定噪声实测值域 0.227~0.776，阈值一律过 nrm() 归一；预乘输出进 AlphaBlend
// ============================================================================

sampler uNoiseTex : register(s1); //PerlinNoise，消费端上 s1 + LinearWrap
sampler uFieldTex : register(s2); //离地距离场（R=距离 G=抑制 B=采光），s2 + LinearClamp
sampler uThemeTex : register(s3); //主题带色条带（CapW×1），s3 + LinearClamp

float4x4 transformMatrix;

float uTime;      //秒（GlobalTimeWrappedHourly，与雾海同源）
float uWind;      //风相 px/s，正=向东爬（雾从湖里上岸往村里渗）
float uSeed;      //噪声相位偏移 px（MacroSeed 定相，同存档同相）
float uAlpha;     //带体不透明度 × presence（CPU 折算好）

//===距离场窗口映射（KiyumeFog 的 uFogOrigin/uFogUvMul/uFogUvClamp 同式）===
float2 uFieldOrigin;  //窗口原点（世界px，整tile对齐）
float2 uFieldUvMul;   //1/(容量tile数×16px)
float4 uFieldUvClamp; //xy=min uv, zw=max uv（半 texel 内缩到实际窗口子矩形）

//===带几何与潮汐/染色（热调走 KiyumeFogDebug）===
float uBandH;        //地上带高 px
float uSkirt;        //地下裙边 px
float uFogLineY;     //雾线基准 Y（世界px）
float uLakeRightPx;  //湖右缘 x
float uTiltPx;       //近湖抬升幅 px
float uTiltSpanPx;   //抬升过渡跨度 px
float uExposeSpanPx; //潮汐露出跨度 px
float uVisFloor;     //暗处雾可见度地板
float uTintMax;      //亮处向烬色偏移的最大插值

//===瀑布雾逐道 uniform（每道单独 Apply）===
float uFallLen;   //瀑带总长 px（落差+裙）
float uFallDrop;  //瀑口到落点地面 px
float uFallFlow;  //sqrt 域滚速（texcoord/s，CPU 按视觉流速 ~30px/s 折算）

//烬色与 KiyumeFogSim.EmberTint 同源
static const float3 EMBER = float3(0.95, 0.34, 0.14);
//R 通道解码：(r - 128/255) × 1020 → ±508px
static const float DIST_BIAS = 128.0 / 255.0;
static const float DIST_SPAN = 1020.0;

float nrm(float n) {
    return saturate((n - 0.227) * 1.821);
}

float2 FieldUv(float2 world) {
    return clamp((world - uFieldOrigin) * uFieldUvMul, uFieldUvClamp.xy, uFieldUvClamp.zw);
}

//带符号离地距离（世界px入参）：正=空气离地高，负=岩内沉深
float FieldDist(float2 world) {
    return (tex2D(uFieldTex, FieldUv(world)).r - DIST_BIAS) * DIST_SPAN;
}

struct VSInput {
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

struct PSInput {
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
    float2 World : TEXCOORD1;
};

PSInput VSFog(VSInput v) {
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    //世界坐标转发：距离场/噪声锚定按世界像素算，相机平移不带着雾走
    o.World = v.Position.xy;
    return o;
}

//====== 贴地残雾带 ======

float4 PSGroundBand(PSInput i) : COLOR0 {
    //中心采样一次拿全通道：R=距离 G=抑制 B=采光
    float4 cell = tex2D(uFieldTex, FieldUv(i.World));
    float dist = (cell.r - DIST_BIAS) * DIST_SPAN;
    float sup = cell.g;
    float lit = cell.b;
    //带内高度01：0=裙底 1=带顶（旧三角带纵坐标同语义，带外值不钳、由剖面自然归零）
    float h = (dist + uSkirt) / (uBandH + uSkirt);

    //跳变闸：±1tile 场值差读连续性（32px 样距，平滑场恒 32）。水平向读坡度
    //（60°内全保、74°以上闸零，竖直岩壁/崖口在此收没），竖直向抓薄板底面跳变
    float gL = FieldDist(i.World - float2(16.0, 0.0));
    float gR = FieldDist(i.World + float2(16.0, 0.0));
    float gU = FieldDist(i.World - float2(0.0, 16.0));
    float gD = FieldDist(i.World + float2(0.0, 16.0));
    float jump = max(abs(gR - gL), abs(gD - gU));
    float jumpGate = saturate((110.0 - jump) / 55.0);

    //潮汐露出门控：地面露出雾线才显形，涨潮时被雾海接管吞没（y 向下为正）。
    //雾线式与 KiyumeFogTide.SurfaceAt 同源：越靠湖抬得越高
    float groundY = i.World.y + dist;
    float t = saturate(1.0 - (i.World.x - uLakeRightPx) / uTiltSpanPx);
    float surfaceY = uFogLineY - uTiltPx * t * t * (3.0 - 2.0 * t);
    float expose = saturate((surfaceY - groundY) / uExposeSpanPx);

    float xw = i.World.x + uSeed - uTime * uWind;

    //顶缘冠线（低频）与雾体（中频）双频错速，两层假视差，雾在翻涌而非贴图平移
    float nCrest = nrm(tex2D(uNoiseTex, float2(xw * 0.0021, 0.23 + uTime * 0.008)).r);
    float nBody = nrm(tex2D(uNoiseTex, float2(xw * 0.0058 + uTime * 0.006, h * 0.34 - uTime * 0.011)).r);

    //雾顶在 0.42~0.86 间起伏，顶缘被噪声撕出软边
    float crest = 0.42 + 0.44 * nCrest;
    float edge = saturate((crest - h) * 4.5);
    //底重剖面：地线一带最实、裙底轻收、向上渐薄
    float profile = saturate(1.15 - h * 1.05) * saturate(h * 5.0 + 0.30);
    float dens = profile * edge * (0.62 + 0.38 * nBody);

    //孔缘微堆亮：抑制因子 0.3~0.8 的过渡带=雾被拨开挤在孔边上（孔心与远处都不亮）
    float lift = saturate((sup - 0.30) * 4.0) * saturate((0.80 - sup) * 4.0);

    //染色：主题带色沿 x 采条带，采光烬染与 KiyumeFogSim 同式（贴地雾继承雾海吃光语言）
    float3 theme = tex2D(uThemeTex, float2(FieldUv(i.World).x, 0.5)).rgb;
    float3 tint = lerp(theme, EMBER, lit * uTintMax) * (uVisFloor + (1.0 - uVisFloor) * lit);
    float3 col = tint * (0.88 + 0.24 * nBody + 0.35 * lift);

    float alpha = saturate(dens * (1.0 + 0.30 * lift)) * uAlpha * expose * sup * jumpGate;
    return float4(col * alpha, alpha);
}

//====== 瀑布雾（柱状收口三问：源头=衔接贴地带冠线；落点=横向膨出散入贴地层；雾瀑必有落点）======

float4 PSFogFall(PSInput i) : COLOR0 {
    float u = i.TexCoords.x;
    float py = saturate(i.TexCoords.y) * uFallLen;    //瀑口以下 px
    float belowGround = py - uFallDrop;               //相对落点地面（负=还在半空）

    //重力签名：sqrt 纵坐标上密下疏（雾在加速下坠但视觉流速被粘度压到 ~30px/s）
    float sv = sqrt(py + 4.0);
    float nx = (u * 64.0 + uSeed) * 0.0058;
    float n1 = nrm(tex2D(uNoiseTex, float2(nx, sv * 0.0635 - uTime * uFallFlow)).r);
    float n2 = nrm(tex2D(uNoiseTex, float2(nx * 2.3 + 0.37, sv * 0.0320 - uTime * uFallFlow * 0.62)).r);

    //中轴体 + 落点前 70px 起横向膨出（帘底散开）
    float xc = abs(u - 0.5) * 2.0;
    float widthEnv = 0.60 + 0.52 * saturate(1.0 + belowGround / 70.0);
    float body = saturate((widthEnv - xc) * 3.0);
    //缘蚀：帘边被噪声撕成缕，越靠边撕得越狠
    body = saturate(body - n2 * 0.9 * saturate(xc * 1.8 + 0.2));

    //上密下疏 + 源头 26px 淡入衔接贴地带 + 裙内 64px 平方软收散入贴地层
    float densV = 1.0 - 0.38 * saturate(py / max(uFallDrop, 1.0));
    float srcFade = saturate(py / 26.0);
    float landFade = 1.0 - saturate(belowGround / 64.0);
    landFade *= landFade;

    float dens = body * (0.42 + 0.58 * n1) * densV * srcFade * landFade;
    float alpha = saturate(dens) * uAlpha * i.Color.a;
    float3 col = i.Color.rgb * (0.88 + 0.24 * n1);
    return float4(col * alpha, alpha);
}

technique TechGroundBand {
    pass P0 {
        VertexShader = compile vs_3_0 VSFog();
        PixelShader = compile ps_3_0 PSGroundBand();
    }
}

technique TechFogFall {
    pass P0 {
        VertexShader = compile vs_3_0 VSFog();
        PixelShader = compile ps_3_0 PSFogFall();
    }
}
