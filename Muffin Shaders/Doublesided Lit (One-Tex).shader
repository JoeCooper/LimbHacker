Shader "Noble Muffins/Doublesided (One-Tex, Lit)" {
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" { }
    }

    Category
    {
	    BindChannels {
		   Bind "Vertex", vertex
		   Bind "texcoord", texcoord0
		   Bind "Normal", normal
	   }
        ZWrite On
        SubShader
        {
            Lighting On
            Pass
            {
            	Material {
                	Diffuse (1,1,1,1)
                	Ambient (1,1,1,1)
                }
        		Cull Off
                SetTexture [_MainTex]
                {
                    Combine texture * primary DOUBLE
                } 
            }
        } 
    }
}
