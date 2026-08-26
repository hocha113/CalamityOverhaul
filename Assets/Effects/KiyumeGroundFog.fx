// ============================================================================
//KiyumeGroundFog.fx 鬼梦贴地残雾（TechGroundBand）与瀑布雾（TechFogFall）
//母本 KikasaDreamFog.fx（结构照搬：底重剖面 × 冠线/雾体双频错速侵蚀 × 顶点渐隐），
//为鬼梦潮汐改造：驱散不走 uRepulse（CPU 逐列喂 KiyumeFogSuppression 因子），
//颜色不走常量色板（CPU 逐列喂 KiyumeFogTheme 带色 + 烬色采光染），潮汐门控在顶点 alpha 里。
//顶点契约（KiyumeGroundFogRender.cs 对齐）：
//  POSITION=世界坐标（VS 过 transformMatrix，世界 xy 转发 TEXCOORD1）
//  带：TEXCOORD0.y=带内高度01（0=裙底 1=带顶），TEXCOORD0.x=抑制因子（1=无抑制，只驱动孔缘微堆亮）
//  瀑：TEXCOORD0.y=沿瀑01（0=瀑口），TEXCOORD0.x=横向01
//  COLOR0.rgb=染色后雾色，COLOR0.a=断崖×潮汐露出×抑制复合渐隐
//直线算术、平 tex2D ≤3 次/像素、无 atan2 无动态分支（FNA3D 翻译纪律）；
//绑定噪声实测值域 0.227~0.776，阈值一律过 nrm() 归一；预乘输出进 AlphaBlend
// ============================================================================

sampler uNoiseTex : register(s1); //PerlinNoise，消费端上 s1 + LinearWrap

float4x4 transformMatrix;

float uTime;      //秒（GlobalTimeWrappedHourly，与雾海同源）
float uWind;      //风相 px/s，正=向东爬（雾从湖里上岸往村里渗）
float uSeed;      //噪声相位偏移 px（MacroSeed 定相，同存档同相）
float uAlpha;     //带体不透明度 × presence（CPU 折算好）

//===瀑布雾逐道 uniform（每道单独 Apply）===
float uFallLen;   //瀑带总长 px（落差+裙）
float uFallDrop;  //瀑口到落点地面 px
float uFallFlow;  //sqrt 域滚速（texcoord/s，CPU 按视觉流速 ~30px/s 折算）

float nrm(float n) {
    return saturate((n - 0.227) * 1.821);
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
    //世界坐标转发：噪声锚定按世界像素算，相机平移不带着雾走
    o.World = v.Position.xy;
    return o;
}

//====== 贴地残雾带 ======

float4 PSGroundBand(PSInput i) : COLOR0 {
    float h = i.TexCoords.y;                       //0=裙底 1=带顶
    float sup = i.TexCoords.x;                     //抑制因子（1=无抑制）
    float xw = i.World.x + uSeed - uTime * uWind;  //风相：整场雾向东缓行

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

    //颜色全部来自顶点（CPU 逐列采光染色，贴地雾继承雾海的吃光语言，不搬程序化天光）
    float3 col = i.Color.rgb * (0.88 + 0.24 * nBody + 0.35 * lift);
    float alpha = saturate(dens * (1.0 + 0.30 * lift)) * uAlpha * i.Color.a;
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
