sampler uImage0 : register(s0);//u0就是tml里面的gd.Texture[0]
sampler uImage1 : register(s1);//u1就是tml里面的gd.Texture[1]
//我抄了yiyang的shader，顺便进行了一些改造，现在可以让纹理运动起来
float m;
float n;
float OffsetX = 0;//X偏移,请传入对应的参数
float OffsetY = 0; //Y偏移,请传入对应的参数
float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 c = tex2D(uImage0,coords);
	float a=max(c.r,max(c.g,c.b));
    if(a>m)//明度超过m的部分被替换为背景图片
	{
        float4 c1 = tex2D(uImage1, float2((0.5 * coords.x + OffsetX) % 1, (coords.y + OffsetY) % 1));
		return c1;
	}
	else if(abs(a-m)<n)//明度与m差值小于n的部分，替换为纯色当作描边
		return float4(0.02,0.1,0.9,1);
	else
		return c*a;
}

technique Technique1
{
    pass StarsShaderPass
    {
		PixelShader = compile ps_2_0 PixelShaderFunction();
	}
}