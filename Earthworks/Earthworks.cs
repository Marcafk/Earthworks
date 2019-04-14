using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory;
using Vintagestory.Client;

namespace Earthworks
{
    public class Earthworks : ModSystem
    {
        ICoreClientAPI capi;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.BlockTexturesLoaded += ReloadTextures;
        }

        public void ReloadTextures()
        {
            if (capi.Settings.Int["maxTextureAtlasSize"] < 4096)
            {
                capi.Settings.Int["maxTextureAtlasSize"] = 4096;
            }
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            //api.RegisterBlockClass("BlockBowl", typeof(BlockBowlNew));
            api.RegisterBlockClass("BlockCookedContainerFix", typeof(CookedContainerFix));
            api.RegisterBlockClass("BlockCookingContainerFix", typeof(CookingContainerFix)); 
            
            api.RegisterBlockEntityClass("CookedContainerFix", typeof(CookedContainerFixBE));
           
        }
    }
}