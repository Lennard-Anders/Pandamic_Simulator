// <copyright file="CustomBuildingInfoPanel.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.UI
{
    using ColossalFramework.UI;
    using RealTime.Pandemic;
    using UnityEngine;

    /// <summary>Injects an infected-resident count label into the zoned building info panel.</summary>
    internal sealed class CustomBuildingInfoPanel
    {
        private const string PanelName = "(Library) ZonedBuildingWorldInfoPanel";
        private const string LabelName = "RealTimePandemicBldLabel";

        private UILabel infectedLabel;

        private CustomBuildingInfoPanel()
        {
        }

        /// <summary>Creates and returns an instance; always succeeds (initialization is lazy).</summary>
        public static CustomBuildingInfoPanel Enable() => new CustomBuildingInfoPanel();

        /// <summary>Removes the injected label and releases the recycled panel reference.</summary>
        public void Disable()
        {
            if (infectedLabel != null)
            {
                Object.Destroy(infectedLabel.gameObject);
                infectedLabel = null;
            }
        }

        /// <summary>Called each frame while the building info panel is visible.</summary>
        public void UpdateCustomInfo(ref InstanceID instance)
        {
            if (instance.Type != InstanceType.Building)
            {
                return;
            }

            ushort buildingId = instance.Building;
            var manager = PandemicManager.Instance;

            EnsureLabel();

            if (infectedLabel == null)
            {
                return;
            }

            if (manager == null || !manager.IsActive || buildingId == 0)
            {
                infectedLabel.isVisible = false;
                return;
            }

            int count = manager.GetInfectedCountInBuilding(buildingId);
            if (count > 0)
            {
                infectedLabel.text = count == 1
                    ? "\u2623 1 infected resident"
                    : "\u2623 " + count + " infected residents";
                infectedLabel.isVisible = true;
            }
            else
            {
                infectedLabel.isVisible = false;
            }
        }

        private void EnsureLabel()
        {
            if (infectedLabel != null)
            {
                return;
            }

            // ZonedBuildingWorldInfoPanel is a UICustomControl (MonoBehaviour), not a UIComponent.
            // The GameObject named "(Library) ZonedBuildingWorldInfoPanel" has a UIPanel component.
            var panel = UIView.GetAView()?.FindUIComponent<UIPanel>(PanelName);
            if (panel == null)
            {
                return;
            }

            // Re-use existing label if panel was recycled
            infectedLabel = panel.Find<UILabel>(LabelName);
            if (infectedLabel != null)
            {
                return;
            }

            infectedLabel = panel.AddUIComponent<UILabel>();
            infectedLabel.name = LabelName;
            infectedLabel.autoSize = false;
            infectedLabel.width = 270f;
            infectedLabel.height = 18f;
            infectedLabel.textScale = 0.82f;
            infectedLabel.textColor = new Color32(255, 80, 80, 255);
            infectedLabel.isVisible = false;

            // Anchor below the IsHistorical checkbox (bottom of the content area).
            var anchor = panel.Find<UICheckBox>("IsHistorical");
            if (anchor != null)
            {
                infectedLabel.relativePosition = new Vector3(
                    anchor.relativePosition.x,
                    anchor.relativePosition.y + anchor.height + 4f);

                // Grow the panel so the label doesn't get clipped.
                panel.height = Mathf.Max(panel.height, infectedLabel.relativePosition.y + infectedLabel.height + 6f);
            }
            else
            {
                // Fallback: place near bottom of the standard panel.
                infectedLabel.relativePosition = new Vector3(14f, panel.height - 20f);
            }
        }
    }
}
