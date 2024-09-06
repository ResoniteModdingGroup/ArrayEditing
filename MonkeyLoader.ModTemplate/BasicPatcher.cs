using FrooxEngine.ProtoFlux;
using HarmonyLib;
using MonkeyLoader.Patching;
using MonkeyLoader.Resonite;
using MonkeyLoader.Resonite.Features.FrooxEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonkeyLoader.ModTemplate
{
    [HarmonyPatchCategory(nameof(BasicPatcher))]
    [HarmonyPatch(typeof(ProtoFluxTool), nameof(ProtoFluxTool.OnAttach))]
    internal class BasicPatcher : ResoniteMonkey<BasicPatcher>
    {
        // The options for these should be provided by your game's game pack.
        protected override IEnumerable<IFeaturePatch> GetFeaturePatches()
        {
            yield return new FeaturePatch<ProtofluxTool>(PatchCompatibility.HookOnly);
        }

        private static void Postfix()
        {
            Logger.Info(() => "Postfix for ProtoFluxTool.OnAttach()!");
        }
    }
}