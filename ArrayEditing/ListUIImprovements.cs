using FrooxEngine.UIX;
using FrooxEngine;
using HarmonyLib;
using MonkeyLoader.Patching;
using System.Collections.Generic;
using System.Linq;
using MonkeyLoader.Resonite;

namespace ArrayEditing
{
    [HarmonyPatch]
    [HarmonyPatchCategory(nameof(ListUIImprovements))]
    internal sealed class ListUIImprovements : ResoniteMonkey<ListUIImprovements>
    {
        public override bool CanBeDisabled => true;

        protected override IEnumerable<IFeaturePatch> GetFeaturePatches() => [];

        [HarmonyPatch(typeof(ListEditor), "BuildListElement")]
        private static bool Prefix(UIBuilder ui)
        {
            if (Enabled)
                ui.Style.MinHeight = 24f;

            return true;
        }

        [HarmonyPatch(typeof(SyncMemberEditorBuilder), "GenerateMemberField")]
        private static bool Prefix(ISyncMember member, UIBuilder ui)
        {
            if (!Enabled || member.Parent is not ISyncList || member is not SyncObject)
                return true;

            ui.CurrentRect.Slot.AttachComponent<HorizontalLayout>();
            if (ui.CurrentRect.Slot.GetComponent<LayoutElement>() is LayoutElement layoutElement)
            {
                layoutElement.MinWidth.Value = 48f;
                layoutElement.FlexibleWidth.Value = -1f;
            }

            return true;
        }
    }
}